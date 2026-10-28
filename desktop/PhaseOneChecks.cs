using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    private async Task EmulateAppearanceAsync(params (string Name,string Value)[] values)
    {
        await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia",JsonSerializer.Serialize(new {features=values.Select(v=>new {name=v.Name,value=v.Value})}));
        await Task.Delay(80);
    }
    private async Task CapturePhaseOneAsync()
    {
        string path=Environment.GetEnvironmentVariable("GAIDEN_PHASE1_CAPTURE")??Path.Combine(AppContext.BaseDirectory,"after-phase1");
        Directory.CreateDirectory(path);
        await web.ExecuteScriptAsync(PhaseOneAuditScript);
        await web.ExecuteScriptAsync("prefs.reduced=true;prefs.opaque=false;GaidenTheme.select('light');document.querySelector('#toast').style.display='none';const s=document.createElement('style');s.textContent='*{caret-color:transparent}';document.head.append(s);");
        var metadata=new JsonObject();
        foreach(string mode in new[]{"light","dark","opaque","dark-opaque"})
        foreach(var size in new[]{new Size(820,600),new Size(1280,800),new Size(1920,1080)})
        foreach(string scene in new[]{"home","library","stats","sessions","settings","detail","add"})
        {
            Size=size;
            await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.opaque="+(mode.Contains("opaque")?"true":"false")+";GaidenTheme.select("+JsonSerializer.Serialize(mode.StartsWith("dark")?"dark":"light")+");filter='all';search='';navigate("+JsonSerializer.Serialize(scene is "detail" or "add"?"home":scene)+");"+(scene=="detail"?"openDetail(games[0].id);document.querySelector('#detail-dialog').scrollTop=0;":scene=="add"?"document.querySelector('#add-dialog').showModal();":""));
            await Task.Delay(160);
            string key=$"{mode}-{size.Width}x{size.Height}-{scene}";
            metadata[key]=JsonNode.Parse(await web.ExecuteScriptAsync("phaseOneAudit.collect("+JsonSerializer.Serialize(key)+")"));
            using var image=File.Create(Path.Combine(path,key+".png"));
            await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
        }
        // Fixtures exercise existing render branches; they never save the library.
        metadata["supplemental"]=JsonNode.Parse(await web.ExecuteScriptAsync("""
        (()=>{
          const base=structuredClone(GaidenUI.state.snapshot),fixture=structuredClone(base),audits={};
          document.querySelectorAll('dialog[open]').forEach(d=>d.close());
          fixture.emulators=[{id:'4d3c9ff7-cdde-488e-b123-434532ac8c72',name:'PCSX2 fixture',exe:'pcsx2',extensions:['iso'],automatic:true,core:''}];
          fixture.games.slice(0,6).forEach((g,i)=>g.status=['playing','backlog','finished','hold','dropped','forever'][i]);
          const g=fixture.games.find(g=>g.id===games[0].id);g.customCover=null;g.exeIcon=null;g.legacy={};g.executables=[];g.steam=null;g.lastPlayedAt='2099-01-01T00:00:00Z';
          window.applyNativeSnapshot(fixture);prefs.opaque=false;GaidenTheme.select('light');navigate('home');audits.placeholders=phaseOneAudit.collect('placeholders');
          openDetail(g.id);audits.emptyExe=phaseOneAudit.collect('empty-exe');document.querySelector('#detail-dialog').close();
          navigate('settings');document.querySelectorAll('details').forEach(d=>d.open=true);audits.settingsExpanded=phaseOneAudit.collect('settings-expanded');
          document.querySelector('[data-edit-emulator]').click();audits.emulatorEditor=phaseOneAudit.collect('emulator-editor');document.querySelector('#emulator-editor').close();
          navigate('library');phaseOneAudit.cover();search='unmatched-fixture';render();phaseOneAudit.cover();search='';navigate('stats');
          const played=[...document.querySelectorAll('.calendar-day.played')][0];played?.click();audits.calendar=phaseOneAudit.collect('calendar-selected');
          notify('Fixture notification');phaseOneAudit.cover();window.applyNativeSnapshot(base);
          for(const mode of ['light','dark']){
            GaidenTheme.select(mode);navigate('library');document.querySelector('[data-steam-open]').click();audits[mode+'SteamForm']=phaseOneAudit.collect(mode+'-steam-form');
            window.applySteamMessage({steamPreview:{steamId:'76561198000000000',games:[{appId:10,name:'Steam fixture',minutes:60}]},token:'fixture'});audits[mode+'SteamRows']=phaseOneAudit.collect(mode+'-steam-rows');document.querySelector('#steam-dialog').close();
            window.chooseGameExe({action:'pickNewExe'});document.querySelector('[data-process-load]').click();window.applyProcessMessage({processList:[{token:'fixture',pid:999,name:'Fixture.exe',title:'Fixture',memory:1048576,available:true},{token:'unavailable',pid:998,name:'Protected.exe',title:'',memory:1,available:false}]});audits[mode+'Processes']=phaseOneAudit.collect(mode+'-processes');document.querySelector('#process-dialog').close();
          }
          return audits;
        })()
        """));
        // Cover states and auxiliary dialogs are observed for selector coverage, not new product features.
        await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.opaque=false;GaidenTheme.select('light');navigate('settings');document.querySelectorAll('details').forEach(d=>d.open=true);phaseOneAudit.cover();navigate('computer');phaseOneAudit.cover();navigate('library');document.querySelector('[data-steam-open]').click();phaseOneAudit.cover();window.applySteamMessage({steamPreview:{steamId:'76561198000000000',games:[{appId:10,name:'Steam fixture',minutes:60}]},token:'fixture'});phaseOneAudit.cover();document.querySelector('#steam-dialog').close();window.chooseGameExe({action:'pickNewExe'});phaseOneAudit.cover();document.querySelector('[data-process-load]').click();window.applyProcessMessage({processList:[{token:'fixture',pid:999,name:'Fixture.exe',title:'Fixture',memory:1048576,available:true}]});phaseOneAudit.cover();document.querySelector('#process-dialog').close();navigate('home');window.showTrackerState({text:'Fixture',name:'Fixture',seconds:65});phaseOneAudit.cover();window.showTrackerState('Ожидание игры');const cover=games[0].legacy?.icon?.data;if(cover){const img=new Image();img.className='game-art';img.src='data:image/png;base64,'+cover;document.querySelector('.hero').prepend(img);}phaseOneAudit.cover();");
        var modes=new JsonObject();
        await web.ExecuteScriptAsync("prefs.reduced=false;prefs.opaque=false;GaidenTheme.select('auto');");
        await EmulateAppearanceAsync(("prefers-color-scheme","dark"));
        modes["autoDark"]=JsonNode.Parse(await web.ExecuteScriptAsync("document.documentElement.dataset.theme==='dark'&&prefs.theme==='auto'"));
        int darkResult=DwmGetWindowAttribute(Handle,20,out int darkTitle,sizeof(int));
        await EmulateAppearanceAsync(("prefers-color-scheme","light"));
        modes["autoLight"]=JsonNode.Parse(await web.ExecuteScriptAsync("document.documentElement.dataset.theme==='light'&&prefs.theme==='auto'"));
        int lightResult=DwmGetWindowAttribute(Handle,20,out int lightTitle,sizeof(int));
        modes["nativeTitlebar"]=darkResult==0&&lightResult==0&&darkTitle==1&&lightTitle==0;
        await web.ExecuteScriptAsync("GaidenTheme.select('dark');");
        modes["savedExplicitTheme"]=JsonNode.Parse(await web.ExecuteScriptAsync("document.documentElement.dataset.theme==='dark'&&JSON.parse(localStorage.getItem('gaiden.prefs.v1')).theme==='dark'"));
        await web.ExecuteScriptAsync("document.querySelector('#theme-toggle').click();navigate('settings');");
        modes["toolbarAndSettingsAgree"]=JsonNode.Parse(await web.ExecuteScriptAsync("document.querySelector('[data-theme-mode]').value==='light'&&document.documentElement.dataset.theme==='light'"));
        await EmulateAppearanceAsync(("prefers-reduced-motion","reduce"));
        modes["systemReducedMotion"]=JsonNode.Parse(await web.ExecuteScriptAsync("document.documentElement.dataset.reducedMotion==='true'&&getComputedStyle(document.documentElement).getPropertyValue('--normal').trim()==='0ms'"));
        await web.ExecuteScriptAsync("navigate('library');openDetail(games[0].id);phaseOneAudit.cover();");
        modes["noAnimationsWhenReduced"]=JsonNode.Parse(await web.ExecuteScriptAsync("!document.getAnimations().some(a=>a.playState==='running')"));
        await EmulateAppearanceAsync(("prefers-reduced-transparency","reduce"));
        modes["systemReducedTransparency"]=JsonNode.Parse(await web.ExecuteScriptAsync("document.documentElement.dataset.opaque==='true'&&getComputedStyle(document.querySelector('.sidebar')).backdropFilter==='none'&&getComputedStyle(document.querySelector('dialog[open]')).backdropFilter==='none'"));
        await EmulateAppearanceAsync(("forced-colors","active"));
        modes["forcedColors"]=JsonNode.Parse(await web.ExecuteScriptAsync("matchMedia('(forced-colors: active)').matches&&getComputedStyle(document.querySelector('.sidebar')).backdropFilter==='none'&&document.documentElement.dataset.reducedMotion==='true'&&[...document.querySelectorAll('.primary span')].every(e=>getComputedStyle(e).color!==getComputedStyle(e.closest('.primary')).backgroundColor)"));
        using(var image=File.Create(Path.Combine(path,"forced-colors-1280x800-detail.png"))) {Size=new Size(1280,800);await Task.Delay(100);await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);}
        await EmulateAppearanceAsync();
        await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=true;applyPrefs();navigate('stats');window.scrollTo(0,200);phaseOneAudit.cover();");
        await Task.Delay(100);await web.ExecuteScriptAsync("phaseOneAudit.cover()");
        modes["glassScope"]=JsonNode.Parse(await web.ExecuteScriptAsync("[...document.querySelectorAll('*')].filter(e=>getComputedStyle(e).backdropFilter!=='none').every(e=>e.matches('.sidebar,.toolbar-group,.segmented,.segmented .selected,.nav-lens,.hero .glass-button,dialog,#toast,[role=menu],[role=tooltip]'))"));
        modes["manualReducedMotion"]=JsonNode.Parse(await web.ExecuteScriptAsync("document.documentElement.dataset.reducedMotion==='true'"));
        await web.ExecuteScriptAsync("prefs.opaque=true;applyPrefs();");
        modes["manualOpaque"]=JsonNode.Parse(await web.ExecuteScriptAsync("getComputedStyle(document.querySelector('.sidebar')).backdropFilter==='none'&&getComputedStyle(document.querySelector('.topbar')).backdropFilter==='none'"));
        File.WriteAllText(Path.Combine(path,"matrix.json"),metadata.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(path,"modes.json"),modes.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(path,"selector-coverage.json"),await web.ExecuteScriptAsync("phaseOneAudit.coverage()"));
        if(modes.Any(p=>p.Value?.GetValue<bool>()!=true))throw new InvalidOperationException("Phase 1 appearance check failed: "+modes.ToJsonString());
    }

    private const string PhaseOneAuditScript="""
    window.phaseOneAudit=(()=>{
      const rules=[],matches=new Set();
      function walk(list,media=''){for(const rule of list){if(rule.selectorText)for(const selector of rule.selectorText.split(/,(?![^()]*\))/))rules.push({selector:selector.trim(),media});else if(rule.cssRules)walk(rule.cssRules,rule.conditionText||media);}}
      [...document.styleSheets].filter(s=>s.href).forEach(s=>walk(s.cssRules));
      function cover(){for(const {selector} of rules){let s=selector.replace(/::[\w-]+(?:\([^)]*\))?/g,'').replace(/:(hover|active|focus-visible|focus|checked|disabled)\b/g,'');s=s.trim()||'*';try{if(document.querySelector(s))matches.add(selector);}catch{}}}
      const rgba=v=>(v.match(/[\d.]+/g)||[]).map(Number);
      function over(a,b){const t=a.length>3?a[3]:1;return a.slice(0,3).map((v,i)=>v*t+b[i]*(1-t));}
      function bg(e){const chain=[];for(let p=e;p;p=p.parentElement)chain.unshift(p);let c=[245,245,247];for(const p of chain)c=over(rgba(getComputedStyle(p).backgroundColor),c);return c;}
      const lum=c=>c.map(x=>{x/=255;return x<=.04045?x/12.92:((x+.055)/1.055)**2.4;}).reduce((s,v,i)=>s+v*[.2126,.7152,.0722][i],0);
      const ratio=(a,b)=>{const x=lum(a),y=lum(b);return (Math.max(x,y)+.05)/(Math.min(x,y)+.05);};
      function collect(key){
        cover();const pairs=new Map(),failures=[],overflow=[];const modal=document.querySelector('dialog[open]');
        for(const e of (modal||document.body).querySelectorAll('*')){
          const rect=e.getBoundingClientRect(),style=getComputedStyle(e);if(!rect.width||!rect.height||style.visibility!=='visible'||style.display==='none'||!e.getClientRects().length)continue;
          if(e.closest('dialog:not([open]),[hidden],.sprite'))continue;
          if([...e.childNodes].some(n=>n.nodeType===3&&n.textContent.trim())||e.matches('input:not([type=checkbox]),select,textarea')){
            if(parseFloat(style.fontSize)===0)continue;
            let background=bg(e);if(e.closest('.hero-content')&&document.querySelector('.hero[data-art-layout]:not([data-art-layout=icon])')&&!e.closest('button'))background=[115,115,115];
            const foreground=over(rgba(style.color),background),value=ratio(foreground,background),large=parseFloat(style.fontSize)>=24||(parseFloat(style.fontSize)>=18.66&&Number(style.fontWeight)>=700),minimum=large?3:4.5;
            const pair={foreground:foreground.map(Math.round),background:background.map(Math.round),ratio:+value.toFixed(3),minimum};pairs.set(JSON.stringify(pair),pair);
            if(value+0.01<minimum)failures.push({tag:e.tagName,class:e.className,text:e.textContent.slice(0,90),...pair});
          }
          if(rect.right>innerWidth+1&&!e.closest('.table-wrap')&&e.matches('button,input,select,textarea,.panel,.game-card'))overflow.push({tag:e.tagName,class:e.className,right:rect.right});
        }
        const root=getComputedStyle(document.documentElement),tokens={};for(const name of ['background','surface','surface-soft','text','muted','accent','on-accent','control-line','success','danger','warning','selection'])tokens[name]=root.getPropertyValue('--'+name).trim();
        const smallTargets=[...(modal||document.body).querySelectorAll('button,input,select,summary')].filter(e=>{const r=e.getBoundingClientRect();return r.width&&r.height&&(r.width<43.5||r.height<43.5)&&getComputedStyle(e).visibility==='visible'&&!e.closest('[hidden],dialog:not([open])');}).map(e=>({tag:e.tagName,class:e.className,width:e.getBoundingClientRect().width,height:e.getBoundingClientRect().height}));
        return {key,width:innerWidth,height:innerHeight,theme:document.documentElement.dataset.theme,opaque:document.documentElement.dataset.opaque,tokens,contrastPairs:[...pairs.values()],failures,overflow,smallTargets,panelsOpaque:[...document.querySelectorAll('.panel,.game-card')].every(e=>getComputedStyle(e).backdropFilter==='none')};
      }
      return {collect,cover,coverage:()=>({rules:rules.length,matched:[...matches],unmatched:rules.filter(r=>!matches.has(r.selector))})};
    })();
    """;
}
