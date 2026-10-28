using System.Text.Json;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    private async Task PhaseFourBChecksAsync(string smoke)
    {
        string folder=Environment.GetEnvironmentVariable("GAIDEN_PHASE4B_OUTPUT")??Path.Combine(AppContext.BaseDirectory,"phase4b");Directory.CreateDirectory(folder);var log=new List<string>();
        async Task Check(string name,string js){string value=await web.ExecuteScriptAsync(js);if(value!="true")throw new InvalidOperationException(name+" "+value);log.Add(name+" PASS");}
        Show();Activate();BringToFront();await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setFocusEmulationEnabled","{\"enabled\":true}");
        await PhaseFourAChecksAsync(smoke);log.Add("WebView / PhaseZero14 / PhaseOne / Two / Three / 4A PASS");
        // Capture data is separate from the tests: restore the original fixture, not test probes.
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--fixture");var captureStore=new LibraryStore(Path.Combine(Path.GetTempPath(),"Gaiden-4b-capture-"+Guid.NewGuid(),"capture.db"));captureStore.Import(File.ReadAllText(args[at+1]));var fixture=captureStore.Snapshot();fixture["version"]=AppInfo.Version;
        // applyPrefs() applies the test's reduced flag: the app builds the sheet inside the View Transition update callback, so it is readable synchronously only when motion is reduced.
        await web.ExecuteScriptAsync("GaidenKit.clearToasts();document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=true;prefs.opaque=false;applyPrefs();applyNativeSnapshot("+fixture.ToJsonString()+");navigate('home')");await Task.Delay(300);
        await Check("split totals","(()=>{const text=document.querySelector('.overview-metrics').textContent;return text.includes('В Questlio')&&(!games.some(g=>g.steam?.minutes>0)||text.includes('Часы из Steam'))&&!text.includes('Общее время')&&typeof GaidenUI.components.todaySummary==='function';})()");
        await Check("daily streak","(()=>{const s={daily:[{play_date:'2026-10-20',play_time:40},{play_date:'2026-10-21',play_time:30},{play_date:'2026-10-22',play_time:50}],sessions:[]};const t=GaidenUI.components.todaySummary(s,new Date(2026,9,22));return t.minutes===50&&t.streak===3&&t.sessions===0;})()");
        await Check("no Steam inflation","(()=>{const before=games;games=[{minutes:60,steam:{minutes:6000},status:'playing'}];const host=document.createElement('div');host.innerHTML=metrics();const values=[...host.querySelectorAll('.metric-value')].map(x=>x.textContent);games=before;return values[0]==='1 ч'&&values[1]==='100 ч';})()");
        await web.ExecuteScriptAsync("openDetail(games[0].id)");await Check("missing path offers binding","document.querySelector('.detail-play').textContent==='Указать файл игры'&&document.querySelector('.launch-hint').textContent.includes('запомнит путь сам')");
        await web.ExecuteScriptAsync("document.querySelector('#detail-dialog .ui-close').dispatchEvent(new PointerEvent('pointerover',{bubbles:true}))");await Task.Delay(650);
        await Check("sheet tooltip stays inside","(()=>{const d=document.querySelector('#detail-dialog'),t=d.querySelector('.ui-tooltip');if(!t)return false;const dr=d.getBoundingClientRect(),tr=t.getBoundingClientRect();return d.scrollWidth<=d.clientWidth+1&&tr.left>=dr.left-1&&tr.right<=dr.right+1&&tr.top>=dr.top-1&&tr.bottom<=dr.bottom+1;})()");
        await web.ExecuteScriptAsync("document.querySelector('#detail-dialog .ui-close').dispatchEvent(new PointerEvent('pointerout',{bubbles:true}));document.querySelector('#detail-dialog').close()");await Task.Delay(240);
        await web.ExecuteScriptAsync("document.querySelector('#add-form').reset();delete document.querySelector('#selected-exe').dataset.selected;GaidenAdd.validate();document.querySelector('#add-dialog').showModal()");
        await Check("add order and empty file hidden","(()=>{const f=document.querySelector('#add-form'),platform=f.querySelector('.add-platform'),name=f.querySelector('[name=name]');return !f.querySelector('.add-drop')&&f.querySelector('.add-file-chip').hidden&&!!(platform.compareDocumentPosition(name)&Node.DOCUMENT_POSITION_FOLLOWING)&&f.querySelector('[type=submit]').textContent==='Добавить'&&f.querySelectorAll('.add-entry svg').length===3;})()");
        await web.ExecuteScriptAsync("document.querySelector('#detail-dialog').close();document.querySelector('#add-dialog').close();navigate('settings')");await Check("setting descriptions","['Авто следует настройкам Windows','Отключает анимации интерфейса','Заменяет стеклянные поверхности сплошными'].every(text=>document.querySelector('main').textContent.includes(text))");
        foreach(string mode in new[]{"light","dark","opaque"}){
            await web.ExecuteScriptAsync("prefs.reduced=false;prefs.opaque="+(mode=="opaque"?"true":"false")+";GaidenTheme.select('"+(mode=="dark"?"dark":"light")+"');navigate('library');scrollTo(0,280)");await Task.Delay(120);
            await Check("scroll-edge "+mode,"(()=>{const s=getComputedStyle(document.querySelector('.topbar'),'::before');return s.height==='96px'&&s.maskImage.includes('gradient')&&s.opacity==='1'&&s.backdropFilter==='none';})()");
        }
        foreach(string feature in new[]{"prefers-reduced-motion","prefers-reduced-transparency","forced-colors"}){
            await web.ExecuteScriptAsync("prefs.opaque=false;prefs.reduced=false;applyPrefs();navigate('library');scrollTo(0,280)");await EmulateAppearanceAsync((feature,feature=="forced-colors"?"active":"reduce"));await Task.Delay(100);await Check("scroll-edge solid "+feature,"getComputedStyle(document.querySelector('.topbar'),'::before').backdropFilter==='none'");await EmulateAppearanceAsync();
        }
        Size=new Size(1280,800);Show();Activate();
        if(!Environment.GetCommandLineArgs().Contains("--phase4b-no-capture"))foreach(string shot in new[]{"light-home","dark-home","light-add","light-settings","dark-library-scroll"}){
            string view=shot.Contains("settings")?"settings":shot.Contains("scroll")?"library":"home";
            await web.ExecuteScriptAsync("GaidenKit.closeMenu(false);GaidenKit.clearToasts();document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=false;prefs.opaque=false;GaidenTheme.select('"+(shot.StartsWith("dark")?"dark":"light")+"');filter='all';search='';navigate('"+view+"');scrollTo(0,0)");await Task.Delay(280);
            if(shot=="light-add")await web.ExecuteScriptAsync("document.querySelector('#add-form').reset();document.querySelector('#selected-exe').textContent='Файл не выбран';delete document.querySelector('#selected-exe').dataset.selected;GaidenAdd.validate();document.querySelector('#add-dialog').showModal()");
            if(shot=="light-settings")await web.ExecuteScriptAsync("document.querySelectorAll('.settings-group').forEach(d=>d.open=d.dataset.group==='Внешний вид')");
            if(shot.Contains("scroll"))await web.ExecuteScriptAsync("scrollTo(0,290)");
            await Task.Delay(320);
            if(shot=="light-add"){await web.ExecuteScriptAsync("document.querySelector('#add-form [name=name]').value='Vampire Survivors';document.querySelector('#selected-exe').textContent='VampireSurvivors.exe';document.querySelector('#selected-exe').dataset.selected='true';document.querySelector('[data-add-exe]').classList.add('selected');GaidenAdd.validate()");await Task.Delay(100);}
            if(shot=="light-settings"){
                var point=JsonNode.Parse(await web.ExecuteScriptAsync("(()=>{const r=document.querySelector('.nav-item[data-view=settings]').getBoundingClientRect();return {x:r.left+r.width/2,y:r.top+r.height/2};})()"))!;
                foreach(string type in new[]{"mousePressed","mouseReleased"})await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type,x=point["x"]!.GetValue<double>(),y=point["y"]!.GetValue<double>(),button="left",clickCount=1}));await Task.Delay(100);
            }
            await web.ExecuteScriptAsync("document.activeElement?.blur()");await Check("no horizontal overflow "+shot,"document.documentElement.scrollWidth<=innerWidth+1");
            using var bitmap=new Bitmap(Width,Height);using var graphics=Graphics.FromImage(bitmap);IntPtr dc=graphics.GetHdc();bool printed;try{printed=PrintWindow(Handle,dc,2);}finally{graphics.ReleaseHdc(dc);}if(!printed)graphics.CopyFromScreen(Location,Point.Empty,Size);bitmap.Save(Path.Combine(folder,shot+".png"),System.Drawing.Imaging.ImageFormat.Png);log.Add(shot+" captured 1280x800");
        }
        await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close())");File.WriteAllLines(Path.Combine(folder,"checks.log"),log);
    }
}
