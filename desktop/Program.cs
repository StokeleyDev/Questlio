using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Runtime.InteropServices;

namespace GaidenDesktop;

internal static class Program
{
    private static readonly string InstanceName = "Local\\Questlio.Desktop.v1"+(Environment.GetCommandLineArgs().Contains("--upgrade-test")?"."+Environment.GetEnvironmentVariable("GAIDEN_UPGRADE_TEST_ID"):"");
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    internal static readonly uint ShowMessage = RegisterWindowMessage(InstanceName + ".Show");
    internal static readonly uint UpgradeMessage=RegisterWindowMessage(InstanceName+".Upgrade");

    [STAThread]
    private static void Main()
    {
        Application.ThreadException+=(_,e)=>DiagnosticLog.Error("ui",e.Exception);
        AppDomain.CurrentDomain.UnhandledException+=(_,e)=>{if(e.ExceptionObject is Exception error)DiagnosticLog.Error("unhandled",error);};
        var args = Environment.GetCommandLineArgs();
        if(args.Contains("--test-shell")){ApplicationConfiguration.Initialize();MainWindow.NativeStartupChecks(args[^1]);return;}
        if(args.Contains("--test-covers")){CoverChecks.RunAsync().GetAwaiter().GetResult();return;}
        if(args.Contains("--test-launch-path")){LaunchPathChecks.Run(args[^1]);return;}
        if(args.Contains("--test-features")){FeatureChecks.Run();return;}
        if(args.Contains("--test-review")){ReviewChecks.Run(args[^1]);return;}
        if(args.Contains("--test-processes")){ProcessChecks.Run();return;}
        if(args.Contains("--test-steam")){SteamChecks.Run();return;}
        if(args.Contains("--test-backups")){BackupChecks.Run(args[^1]);return;}
        if (args.Contains("--test-store")) { StoreChecks.Run(args[^1]); return; }
        if(args.Contains("--test-phase8a")){AutoStatusChecks.Run(args[^1]);return;}
        if (args.Contains("--test-tracker")) { TrackerChecks.Run(args[^1]); return; }
        if (args.Contains("--test-tracker-edges")) { TrackerEdgeChecks.Run(args[^1]); return; }
        if (args.Contains("--test-emulators")) { EmulatorChecks.Run(args[^1]); return; }
        if(args.Contains("--test-hardware")){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"hardware-result.json"),HardwareInfo.Read().ToJsonString());return;}
        if (args.Contains("--test-pcs")) { ComputerChecks.Run(); return; }
        if (args.Contains("--checkpoint-worker")) { var worker=new GameTracker(new LibraryStore(args[^1]));while(true){worker.Poll();Thread.Sleep(200);} }
        if (args.Contains("--import-package"))
        {
            try {
                var store = new LibraryStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Questlio","questlio.db"));
                bool added = store.Import(File.ReadAllText(args[^1]));
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"import-result.json"),new System.Text.Json.Nodes.JsonObject{["imported"]=added,["games"]=store.Snapshot()["games"]!.AsArray().Count}.ToJsonString());
            } catch (Exception error) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"import-result.json"),System.Text.Json.JsonSerializer.Serialize(new {error=error.Message})); Environment.ExitCode=1; }
            return;
        }
        if (args.Contains("--smoke-test")) { ApplicationConfiguration.Initialize(); Application.Run(new MainWindow(args.Contains("--fixture") ? args[^1] : null)); return; }
        using var mutex = new Mutex(true, InstanceName, out bool first);
        if (!first)
        {
            bool different=false;
            foreach(var running in System.Diagnostics.Process.GetProcessesByName("Questlio"))using(running){if(running.Id==Environment.ProcessId)continue;try{if(!string.Equals(running.MainModule?.FileName,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase))different=true;}catch(System.ComponentModel.Win32Exception){}}
            if(!different){if(args.Contains("--background"))return;PostMessage(new IntPtr(0xffff),ShowMessage,IntPtr.Zero,IntPtr.Zero);return;}
            PostMessage(new IntPtr(0xffff),UpgradeMessage,IntPtr.Zero,IntPtr.Zero);
            bool ready;try{ready=mutex.WaitOne(10000);}catch(AbandonedMutexException){ready=true;}
            if(!ready){MessageBox.Show("An older version of Questlio is still running. Choose Exit in its tray menu, then start Questlio again.","Questlio update");return;}
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainWindow(args.Contains("--upgrade-test")?args[^1]:null));
    }
}

internal sealed partial class MainWindow : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd,int attr,out int value,int size);
    private void UpdateTitleBarTheme(bool dark)
    {
        if(!IsHandleCreated)return;
        int value=dark?1:0;
        if(!TryDwm(()=>DwmSetWindowAttribute(Handle,20,ref value,sizeof(int))))TryDwm(()=>DwmSetWindowAttribute(Handle,19,ref value,sizeof(int)));
        if(SupportsMica(Environment.OSVersion.Version)){
            int mica=2;TryDwm(()=>DwmSetWindowAttribute(Handle,38,ref mica,sizeof(int)));
            int color=ColorTranslator.ToWin32(dark?Color.FromArgb(15,20,27):Color.FromArgb(245,245,247));
            TryDwm(()=>DwmSetWindowAttribute(Handle,35,ref color,sizeof(int)));TryDwm(()=>DwmSetWindowAttribute(Handle,34,ref color,sizeof(int)));
            int text=ColorTranslator.ToWin32(dark?Color.FromArgb(245,245,247):Color.FromArgb(29,29,31));TryDwm(()=>DwmSetWindowAttribute(Handle,36,ref text,sizeof(int)));
        }
    }
    private void PostWindowActivity()
    {
        bool paused=WindowState==FormWindowState.Minimized||!Visible;
        if(IsDisposed)return;
        if(!paused&&!web.Visible)web.Visible=true;
        if(uiPausedState!=paused&&web.CoreWebView2 is not null){uiPausedState=paused;try{web.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new{windowState=paused?"paused":"normal"}));}catch(InvalidOperationException){}}
        if(paused&&web.Visible)web.Visible=false;
        _ = SyncWebViewActivityAsync();
    }
    private readonly WebView2 web = new() { Dock = DockStyle.Fill };
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new();
    private string nativeLanguage = "en";
    private string languagePath = "";
    private bool exiting;
    private bool initialized;
    private bool smokeReviewStarted;
    private string? trackerUiSignature;
    private bool trackerUiDeferred;
    private readonly LibraryStore store;
    private readonly string profileFolder;
    private string? bootstrapId;
    private string? selectedNewExe;
    private GameTracker? tracker;
    private readonly System.Windows.Forms.Timer trackerTimer = new() { Interval=1000 };
    private bool polling;
    private bool maintenance;
    private bool closingSession;
    private bool closeSaved;
    private bool? uiPausedState;
    private readonly System.Windows.Forms.Timer backupTimer=new(){Interval=300000};

    internal MainWindow(string? fixture = null,string? shellTestFolder = null)
    {
        var folder = shellTestFolder ?? (fixture is null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Questlio") : Path.Combine(Path.GetTempPath(), "Gaiden-ui-check-" + Guid.NewGuid()));
        languagePath=Path.Combine(folder,"language.ini");
        try { if(File.Exists(languagePath)&&File.ReadAllText(languagePath).Trim()=="ru") nativeLanguage="ru"; } catch(IOException) {} catch(UnauthorizedAccessException) {}
        shell=new NativeShellSettings(folder);ApplyNativeTheme(shell.Dark);
        store = new LibraryStore(Path.Combine(folder,"questlio.db"));
        if (fixture is not null) store.Import(File.ReadAllText(fixture));
        profileFolder = Path.Combine(folder,"WebView2");
        if (!Environment.GetCommandLineArgs().Contains("--smoke-test")) tracker=new GameTracker(store);
        Text = "Questlio";
        DiagnosticLog.Folder=Path.Combine(folder,"logs");
        DiagnosticLog.Write("startup","Questlio "+AppInfo.Version);
        if(fixture is null && StartupRegistration.Enabled)StartupRegistration.Set(true);
        Size = new Size(1280, 860);
        MinimumSize = new Size(820, 600);
        StartPosition = FormStartPosition.CenterScreen;
        StartPosition=FormStartPosition.Manual;
        Bounds=NativeShellSettings.SafeBounds(shell.Bounds,MinimumSize,Screen.AllScreens.Select(s=>s.WorkingArea),Screen.PrimaryScreen?.WorkingArea??new Rectangle(0,0,1280,860));
        if(shell.Maximized)WindowState=FormWindowState.Maximized;
        Icon=new Icon(Path.Combine(AppContext.BaseDirectory,"assets","questlio.ico"));
        trayArtwork=new Icon(Path.Combine(AppContext.BaseDirectory,"assets","questlio-tray.ico"));
        ResizeEnd+=(_,_)=>SaveShellWindow();
        lastShellState=WindowState;
        Resize+=(_,_)=>{if(WindowState!=FormWindowState.Minimized&&WindowState!=lastShellState){lastShellState=WindowState;SaveShellWindow();}};
        Resize+=(_,_)=>PostWindowActivity();VisibleChanged+=(_,_)=>PostWindowActivity();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged+=NativePreferenceChanged;
        Controls.Add(web);
        menu.Items.Add("Открыть Questlio", null, (_, _) => RestoreWindow());
        menu.Items.Add("Быстрый просмотр", null, (_, _) => ShowQuickView());
        menu.Items.Add("Импорт библиотеки…", null, async (_, _) => await ImportAsync());
        menu.Items.Add("Добавить EXE…", null, async (_, _) => await AddExeFromTrayAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => { exiting = true; Close(); });
        tray = new NotifyIcon { Text = "Questlio — ожидание игры", Icon = trayArtwork, ContextMenuStrip = menu, Visible = true };
        UpdateNativeLanguage(nativeLanguage, false);
        tray.DoubleClick += (_, _) => {quickView?.Close();RestoreWindow();};
        tray.MouseClick += (_,e)=>{if(e.Button==MouseButtons.Left)ShowQuickView();};
        trackerTimer.Tick += async (_, _) => {
            if(polling || maintenance || closingSession || tracker is null || web.CoreWebView2 is null)return;
            polling=true;
            try { bool changed=await Task.Run(()=>tracker.Poll()); if(!IsDisposed&&!closingSession&&!maintenance)await UpdateTrackerAsync(changed); }
            catch(Exception error) { DiagnosticLog.Error("tracker",error);trackerTimer.Stop(); if(!IsDisposed){tray.Text=NativeText("Questlio — tracker error","Questlio — ошибка трекера"); if(web.CoreWebView2 is not null)await web.ExecuteScriptAsync("window.showTrackerState?.("+System.Text.Json.JsonSerializer.Serialize("Ошибка трекера: "+error.Message)+")");} }
            finally { polling=false; }
        };
        Microsoft.Win32.SystemEvents.PowerModeChanged += PowerChanged;
        backupTimer.Tick+=(_,_)=>{if(!polling&&!maintenance)AutomaticBackup();};
        Shown += async (_, _) => {if(Environment.GetCommandLineArgs().Contains("--background"))Hide();ApplyNativeTheme(shell.Dark);await InitializeAsync();};
        FormClosing += async (_, e) =>
        {
            if(!closeSaved&&!uiCloseReady&&web.CoreWebView2 is not null){
                e.Cancel=true;if(flushingUiClose)return;flushingUiClose=true;
                try{await FlushPendingUiDeletesAsync();uiCloseReady=true;BeginInvoke(new Action(Close));}
                catch(Exception error){DiagnosticLog.Error("close-ui",error);await NativeNoticeAsync(error.Message);}
                finally{flushingUiClose=false;}return;
            }
            uiCloseReady=false;SaveShellWindow();
            if(!exiting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();return;}
            if(closeSaved)return;
            e.Cancel=true;if(closingSession)return;exiting=true;closingSession=true;trackerTimer.Stop();backupTimer.Stop();
            try {await WaitForPollingAsync();tracker?.Stop("application-exit");closeSaved=true;BeginInvoke(new Action(Close));}
            catch(Exception error){closingSession=false;exiting=false;trackerTimer.Start();backupTimer.Start();await NativeNoticeAsync("Не удалось сохранить сессию: "+error.Message);}
        };
    }

    private string NativeText(string english,string russian)=>nativeLanguage=="ru"?russian:english;
    private string NativeStatus(string text)=>nativeLanguage=="en" ? text switch { "Трекер остановлен"=>"Tracker stopped", "Ожидание игры"=>"Waiting for a game", "Проверка интерфейса"=>"Interface check", _ when text.StartsWith("Игра: ")=>"Playing: "+text[6..], _=>text } : text;

    private void UpdateNativeLanguage(string? requested, bool persist)
    {
        nativeLanguage=requested=="ru"?"ru":"en";
        bool en=nativeLanguage=="en";
        if(menu.Items.Count>=6){menu.Items[0].Text=en?"Open Questlio":"Открыть Questlio";menu.Items[1].Text=en?"Quick view":"Быстрый просмотр";menu.Items[2].Text=en?"Import library…":"Импорт библиотеки…";menu.Items[3].Text=en?"Add EXE…":"Добавить EXE…";menu.Items[5].Text=en?"Exit":"Выход";}
        if(tray is not null)tray.Text=en?"Questlio — waiting for a game":"Questlio — ожидание игры";
        if(persist)try{Directory.CreateDirectory(Path.GetDirectoryName(languagePath)!);File.WriteAllText(languagePath,nativeLanguage);}catch(IOException){}catch(UnauthorizedAccessException){}
    }

    private async Task InitializeAsync()
    {
        if (initialized) return;
        initialized = true;
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, profileFolder);
            if (IsDisposed) return;
            await web.EnsureCoreWebView2Async(environment);
            if (IsDisposed) return;
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("gaiden.local", Path.Combine(AppContext.BaseDirectory, "ui"), CoreWebView2HostResourceAccessKind.DenyCors);
            InitializeCovers();
            web.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            web.CoreWebView2.Settings.IsWebMessageEnabled = true;
            string nativeAppearanceSeed=System.Text.Json.JsonSerializer.Serialize(new {mode=shell.Theme,dark=shell.Dark,hasTheme=shell.HasTheme});
            if(Environment.GetCommandLineArgs().Contains("--smoke-test")&&Environment.GetCommandLineArgs().Contains("--phase8b-review")) await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("(()=>{const D=Date;globalThis.Date=class extends D{constructor(...a){super(...(a.length?a:[\"2026-10-26T12:00:00\"]));}static now(){return new D(\"2026-10-26T12:00:00\").getTime();}};})();");
            if(Environment.GetCommandLineArgs().Contains("--smoke-test")&&Environment.GetEnvironmentVariable("QUESTLIO_TEST_LANGUAGE")=="ru") await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("localStorage.setItem(\"questlio.language.v1\",\"ru\");");
            await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("(()=>{const native="+nativeAppearanceSeed+";window.gaidenNativeAppearance=native;if(native.hasTheme){try{const p=JSON.parse(localStorage.getItem('gaiden.prefs.v1')||'{}');p.theme=native.mode;p.dark=native.dark;localStorage.setItem('gaiden.prefs.v1',JSON.stringify(p));}catch{}}})();");
            await RefreshBootstrapAsync();
            web.CoreWebView2.NavigationCompleted += (_, _) => { uiPausedState=null; PostWindowActivity(); };
            web.CoreWebView2.WebMessageReceived += async (_, e) =>
            {
                if (!IsLocal(e.Source)) return;
                try {
                    var message = System.Text.Json.Nodes.JsonNode.Parse(e.WebMessageAsJson)!;
                    if(message["uiConfirmation"] is System.Text.Json.Nodes.JsonObject confirmation){if(confirmation["id"]?.GetValue<string>() is string key&&uiConfirmations.TryGetValue(key,out var completion))completion.TrySetResult(confirmation["accepted"]?.GetValue<bool>()==true);return;}
                    if(message["action"]?.GetValue<string>()=="languageChanged"){UpdateNativeLanguage(message["language"]?.GetValue<string>(), true);return;}
                    if(message["action"]?.GetValue<string>()=="themeChanged"){await PersistNativeThemeAsync(message["dark"]?.GetValue<bool>()==true);return;}
                    if(maintenance||closingSession)throw new InvalidOperationException("Подождите завершения работы с резервной копией");
                    if(await FeatureActionAsync(message))return;
                    if(message["action"]?.GetValue<string>() is "processList" or "processSelect"){await ProcessActionAsync(message);return;}
                    if(message["action"]?.GetValue<string>()=="steamKeyPage"){System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://steamcommunity.com/dev/apikey"){UseShellExecute=true});return;}
                    if(message["action"]?.GetValue<string>() is "steamPreview" or "steamImport" or "steamCancel"){await SteamActionAsync(message);return;}
                    if(message["action"]?.GetValue<string>() is "backupList" or "backupCreate" or "backupRestore" or "backupFolder"){
                        await BackupActionAsync(message["action"]!.GetValue<string>());return;
                    }
                    if (message["action"]?.GetValue<string>() == "import") { await ImportAsync(); return; }
                    if(message["action"]?.GetValue<string>()=="hardware"){
                        var hardware=await Task.Run(HardwareInfo.Read);
                        if(!IsDisposed&&web.CoreWebView2 is not null)web.CoreWebView2.PostWebMessageAsJson(new System.Text.Json.Nodes.JsonObject{["hardware"]=hardware}.ToJsonString());return;
                    }
                    if(message["action"]?.GetValue<string>()=="addEmulator"){
                        store.AddEmulator(message["name"]!.GetValue<string>(),message["exe"]!.GetValue<string>(),message["extensions"]?.GetValue<string>()??"iso,chd,bin",message["core"]?.GetValue<string>()??"",message["automatic"]?.GetValue<bool>()??true);
                        await RefreshBootstrapAsync();web.CoreWebView2.PostWebMessageAsJson(new System.Text.Json.Nodes.JsonObject{["emulatorSaved"]=true,["snapshot"]=store.Snapshot()}.ToJsonString());return;
                    }
                    if(message["action"]?.GetValue<string>()=="removeEmulator"){
                        store.RemoveEmulator(message["id"]!.GetValue<string>());await RefreshBootstrapAsync();web.CoreWebView2.PostWebMessageAsJson(new System.Text.Json.Nodes.JsonObject{["emulatorSaved"]=true,["snapshot"]=store.Snapshot()}.ToJsonString());return;
                    }
                    var editAction=message["action"]?.GetValue<string>();
                    if(editAction is "deleteGame" or "removeExe" or "cover" or "resetCover") {
                        var id=message["id"]!.GetValue<string>();
                        if(editAction=="deleteGame") {
                            if(DeleteNeedsConfirmation(message)&&MessageBox.Show(this,NativeText("Remove this game from your library? Session history and original data will be kept.","Удалить игру из библиотеки? История сессий и исходные данные будут сохранены."),NativeText("Delete game","Удаление игры"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
                            await WaitForPollingAsync();if(tracker?.Current?.GameId==id)tracker.Stop("game-removed",false);
                            store.DeleteGame(id);
                        }
                        if(editAction=="removeExe") {
                            await WaitForPollingAsync();if(tracker?.Current?.GameId==id)tracker.Stop("exe-edited",false);
                            store.RemoveExe(id,message["exe"]!.GetValue<string>());
                        }
                        if(editAction=="resetCover")store.SetCover(id,null);
                        if(editAction=="cover") {
                            using var picker=new OpenFileDialog{Title=NativeText("Choose cover","Выбрать обложку"),Filter=NativeText("Images|*.png;*.jpg;*.jpeg;*.bmp","Изображение|*.png;*.jpg;*.jpeg;*.bmp")};
                            if(picker.ShowDialog(this)!=DialogResult.OK)return;
                            if(new FileInfo(picker.FileName).Length>20*1024*1024)throw new InvalidDataException("Изображение должно быть меньше 20 МБ");
                            using var image=System.Drawing.Image.FromFile(picker.FileName);
                            if((long)image.Width*image.Height>40000000)throw new InvalidDataException("Изображение слишком большое");
                            double scale=Math.Min(1,1200.0/Math.Max(image.Width,image.Height));
                            using var bitmap=new Bitmap(image,new Size(Math.Max(1,(int)(image.Width*scale)),Math.Max(1,(int)(image.Height*scale))));
                            using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
                            store.SetCover(id,"data:image/png;base64,"+Convert.ToBase64String(stream.ToArray()));
                        }
                        await RefreshBootstrapAsync();
                        web.CoreWebView2.PostWebMessageAsJson(new System.Text.Json.Nodes.JsonObject{["edited"]=editAction=="deleteGame",["coverEdited"]=editAction is "cover" or "resetCover",["inlineEdited"]=editAction=="removeExe",["id"]=id,["deleted"]=editAction=="deleteGame",["snapshot"]=store.Snapshot()}.ToJsonString());return;
                    }
                    if(message["action"]?.GetValue<string>() is "pickEmulatorExe" or "pickEmulatorCore"){
                        bool core=message["action"]!.GetValue<string>()=="pickEmulatorCore";
                        using var picker=new OpenFileDialog{Title=core?NativeText("Choose RetroArch core","Выбрать ядро RetroArch"):NativeText("Choose emulator","Выбрать эмулятор"),Filter=core?NativeText("RetroArch core|*.dll","Ядро RetroArch|*.dll"):NativeText("Emulator executable|*.exe","Программа эмулятора|*.exe"),CheckFileExists=true};
                        if(picker.ShowDialog(this)==DialogResult.OK)web.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new{emulatorFile=Path.GetFileName(picker.FileName),field=core?"core":"exe"}));return;
                    }
                    if (message["action"]?.GetValue<string>() == "pickNewExe") {
                        using var picker=new OpenFileDialog{Filter=NativeText("Executable|*.exe","Программа|*.exe"),Title=NativeText("Choose game EXE","Выбрать EXE игры"),CheckFileExists=true};
                        if(picker.ShowDialog(this)==DialogResult.OK){selectedNewExe=picker.FileName;web.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new {exeSelected=Path.GetFileName(selectedNewExe),suggestedName=Path.GetFileNameWithoutExtension(selectedNewExe)}));}return;
                    }
                    if(message["action"]?.GetValue<string>()=="setCoverDownloads"){await SetCoverDownloadsAsync(message["enabled"]?.GetValue<bool>()==true);return;}
                    if(message["action"]?.GetValue<string>() is "launchGame" or "revealGame") {
                        string id=message["id"]!.GetValue<string>();
                        try{using var launched=System.Diagnostics.Process.Start(store.GameStart(id,message["action"]!.GetValue<string>()=="revealGame"));web.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new{launched=message["action"]!.GetValue<string>()=="launchGame",revealed=message["action"]!.GetValue<string>()=="revealGame",id}));}
                        catch(Exception error){web.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new{launchError=error.Message,id}));}return;
                    }
                    if(message["action"]?.GetValue<string>()=="undoAutoStatus"){store.UndoAutoStatus(message["id"]!.GetValue<string>(),message["session"]!.GetValue<string>());await UpdateTrackerAsync(true);return;}
                    if(message["action"]?.GetValue<string>()=="updateGame") {
                        string id=message["id"]!.GetValue<string>();
                        if(!store.Snapshot()["games"]!.AsArray().Any(g=>g!["id"]!.GetValue<string>()==id))throw new InvalidOperationException("Игра не найдена");
                        store.SaveGames(new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject{["id"]=id,["name"]=message["name"]!.GetValue<string>(),["status"]=message["status"]!.GetValue<string>(),["notes"]=message["notes"]!.GetValue<string>(),["statusEdited"]=message["statusEdited"]?.DeepClone()}));
                        await RefreshBootstrapAsync();web.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new{detailSaved=id,revision=message["revision"]?.GetValue<int>()??0}));return;
                    }
                    if (message["action"]?.GetValue<string>() == "createGame") {
                        var platform=message["platform"]?.GetValue<string>()??"PC";
                        if(platform=="PC"){if(selectedNewExe is null||!File.Exists(selectedNewExe))throw new InvalidOperationException("Сначала выберите EXE игры");store.CreateWithExe(message["name"]!.GetValue<string>(),selectedNewExe);}
                        else store.CreatePlatformGame(message["name"]!.GetValue<string>(),platform);selectedNewExe=null;
                        await RefreshBootstrapAsync();web.CoreWebView2.PostWebMessageAsJson(new System.Text.Json.Nodes.JsonObject{["created"]=true,["snapshot"]=store.Snapshot()}.ToJsonString());return;
                    }
                    if (message["action"]?.GetValue<string>() == "bind") {
                        using var picker=new OpenFileDialog {Filter=NativeText("Executable|*.exe","Программа|*.exe"),Title=NativeText("Choose game EXE","Выберите EXE игры")};
                        if(picker.ShowDialog(this)==DialogResult.OK){store.BindExe(message["id"]!.GetValue<string>(),picker.FileName);await RefreshBootstrapAsync();web.CoreWebView2.PostWebMessageAsJson(new System.Text.Json.Nodes.JsonObject{["inlineEdited"]=true,["id"]=message["id"]!.GetValue<string>(),["snapshot"]=store.Snapshot()}.ToJsonString());}return;
                    }
                    if (message["action"]?.GetValue<string>() != "save") return;
                    store.SaveGames(message["games"]!.AsArray());
                    await RefreshBootstrapAsync();
                    web.CoreWebView2.PostWebMessageAsJson("{\"saved\":true}");
                } catch (Exception error) {
                    DiagnosticLog.Error("command",error);
                    web.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new { saved=false, error=error.Message }));
                }
            };
            web.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (!IsLocal(e.Uri)) e.Cancel = true;
            };
            web.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; };
            web.CoreWebView2.DownloadStarting += (_, e) => { e.Cancel = true; };
            web.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            if (Environment.GetCommandLineArgs().Contains("--smoke-test"))
            {
                web.CoreWebView2.NavigationCompleted += async (_, e) =>
                {
                    if(!e.IsSuccess||smokeReviewStarted)return;smokeReviewStarted=true;
                    try
                    {
                        
                        if(Environment.GetCommandLineArgs().Contains("--phase1-capture")){await CapturePhaseOneAsync();return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase0-capture")){await CapturePhaseZeroAsync();return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase8b-review")){await PhaseEightBChecksAsync();return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase8a-review")){await PhaseEightAChecksAsync();return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase5-review")){await PhaseFiveChecksAsync();return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase5-final")){await PhaseFiveFinalAsync();return;}
                        await web.ExecuteScriptAsync("prefs.reduced=true;applyPrefs();");
                        var result = await web.ExecuteScriptAsync("(()=>{const library=games.length;navigate('sessions');return JSON.stringify({polish:(()=>{navigate('settings');const groups=document.querySelectorAll('details.settings-group').length>=3;openDetail(games[0].id);document.querySelector('#edit-game-name').value='Draft';document.querySelector('#game-notes').value='Draft note';const snap=structuredClone(window.gaidenBootstrap);const id=games[0].id;snap.games.find(g=>g.id===id).executables=['inline-probe'];window.applyExeSnapshot({id,snapshot:snap});const draft=document.querySelector('#detail-dialog').open&&document.querySelector('#edit-game-name').value==='Draft'&&document.querySelector('#game-notes').value==='Draft note'&&document.querySelector('.exe-file').textContent.includes('inline-probe.exe');document.querySelector('#detail-dialog').close();window.applyNativeSnapshot(window.gaidenBootstrap);navigate('sessions');return groups&&draft;})(),backups:(()=>{navigate('settings');const ok=!!document.querySelector('#backup-panel')&&document.querySelectorAll('[data-backup]').length===4;navigate('sessions');return ok;})(),computers:(()=>{navigate('computer');const ok=!!document.querySelector('#hardware-info')&&document.querySelector('main h1').textContent==='Мой компьютер';navigate('settings');const separate=!document.querySelector('#hardware-info')&&!!document.querySelector('#emulator-form');navigate('sessions');return ok&&separate;})(),statistics:(()=>{navigate('stats');const ok=document.querySelectorAll('.calendar-day').length>=28&&(document.querySelectorAll('.week-chart .bar').length===7||document.querySelector('.period-panel .ui-empty'));document.querySelector('[data-cal-step]')?.click();const changed=document.querySelectorAll('.calendar-day').length>=28;navigate('sessions');return ok&&changed;})(),libraryEditor:(()=>{if(!games.length)return null;openDetail(games[0].id);const ok=!!document.querySelector('#edit-game-name')&&!!document.querySelector('.detail-body .exe-binding');document.querySelector('#detail-dialog').close();return ok;})(),liveIndicator:(()=>{window.showTrackerState?.({text:'Тест',name:'Тестовая игра',seconds:65,active:true});const box=document.querySelector('.tracker');const ok=box.classList.contains('is-playing')&&box.textContent.includes('Сейчас играет · Тестовая игра')&&!box.querySelector('p')&&!document.querySelector('#live-game');window.showTrackerState?.({active:true});return ok&&!box.classList.contains('is-playing')&&box.textContent.includes('Отслеживание активно');})(),loaded:document.readyState,steamUI:(()=>{navigate('library');document.querySelector('[data-steam-open]').click();const testGames=Array.from({length:12},(_,i)=>({appId:100+i,name:'Steam test '+i,minutes:60+i}));window.applySteamMessage({steamPreview:{steamId:'76561198000000000',games:testGames},token:'fixture'});const first=document.querySelectorAll('.steam-row').length===10;document.querySelector('[data-steam-more]').click();const expanded=document.querySelectorAll('.steam-row').length===12;document.querySelector('[data-steam-none]').click();const empty=document.querySelector('[data-steam-import]').disabled;document.querySelector('#steam-dialog').close();navigate('sessions');return first&&expanded&&empty;})(),processUI:(()=>{window.chooseGameExe({action:'pickNewExe'});document.querySelector('[data-process-load]').click();window.applyProcessMessage({processList:[{token:'a',pid:1,name:'small.exe',title:'Small',memory:1048576,available:true},{token:'b',pid:2,name:'large.exe',title:'Large',memory:900000000,available:true},{token:'c',pid:3,name:'protected.exe',title:'',memory:2,available:false}]});const sorted=document.querySelector('.process-row strong').textContent==='large.exe';const disabled=document.querySelectorAll('.process-row:disabled').length===1;document.querySelector('#process-dialog').close();return sorted&&disabled;})(),main:!!document.querySelector('#main > *'),platformSelector:!!document.querySelector('#add-form [name=platform]'),exePicker:!!document.querySelector('#selected-exe'),games:library,sessionPagination:(()=>{navigate('home');navigate('sessions');const initial=document.querySelectorAll('#main .session-row').length;GaidenUI.components.loadMoreSessions();const expanded=document.querySelectorAll('#main .session-row').length;navigate('home');navigate('sessions');return initial===30&&expanded===60&&document.querySelectorAll('#main .session-row').length===30;})(),sessionRows:document.querySelectorAll('#main .session-row').length,nativeReady:typeof window.applyNativeSnapshot==='function',navigation:document.querySelectorAll('[data-view]').length,styles:[...document.styleSheets].map(s=>s.href),origin:location.origin});})()");
                        if(Environment.GetCommandLineArgs().Contains("--phase4c-review")){await PhaseFourCChecksAsync(result);return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase4b-review")){await PhaseFourBChecksAsync(result);return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase4a-review")){await PhaseFourAChecksAsync(result);return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase3-review")){await PhaseThreeChecksAsync(result);return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase2-review")){await PhaseTwoChecksAsync(result);return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase1d-review")){await PhaseOneDesktopChecksAsync(result);return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase1c-review")){await PhaseOneCorrectionChecksAsync(result);return;}
                        if(Environment.GetCommandLineArgs().Contains("--phase1b-review")){await PhaseOneGlassChecksAsync(result);return;}
                        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-result.json"), result);
                        if(Environment.GetCommandLineArgs().Contains("--features-preview"))await FeatureUiChecksAsync();
                        if(Environment.GetCommandLineArgs().Contains("--phase0-review"))await PhaseZeroUiChecksAsync();
                        if(Environment.GetCommandLineArgs().Contains("--design-preview")) {
                            await web.ExecuteScriptAsync("prefs.reduced=true;applyPrefs();");
                            foreach(var scene in new[]{"light-home","dark-stats","light-detail","dark-detail","dark-detail-bottom"}) {
                                bool dark=scene.StartsWith("dark");
                                await web.ExecuteScriptAsync("prefs.dark="+(dark?"true":"false")+";applyPrefs();");
                                if(scene.EndsWith("home"))await web.ExecuteScriptAsync("navigate('home')");
                                if(scene.EndsWith("stats"))await web.ExecuteScriptAsync("navigate('stats')");
                                if(scene.EndsWith("detail"))await web.ExecuteScriptAsync("document.querySelector('#detail-dialog').close();openDetail(games[0].id);document.querySelector('#detail-dialog').scrollTop=0;");
                                if(scene.EndsWith("bottom"))await web.ExecuteScriptAsync("document.querySelector('#detail-dialog').scrollTop=99999;");
                                await Task.Delay(250);
                                using(var shot=File.Create(Path.Combine(AppContext.BaseDirectory,scene+".png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
                            }
                            var check=await web.ExecuteScriptAsync("(()=>{const d=document.querySelector('#detail-dialog');const input=d.querySelector('#edit-game-name'),select=d.querySelector('#game-status');return {sections:d.querySelectorAll('.detail-section').length,dangerSeparate:!d.querySelector('.detail-danger').contains(input),fieldsEqual:Math.abs(input.getBoundingClientRect().height-select.getBoundingClientRect().height)<1,fieldHeight:input.getBoundingClientRect().height,contentBlur:getComputedStyle(d).backdropFilter,coverAbove:d.querySelector('.detail-art').getBoundingClientRect().top<d.querySelector('.detail-body').getBoundingClientRect().top};})()");
                            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"design-check.json"),check);
                        }
                        if(Environment.GetCommandLineArgs().Contains("--steam-preview")) {
                            await web.ExecuteScriptAsync("prefs.reduced=true;applyPrefs();navigate('library');document.querySelector('[data-steam-open]').click();window.applySteamMessage({steamPreview:{steamId:'76561198000000000',games:[{appId:10,name:'Counter-Strike',minutes:12345},{appId:20,name:'Team Fortress Classic',minutes:90},{appId:30,name:'Day of Defeat',minutes:0}]},token:'visual-test'});document.querySelector('#steam-dialog').scrollTop=99999;");
                            await Task.Delay(200);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"steam-layout-check.json"),await web.ExecuteScriptAsync("({height:document.querySelector('#steam-dialog').getBoundingClientRect().height,rows:document.querySelectorAll('.steam-row').length,open:document.querySelector('#steam-dialog').open})"));using(var shot=File.Create(Path.Combine(AppContext.BaseDirectory,"steam-preview.png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
                            await web.ExecuteScriptAsync("document.querySelector('#steam-dialog').close()");
                        }
                        if(Environment.GetCommandLineArgs().Contains("--process-preview")) {
                            await AddExeFromTrayAsync();
                            var trayCheck=await web.ExecuteScriptAsync("({add:document.querySelector('#add-dialog').open,choice:document.querySelector('#process-dialog').open})");
                            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"tray-check.json"),System.Text.Json.JsonSerializer.Serialize(new{items=menu.Items.Cast<ToolStripItem>().Select(x=>x.Text),ui=trayCheck}));
                            await web.ExecuteScriptAsync("document.querySelector('#process-dialog').close();document.querySelector('#add-dialog').close();window.chooseGameExe({action:'pickNewExe'});document.querySelector('[data-process-load]').click();");
                            await Task.Delay(1200);
                            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"process-layout-check.json"),await web.ExecuteScriptAsync("({open:document.querySelector('#process-dialog').open,height:document.querySelector('#process-dialog').getBoundingClientRect().height,rows:document.querySelectorAll('.process-row').length,first:document.querySelector('.process-row strong')?.textContent})"));
                            using(var shot=File.Create(Path.Combine(AppContext.BaseDirectory,"process-preview.png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
                            await web.ExecuteScriptAsync("document.querySelector('#process-dialog').close();");
                            await web.ExecuteScriptAsync("prefs.dark=false;applyPrefs();navigate('home');const hero=document.querySelector('.hero');hero.querySelector('.icon-cover,.placeholder-cover')?.remove();const image=document.createElement('img');image.className='game-art';const data=games[0].legacy.icon.data;image.src='data:image/'+(data.startsWith('/9j/')?'jpeg':'png')+';base64,'+data;hero.prepend(image);");
                            await Task.Delay(400);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"hero-contrast-check.json"),await web.ExecuteScriptAsync("({text:getComputedStyle(document.querySelector('.hero .glass-button')).color,background:getComputedStyle(document.querySelector('.hero .glass-button')).backgroundColor})"));
                            using(var shot=File.Create(Path.Combine(AppContext.BaseDirectory,"hero-contrast.png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
                        }
                        if(Environment.GetCommandLineArgs().Contains("--review-preview")) {
                            var checks=await web.ExecuteScriptAsync("(()=>{prefs.reduced=true;applyPrefs();const baseline=structuredClone(window.gaidenBootstrap);const id=baseline.games[0].id;openDetail(id);document.querySelector('#edit-game-name').value='Unsaved name';document.querySelector('#game-notes').value='Unsaved notes';document.querySelector('#game-status').value='finished';const changed=structuredClone(baseline);changed.games[0].customCover='data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=';window.applyCoverSnapshot({id,snapshot:changed});const changedInline=!!document.querySelector('#detail-dialog[open] .detail-art img.game-art');window.applyCoverSnapshot({id,snapshot:baseline});const draft=document.querySelector('#detail-dialog').open&&document.querySelector('#edit-game-name').value==='Unsaved name'&&document.querySelector('#game-notes').value==='Unsaved notes'&&document.querySelector('#game-status').value==='finished';document.querySelector('#detail-dialog').close();const external=structuredClone(baseline);external.games.push({id:'ee697981-f154-460c-9378-8486c6cfb0bb',name:'Steam newest',minutes:0,sessions:0,platform:'PC',status:'backlog',notes:'',lastPlayedAt:'2000-01-01T00:00:00Z',steam:{minutes:600,lastPlayedAt:'2099-01-01T00:00:00Z'}});window.applyNativeSnapshot(external);navigate('home');const steamHero=document.querySelector('.hero h2').textContent==='Steam newest'&&document.querySelector('.hero-tag').textContent.includes('Steam');const totals=displayMinutes({minutes:120,steam:{minutes:100}})===120&&displayMinutes({minutes:0,steam:{minutes:600}})===600;const flatBackground=getComputedStyle(document.body).backgroundImage==='none';const brief=structuredClone(baseline);brief.sessions=[{id:'short',gameId:id,legacyGameName:'Short',startedAt:'2026-10-21T00:00:00Z',activeSeconds:42,activeMinutes:0}];window.applyNativeSnapshot(brief);navigate('sessions');const shortTime=document.querySelector('tbody').textContent.includes('42 с');window.applyNativeSnapshot(baseline);navigate('home');return {coverChangedInline:changedInline,coverResetPreservesDraft:draft,steamSortedByLatestDate:steamHero,steamMinutesWithoutDoubleCount:totals,flatBackground:flatBackground,shortSessionInSeconds:shortTime};})()");
                            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"review-ui-result.json"),checks);
                            await Task.Delay(300);
                        }
                        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"removed-economy-check.json"),await web.ExecuteScriptAsync("(()=>{prefs.reduced=true;applyPrefs();document.querySelector('#detail-dialog').close();navigate('computer');const hardware=!!document.querySelector('#hardware-info');const pcGone=!document.querySelector('.pc-economy,#pc-economy-form');openDetail(games[0].id);const gameGone=!document.querySelector('#game-price,#game-price-currency,.game-value-stat');document.querySelector('#detail-dialog').close();return{hardware,pcGone,gameGone};})()"));
                        await web.ExecuteScriptAsync("navigate('stats')");if(Environment.GetCommandLineArgs().Contains("--preview-detail"))await web.ExecuteScriptAsync("openDetail(games[0].id);document.querySelector('.exe-binding').scrollIntoView({block:'center'});");await Task.Delay(300);if(!Environment.GetCommandLineArgs().Contains("--no-capture"))using(var picture=File.Create(Path.Combine(AppContext.BaseDirectory,"statistics-preview.png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,picture);
                        if (!e.IsSuccess) Environment.ExitCode = 1;
                    }
                    catch(Exception error) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"smoke-error.txt"),error.ToString());Environment.ExitCode = 1; }
                    finally { exiting = true; Close(); }
                };
            }
            web.CoreWebView2.Navigate("https://gaiden.local/index.html#home");
            if(tracker is not null){trackerTimer.Start();AutomaticBackup();backupTimer.Start();}
        }
        catch (Exception error)
        {
            MessageBox.Show(this, NativeText("Could not open the interface. Check that Microsoft Edge WebView2 Runtime is installed.\n\n","Не удалось открыть интерфейс. Проверьте установку Microsoft Edge WebView2 Runtime.\n\n") + error.Message, "Questlio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            exiting = true;
            Close();
        }
    }

    private async Task ImportAsync()
    {
        if (web.CoreWebView2 is null) return;
        using var dialog = new OpenFileDialog { Title = NativeText("Import library","Импорт библиотеки"), Filter = NativeText("JSON package|*.json","Пакет JSON|*.json"), CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try {
            if (new FileInfo(dialog.FileName).Length > 100 * 1024 * 1024) throw new InvalidDataException("Пакет больше 100 МБ");
            var text = await File.ReadAllTextAsync(dialog.FileName);
            var p = LibraryStore.ParsePackage(text);
            var g = p["normalized"]!["games"]!.AsArray(); var s = p["normalized"]!["sessions"]!.AsArray();
            var minutes = g.Sum(x => x!["playMinutes"]!.GetValue<long>());
            var historyMinutes = s.Sum(x => x!["activeMinutes"]!.GetValue<long>());
            var counts = g.Sum(x => x!["legacySessionCount"]!.GetValue<long>());
            var preview = nativeLanguage=="ru" ? $"Игр: {g.Count}\nСессий в истории: {s.Count}\nДневных записей: {p["rawTables"]!["daily_playtime"]!.AsArray().Count}\n\nСчётчики игр: {minutes} мин / {counts} сессий\nИстория: {historyMinutes} мин / {s.Count} сессий\n\nИсходные значения и расхождения будут сохранены отдельно. Оригинальная база не изменится.\n\nИмпортировать?" : $"Games: {g.Count}\nSession history: {s.Count}\nDaily records: {p["rawTables"]!["daily_playtime"]!.AsArray().Count}\n\nGame totals: {minutes} min / {counts} sessions\nHistory: {historyMinutes} min / {s.Count} sessions\n\nOriginal values and differences will be saved separately. The original database will not be changed.\n\nImport?";
            if (!await ConfirmUiAsync(preview,"Проверка импорта","Импортировать")) return;
            bool added = store.Import(text);
            await RefreshBootstrapAsync();
            await web.ExecuteScriptAsync("window.applyNativeSnapshot(" + store.Snapshot().ToJsonString() + ")");
            await NativeNoticeAsync(added ? "Библиотека импортирована." : "Этот пакет уже импортирован. Дубликаты не созданы.");
        } catch (Exception error) { DiagnosticLog.Error("import",error);await NativeNoticeAsync("Ошибка импорта: "+error.Message); }
    }

    private async Task RefreshBootstrapAsync()
    {
        if (bootstrapId is not null) web.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(bootstrapId);
        var snapshot=store.Snapshot();snapshot["version"]=AppInfo.Version;
        bootstrapId = await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.gaidenFeatureSettings="+FeatureSettings().ToJsonString()+";window.gaidenCoverState="+CoverBootstrap().ToJsonString()+";window.gaidenBootstrap=" + snapshot.ToJsonString() + ";");
        _ = RefreshSteamCoversAsync();
    }

    private async Task UpdateTrackerAsync(bool refresh)
    {
        if(web.CoreWebView2 is null)return;
        if(WindowState==FormWindowState.Minimized||!Visible||web.CoreWebView2.IsSuspended){trackerSnapshotDeferred|=refresh;trackerUiDeferred=true;return;}
        refresh|=trackerSnapshotDeferred;trackerSnapshotDeferred=false;
        if(refresh){await RefreshBootstrapAsync();await web.ExecuteScriptAsync("window.applyNativeSnapshot?.("+store.Snapshot().ToJsonString()+")");if(!maintenance&&tracker is not null)AutomaticBackup();}
        var text=tracker?.Status??"Проверка интерфейса";
        string trayStatus=NativeStatus(text);
        tray.Text=("Questlio — "+trayStatus)[..Math.Min(63,("Questlio — "+trayStatus).Length)];
        if(WindowState==FormWindowState.Minimized||!Visible){trackerUiDeferred=true;return;}
        var state=new {text,name=tracker?.Current?.GameName,seconds=tracker?.Current?.ActiveSeconds??0,active=tracker?.Enabled==true};
        string signature=System.Text.Json.JsonSerializer.Serialize(state);
        if(signature==trackerUiSignature&&!trackerUiDeferred)return;
        trackerUiSignature=signature;trackerUiDeferred=false;
        await web.ExecuteScriptAsync("window.showTrackerState?.("+signature+")");
    }
    private async Task WaitForPollingAsync(){while(polling)await Task.Delay(15);}
    private Task powerTransition=Task.CompletedTask;
    private void PowerChanged(object? sender,Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if(IsDisposed||!IsHandleCreated||e.Mode==Microsoft.Win32.PowerModes.StatusChange)return;
        BeginInvoke(new Action(()=>{powerTransition=ApplyPowerChangeAsync(powerTransition,e.Mode);}));
    }
    private async Task ApplyPowerChangeAsync(Task previous,Microsoft.Win32.PowerModes mode)
    {
        await previous;if(closingSession||maintenance||IsDisposed)return;
        trackerTimer.Stop();
        try {await WaitForPollingAsync();if(closingSession||IsDisposed)return;
            if(mode==Microsoft.Win32.PowerModes.Suspend)tracker?.Suspend();
            if(mode==Microsoft.Win32.PowerModes.Resume){tracker?.Resume();trackerTimer.Start();}
        }catch{if(!IsDisposed)trackerTimer.Stop();}
    }

    private void AutomaticBackup()
    {
        try{store.CreateAutoBackupIfChanged();}
        catch(Exception error){DiagnosticLog.Error("backup",error);if(web.CoreWebView2 is not null)web.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(new{backupWarning="Не удалось создать копию: "+error.Message}));}
    }
    private async Task BackupActionAsync(string action)
    {
        if(action=="backupFolder"){Directory.CreateDirectory(store.BackupFolder);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(store.BackupFolder){UseShellExecute=true});return;}
        if(action=="backupList"){web.CoreWebView2.PostWebMessageAsJson(new System.Text.Json.Nodes.JsonObject{["backups"]=store.BackupList()}.ToJsonString());return;}
        maintenance=true;trackerTimer.Stop();backupTimer.Stop();bool enabled=tracker?.Enabled==true;
        try{
            while(polling)await Task.Delay(25);
            string text;
            if(action=="backupCreate"){store.CreateBackup();text="Резервная копия создана";}
            else{
                using var picker=new OpenFileDialog{Title=NativeText("Restore Questlio database","Восстановить базу Questlio"),Filter=NativeText("Questlio database backup|*.db","Копия базы Questlio|*.db"),InitialDirectory=store.BackupFolder,CheckFileExists=true};
                if(picker.ShowDialog(this)!=DialogResult.OK)return;
                LibraryStore.ValidateBackup(picker.FileName);
                if(!await ConfirmUiAsync("Заменить библиотеку данными выбранной копии? Текущая сессия будет сохранена, а текущая база — скопирована отдельно перед заменой.","Восстановление Questlio","Восстановить"))return;
                tracker?.Stop("restore");store.RestoreBackup(picker.FileName);tracker=new GameTracker(store);if(!enabled)tracker.Stop();
                await RefreshBootstrapAsync();await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());window.applyNativeSnapshot("+store.Snapshot().ToJsonString()+")");
                text="База восстановлена. Предыдущая база сохранена в страховочной копии";
            }
            web.CoreWebView2.PostWebMessageAsJson(new System.Text.Json.Nodes.JsonObject{["backups"]=store.BackupList(),["backupMessage"]=text}.ToJsonString());
        }finally{maintenance=false;if(tracker is not null){if(enabled&&!tracker.Enabled)tracker.Start();trackerTimer.Start();backupTimer.Start();}}
    }

    private static bool IsLocal(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "gaiden.local" && uri.IsDefaultPort;

    private void RestoreWindow()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void WndProc(ref Message message)
    {
        if(message.Msg is var testMessage&&(testMessage==Program.UpgradeMessage||testMessage==Program.ShowMessage)&&Environment.GetCommandLineArgs().Contains("--smoke-test"))return;
        if(message.Msg==Program.UpgradeMessage){if(maintenance)return;exiting=true;Close();return;}
        if (message.Msg == Program.ShowMessage) RestoreWindow();
        base.WndProc(ref message);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { quickView?.Dispose();Microsoft.Win32.SystemEvents.UserPreferenceChanged-=NativePreferenceChanged;Microsoft.Win32.SystemEvents.PowerModeChanged-=PowerChanged;backupTimer.Stop();backupTimer.Dispose();trackerTimer.Stop();trackerTimer.Dispose();tray.Visible = false; tray.Dispose(); menu.Dispose(); web.Dispose(); }
        base.Dispose(disposing);
        if(disposing){trayArtwork.Dispose();Icon?.Dispose();}
    }
}








