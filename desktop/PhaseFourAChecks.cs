using System.Text.Json;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    private async Task PhaseFourAChecksAsync(string smoke)
    {
        string folder=Environment.GetEnvironmentVariable("GAIDEN_PHASE4A_OUTPUT")??Path.Combine(AppContext.BaseDirectory,"phase4a");Directory.CreateDirectory(folder);var log=new List<string>();
        void Assert(bool ok,string name){if(!ok)throw new InvalidOperationException(name);log.Add(name+" PASS");}
        async Task Check(string name,string js){string answer=await web.ExecuteScriptAsync(js);Assert(answer=="true",name+" "+answer);}
        async Task Key(string key,int code){foreach(string type in new[]{"keyDown","keyUp"})await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",JsonSerializer.Serialize(new{type,key,windowsVirtualKeyCode=code}));await Task.Delay(80);}
        int fixtureWithout=store.Snapshot()["games"]!.AsArray().Count(g=>LibraryStore.LaunchPath(g!) is null&&LibraryStore.SteamLaunchId(g!) is null);
        await PhaseThreeChecksAsync(smoke);log.Add("PhaseZero14/PhaseOne/Two/Three/WebView PASS");
        int without=fixtureWithout;log.Add("Fixture games without launch path or Steam ID: "+without);
        string id=Guid.NewGuid().ToString();store.SaveGames(new JsonArray(new JsonObject{["id"]=id,["name"]="Launch fixture",["platform"]="PC",["status"]="backlog",["notes"]=""}));
        try{store.GameStart(id);throw new Exception("missing path accepted");}catch(InvalidOperationException){log.Add("missing path rejected PASS");}
        store.BindExe(id,Environment.ProcessPath!);var plan=store.GameStart(id);Assert(plan.FileName==Environment.ProcessPath&&plan.UseShellExecute&&plan.WorkingDirectory==Path.GetDirectoryName(Environment.ProcessPath),"library-only launch plan");Assert(store.GameStart(id,true).FileName=="explorer.exe","reveal plan");
        Assert(LibraryStore.SteamLaunchId(new JsonObject{["steam"]=new JsonObject{["appId"]="1/../../evil"}}) is null,"Steam ID rejects URL injection");
        Assert(LibraryStore.SteamLaunchId(new JsonObject{["steam"]=new JsonObject{["appId"]=570}})=="570","numeric Steam ID");
        string noPath=store.Snapshot()["games"]!.AsArray().First(g=>LibraryStore.LaunchPath(g!) is null&&LibraryStore.SteamLaunchId(g!) is null)!["id"]!.GetValue<string>();
        await web.ExecuteScriptAsync("window.launchReplies=[];window.chrome.webview.addEventListener('message',e=>{if(e.data.launchError)launchReplies.push(e.data)});window.chrome.webview.postMessage({action:'launchGame',id:'unknown',path:'C:/Windows/notepad.exe'});window.chrome.webview.postMessage({action:'launchGame',id:"+JsonSerializer.Serialize(noPath)+",path:'C:/Windows/notepad.exe'})");await Task.Delay(350);await Check("invalid ID and missing path return launchError","launchReplies.length===2&&launchReplies.every(r=>typeof r.launchError==='string')");
        await web.ExecuteScriptAsync("GaidenKit.clearToasts();document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=true;prefs.opaque=false;GaidenTheme.select('light');applyNativeSnapshot("+store.Snapshot().ToJsonString()+");navigate('library');openDetail("+JsonSerializer.Serialize(id)+")");await Task.Delay(260);
        await web.ExecuteScriptAsync("document.querySelector('#game-notes').value='Autosaved 4A';document.querySelector('#game-notes').dispatchEvent(new Event('input',{bubbles:true}));document.querySelector('#game-status').value='playing';document.querySelector('#game-status').dispatchEvent(new Event('change',{bubbles:true}))");await Task.Delay(850);
        JsonNode Stored()=>store.Snapshot()["games"]!.AsArray().Single(g=>g!["id"]!.GetValue<string>()==id)!;
        Assert(Stored()["notes"]!.GetValue<string>()=="Autosaved 4A"&&Stored()["status"]!.GetValue<string>()=="playing","autosave SQLite status and notes");
        await web.ExecuteScriptAsync("document.querySelector('#edit-game-name').focus();document.querySelector('#edit-game-name').value='Renamed by Enter'");await Key("Enter",13);await Task.Delay(250);Assert(Stored()["name"]!.GetValue<string>()=="Renamed by Enter","inline Enter persists SQLite");
        await web.ExecuteScriptAsync("document.querySelector('#edit-game-name').focus();document.querySelector('#edit-game-name').value='Cancelled'");await Key("Escape",27);await Check("inline Escape cancels","document.querySelector('#detail-dialog').open&&document.querySelector('#edit-game-name').value==='Renamed by Enter'");Assert(Stored()["name"]!.GetValue<string>()=="Renamed by Enter","Escape leaves SQLite intact");
        await web.ExecuteScriptAsync("document.querySelector('#game-notes').value='Debounced notes';document.querySelector('#game-notes').dispatchEvent(new Event('input',{bubbles:true}))");await Task.Delay(850);Assert(Stored()["notes"]!.GetValue<string>()=="Debounced notes","600ms debounce persists SQLite");
        foreach(int width in new[]{820,1280}){Size=new Size(width,width==820?600:800);await web.ExecuteScriptAsync("GaidenKit.toast('Проверка',{action:'Отменить',run(){}})");await Task.Delay(100);await Check("toast outside sheet "+width,"(()=>{const t=document.querySelector('.ui-toast').getBoundingClientRect(),d=document.querySelector('#detail-dialog').getBoundingClientRect();return t.top>=d.bottom+4&&t.bottom<=innerHeight-23;})()");await web.ExecuteScriptAsync("GaidenKit.clearToasts()");}
        Size=new Size(1280,800);
        foreach(string mode in new[]{"light","dark","opaque"}){
            await web.ExecuteScriptAsync("prefs.opaque="+(mode=="opaque"?"true":"false")+";GaidenTheme.select('"+(mode=="dark"?"dark":"light")+"')");
            await Check("new text contrast "+mode,"(()=>{const color=v=>{const c=document.createElement('canvas').getContext('2d');c.fillStyle=v;c.fillRect(0,0,1,1);return [...c.getImageData(0,0,1,1).data].slice(0,3).map(x=>{x/=255;return x<=.04045?x/12.92:((x+.055)/1.055)**2.4})},lum=v=>{const a=color(v);return a[0]*.2126+a[1]*.7152+a[2]*.0722},root=getComputedStyle(document.documentElement),ratio=(a,b)=>{a=lum(a);b=lum(b);return (Math.max(a,b)+.05)/(Math.min(a,b)+.05)};return ['--text','--muted'].every(t=>['--surface','--surface-soft'].every(b=>ratio(root.getPropertyValue(t),root.getPropertyValue(b))>=4.5));})()");
        }
        await web.ExecuteScriptAsync("prefs.opaque=false;document.querySelector('#detail-dialog').close();document.querySelector('#add-dialog').showModal();document.querySelector('#add-form [name=name]').value='Console game';document.querySelector('[data-platform=\"PlayStation 2\"]').click()");await Check("non PC creation valid without EXE","!document.querySelector('#add-form [type=submit]').disabled&&document.querySelector('#add-form [name=platform]').value==='PlayStation 2'");
        int count=store.Snapshot()["games"]!.AsArray().Count;store.CreatePlatformGame("Console backend probe","Nintendo Switch");Assert(store.Snapshot()["games"]!.AsArray().Count==count+1,"additive console creation SQLite");
        await web.ExecuteScriptAsync("document.querySelector('#add-dialog').close()");await Task.Delay(250);await web.ExecuteScriptAsync("applyNativeSnapshot("+store.Snapshot().ToJsonString()+")");
        if(!Environment.GetCommandLineArgs().Contains("--phase4a-no-capture"))foreach(string shot in new[]{"light-detail","dark-detail","light-add","dark-context-play","light-detail-820"}){
            Size=new Size(shot.EndsWith("820")?820:1280,shot.EndsWith("820")?600:800);await web.ExecuteScriptAsync("GaidenKit.clearToasts();GaidenKit.closeMenu(false);document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=false;prefs.opaque=false;GaidenTheme.select('"+(shot.StartsWith("dark")?"dark":"light")+"');navigate('library');scrollTo(0,0)");await Task.Delay(250);
            if(shot=="light-add")await web.ExecuteScriptAsync("document.querySelector('#add-form').reset();document.querySelector('#add-form [name=platform]').value='PC';document.querySelectorAll('[data-platform]').forEach(b=>b.classList.toggle('selected',b.dataset.platform==='PC'));GaidenAdd.validate();document.querySelector('#add-dialog').showModal()");
            else if(shot=="dark-context-play")await web.ExecuteScriptAsync("(()=>{const c=[...document.querySelectorAll('.game-card')].find(c=>c.dataset.game==="+JsonSerializer.Serialize(id)+");c.scrollIntoView({block:'center'});GaidenKit.gameMenu(c);})()");
            else await web.ExecuteScriptAsync("openDetail("+JsonSerializer.Serialize(id)+")");
            await Task.Delay(300);await web.ExecuteScriptAsync("document.activeElement?.blur()");await Task.Delay(80);Show();Activate();
            using var bitmap=new Bitmap(Width,Height);using var graphics=Graphics.FromImage(bitmap);IntPtr dc=graphics.GetHdc();bool printed;try{printed=PrintWindow(Handle,dc,2);}finally{graphics.ReleaseHdc(dc);}if(!printed)graphics.CopyFromScreen(Location,Point.Empty,Size);bitmap.Save(Path.Combine(folder,shot+".png"),System.Drawing.Imaging.ImageFormat.Png);log.Add(shot+" captured");
        }
        await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());GaidenKit.clearToasts()");store.DeleteGame(id);File.WriteAllLines(Path.Combine(folder,"checks.log"),log);
    }
}
