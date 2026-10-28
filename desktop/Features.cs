using System.Text.Json.Nodes;
using Microsoft.Win32;
using System.IO.Compression;
using System.Diagnostics;

namespace GaidenDesktop;

internal sealed record TrackingOptions(bool ExcludeIdle = true, int IdleMinutes = 10)
{
    internal static TrackingOptions From(JsonNode? node)
    {
        var minutes=node?["idleMinutes"]?.GetValue<int>()??10;
        if(minutes is <1 or >120)throw new InvalidDataException("Порог простоя: от 1 до 120 минут");
        return new(node?["excludeIdle"]?.GetValue<bool>()??true,minutes);
    }
    internal bool IsIdle(uint idleMilliseconds)=>ExcludeIdle && idleMilliseconds >= (long)IdleMinutes*60000;
    internal JsonObject Json()=>new(){["excludeIdle"]=ExcludeIdle,["idleMinutes"]=IdleMinutes};
}

internal sealed partial class LibraryStore
{
    internal TrackingOptions TrackingSettings()
    {
        using var db=Open();using var command=db.CreateCommand();command.CommandText="SELECT value FROM settings WHERE key='tracking'";
        return command.ExecuteScalar() is string text?TrackingOptions.From(JsonNode.Parse(text)):new();
    }
    internal void SaveTracking(TrackingOptions options)
    {
        _=TrackingOptions.From(options.Json());
        using var db=Open();using var command=db.CreateCommand();
        command.CommandText="INSERT INTO settings(key,value) VALUES('tracking',$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        command.Parameters.AddWithValue("$value",options.Json().ToJsonString());command.ExecuteNonQuery();
    }
    internal void SetBaseline(string id,long minutes)
    {
        if(minutes is <0 or >60000000)throw new InvalidDataException("Время до трекера: от 0 до 1 000 000 часов");
        EditGame(id,g=>{
            long tracked=(long)((g["trackedActiveSeconds"]?.GetValue<double>()??0)/60);
            g["baselineMinutes"]=minutes;g["minutes"]=checked(minutes+tracked);
            g["baselineEditedAt"]=DateTimeOffset.UtcNow.ToString("O");
        });
    }
    internal void UpdateEmulator(string id,string name,string extensions,string core,bool automatic,string? executable=null)
    {
        name=name.Trim();core=Path.GetFileName(core.Trim());
        var ext=extensions.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(e=>e.TrimStart('.').ToLowerInvariant()).Distinct().ToArray();
        if(name.Length is <1 or >80||ext.Length==0||ext.Any(e=>!System.Text.RegularExpressions.Regex.IsMatch(e,@"^[a-z0-9]{1,12}$")))throw new InvalidDataException("Проверьте название и расширения ROM");
        var profiles=Snapshot()["emulators"]!.AsArray();var profile=profiles.FirstOrDefault(p=>p!["id"]!.GetValue<string>()==id)?.AsObject()??throw new InvalidDataException("Эмулятор не найден");
        string exe=executable is null?profile["exe"]!.GetValue<string>():Path.GetFileNameWithoutExtension(executable.Trim());
        if(string.IsNullOrWhiteSpace(exe)||exe.Length>100||exe.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidDataException("Некорректный EXE");
        if(exe.Contains("retroarch",StringComparison.OrdinalIgnoreCase)&&!automatic&&core.Length==0)throw new InvalidDataException("Укажите DLL ядра RetroArch или включите автоматическое определение");
        if(profiles.Any(p=>p!["id"]!.GetValue<string>()!=id&&string.Equals(p["exe"]!.GetValue<string>(),exe,StringComparison.OrdinalIgnoreCase)&&string.Equals(p["core"]?.GetValue<string>()??"",core,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Такой EXE и ядро уже настроены");
        profile["exe"]=exe;profile["name"]=name;profile["core"]=core;profile["automatic"]=automatic;profile["extensions"]=new JsonArray(ext.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray());
        using var db=Open();using var command=db.CreateCommand();command.CommandText="UPDATE emulator_profiles SET payload=$payload WHERE id=$id";command.Parameters.AddWithValue("$id",id);command.Parameters.AddWithValue("$payload",profile.ToJsonString());
        using var transaction=db.BeginTransaction();command.Transaction=transaction;command.ExecuteNonQuery();
        command.CommandText="SELECT id,payload FROM games";var games=new List<(string Id,JsonObject Game)>();using(var reader=command.ExecuteReader())while(reader.Read())games.Add((reader.GetString(0),JsonNode.Parse(reader.GetString(1))!.AsObject()));
        foreach(var (gameId,game) in games.Where(x=>x.Game["emulatorProfileId"]?.GetValue<string>()==id)){game["platform"]=automatic?EmulatorResolver.AutoPlatform(exe,core):name;command.CommandText="UPDATE games SET payload=$game WHERE id=$gameId";command.Parameters.Clear();command.Parameters.AddWithValue("$game",game.ToJsonString());command.Parameters.AddWithValue("$gameId",gameId);command.ExecuteNonQuery();}
        transaction.Commit();
    }
    internal void CaptureExeIcon(string id,string executable)
    {
        try
        {
            if(!File.Exists(executable))return;
            using var icon=System.Drawing.Icon.ExtractAssociatedIcon(executable);if(icon is null)return;
            using var bitmap=icon.ToBitmap();using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
            EditGame(id,g=>g["exeIcon"]="data:image/png;base64,"+Convert.ToBase64String(stream.ToArray()));
        }
        catch(Exception error)when(error is ArgumentException or IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException){DiagnosticLog.Error("exe-icon",error);}
    }
}

internal static class DiagnosticLog
{
    private static readonly object Gate=new();
    internal static string Folder { get; set; }=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Questlio","logs");
    internal static string LogPath=>Path.Combine(Folder,"app.log");
    internal static void Error(string area,Exception error)=>Write(area,error.GetType().Name+": "+error.Message);
    internal static void Write(string area,string message)
    {
        try{lock(Gate){Directory.CreateDirectory(Folder);if(File.Exists(LogPath)&&new FileInfo(LogPath).Length>2*1024*1024)File.Move(LogPath,LogPath+".previous",true);File.AppendAllText(LogPath,DateTimeOffset.UtcNow.ToString("O")+" ["+area+"] "+message.Replace('\r',' ').Replace('\n',' ')+Environment.NewLine);}}
        catch(IOException){}catch(UnauthorizedAccessException){}
    }
    internal static void Export(string destination,JsonObject summary)
    {
        // Export metadata and logs only: never the database, notes, covers or Steam credentials.
        using var stream=new FileStream(destination,FileMode.Create,FileAccess.Write);using var zip=new ZipArchive(stream,ZipArchiveMode.Create);
        using(var writer=new StreamWriter(zip.CreateEntry("summary.json").Open()))writer.Write(summary.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        lock(Gate)foreach(var file in new[]{LogPath,LogPath+".previous"})if(File.Exists(file))zip.CreateEntryFromFile(file,Path.GetFileName(file));
    }
}

internal static class StartupRegistration
{
    private const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static bool Enabled {get{using var key=Registry.CurrentUser.OpenSubKey(Key);return key?.GetValue("Questlio") is string;}}
    internal static string Command(string executable)=>"\""+executable+"\" --background";
    internal static string Launcher()
    {
        var parent=Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.Parent;
        string? launcher=parent is null?null:Path.Combine(parent.FullName,"Questlio.exe");
        return launcher is not null&&File.Exists(launcher)?launcher:Environment.ProcessPath!;
    }
    internal static void Set(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(Key,true);
        if(enabled)key.SetValue("Questlio",Command(Launcher()));else key.DeleteValue("Questlio",false);
    }
}

internal sealed partial class MainWindow
{
    private Form? quickView;
    private void ShowQuickView()
    {
        if(quickView is {IsDisposed:false}){quickView.Close();return;}
        var snapshot=store.Snapshot();var items=snapshot["games"]!.AsArray().OrderByDescending(g=>LastPlayed(g!)).Take(5).ToArray();
        var form=new Form{Text=NativeText("Questlio · Quick view","Questlio · Быстрый просмотр"),Size=new Size(340,390),Font=new Font("Segoe UI",10),FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,BackColor=Color.FromArgb(28,37,48),ForeColor=Color.White,KeyPreview=true};quickView=form;
        using(var shape=Rounded(new Rectangle(0,0,form.Width,form.Height),22))form.Region=new Region(shape);
        form.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)form.Close();};
        var area=Screen.FromPoint(Cursor.Position).WorkingArea;form.Location=new Point(Math.Clamp(Cursor.Position.X-form.Width,area.Left,Math.Max(area.Left,area.Right-form.Width)),area.Bottom-form.Height-8);
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(14),AutoScroll=true};form.Controls.Add(panel);
        var heading=new Panel{Width=300,Height=56,Margin=new Padding(0,0,0,8)};
        heading.Controls.Add(new Label{Text="Questlio",Font=new Font("Segoe UI",12,FontStyle.Bold),AutoSize=true,Location=new Point(2,0)});
        heading.Controls.Add(new Label{Text=tracker is null?NativeText("Recent games","Последние игры"):NativeStatus(tracker.Status),AutoEllipsis=true,Width=255,Height=24,ForeColor=Color.FromArgb(167,187,205),Location=new Point(2,29),Font=new Font("Segoe UI",9)});
        var close=new Button{Text="×",AccessibleName=NativeText("Close quick view","Закрыть быстрый просмотр"),Width=30,Height=30,Location=new Point(266,0),FlatStyle=FlatStyle.Flat,ForeColor=Color.FromArgb(185,202,219),BackColor=form.BackColor};close.FlatAppearance.BorderSize=0;close.Click+=(_,_)=>form.Close();heading.Controls.Add(close);panel.Controls.Add(heading);
        foreach(var game in items)
        {
            string id=game!["id"]!.GetValue<string>();long minutes=Math.Max(game["minutes"]?.GetValue<long>()??0,game["steam"]?["minutes"]?.GetValue<long>()??0);
            var button=new Button{Text=game["name"]!.GetValue<string>()+"\n"+minutes/60+NativeText(" h "," ч ")+minutes%60+NativeText(" min"," м"),Width=300,Height=52,Margin=new Padding(0,0,0,7),Padding=new Padding(10,0,0,0),FlatStyle=FlatStyle.Flat,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(233,241,249),BackColor=Color.FromArgb(38,51,65)};
            button.FlatAppearance.BorderSize=0;button.FlatAppearance.MouseOverBackColor=Color.FromArgb(50,68,86);using(var shape=Rounded(new Rectangle(0,0,button.Width,button.Height),11))button.Region=new Region(shape);
            button.Click+=async(_,_)=>{form.Close();RestoreWindow();if(web.CoreWebView2 is not null)await web.ExecuteScriptAsync("openDetail("+System.Text.Json.JsonSerializer.Serialize(id)+")");};panel.Controls.Add(button);
        }
        if(items.Length==0)panel.Controls.Add(new Label{Text=NativeText("Your library is empty","Библиотека пока пуста"),AutoSize=true});
        form.Deactivate+=(_,_)=>form.Close();form.FormClosed+=(_,_)=>{if(quickView==form)quickView=null;};form.Show();
    }
    private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle bounds,int radius)
    {
        var path=new System.Drawing.Drawing2D.GraphicsPath();int size=radius*2;
        path.AddArc(bounds.Left,bounds.Top,size,size,180,90);path.AddArc(bounds.Right-size,bounds.Top,size,size,270,90);path.AddArc(bounds.Right-size,bounds.Bottom-size,size,size,0,90);path.AddArc(bounds.Left,bounds.Bottom-size,size,size,90,90);path.CloseFigure();return path;
    }
    private static DateTimeOffset LastPlayed(JsonNode game)
    {
        return new[]{game["lastPlayedAt"],game["legacy"]?["lastPlayedAt"],game["steam"]?["lastPlayedAt"]}.Select(x=>DateTimeOffset.TryParse(x?.GetValue<string>(),out var date)?date:DateTimeOffset.MinValue).Max();
    }
    private JsonObject FeatureSettings()=>new(){["tracking"]=store.TrackingSettings().Json(),["startup"]=StartupRegistration.Enabled};
    private async Task<bool> FeatureActionAsync(JsonNode message)
    {
        string action=message["action"]?.GetValue<string>()??"";
        if(action=="exportDiagnostics")
        {
            using var dialog=new SaveFileDialog{Title=NativeText("Save diagnostics","Сохранить диагностику"),Filter=NativeText("ZIP archive|*.zip","Архив ZIP|*.zip"),FileName="Questlio-diagnostics-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".zip"};
            if(dialog.ShowDialog(this)==DialogResult.OK){var snap=store.Snapshot();DiagnosticLog.Export(dialog.FileName,new JsonObject{["version"]=AppInfo.Version,["os"]=Environment.OSVersion.ToString(),["runtime"]=Environment.Version.ToString(),["games"]=snap["games"]!.AsArray().Count,["sessions"]=snap["sessions"]!.AsArray().Count,["tracking"]=store.TrackingSettings().Json(),["startup"]=StartupRegistration.Enabled});web.CoreWebView2.PostWebMessageAsJson("{\"featureNotice\":\"Диагностика сохранена\"}");}return true;
        }
        if(action is not ("featureSettings" or "saveTracking" or "setBaseline" or "updateEmulator"))return false;
        bool resume=trackerTimer.Enabled;trackerTimer.Stop();maintenance=true;
        try
        {
            await WaitForPollingAsync();
            if(action=="saveTracking")
            {
                var options=TrackingOptions.From(message["tracking"]);
                // Start a new observation boundary; settings never rewrite old session time.
                tracker?.ObserveBoundary();
                bool wasStartup=StartupRegistration.Enabled;
                try{StartupRegistration.Set(message["startup"]?.GetValue<bool>()==true);store.SaveTracking(options);}
                catch{try{StartupRegistration.Set(wasStartup);}catch(Exception error){DiagnosticLog.Error("startup-rollback",error);}throw;}
            }
            if(action=="setBaseline")store.SetBaseline(message["id"]!.GetValue<string>(),message["minutes"]!.GetValue<long>());
            if(action=="updateEmulator")store.UpdateEmulator(message["id"]!.GetValue<string>(),message["name"]!.GetValue<string>(),message["extensions"]!.GetValue<string>(),message["core"]?.GetValue<string>()??"",message["automatic"]?.GetValue<bool>()==true,message["exe"]?.GetValue<string>());
            await RefreshBootstrapAsync();
            web.CoreWebView2.PostWebMessageAsJson(new JsonObject{["featuresUpdated"]=true,["settings"]=FeatureSettings(),["snapshot"]=store.Snapshot(),["operation"]=action}.ToJsonString());
        }
        finally{maintenance=false;if(resume&&!closingSession)trackerTimer.Start();}
        return true;
    }
}

