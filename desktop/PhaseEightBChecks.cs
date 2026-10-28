using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace GaidenDesktop;

internal sealed partial class MainWindow
{
    private async Task PhaseEightBChecksAsync()
    {
        string folder=Environment.GetEnvironmentVariable("GAIDEN_PHASE8B_OUTPUT")??Path.Combine(AppContext.BaseDirectory,"phase8b");
        Directory.CreateDirectory(folder);
        bool debug=Environment.GetCommandLineArgs().Contains("--phase8b-debug");
        string debugFolder=Path.Combine(folder,"debug");if(debug)Directory.CreateDirectory(debugFolder);
        var log=new List<string>();
        void Check(bool value,string name){if(!value){try{File.WriteAllLines(Path.Combine(folder,"checks.log"),log);}catch{}throw new InvalidOperationException(name);}log.Add(name+" PASS");}
        async Task<string> Eval(string js)=>await web.ExecuteScriptAsync(js);
        async Task True(string name,string js){string raw=await Eval(js);Check(raw=="true",name+" => "+raw);}

        string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--fixture");
        if(at<0||at+1>=args.Length)throw new InvalidOperationException("Нужна фикстура --fixture");
        var captures=new LibraryStore(Path.Combine(Path.GetTempPath(),"Gaiden-8b-capture-"+Guid.NewGuid(),"capture.db"));
        captures.Import(File.ReadAllText(args[at+1]));
        int[] coverIds={990001,990002,990003,990004};string[] names={"Hades","Hollow Knight","Celeste","Stardew Valley"};Color[] colors={Color.DarkSlateBlue,Color.Firebrick,Color.SeaGreen,Color.SteelBlue};
        var incoming=new JsonArray();var selection=new JsonArray();
        for(int i=0;i<4;i++){incoming.Add(new JsonObject{["appId"]=coverIds[i],["name"]=names[i],["minutes"]=1200+i*600,["lastPlayedAt"]=DateTimeOffset.Now.AddDays(-i-1).ToString("O")});selection.Add(new JsonObject{["appId"]=coverIds[i],["gameId"]=""});File.WriteAllBytes(coverCache!.FileFor((uint)coverIds[i]),CoverChecks.Jpeg(colors[i],names[i]));}
        captures.ImportSteam(new JsonObject{["steamId"]="76561198000000000",["games"]=incoming},selection);
        var fixture=captures.Snapshot();fixture["version"]=AppInfo.Version;
        var steamGames=fixture["games"]!.AsArray().Where(g=>g!["steam"] is not null).ToArray();
        var hero=steamGames[0]!;string heroId=hero["id"]!.GetValue<string>();
        hero["minutes"]=660;hero["sessions"]=14;hero["status"]="playing";hero["lastPlayedAt"]=DateTimeOffset.Now.AddMinutes(-30).ToString("O");hero["steam"]!["minutes"]=1200;
        steamGames[1]!["status"]="finished";
        var iconGame=fixture["games"]!.AsArray().First(g=>g!["steam"] is null&&g["legacy"]?["icon"]?["data"] is not null);string iconId=iconGame["id"]!.GetValue<string>();
        iconGame["lastPlayedAt"]=DateTimeOffset.Now.AddDays(-20).ToString("O");
        fixture["emulators"]=new JsonArray(
            new JsonObject{["id"]="8b-emu-pcsx2",["name"]="PCSX2",["exe"]="pcsx2-qt",["automatic"]=true,["extensions"]=new JsonArray("iso","chd","bin"),["core"]=""},
            new JsonObject{["id"]="8b-emu-retro",["name"]="RetroArch",["exe"]="retroarch",["automatic"]=false,["extensions"]=new JsonArray("sfc","nes","gba"),["core"]="snes9x_libretro.dll"});
        var sessions=fixture["sessions"]!.AsArray();
        void AddSession(DateTime started,int seconds)=>sessions.Add(new JsonObject{["id"]=Guid.NewGuid().ToString(),["gameId"]=heroId,["legacyGameName"]=hero["name"]!.GetValue<string>(),["startedAt"]=new DateTimeOffset(started).ToString("O"),["activeSeconds"]=seconds,["source"]="gaiden-tracker"});
        AddSession(DateTime.Now.AddMinutes(-130).AddSeconds(-DateTime.Now.Second),2700);
        AddSession(DateTime.Now.AddMinutes(-40).AddSeconds(-DateTime.Now.Second),1800);
        for(int i=1;i<=6;i++)AddSession(DateTime.Today.AddDays(-i).AddHours(19),1800+i*240);
        for(int i=7;i<=13;i++)AddSession(DateTime.Today.AddDays(-i).AddHours(19),1200+i*300);

        Size=new Size(1280,800);WindowState=FormWindowState.Normal;Show();Activate();BringToFront();
        await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setFocusEmulationEnabled","{\"enabled\":true}");
        string fixtureJson=fixture.ToJsonString();
        string coverJson=JsonSerializer.Serialize(coverIds);
        await Eval("window.__p8bFixture="+fixtureJson+";window.__p8bHero="+JsonSerializer.Serialize(heroId)+";window.__p8bIcon="+JsonSerializer.Serialize(iconId)+";window.__p8bCovers="+coverJson+";window.__p8bApplyOriginal=window.applyNativeSnapshot;window.__p8bTrackerOriginal=window.showTrackerState;window.__p8bReset=(theme,view)=>{GaidenKit.clearToasts();document.querySelectorAll('dialog[open]').forEach(d=>d.close());window.__p8bApplyOriginal(window.__p8bFixture);prefs.reduced=false;prefs.opaque=false;applyPrefs();GaidenTheme.select(theme==='dark'?'dark':'light');window.gaidenCoverState={enabled:true,pending:[],available:window.__p8bCovers};modelMedia.failedSteamCovers.clear();window.__p8bTrackerOriginal({name:'Hades',seconds:5432,active:true});navigate(view);scrollTo(0,0);};");
        await Eval("window.__p8bReset('light','home')");await Task.Delay(500);

        bool fontPublished=File.Exists(Path.Combine(AppContext.BaseDirectory,"ui","fonts","InterVariable.ttf"));Check(fontPublished,"InterVariable.ttf published");
        string fontState="false";
        for(int i=0;i<40;i++){fontState=await Eval("(()=>{const face=[...document.fonts].find(f=>f.family.replace(/[\"']/g,'')==='Inter');return document.fonts.check('16px Inter')&&face?.status==='loaded'&&getComputedStyle(document.body).fontFamily.startsWith('Inter');})()");if(fontState=="true")break;await Task.Delay(100);}
        Check(fontState=="true","Inter loaded offline via font-face");
        await True("no external network resources","[...performance.getEntriesByType('resource')].filter(e=>/^https?:/.test(e.name)).every(e=>/^https:\\/\\/(covers\\.)?gaiden\\.local\\//.test(e.name))");
        log.Add("font perf="+await Eval("JSON.stringify([...performance.getEntriesByType('resource')].filter(e=>/InterVariable/.test(e.name)).map(e=>({name:e.name,transferSize:e.transferSize,decodedBodySize:e.decodedBodySize,initiatorType:e.initiatorType})))"));
        await True("font resource served by virtual host","[...performance.getEntriesByType('resource')].some(e=>e.name===location.origin+'/fonts/InterVariable.ttf')");
        await True("tabular numbers on metrics and detail","(()=>{const probe=document.createElement('div');probe.className='metric-row';document.body.append(probe);const ok=getComputedStyle(probe).fontVariantNumeric.includes('tabular-nums');probe.remove();return ok&&getComputedStyle(document.querySelector('.metric-row')).fontVariantNumeric.includes('tabular-nums');})()");
        await True("large heading tracking","(()=>{const h=document.querySelector('h1'),s=getComputedStyle(h);return Math.abs(parseFloat(s.letterSpacing)/parseFloat(s.fontSize)+.03)<.005;})()");

        await True("week Monday hides comparison","(()=>{const snap={sessions:[{startedAt:new Date(2026,9,26,10).toISOString(),activeSeconds:3600},{startedAt:new Date(2026,9,19,10).toISOString(),activeSeconds:1800}]};const p=GaidenStatistics.period(snap,'week',new Date(2026,9,26,12));return p.elapsedDays===1&&p.before.seconds===1800&&p.change===null;})()");
        await True("week Tuesday compares equal two days","(()=>{const snap={sessions:[{startedAt:new Date(2026,9,26,10).toISOString(),activeSeconds:3600},{startedAt:new Date(2026,9,27,10).toISOString(),activeSeconds:7200},{startedAt:new Date(2026,9,19,10).toISOString(),activeSeconds:1800},{startedAt:new Date(2026,9,20,10).toISOString(),activeSeconds:3600},{startedAt:new Date(2026,9,21,10).toISOString(),activeSeconds:9999}]};const p=GaidenStatistics.period(snap,'week',new Date(2026,9,27,12));return p.elapsedDays===2&&p.current.seconds===10800&&p.before.seconds===5400&&p.change===100;})()");
        await True("zero base and short segment hide comparison","(()=>{const only={sessions:[{startedAt:new Date(2026,9,26,10).toISOString(),activeSeconds:3600}]};const p=GaidenStatistics.period(only,'week',new Date(2026,9,27,12));const monday=GaidenStatistics.period(only,'week',new Date(2026,9,26,12));return p.before.seconds===0&&p.change===null&&monday.change===null;})()");
        await True("month comparison uses equal elapsed days","(()=>{const snap={sessions:[{startedAt:new Date(2026,8,28,10).toISOString(),activeSeconds:1111},{startedAt:new Date(2026,8,10,10).toISOString(),activeSeconds:2222},{startedAt:new Date(2026,9,1,10).toISOString(),activeSeconds:3333},{startedAt:new Date(2026,9,27,10).toISOString(),activeSeconds:4444}]};const p=GaidenStatistics.period(snap,'month',new Date(2026,9,27,12));return p.elapsedDays===27&&p.current.seconds===7777&&p.before.seconds===2222;})()");
        await True("year comparison uses equal elapsed days","(()=>{const snap={sessions:[{startedAt:new Date(2025,9,27,10).toISOString(),activeSeconds:1111},{startedAt:new Date(2025,9,28,10).toISOString(),activeSeconds:2222},{startedAt:new Date(2026,0,1,10).toISOString(),activeSeconds:3333}]};const p=GaidenStatistics.period(snap,'year',new Date(2026,9,27,12));return p.elapsedDays===300&&p.current.seconds===3333&&p.before.seconds===1111;})()");
        await True("Monday statistics screen hides comparison","new Date().getDay()===1&&(()=>{GaidenUI.moduleState('statistics').period='week';navigate('stats');return !document.querySelector('.period-change')&&GaidenStatistics.period(GaidenUI.state.snapshot).before.seconds>0;})()");

        await Eval("window.__p8bClock={now:0,id:0,timers:new Map(),advance(ms){this.now+=ms;for(const [id,t] of [...this.timers])if(t.at<=this.now){this.timers.delete(id);t.fn();}}};GaidenKit.deletions.useClock({now:()=>__p8bClock.now,set:(fn,ms)=>{const id=++__p8bClock.id;__p8bClock.timers.set(id,{fn,at:__p8bClock.now+ms,ms});return id;},clear:id=>__p8bClock.timers.delete(id)});GaidenKit.clearToasts();GaidenKit.toast('Информационное уведомление')");
        log.Add("fake toast="+await Eval("JSON.stringify({toasts:GaidenKit.diagnostics().toasts,hidden:document.hidden,state:document.visibilityState,focus:document.hasFocus(),timers:[...__p8bClock.timers.values()].map(t=>t.ms),html:document.querySelector('#toast')?.outerHTML.slice(0,120)})"));
        await True("info toast is scheduled for 2.5s","GaidenKit.diagnostics().toasts===1&&[...__p8bClock.timers.values()].some(t=>t.ms===2500)");
        await Eval("__p8bClock.advance(2499)");await True("info toast remains at 2.499s","GaidenKit.diagnostics().toasts===1");
        await Eval("__p8bClock.advance(1)");await True("info toast expires at 2.5s","GaidenKit.diagnostics().toasts===0");
        await Eval("GaidenKit.toast('Действие выполнено',{action:'Отменить',run:()=>{}})");
        await True("action toast is scheduled for 6s","GaidenKit.diagnostics().toasts===1&&[...__p8bClock.timers.values()].some(t=>t.ms===6000)");
        await Eval("__p8bClock.advance(5999)");await True("action toast remains at 5.999s","GaidenKit.diagnostics().toasts===1");
        await Eval("__p8bClock.advance(1)");await True("action toast expires at 6s","GaidenKit.diagnostics().toasts===0");
        await Eval("GaidenKit.clearToasts();window.__p8bTopA=GaidenKit.toast('Первое уведомление',{action:'Отменить',run:()=>{}}).element.getBoundingClientRect().top;GaidenKit.toast('Второе уведомление',{action:'Отменить',run:()=>{}});");
        await True("toast stack uses transform animation","(()=>{const first=document.querySelector('#toast .ui-toast');return first.getAnimations().some(a=>a.effect.getKeyframes().some(k=>String(k.transform||'').includes('translateY')));})()");
        await Task.Delay(260);
        await True("stack shifts previous toast without jump","(()=>{const first=document.querySelector('#toast .ui-toast');return first.getBoundingClientRect().top<window.__p8bTopA-4;})()");
        await Eval("GaidenKit.clearToasts();GaidenKit.deletions.useClock(null)");
        await True("toast host is transparent and unfiltered","(()=>{GaidenKit.toast('Проверка');const host=document.querySelector('#toast'),capsule=host.querySelector('.ui-toast'),h=getComputedStyle(host),c=getComputedStyle(capsule);return host.classList.contains('show')&&h.backgroundColor==='rgba(0, 0, 0, 0)'&&h.backdropFilter==='none'&&h.borderRadius==='0px'&&h.boxShadow==='none'&&parseFloat(h.borderWidth)===0&&parseFloat(c.borderRadius)>=999&&c.backgroundColor!=='rgba(0, 0, 0, 0)'&&c.boxShadow!=='none';})()");
        log.Add("toast capsule backdrop-filter="+await Eval("getComputedStyle(document.querySelector('#toast .ui-toast')).backdropFilter"));
        await Eval("GaidenKit.clearToasts();navigate('library');openDetail(window.__p8bHero)");await Task.Delay(450);
        await Eval("GaidenKit.toast('Уведомление поверх листа')");
        log.Add("sheet toast="+await Eval("JSON.stringify((()=>{const host=document.querySelector('#toast'),h=getComputedStyle(host),d=document.querySelector('#detail-dialog');return {open:d?.open,parent:host.parentElement?.id||host.parentElement?.tagName,closest:host.closest('dialog')?.id,bg:h.backgroundColor,filter:h.backdropFilter,radius:h.borderRadius,border:h.borderWidth,shadow:h.boxShadow,show:host.classList.contains('show')};})())"));
        await True("toast host stays transparent inside sheet","(()=>{const host=document.querySelector('#toast'),h=getComputedStyle(host);return document.querySelector('#detail-dialog').open&&h.backgroundColor==='rgba(0, 0, 0, 0)'&&h.backdropFilter==='none'&&h.borderRadius==='0px';})()");
        await Eval("GaidenKit.clearToasts();document.querySelector('#detail-dialog .detail-more').click()");
        await True("context menu inside sheet is a rounded surface","(()=>{const menu=document.querySelector('#detail-dialog .ui-menu'),s=getComputedStyle(menu);return !!menu&&menu.closest('dialog')?.open===true&&parseFloat(s.borderRadius)>=12&&s.backgroundColor!=='rgba(0, 0, 0, 0)'&&s.boxShadow!=='none';})()");
        await Eval("GaidenKit.closeMenu(false);document.querySelector('#detail-dialog .detail-more').dispatchEvent(new PointerEvent('pointerover',{bubbles:true,relatedTarget:document.body}))");await Task.Delay(700);
        await True("tooltip inside sheet owns its rounded surface","(()=>{const tip=document.querySelector('#detail-dialog .ui-tooltip'),s=getComputedStyle(tip);return !!tip&&parseFloat(s.borderRadius)>=10&&s.backgroundColor!=='rgba(0, 0, 0, 0)'&&s.boxShadow!=='none';})()");
        await Eval("document.querySelector('#detail-dialog .detail-more').dispatchEvent(new PointerEvent('pointerout',{bubbles:true,relatedTarget:document.body}))");

        await Eval("document.querySelector('#detail-dialog').close();navigate('library');document.querySelector('[data-steam-open]').click();window.applySteamMessage({steamPreview:{steamId:'76561198000000000',games:[{appId:990010,name:'Disco Elysium',minutes:500}]},token:'fixture'})");await Task.Delay(250);
        await Eval("document.querySelector('.steam-row .ui-select').click()");
        await True("select menu inside sheet is a rounded surface","(()=>{const menu=document.querySelector('#steam-dialog .ui-menu'),s=getComputedStyle(menu);return !!menu&&menu.closest('dialog')?.open===true&&parseFloat(s.borderRadius)>=12&&s.backgroundColor!=='rgba(0, 0, 0, 0)';})()");
        await Eval("GaidenKit.closeMenu(false);document.querySelector('#steam-dialog').close()");

        async Task OpenDetail(string id){await Eval("document.querySelector('#detail-dialog').close();navigate('library');openDetail("+JsonSerializer.Serialize(id)+")");await Task.Delay(450);}
        await Eval("window.__p8bReset('dark','library')");await Task.Delay(150);await OpenDetail(heroId);
        await True("detail labels stay next to numbers","(()=>{const v=document.querySelector('#detail-dialog .detail-value'),strong=v.querySelector('strong').getBoundingClientRect(),source=v.querySelector('.detail-source').getBoundingClientRect(),steam=document.querySelector('#detail-dialog .detail-steam-time');return Math.abs((strong.top+strong.height/2)-(source.top+source.height/2))<8&&source.left>=strong.right-1&&source.left-strong.right<24&&/· в Questlio/.test(v.textContent)&&/· в Steam/.test(steam.textContent);})()");
        async Task<bool> PosterGap(int width,int height){await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride",JsonSerializer.Serialize(new{width,height,deviceScaleFactor=1,mobile=false}));await Task.Delay(180);await OpenDetail(heroId);string raw=await Eval("(()=>{const art=document.querySelector('#detail-dialog .detail-art'),poster=art.querySelector(':scope > .game-art'),more=document.querySelector('#detail-dialog .detail-more').getBoundingClientRect(),close=document.querySelector('#detail-dialog .close-dialog').getBoundingClientRect(),p=poster?.getBoundingClientRect();if(!p)return false;return Math.min(more.left-p.right,close.left-p.right)>=12&&document.querySelector('#detail-dialog').scrollWidth<=document.querySelector('#detail-dialog').clientWidth+1;})()");await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride","{}");await Task.Delay(120);Check(raw=="true","poster clears more/close by 12px at "+width+"x"+height+" => "+raw);return raw=="true";}
        Check(await PosterGap(820,600),"detail poster gap 820");Check(await PosterGap(1280,800),"detail poster gap 1280");
        await OpenDetail(iconId);
        await True("icon header uses dominant gradient and 64px squircle","(()=>{const art=document.querySelector('#detail-dialog .detail-art'),icon=art.querySelector('.game-icon'),s=getComputedStyle(art),i=icon.getBoundingClientRect(),is=getComputedStyle(icon);return art.classList.contains('detail-art-icon')&&s.backgroundImage.includes('linear-gradient')&&!s.backgroundImage.includes('url(')&&getComputedStyle(art,'::before').display==='none'&&Math.round(i.width)===64&&Math.round(i.height)===64&&parseFloat(is.borderRadius)>=14;})()");
        await OpenDetail(heroId);
        string baseColor=JsonSerializer.Deserialize<string>(await Eval("getComputedStyle(document.querySelector('#detail-dialog .detail-play')).backgroundColor"))!;
        var point=JsonNode.Parse(await Eval("(()=>{const r=document.querySelector('#detail-dialog .detail-play').getBoundingClientRect();return {x:r.left+r.width/2,y:r.top+r.height/2};})()"))!;
        foreach(string type in new[]{"mouseMoved"})await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type,x=point["x"]!.GetValue<double>(),y=point["y"]!.GetValue<double>()}));await Task.Delay(300);
        log.Add("play base="+baseColor+" hover="+await Eval("JSON.stringify((()=>{const b=document.querySelector('#detail-dialog .detail-play'),s=getComputedStyle(b);return {hover:b.matches(':hover'),bg:s.backgroundColor,transform:s.transform,rect:b.getBoundingClientRect().toJSON()};})())"));
        log.Add("play assert="+await Eval("JSON.stringify((()=>{const b=document.querySelector('#detail-dialog .detail-play'),hover=getComputedStyle(b).backgroundColor,base="+JsonSerializer.Serialize(baseColor)+";const parse=s=>{let m=String(s).match(/^rgba?\\(([^)]+)\\)/);if(m)return m[1].split(/[ ,\\/]+/).slice(0,3).map(Number);m=String(s).match(/^color\\(srgb ([^)]+)\\)/);if(m)return m[1].split(/[ \\/]+/).slice(0,3).map(v=>v*255);return[];};const expected=parse(base).map(v=>Math.round(v*.92+255*.08));return {matches:b.matches(':hover'),base,hover,parsed:parse(hover),expected};})())"));
        await True("Play hover lightens by the 8% mix","(()=>{const b=document.querySelector('#detail-dialog .detail-play'),hover=getComputedStyle(b).backgroundColor,base="+JsonSerializer.Serialize(baseColor)+";const parse=s=>{let m=String(s).match(/^rgba?\\(([^)]+)\\)/);if(m)return m[1].split(/[ ,\\/]+/).slice(0,3).map(Number);m=String(s).match(/^color\\(srgb ([^)]+)\\)/);if(m)return m[1].split(/[ \\/]+/).slice(0,3).map(v=>v*255);return[];};const expected=parse(base).map(v=>Math.round(v*.92+255*.08));return b.matches(':hover')&&parse(hover).every((v,i)=>Math.abs(v-expected[i])<=1);})()");
        foreach(string type in new[]{"mousePressed"})await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type,x=point["x"]!.GetValue<double>(),y=point["y"]!.GetValue<double>(),button="left",clickCount=1}));await Task.Delay(240);
        log.Add("play pressed="+await Eval("JSON.stringify((()=>{const b=document.querySelector('#detail-dialog .detail-play'),s=getComputedStyle(b);return {active:b.matches(':active'),transform:s.transform,hover:b.matches(':hover')};})())"));
        await True("Play pressed scales to .98","(()=>{const m=getComputedStyle(document.querySelector('#detail-dialog .detail-play')).transform;const n=m.match(/matrix\\(([^,]+)/);return !!n&&Math.abs(Number(n[1])-.98)<.01;})()");
        foreach(string type in new[]{"mouseReleased"})await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type,x=point["x"]!.GetValue<double>(),y=point["y"]!.GetValue<double>(),button="left",clickCount=1}));await Task.Delay(120);

        await Eval("window.__p8bTrackerOriginal({name:'Очень длинное название игры для проверки многоточия в трекере',seconds:5432,active:true})");
        await True("tracker has two rows, ellipsis and full title","(()=>{const t=document.querySelector('.tracker'),label=t.querySelector('.tracker-label'),strong=t.querySelector('strong'),sep=t.querySelector('.tracker-sep');const ls=getComputedStyle(label),ss=getComputedStyle(strong),ps=getComputedStyle(sep);return !!sep&&sep.textContent===' · '&&ps.position==='absolute'&&ps.width==='1px'&&!label.hidden&&ls.fontSize==='11px'&&label.getBoundingClientRect().bottom<=strong.getBoundingClientRect().top+1&&strong.scrollWidth>strong.clientWidth&&ss.textOverflow==='ellipsis'&&ss.whiteSpace==='nowrap'&&t.title===strong.textContent&&t.textContent.trim()==='Сейчас играет · '+strong.textContent;})()");
        await Eval("window.__p8bTrackerOriginal({name:'Hades',seconds:5432,active:true})");

        async Task CompactAt(int width){await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride",JsonSerializer.Serialize(new{width,height=800,deviceScaleFactor=1,mobile=false}));await Task.Delay(150);await Eval("navigate('library')");await Task.Delay(150);string raw=await Eval("(()=>{const cards=[...document.querySelectorAll('.game-card')];return cards.length>0&&cards.every(c=>{const p=c.querySelector('.game-meta-platform').getBoundingClientRect(),t=c.querySelector('.game-meta-time'),r=t.getBoundingClientRect();return /^(< 1 мин|\\d+ мин|\\d+ ч( \\d+ мин)?)$/.test(t.textContent.trim())&&r.left>=p.right-1&&r.right<=c.getBoundingClientRect().right+1;})&&document.documentElement.scrollWidth<=innerWidth+1;})()");await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride","{}");await Task.Delay(100);Check(raw=="true","compact card time clears platform at "+width+" => "+raw);}
        await CompactAt(820);await CompactAt(1280);
        await True("compact formatter boundary","fmtCompact(59)==='< 1 мин'&&fmtCompact(60)==='1 мин'&&fmtCompact(7140)==='1 ч 59 мин'&&fmt(59)==='меньше минуты'");

        await Eval("navigate('home')");await Task.Delay(150);
        await True("overview today chips live in metric grid","(()=>{const chips=document.querySelectorAll('.overview-metrics .today-chips .today-chip'),box=document.querySelector('.overview-metrics .today-chips');return chips.length===2&&box.closest('.metric-row')===document.querySelector('.overview-metrics')&&getComputedStyle(box).display==='flex'&&getComputedStyle(box).gridColumnStart==='1'&&getComputedStyle(box).gridColumnEnd==='-1'&&[...chips].map(c=>c.textContent).join('|').includes('Сегодня')&&[...chips].map(c=>c.textContent).join('|').includes('Серия');})()");
        await Eval("window.__p8bApplyOriginal({games:[],sessions:[],daily:[],emulators:[]});navigate('home')");await Task.Delay(120);
        await True("zero overview hides chips and shows first-launch empty state","document.querySelector('.home-empty')&&!document.querySelector('.today-chips')&&!document.querySelector('.hero')&&document.querySelectorAll('.home-empty .primary').length===1");
        await Eval("window.__p8bReset('light','home')");await Task.Delay(150);

        await Eval("navigate('computer');GaidenUI.moduleState('hardware').hardware={windows:'Windows 11 Pro 24H2',architecture:'x64',processor:'AMD Ryzen 7 7800X3D',graphics:'NVIDIA GeForce RTX 4070',ram:'32 ГБ',motherboard:'ASUS ROG STRIX B650E-F'};GaidenUI.moduleState('hardware').busy=false;render()");await Task.Delay(150);
        await True("computer uses inset group and no card outline","(()=>{const panel=document.querySelector('#hardware-info'),list=panel.querySelector('.inset-group.hardware-list'),rows=list.querySelectorAll('.hardware-row'),s=getComputedStyle(panel);return s.borderTopWidth==='0px'&&s.borderBottomWidth==='0px'&&s.borderLeftWidth==='0px'&&s.borderRightWidth==='0px'&&getComputedStyle(list).borderRadius==='12px'&&rows.length===6&&getComputedStyle(rows[0]).borderTopWidth==='0px'&&[...rows].slice(1).every(r=>getComputedStyle(r).borderTopWidth==='1px');})()");
        await Eval("navigate('settings');GaidenUI.moduleState('backups').backups=[{kind:'Ручная копия',date:new Date(2026,9,26,9,12).toISOString(),bytes:1835008},{kind:'Автоматическая копия',date:new Date(2026,9,25,21,0).toISOString(),bytes:1769472},{kind:'Страховочная копия',date:new Date(2026,9,24,18,45).toISOString(),bytes:1710000}];render();['Эмуляторы','Резервные копии'].forEach(n=>{const d=[...document.querySelectorAll('.settings-group')].find(x=>x.dataset.group===n);if(d)d.open=true;});");await Task.Delay(200);
        log.Add("settings="+await Eval("JSON.stringify({names:[...document.querySelectorAll('.settings-group summary strong')].map(n=>n.textContent),panels:[...document.querySelectorAll('.settings-panel')].map(p=>getComputedStyle(p).borderTopWidth),emus:document.querySelectorAll('.emulator-list .emulator-row').length,backups:document.querySelectorAll('.backup-list .backup-row').length,open:[...document.querySelectorAll('.settings-group')].map(d=>d.open),html:document.querySelector('#main')?.textContent.slice(0,80)})"));
        await True("settings groups named and inset without outlines","(()=>{const groups=[...document.querySelectorAll('.settings-group')],names=groups.map(g=>g.querySelector('summary strong').textContent);return names.slice(0,3).join('|')==='Внешний вид|Эмуляторы|Резервные копии'&&names.length===5&&[...document.querySelectorAll('.settings-panel')].every(p=>getComputedStyle(p).borderTopWidth==='0px')&&groups.filter(g=>['Эмуляторы','Резервные копии'].includes(g.dataset.group)).every(g=>g.open)&&document.querySelectorAll('.emulator-list .emulator-row').length===2&&document.querySelectorAll('.backup-list .backup-row').length===3;})()");
        await Eval("document.querySelector('[data-edit-emulator]').click()");await Task.Delay(120);
        log.Add("editor="+await Eval("JSON.stringify((()=>{const d=document.querySelector('#emulator-editor'),input=d?.querySelector('input.toggle'),box=d?.querySelector('.ui-switch');return {open:d?.open,hidden:input?.hidden,switch:box?.outerHTML.slice(0,80),primaries:d?.querySelectorAll('.primary').length,forms:d?.querySelectorAll('form').length,html:d?.innerHTML.slice(0,100),emus:(window.gaidenEmulators||[]).map(e=>e.id),button:document.querySelector('[data-edit-emulator]')?.outerHTML.slice(0,140)};})())"));
        await True("emulator editor uses kit switch and one primary","(()=>{const dialog=document.querySelector('#emulator-editor');const input=dialog.querySelector('input.toggle'),box=dialog.querySelector('.ui-switch');const ok=dialog.open&&input.hidden&&!!box&&box.getAttribute('role')==='switch'&&dialog.querySelectorAll('.primary').length===1;dialog.close();return ok;})()");
        await Eval("GaidenUI.moduleState('backups').backups=[];render()");await Task.Delay(80);
        await True("backups empty state offers an action","document.querySelector('.backup-empty button')&&document.querySelectorAll('#backup-panel .primary').length===1");

        await Eval("(()=>{const originalPost=window.chrome.webview.postMessage;try{window.chrome.webview.postMessage=message=>{if(message.action!=='processList')originalPost.call(window.chrome.webview,message);};window.chooseGameExe({action:'pickNewExe'});document.querySelector('[data-process-load]').click();window.applyProcessMessage({processList:[{token:'a',pid:1,name:'small.exe',title:'Small',memory:1048576,available:true},{token:'b',pid:2,name:'large.exe',title:'Large',memory:900000000,available:true},{token:'c',pid:3,name:'protected.exe',title:'',memory:2,available:false}]})}finally{window.chrome.webview.postMessage=originalPost;}})()");await Task.Delay(250);
        await True("process dialog uses inset rows and one primary","(()=>{const picker=document.querySelector('#process-dialog'),list=document.querySelector('#process-results');return picker.open&&list.classList.contains('has-rows')&&getComputedStyle(list).borderRadius==='12px'&&document.querySelectorAll('.process-row').length===3&&getComputedStyle(document.querySelector('.process-row')).borderTopWidth==='0px'&&picker.querySelectorAll('.primary').length===1&&document.querySelectorAll('.process-row:disabled').length===1;})()");
        await Eval("document.querySelector('#process-dialog').close();navigate('library');document.querySelector('[data-steam-open]').click();window.applySteamMessage({steamPreview:{steamId:'76561198000000000',games:[{appId:990001,name:'Hades',minutes:1200},{appId:990002,name:'Hollow Knight',minutes:1800},{appId:990003,name:'Celeste',minutes:95}]},token:'fixture'})");await Task.Delay(250);
        await True("steam sheet uses kit select and inset rows","(()=>{const dialog=document.querySelector('#steam-dialog'),list=document.querySelector('#steam-results'),native=document.querySelector('.steam-row select');return dialog.open&&list.classList.contains('has-rows')&&getComputedStyle(list).borderRadius==='12px'&&native.hidden&&native.dataset.uiSelect==='true'&&document.querySelectorAll('.steam-row .ui-select').length===3&&document.querySelectorAll('.steam-row').length===3;})()");
        await Eval("document.querySelector('#steam-dialog').close();document.querySelector('#add-dialog').showModal()");await Task.Delay(100);
        await True("add sheet has one primary and borderless entries","(()=>{const d=document.querySelector('#add-dialog');return d.open&&d.querySelectorAll('.primary').length===1&&[...d.querySelectorAll('.add-entry')].every(e=>{const s=getComputedStyle(e);return s.borderTopWidth==='0px'||s.borderTopColor==='rgba(0, 0, 0, 0)';});})()");
        await Eval("document.querySelector('#add-dialog').close();navigate('library');(()=>{const field=document.querySelector('#library-search');field.value='Несуществующая игра';field.dispatchEvent(new Event('input',{bubbles:true}));})()");await Task.Delay(120);
        log.Add("empty search="+await Eval("JSON.stringify({view,search,empties:document.querySelectorAll('#library-grid .ui-empty').length,html:document.querySelector('#library-grid')?.innerHTML.slice(0,240)})"));
        await True("empty search has magnifier and no action","(()=>{const e=document.querySelector('#library-grid .ui-empty');return !!e&&e.textContent.includes('Ничего не найдено')&&!!e.querySelector('use[href=\"#search\"]')&&!e.querySelector('button');})()");
        await Eval("const s=document.querySelector('#library-search');s.value='';s.dispatchEvent(new Event('input',{bubbles:true}))");

        foreach(int width in new[]{820,1280,1920}){
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride",JsonSerializer.Serialize(new{width,height=900,deviceScaleFactor=1,mobile=false}));
            foreach(string view in new[]{"home","library","stats","sessions","settings","computer"}){
                await Eval("navigate('"+view+"')");await Task.Delay(100);
                string raw=await Eval("document.documentElement.scrollWidth<=innerWidth+1");
                Check(raw=="true","no horizontal overflow "+view+" @"+width+" => "+raw);
            }
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride","{}");
        }

        await Eval("GaidenTheme.select('light');prefs.opaque=false;applyPrefs();navigate('home')");
        foreach(string mode in new[]{"light","dark","opaque"}){
            await Eval("prefs.opaque="+(mode=="opaque"?"true":"false")+";GaidenTheme.select('"+(mode=="dark"?"dark":"light")+"');navigate('home')");await Task.Delay(160);
            var audit=JsonNode.Parse(await Eval(PhaseOneAuditScript+";phaseOneAudit.collect('8b-"+mode+"')"))!;if(audit["failures"]!.AsArray().Count>0)throw new InvalidOperationException("8B contrast "+mode+": "+audit["failures"]!.ToJsonString());log.Add("contrast home "+mode+" PASS");
        }
        await Eval("prefs.opaque=true;applyPrefs();navigate('home');GaidenKit.toast('Непрозрачный режим')");await True("opaque mode keeps solid toast without filter","(()=>{const h=getComputedStyle(document.querySelector('#toast')),c=getComputedStyle(document.querySelector('#toast .ui-toast'));return document.documentElement.dataset.opaque==='true'&&h.backdropFilter==='none'&&c.backdropFilter==='none'&&c.backgroundColor!=='rgba(0, 0, 0, 0)';})()");await Eval("GaidenKit.clearToasts();prefs.opaque=false;applyPrefs()");
        await Eval("GaidenKit.clearToasts();navigate('library');openDetail(window.__p8bHero)");await Task.Delay(450);await Eval("GaidenKit.toast('Тост поверх листа')");await True("toast fits sheet without square host corners","document.querySelector('#toast').closest('dialog')?.open===true&&getComputedStyle(document.querySelector('#toast')).borderRadius==='0px'");await Eval("GaidenKit.clearToasts();document.querySelector('#detail-dialog').close()");

        await Eval("window.__p8bCaptureReset=()=>{GaidenKit.clearToasts();document.querySelectorAll('dialog[open]').forEach(d=>d.close());window.gaidenCoverState={enabled:true,pending:[],available:window.__p8bCovers};modelMedia.failedSteamCovers.clear();prefs.opaque=false;prefs.reduced=false;applyPrefs();scrollTo(0,0);};");

        async Task Capture(string name,string setup,string? hover=null,string? output=null,int width=1280,int height=800,int extra=0){
            Size=new Size(width,height);WindowState=FormWindowState.Normal;Show();Activate();BringToFront();await Task.Delay(240);
            await Eval(setup);await Task.Delay(420);
            await Eval("GaidenGlass.refresh()");await Task.Delay(180+extra);
            if(hover is null&&extra==0){await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type="mouseMoved",x=2,y=2}));await Task.Delay(80);}
            if(hover is not null){
                string raw=await Eval("(()=>{const el=document.querySelector("+JsonSerializer.Serialize(hover)+");if(!el)return null;const r=el.getBoundingClientRect();return JSON.stringify({x:r.left+r.width/2,y:r.top+r.height/2});})()");
                string? value=JsonSerializer.Deserialize<string>(raw);if(value is not null){var p=JsonNode.Parse(value)!;await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type="mouseMoved",x=p["x"]!.GetValue<double>(),y=p["y"]!.GetValue<double>()}));await Task.Delay(280);string hovering=await Eval("document.querySelector("+JsonSerializer.Serialize(hover)+").matches(':hover')");if(hovering!="true")throw new InvalidOperationException("hover "+name+" "+hovering);}
            }
            string overflow=await Eval("document.documentElement.scrollWidth<=innerWidth+1&&(!document.querySelector('dialog[open]')||[...document.querySelectorAll('dialog[open]')].every(d=>d.scrollWidth<=d.clientWidth+1))");
            if(overflow!="true")throw new InvalidOperationException("overflow "+name+" "+overflow);
            if(Environment.GetCommandLineArgs().Contains("--checks-no-captures")){log.Add(name+" checks PASS (capture disabled)");return;}
            Activate();BringToFront();await Task.Delay(160);
            using var bitmap=new Bitmap(Width,Height);using var graphics=Graphics.FromImage(bitmap);IntPtr dc=graphics.GetHdc();try{PrintWindow(Handle,dc,2);}finally{graphics.ReleaseHdc(dc);}
            using(var stream=new MemoryStream()){await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);stream.Position=0;using var preview=Image.FromStream(stream);var origin=web.PointToScreen(Point.Empty);graphics.DrawImage(preview,new Rectangle(origin.X-Location.X,origin.Y-Location.Y,web.Width,web.Height));}
            string target=Path.Combine(output??folder,name+".png");bitmap.Save(target,System.Drawing.Imaging.ImageFormat.Png);log.Add(name+" captured "+width+"x"+height);
        }

        await Eval("window.applyNativeSnapshot=()=>{};window.showTrackerState=()=>{};window.__p8bApplyFrozen=window.__p8bApplyOriginal;");

        if(debug){
            async Task Snap(string name,string setup,string? hover=null,int w=1280,int h=800,int extra=0)=>await Capture(name,setup,hover,debugFolder,w,h,extra);
            string common="window.__p8bCaptureReset();window.__p8bApplyFrozen(window.__p8bFixture);";
            await Snap("toast-light-stack",common+"GaidenTheme.select('light');navigate('home');GaidenKit.toast('Запускаю Hades…');GaidenKit.toast('Сессия сохранена',{action:'Отменить',run:()=>{}})");
            await Snap("toast-dark-stack",common+"GaidenTheme.select('dark');navigate('home');GaidenKit.toast('Запускаю Hades…');GaidenKit.toast('Сессия сохранена',{action:'Отменить',run:()=>{}})");
            await Snap("toast-light-sheet",common+"GaidenTheme.select('light');navigate('library');openDetail(window.__p8bHero);GaidenKit.toast('Тост поверх листа',{action:'Отменить',run:()=>{}})");
            await Snap("toast-dark-sheet",common+"GaidenTheme.select('dark');navigate('library');openDetail(window.__p8bHero);GaidenKit.toast('Тост поверх листа',{action:'Отменить',run:()=>{}})");
            await Snap("toast-light-stack-820",common+"GaidenTheme.select('light');navigate('home');GaidenKit.toast('Запускаю Hades…');GaidenKit.toast('Сессия сохранена',{action:'Отменить',run:()=>{}})",null,820,600);
            await Snap("toast-dark-sheet-820",common+"GaidenTheme.select('dark');navigate('library');openDetail(window.__p8bHero);GaidenKit.toast('Тост поверх листа',{action:'Отменить',run:()=>{}})",null,820,600);
            await Snap("menu-light-sheet",common+"GaidenTheme.select('light');navigate('library');openDetail(window.__p8bHero);(()=>{const go=()=>{const b=document.querySelector('#detail-dialog .detail-more');if(document.querySelector('#detail-dialog').open&&b)b.click();else setTimeout(go,60)};setTimeout(go,60)})()");
            await Snap("menu-dark-sheet",common+"GaidenTheme.select('dark');navigate('library');openDetail(window.__p8bHero);(()=>{const go=()=>{const b=document.querySelector('#detail-dialog .detail-more');if(document.querySelector('#detail-dialog').open&&b)b.click();else setTimeout(go,60)};setTimeout(go,60)})()");
            await Snap("select-light-sheet",common+"GaidenTheme.select('light');navigate('library');document.querySelector('[data-steam-open]').click();window.applySteamMessage({steamPreview:{steamId:'76561198000000000',games:[{appId:990010,name:'Disco Elysium',minutes:500}]},token:'fixture'});(()=>{const go=()=>{const b=document.querySelector('#steam-dialog .steam-row .ui-select');if(b)b.click();else setTimeout(go,60)};setTimeout(go,60)})()");
            await Snap("tooltip-light-sheet",common+"GaidenTheme.select('light');navigate('library');openDetail(window.__p8bHero);(()=>{const go=()=>{const b=document.querySelector('#detail-dialog .detail-more');if(document.querySelector('#detail-dialog').open&&b)b.dispatchEvent(new PointerEvent('pointerover',{bubbles:true,relatedTarget:document.body}));else setTimeout(go,60)};setTimeout(go,60)})()",null,1280,800,700);
            await Snap("detail-poster-light",common+"GaidenTheme.select('light');navigate('library');openDetail(window.__p8bHero)");
            await Snap("detail-poster-dark-820",common+"GaidenTheme.select('dark');navigate('library');openDetail(window.__p8bHero)",null,820,600);
            await Snap("detail-icon-light",common+"GaidenTheme.select('light');navigate('library');openDetail(window.__p8bIcon)");
            await Snap("detail-icon-dark-820",common+"GaidenTheme.select('dark');navigate('library');openDetail(window.__p8bIcon)",null,820,600);
            await Snap("stats-light-820",common+"GaidenTheme.select('light');GaidenUI.moduleState('statistics').period='week';navigate('stats')",null,820,600);
            await Snap("stats-dark",common+"GaidenTheme.select('dark');GaidenUI.moduleState('statistics').period='week';navigate('stats')");
            await Snap("overview-light-820",common+"GaidenTheme.select('light');navigate('home')",null,820,600);
            await Snap("overview-dark",common+"GaidenTheme.select('dark');navigate('home')");
            await Snap("tracker-dark-820",common+"GaidenTheme.select('dark');window.__p8bTrackerOriginal({name:'Очень длинное название игры для проверки многоточия',seconds:5432,active:true});navigate('home')",null,820,600);
        }

        string setup="window.__p8bCaptureReset();window.__p8bApplyFrozen(window.__p8bFixture);";
        await Capture("light-stats-monday",setup+"GaidenTheme.select('light');GaidenUI.moduleState('statistics').period='week';GaidenUI.moduleState('statistics').calendar=new Date(new Date().getFullYear(),new Date().getMonth(),1,12);GaidenUI.moduleState('statistics').selected=GaidenStatistics.key(new Date());navigate('stats')");
        await Capture("dark-detail-hover-play",setup+"GaidenTheme.select('dark');navigate('library');openDetail(window.__p8bHero)",".detail-play");
        await Capture("dark-overview",setup+"GaidenTheme.select('dark');navigate('home')");
        await Capture("light-computer",setup+"GaidenTheme.select('light');navigate('computer');GaidenUI.moduleState('hardware').hardware={windows:'Windows 11 Pro 24H2',architecture:'x64',processor:'AMD Ryzen 7 7800X3D',graphics:'NVIDIA GeForce RTX 4070',ram:'32 ГБ',motherboard:'ASUS ROG STRIX B650E-F'};GaidenUI.moduleState('hardware').busy=false;render()");
        await Capture("light-emulators",setup+"GaidenTheme.select('light');GaidenUI.moduleState('backups').backups=[{kind:'Ручная копия',date:new Date(2026,9,26,9,12).toISOString(),bytes:1835008},{kind:'Автоматическая копия',date:new Date(2026,9,25,21,0).toISOString(),bytes:1769472},{kind:'Страховочная копия',date:new Date(2026,9,24,18,45).toISOString(),bytes:1710000}];navigate('settings');document.querySelectorAll('.settings-group').forEach(d=>d.open=d.dataset.group==='Эмуляторы');document.querySelector('[data-group=\"Эмуляторы\"]')?.scrollIntoView({block:'start'});scrollBy(0,-60)");
        await Capture("light-backups",setup+"GaidenTheme.select('light');GaidenUI.moduleState('backups').backups=[{kind:'Ручная копия',date:new Date(2026,9,26,9,12).toISOString(),bytes:1835008},{kind:'Автоматическая копия',date:new Date(2026,9,25,21,0).toISOString(),bytes:1769472},{kind:'Страховочная копия',date:new Date(2026,9,24,18,45).toISOString(),bytes:1710000}];navigate('settings');document.querySelectorAll('.settings-group').forEach(d=>d.open=d.dataset.group==='Резервные копии');document.querySelector('[data-group=\"Резервные копии\"]')?.scrollIntoView({block:'start'});scrollBy(0,-60)");
        await Capture("dark-processes",setup+"GaidenTheme.select('dark');navigate('home');window.chooseGameExe({action:'pickNewExe'});document.querySelector('#process-panel').hidden=false;window.applyProcessMessage({processList:[{token:'a',pid:4412,name:'Hades.exe',title:'Hades',memory:3221225472,available:true},{token:'b',pid:1180,name:'steam.exe',title:'Steam',memory:734003200,available:true},{token:'c',pid:902,name:'explorer.exe',title:'Проводник',memory:104857600,available:true},{token:'d',pid:5150,name:'protected.exe',title:'',memory:2,available:false}]});setTimeout(()=>window.applyProcessMessage({processList:[{token:'a',pid:4412,name:'Hades.exe',title:'Hades',memory:3221225472,available:true},{token:'b',pid:1180,name:'steam.exe',title:'Steam',memory:734003200,available:true},{token:'c',pid:902,name:'explorer.exe',title:'Проводник',memory:104857600,available:true},{token:'d',pid:5150,name:'protected.exe',title:'',memory:2,available:false}]}),260)");
        await Capture("light-steam",setup+"GaidenTheme.select('light');navigate('library');document.querySelector('[data-steam-open]').click();window.applySteamMessage({steamPreview:{steamId:'76561198000000000',games:[{appId:990001,name:'Hades',minutes:1200},{appId:990002,name:'Hollow Knight',minutes:1800}]},token:'fixture'})");
        await Capture("light-add-game",setup+"GaidenTheme.select('light');navigate('home');document.querySelector('#add-dialog').showModal();const f=document.querySelector('#add-form [name=name]');f.value='Silent Hill 2';f.dispatchEvent(new Event('input',{bubbles:true}));");
        await Capture("light-empty-library",setup+"GaidenTheme.select('light');window.__p8bTrackerOriginal({active:false});window.__p8bApplyOriginal({games:[],sessions:[],daily:[],emulators:[]});navigate('library')");

        File.WriteAllLines(Path.Combine(folder,"checks.log"),log);
    }
}
