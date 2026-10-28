using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
namespace GaidenDesktop;
internal static class AutoStatusChecks
{
    internal static void Run(string probe)
    {
        string folder=Path.Combine(Path.GetTempPath(),"Gaiden-8A-"+Guid.NewGuid());Directory.CreateDirectory(folder);var lines=new List<string>();
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);lines.Add(label+" PASS");}
        string exe=Path.Combine(folder,"gaiden-8a-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(probe,exe);
        var store=new LibraryStore(Path.Combine(folder,"test.db"));store.CreateWithExe("Hollow Knight",exe);
        JsonNode Game(string id)=>store.Snapshot()["games"]!.AsArray().Single(g=>g!["id"]!.GetValue<string>()==id)!;
        string id=store.Snapshot()["games"]![0]!["id"]!.GetValue<string>();
        for(int run=0;run<2;run++){
            using var p=Process.Start(store.GameStart(id))!;Thread.Sleep(180);var tracker=new GameTracker(store);tracker.Poll();
            var g=Game(id);Check(g["lastPlayedAt"] is not null&&g["sessions"]!.GetValue<int>()==run+1,"confirmed start updates metadata "+run);
            Check(LibraryStore.LaunchPath(g)==exe&&g["launchPaths"] is JsonObject,"EXE selection and repeated real launch "+run);
            if(run==0){Check(g["status"]!.GetValue<string>()=="playing"&&g["autoStatus"]!.GetValue<bool>(),"backlog becomes playing");store.ConfirmSessionStart(tracker.Current!);Check(Game(id)["sessions"]!.GetValue<int>()==1,"confirmation idempotent");store.UndoAutoStatus(id,tracker.Current!.Id);Check(Game(id)["status"]!.GetValue<string>()=="backlog"&&Game(id)["autoStatus"]!.GetValue<bool>()==false,"Undo persists manual flag");}
            else Check(g["status"]!.GetValue<string>()=="backlog","Undo prevents second automatic change");
            p.Kill();p.WaitForExit();tracker.Poll();Check(Game(id)["sessions"]!.GetValue<int>()==run+1,"finish does not double count "+run);
        }
        string manual=Guid.NewGuid().ToString();store.SaveGames(new JsonArray(new JsonObject{["id"]=manual,["name"]="Celeste",["platform"]="PC",["status"]="backlog"}));store.SaveGames(new JsonArray(new JsonObject{["id"]=manual,["name"]="Celeste",["status"]="backlog",["statusEdited"]=true}));
        var session=new ActiveSession{GameId=manual,GameName="Celeste"};store.ConfirmSessionStart(session);Check(Game(manual)["status"]!.GetValue<string>()=="backlog","manual backlog preserved");store.Finish(session,"test");
        string legacy=Guid.NewGuid().ToString();store.SaveGames(new JsonArray(new JsonObject{["id"]=legacy,["name"]="Hades",["platform"]="PC",["status"]="backlog"}));store.DeleteGame(id);store.BindExe(legacy,Path.GetFileName(exe));
        // Only the legacy record matches this temporary executable now.
        using(var p=Process.Start(new ProcessStartInfo(exe){UseShellExecute=false})!){Thread.Sleep(180);var tracker=new GameTracker(store);tracker.Poll();Check(Game(legacy)["launchPath"]?.GetValue<string>()==exe,"legacy name-only path learned from real process");p.Kill();p.WaitForExit();tracker.Poll();}
        Check(Game(legacy)["sessions"]!.GetValue<int>()==1,"legacy launch counted once");
        var stale=(JsonObject)Game(legacy).DeepClone();stale["status"]="backlog";stale["statusEdited"]=false;stale["notes"]="Draft preserved";store.SaveGames(new JsonArray(stale));Check(Game(legacy)["status"]!.GetValue<string>()=="playing","notes draft cannot revert automatic status");
        File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("GAIDEN_CHECK_OUTPUT")??AppContext.BaseDirectory,"auto-status.log"),lines);
    }
}
internal sealed partial class MainWindow
{
    private async Task PhaseEightAChecksAsync()
    {
        string folder=Environment.GetEnvironmentVariable("GAIDEN_PHASE8A_OUTPUT")??Path.Combine(AppContext.BaseDirectory,"phase8a");Directory.CreateDirectory(folder);var log=new List<string>();
        void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);log.Add(name+" PASS");}
        async Task Js(string name,string script)=>Check(await web.ExecuteScriptAsync(script)=="true",name);
        string probe=Environment.GetEnvironmentVariable("GAIDEN_PROBE")??throw new InvalidOperationException("GAIDEN_PROBE");
        string exe=Path.Combine(Path.GetTempPath(),"Gaiden-8A-ui-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(probe,exe);store.CreateWithExe("Hollow Knight",exe);
        string id=store.Snapshot()["games"]!.AsArray().Single(g=>g!["name"]!.GetValue<string>()=="Hollow Knight")!["id"]!.GetValue<string>();
        Size=new Size(1280,800);Show();Activate();await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setFocusEmulationEnabled","{\"enabled\":true}");
        await RefreshBootstrapAsync();await web.ExecuteScriptAsync("GaidenKit.clearToasts();GaidenTheme.select('dark');window.applyNativeSnapshot("+store.Snapshot().ToJsonString()+");navigate('library');openDetail("+JsonSerializer.Serialize(id)+")");
        await Task.Delay(400);tracker=new GameTracker(store);
        Process[] Processes()=>Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe));
        try{
            await web.ExecuteScriptAsync("document.querySelector('.detail-play').click()");
            for(int i=0;i<80;i++){var ps=Processes();bool exists=ps.Length>0;foreach(var p in ps)p.Dispose();if(exists)break;await Task.Delay(50);}
            await Task.Run(()=>tracker.Poll());if(tracker.Current is null)File.WriteAllText(Path.Combine(folder,"launch-ui.log"),await web.ExecuteScriptAsync("({toast:document.querySelector('#toast')?.textContent,button:document.querySelector('.detail-play')?.outerHTML,ready:!!window.chrome.webview,game:document.querySelector('#detail-dialog').dataset.gameId})"));Check(tracker.Current?.GameId==id,"real Play process confirmed; found="+Processes().Length+"; current="+tracker.Current?.GameName);await UpdateTrackerAsync(true);await Task.Delay(300);
            await Js("open detail metadata refreshed without reopen","document.querySelector('#detail-dialog').open&&document.querySelectorAll('.detail-stat strong')[1].textContent==='1'&&document.querySelectorAll('.detail-stat strong')[2].textContent==='Сегодня'&&document.querySelector('#game-status').value==='playing'&&document.querySelector('#game-status + .segmented .selected').dataset.value==='playing'");
            await Js("automatic status Undo toast","document.querySelector('#toast').textContent.includes('статус: Играю')&&document.querySelector('#toast button').textContent==='Отменить'");
            async Task Capture(string name){if(Environment.GetCommandLineArgs().Contains("--checks-no-captures"))return;await web.ExecuteScriptAsync("document.activeElement?.blur()");await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseMoved\",\"x\":1180,\"y\":740}");await Task.Delay(120);using var bitmap=new Bitmap(Width,Height);using var graphics=Graphics.FromImage(bitmap);var dc=graphics.GetHdc();try{PrintWindow(Handle,dc,2);}finally{graphics.ReleaseHdc(dc);}using var stream=new MemoryStream();await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);stream.Position=0;using var preview=Image.FromStream(stream);var origin=web.PointToScreen(Point.Empty);graphics.DrawImage(preview,new Rectangle(origin.X-Location.X,origin.Y-Location.Y,web.Width,web.Height));bitmap.Save(Path.Combine(folder,name+".png"),System.Drawing.Imaging.ImageFormat.Png);}
            await Capture("dark-detail-after-launch");await web.ExecuteScriptAsync("document.querySelector('#detail-dialog').close()");await Task.Delay(100);await Capture("dark-auto-status-undo");
            await web.ExecuteScriptAsync("document.querySelector('#toast button').click()");
            for(int i=0;i<80;i++){if(store.Snapshot()["games"]!.AsArray().Single(g=>g!["id"]!.GetValue<string>()==id)!["autoStatus"]?.GetValue<bool>()==false)break;await Task.Delay(25);}
            Check(store.Snapshot()["games"]!.AsArray().Single(g=>g!["id"]!.GetValue<string>()==id)!["status"]!.GetValue<string>()=="backlog","Undo bridge persisted to SQLite");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setFocusEmulationEnabled","{\"enabled\":false}");
            suspendWebView=false;WindowState=FormWindowState.Minimized;await Task.Delay(1000);var before=await MeasureProcessCpuAsync(6000);
            suspendWebView=true;for(int attempt=0;attempt<20&&!web.CoreWebView2.IsSuspended;attempt++){while(webActivityBusy)await Task.Delay(25);await SyncWebViewActivityAsync();await Task.Delay(100);}Check(web.CoreWebView2.IsSuspended,"minimized WebView suspended");await Task.Delay(1000);var after=await MeasureProcessCpuAsync(6000);
            await Task.Run(()=>tracker.Poll());Check(tracker.Current?.GameId==id,"C# tracker continues while WebView suspended");
            WindowState=FormWindowState.Normal;Show();Activate();await SyncWebViewActivityAsync();await Task.Delay(300);Check(!web.CoreWebView2.IsSuspended,"restored WebView resumed");
            log.Add($"CPU minimized before WebView={before.WebViewPercent:F2}% renderer={before.RendererPercent:F2}%; after WebView={after.WebViewPercent:F2}% renderer={after.RendererPercent:F2}% (6s samples, one logical core=100%)");
            Check(LibraryStore.LaunchPath(store.Snapshot()["games"]!.AsArray().Single(g=>g!["id"]!.GetValue<string>()==id)!)==exe,"launchPaths retained");
        }finally{WindowState=FormWindowState.Normal;foreach(var p in Processes()){using(p){if(!p.HasExited){p.Kill();p.WaitForExit();}}}tracker?.Stop();tracker=null;}
        File.WriteAllLines(Path.Combine(folder,"checks.log"),log);
    }
}
