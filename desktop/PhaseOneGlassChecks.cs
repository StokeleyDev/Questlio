using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Management;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    private async Task PhaseOneGlassChecksAsync(string smokeResult)
    {
        Size=new Size(1280,800);
        string folder=Environment.GetEnvironmentVariable("GAIDEN_PHASE1B_OUTPUT")??Path.Combine(AppContext.BaseDirectory,"phase1b");Directory.CreateDirectory(folder);
        var summary=new JsonObject();
        JsonNode smoke=JsonNode.Parse(smokeResult)!;if(smoke is JsonValue)smoke=JsonNode.Parse(smoke.GetValue<string>())!;
        summary["webViewSmoke"]=smoke;
        foreach(var item in smoke.AsObject())if(item.Value is JsonValue v&&v.TryGetValue<bool>(out bool b)&&!b)throw new InvalidOperationException("Smoke: "+item.Key);
        await web.ExecuteScriptAsync("prefs.reduced=true;applyPrefs();");
        summary["phaseZero"]=JsonNode.Parse(await PhaseZeroUiChecksAsync(false));
        var checks=new JsonObject();summary["phaseOne"]=checks;
        async Task Check(string key,string script){var value=JsonNode.Parse(await web.ExecuteScriptAsync(script))!;checks[key]=value;if(value.GetValue<bool>()!=true)throw new InvalidOperationException("Glass check: "+key+" "+await web.ExecuteScriptAsync("[...document.querySelectorAll('[data-lg]')].map(e=>({tag:e.tagName,cls:e.className,active:e.hasAttribute('data-lg-active'),bg:getComputedStyle(e).backgroundColor,filter:getComputedStyle(e).backdropFilter}))"));}
        await web.ExecuteScriptAsync("""
        window.lgScope=()=>{const allowed='.sidebar,.toolbar-group,.segmented,.segmented .selected,.nav-lens,.hero .glass-button,dialog,#toast,[role=menu],[role=tooltip]';const active=[...document.querySelectorAll('*')].filter(e=>{const r=e.getBoundingClientRect();return r.width&&r.height&&r.bottom>0&&r.top<innerHeight&&getComputedStyle(e).backdropFilter!=='none';});return active.length<=6&&active.every(e=>e.matches(allowed))&&[...document.querySelectorAll('.panel,.game-card,.table-wrap,input,select,textarea')].every(e=>getComputedStyle(e).backdropFilter==='none');};
        window.lgSolid=()=>[...document.querySelectorAll('[data-lg]')].every(e=>{const s=getComputedStyle(e),c=s.backgroundColor;return s.backdropFilter==='none'&&(!c.startsWith('rgba(')||Number(c.slice(5,-1).split(',').at(-1))===1);});
        document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=false;prefs.opaque=false;GaidenTheme.select('light');navigate('home');
        """);
        await Task.Delay(700);
        await Check("svgDisplacement","!!document.querySelector('#lg-refract feImage[href^=\"data:image/png\"]')&&!!document.querySelector('#lg-refract feDisplacementMap[in=SourceGraphic][in2=lg-map]')");
        await Check("refractionEnabled","!CSS.supports('backdrop-filter','url(#lg-refract) blur(10px)')||getComputedStyle(document.querySelector('.sidebar')).backdropFilter.includes('lg-refract')");
        await Check("homeScopeAndBudget","lgScope()&&GaidenGlass.diagnostics().surfaces>0");
        await web.ExecuteScriptAsync("document.documentElement.dataset.lgFallback='true';");
        await Check("fallback","!getComputedStyle(document.querySelector('.sidebar')).backdropFilter.includes('url(')&&getComputedStyle(document.querySelector('.sidebar')).backdropFilter.includes('blur(')");
        await web.ExecuteScriptAsync("delete document.documentElement.dataset.lgFallback;navigate('library');");await Task.Delay(500);
        await Check("libraryScopeAndBudget","lgScope()&&GaidenGlass.diagnostics().surfaces<=6");
        await web.ExecuteScriptAsync("notify('Проверка стекла');GaidenGlass.refresh();");
        await Check("toastBudget","lgScope()&&document.querySelector('#toast').hasAttribute('data-lg-active')");
        await web.ExecuteScriptAsync("openDetail(games[0].id);");await Task.Delay(300);
        await Check("dialogScopeAndBudget","lgScope()&&document.querySelector('dialog[open]').hasAttribute('data-lg-active')&&GaidenGlass.diagnostics().surfaces===1");
        await web.ExecuteScriptAsync("document.querySelector('#detail-dialog').close();");await Task.Delay(250);
        await web.ExecuteScriptAsync("prefs.opaque=true;applyPrefs();");await Task.Delay(80);await Check("opaque","lgSolid()");
        await web.ExecuteScriptAsync("prefs.opaque=false;prefs.reduced=true;applyPrefs();");await Task.Delay(80);await Check("reducedMotion","lgSolid()&&!document.getAnimations().some(a=>a.playState==='running')");
        await web.ExecuteScriptAsync("prefs.reduced=false;applyPrefs();");
        await EmulateAppearanceAsync(("prefers-reduced-transparency","reduce"));await Check("systemReducedTransparency","lgSolid()");
        await EmulateAppearanceAsync(("prefers-reduced-motion","reduce"));await Check("systemReducedMotion","lgSolid()");
        await EmulateAppearanceAsync(("forced-colors","active"));await Check("forcedColors","lgSolid()&&getComputedStyle(document.querySelector('.primary span')).color!==getComputedStyle(document.querySelector('.primary')).backgroundColor");
        await EmulateAppearanceAsync();
        await web.ExecuteScriptAsync("GaidenTheme.select('auto');");await EmulateAppearanceAsync(("prefers-color-scheme","dark"));await Check("autoDark","document.documentElement.dataset.theme==='dark'");
        await EmulateAppearanceAsync(("prefers-color-scheme","light"));await Check("autoLight","document.documentElement.dataset.theme==='light'");await EmulateAppearanceAsync();
        await web.ExecuteScriptAsync("clearTimeout(toastTimer);toastTimer=null;document.querySelector('#toast').classList.remove('show');GaidenTheme.select('dark');navigate('home');");await Task.Delay(650);
        await Check("pointerAndHidden","(()=>{const el=document.querySelector('.sidebar');el.dispatchEvent(new PointerEvent('pointermove',{bubbles:true,clientX:50,clientY:110,pointerType:'mouse'}));const changed=!!el.style.getPropertyValue('--spec-x'),before=el.style.cssText;Object.defineProperty(document,'hidden',{configurable:true,get:()=>true});el.dispatchEvent(new PointerEvent('pointermove',{bubbles:true,clientX:99,clientY:220,pointerType:'mouse'}));GaidenGlass.refresh();const frozen=before===el.style.cssText;delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));return changed&&frozen;})()");
        await Task.Delay(650);await Check("idleAnimations","!document.getAnimations().some(a=>a.playState==='running')");
        summary["runtime"]=JsonNode.Parse(await web.ExecuteScriptAsync("GaidenGlass.diagnostics()"));
        var perf=new JsonObject();summary["performance"]=perf;
        foreach(bool enabled in new[]{false,true})foreach(bool scroll in new[]{false,true}){
            await web.ExecuteScriptAsync("GaidenGlass.pause("+(enabled?"false":"true")+");document.querySelector('link[href=\"glass.css\"]').disabled="+(enabled?"false":"true")+";navigate('home');window.scrollTo(0,0);");await Task.Delay(700);
            perf[(enabled?"after":"before")+(scroll?"Scroll":"Idle")]=await MeasureGlassLoadAsync(scroll);
        }
        await web.ExecuteScriptAsync("document.querySelector('link[href=\"glass.css\"]').disabled=false;GaidenGlass.pause(false);navigate('home');");
        // Only the requested eight captures; no broad matrix or JSON sidecars.
        if(Environment.GetCommandLineArgs().Contains("--phase1b-capture")){
            var shots=new JsonArray();summary["screenshots"]=shots;
            foreach(string key in new[]{"light-home","light-library","light-detail","dark-home","dark-library","dark-detail","dark-opaque-home","forced-colors-home"}){
                await web.ExecuteScriptAsync("prefs.reduced=true;applyPrefs();document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=false;prefs.opaque="+(key=="dark-opaque-home"?"true":"false")+";GaidenTheme.select("+JsonSerializer.Serialize(key.StartsWith("dark")?"dark":"light")+");filter='all';search='';navigate("+JsonSerializer.Serialize(key.EndsWith("library")?"library":"home")+");document.querySelector('#toast').classList.remove('show');document.activeElement?.blur();"+(key.EndsWith("detail")?"openDetail(games[0].id);document.querySelector('#detail-dialog').scrollTop=0;":""));
                if(key=="forced-colors-home")await EmulateAppearanceAsync(("forced-colors","active"));else await EmulateAppearanceAsync();
                await Task.Delay(700);await Check("scope-"+key,"lgScope()");
                using var png=File.Create(Path.Combine(folder,key+".png"));await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,png);shots.Add(key+".png");
            }
        }
        File.WriteAllText(Path.Combine(folder,"summary.json"),summary.ToJsonString(new JsonSerializerOptions(JsonSerializerOptions.Default){WriteIndented=true}));
    }

    private async Task<JsonObject> MeasureGlassLoadAsync(bool scroll)
    {
        int[] ids=web.CoreWebView2.Environment.GetProcessInfos().Select(p=>Convert.ToInt32(p.ProcessId)).ToArray();
        double Cpu(){double total=0;foreach(int id in ids)try{using var p=Process.GetProcessById(id);total+=p.TotalProcessorTime.TotalMilliseconds;}catch{}return total;}
        async Task<double?> Gpu(){return await Task.Run(()=>{try{using var query=new ManagementObjectSearcher("SELECT Name,UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");query.Options.Timeout=TimeSpan.FromSeconds(3);using var results=query.Get();double max=0;int total=0;foreach(ManagementObject item in results){using(item){total++;string name=item["Name"]?.ToString()??"";var match=Regex.Match(name,@"^pid_(\d+)_");if(match.Success&&ids.Contains(int.Parse(match.Groups[1].Value))&&name.Contains("engtype_3D"))max=Math.Max(max,Convert.ToDouble(item["UtilizationPercentage"]));}}return total>0?(double?)max:null;}catch{return null;}});}
        var gpu=new List<double>();double cpu=Cpu();var watch=Stopwatch.StartNew();int moves=0;
        for(int sample=0;sample<5;sample++){
            var sampling=Gpu();var interval=Stopwatch.StartNew();
            while(interval.ElapsedMilliseconds<700){if(scroll){await web.ExecuteScriptAsync("window.scrollBy(0,"+(moves++%80<40?18:-18)+")");}await Task.Delay(scroll?16:80);}
            double? value=await sampling;if(value is not null)gpu.Add(value.Value);
        }
        double seconds=watch.Elapsed.TotalSeconds;
        return new JsonObject{["seconds"]=Math.Round(seconds,2),["cpuWebViewPercentAllCores"]=Math.Round((Cpu()-cpu)/(seconds*1000*Environment.ProcessorCount)*100,3),["gpu3DMaxEngineMeanPercent"]=gpu.Count>0?Math.Round(gpu.Average(),3):null,["gpuSamples"]=gpu.Count,["scrollSteps"]=moves,["scope"]="WebView2 environment processes; CPU normalized to all logical cores; GPU mean of per-sample maximum 3D engine utilization; host WMI observer excluded"};
    }
}
