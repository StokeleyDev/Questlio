using System.Globalization;
using Microsoft.Win32;
namespace GaidenDesktop;
internal sealed class NativeShellSettings
{
    internal string Theme {get;set;}="auto";
    internal bool HasTheme {get;private set;}
    internal Rectangle Bounds {get;set;}
    internal bool Maximized {get;set;}
    private readonly string path;
    internal NativeShellSettings(string folder)
    {
        path=Path.Combine(folder,"window.ini");
        try {
            var values=File.ReadAllLines(path).Select(l=>l.Split('=',2)).Where(p=>p.Length==2).ToDictionary(p=>p[0],p=>p[1]);
            if(values.TryGetValue("theme",out var theme)&&theme is "auto" or "light" or "dark"){Theme=theme;HasTheme=true;}
            int Get(string key)=>values.TryGetValue(key,out var v)&&int.TryParse(v,NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)?n:0;
            Bounds=new Rectangle(Get("x"),Get("y"),Get("width"),Get("height"));Maximized=Get("maximized")==1;
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException){ }
    }
    internal static bool SystemDark()
    {
        try {using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");return key?.GetValue("AppsUseLightTheme") is int value&&value==0;}
        catch(Exception e) when(e is System.Security.SecurityException or UnauthorizedAccessException or IOException){return false;}
    }
    internal bool Dark=>Theme=="dark"||Theme=="auto"&&SystemDark();
    internal static Rectangle SafeBounds(Rectangle requested,Size minimum,IEnumerable<Rectangle> screens,Rectangle primary)
    {
        var usable=screens.FirstOrDefault(s=>Rectangle.Intersect(requested,s).Width>=Math.Min(160,requested.Width)&&Rectangle.Intersect(requested,s).Height>=Math.Min(80,requested.Height));
        if(requested.Width<minimum.Width||requested.Height<minimum.Height||usable.IsEmpty){var size=new Size(Math.Max(minimum.Width,Math.Min(1280,primary.Width)),Math.Max(minimum.Height,Math.Min(860,primary.Height)));return new Rectangle(primary.X+(primary.Width-size.Width)/2,primary.Y+(primary.Height-size.Height)/2,size.Width,size.Height);}
        int width=Math.Max(minimum.Width,Math.Min(requested.Width,usable.Width)),height=Math.Max(minimum.Height,Math.Min(requested.Height,usable.Height));
        return new Rectangle(Math.Clamp(requested.X,usable.Left,Math.Max(usable.Left,usable.Right-width)),Math.Clamp(requested.Y,usable.Top,Math.Max(usable.Top,usable.Bottom-height)),width,height);
    }
    internal void Save()
    {
        try {Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temporary=path+".tmp";
            File.WriteAllLines(temporary,new[]{"theme="+Theme,"x="+Bounds.X,"y="+Bounds.Y,"width="+Bounds.Width,"height="+Bounds.Height,"maximized="+(Maximized?1:0)});File.Move(temporary,path,true);HasTheme=true;
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException){DiagnosticLog.Error("window-settings",e);}
    }
}
internal sealed partial class MainWindow
{
    private readonly NativeShellSettings shell;
    private readonly Icon trayArtwork;
    private FormWindowState lastShellState;
    internal static bool SupportsMica(Version version)=>version.Major>=10&&version.Build>=22000;
    internal static bool TryDwm(Func<int> action){try{return action()>=0;}catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException){return false;}}
    private void ApplyNativeTheme(bool dark)
    {
        BackColor=dark?Color.FromArgb(15,20,27):Color.FromArgb(245,245,247);web.DefaultBackgroundColor=BackColor;UpdateTitleBarTheme(dark);
    }
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);if(shell is not null)ApplyNativeTheme(shell.Dark);}
    private void SaveShellWindow()
    {
        if(WindowState==FormWindowState.Minimized)return;
        shell.Bounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;shell.Maximized=WindowState==FormWindowState.Maximized;shell.Save();
    }
    private void NativePreferenceChanged(object? sender,Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if(shell.Theme!="auto"||IsDisposed||!IsHandleCreated)return;
        BeginInvoke(new Action(()=>{if(!IsDisposed)ApplyNativeTheme(shell.Dark);}));
    }
    private async Task PersistNativeThemeAsync(bool dark)
    {
        ApplyNativeTheme(dark);
        string mode=System.Text.Json.JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("typeof prefs==='undefined'?'auto':prefs.theme||'auto'"))??"auto";
        if(mode is "auto" or "light" or "dark"){shell.Theme=mode;shell.Save();}
    }
    internal static void NativeStartupChecks(string fixture)
    {
        var evidence=new List<string>();
        foreach(string theme in new[]{"dark","light","auto"}){
            string folder=Path.Combine(Path.GetTempPath(),"Gaiden-shell-startup-"+Guid.NewGuid());Directory.CreateDirectory(folder);
            try{
                new NativeShellSettings(folder){Theme=theme,Bounds=new Rectangle(80,80,1280,800),Maximized=false}.Save();
                using(var form=new MainWindow(fixture,folder)){
                    bool dark=theme=="dark"||theme=="auto"&&NativeShellSettings.SystemDark();Color expected=dark?Color.FromArgb(15,20,27):Color.FromArgb(245,245,247);
                    if(form.Visible||form.BackColor!=expected||form.web.DefaultBackgroundColor!=expected)throw new InvalidOperationException("Pre-show theme "+theme);
                    IntPtr handle=form.Handle;int nativeDark;DwmGetWindowAttribute(handle,20,out nativeDark,sizeof(int));
                    if(nativeDark!=(dark?1:0))throw new InvalidOperationException("Pre-show title theme "+theme);
                    evidence.Add(theme+" pre-Show Form/WebView/title theme PASS");
                }
            }finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(folder,true);}
        }
        string output=Environment.GetEnvironmentVariable("GAIDEN_CHECK_OUTPUT")??AppContext.BaseDirectory;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"native-startup.log"),evidence);
    }

}
