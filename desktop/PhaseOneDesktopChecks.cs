using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    private async Task PhaseOneDesktopChecksAsync(string smokeResult,bool capture=true)
    {
        string folder=Environment.GetEnvironmentVariable("GAIDEN_PHASE1D_OUTPUT")??Path.Combine(AppContext.BaseDirectory,"phase1d");Directory.CreateDirectory(folder);
        var evidence=new List<string>();
        JsonNode smoke=JsonNode.Parse(smokeResult)!;if(smoke is JsonValue)smoke=JsonNode.Parse(smoke.GetValue<string>())!;
        foreach(var p in smoke.AsObject())if(p.Value is JsonValue v&&v.TryGetValue<bool>(out bool b)&&!b)throw new InvalidOperationException("Smoke: "+p.Key);
        evidence.Add("WebView smoke PASS");
        await web.ExecuteScriptAsync("prefs.reduced=true;applyPrefs();");
        var zero=JsonNode.Parse(await PhaseZeroUiChecksAsync(false))!.AsObject();evidence.Add("PhaseZero "+zero.Count+"/14 PASS");
        async Task Check(string key,string script){string result=await web.ExecuteScriptAsync(script);if(result!="true"){if(result=="null"){string detail=await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate",JsonSerializer.Serialize(new{expression=script,returnByValue=true}));throw new InvalidOperationException(key+" runtime "+detail);}throw new InvalidOperationException(key+": "+result);}evidence.Add(key+" PASS");}
        await web.ExecuteScriptAsync("""
        window.cSolid=()=>[...document.querySelectorAll('[data-lg]')].filter(e=>e.getClientRects().length).every(e=>{const s=getComputedStyle(e);return s.backdropFilter==='none'&&!s.backgroundColor.startsWith('rgba(');});
        window.cLayout=()=>{const aside=document.querySelector('.sidebar').getBoundingClientRect(),main=document.querySelector('main').getBoundingClientRect(),shell=document.querySelector('.main-shell'),s=getComputedStyle(shell);const h=document.querySelector('main h1')?.getBoundingClientRect(),bar=document.querySelector('.topbar').getBoundingClientRect();return !!h&&h.top>=bar.bottom&&h.height>0&&main.left>=aside.right+16&&parseFloat(s.paddingLeft)>=24&&document.documentElement.scrollWidth<=innerWidth+1&&[...document.querySelectorAll('main .heading,main .metric,main .section-heading,main .game-card,main input')].every(e=>{const r=e.getBoundingClientRect();return !r.width||r.left>=main.left-1;});};
        window.cPalette=()=>{const root=getComputedStyle(document.documentElement),dark=document.documentElement.dataset.theme==='dark',layer=document.querySelector('.lg-backdrop > .is-visible');return ['a','b'].every(k=>{const value=layer.style.getPropertyValue('--palette-'+k)||root.getPropertyValue('--glass-neutral-'+k),c=value.split(',').map(Number).map(x=>x/255),max=Math.max(...c),min=Math.min(...c),l=(max+min)/2,s=max===min?0:(max-min)/(1-Math.abs(2*l-1));return l>=(dark?.12:.72)-.003&&l<=1&&s<=(dark?.55:.14)+.003;});};
        clearTimeout(toastTimer);toastTimer=null;document.querySelector('#toast').classList.remove('show');document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=false;prefs.opaque=false;GaidenTheme.select('light');navigate('home');
        """);
        await Task.Delay(700);
        await Check("breadcrumbsRemoved","!document.querySelector('.breadcrumbs,#breadcrumb,.library-status')");
        await Check("toolbar64","getComputedStyle(document.querySelector('.topbar')).height==='64px'&&getComputedStyle(document.querySelector('.toolbar-group .icon-button')).borderRadius==='999px'");
        await Check("mainTransparent","getComputedStyle(document.querySelector('main')).backgroundColor==='rgba(0, 0, 0, 0)'&&getComputedStyle(document.querySelector('main')).borderRadius==='0px'");
        await Check("glassTintLight","getComputedStyle(document.querySelector('.sidebar')).backgroundColor==='rgba(255, 255, 255, 0.38)'&&getComputedStyle(document.querySelector('.sidebar')).backdropFilter.includes('26px')");
        await Check("sharpHero","(()=>{const hero=document.querySelector('.hero'),img=hero.querySelector('.hero-clear-art,.game-icon');return !!hero.dataset.artLayout&&(!img||img.getBoundingClientRect().width<=img.naturalWidth*2+1)&&(!img||img.getBoundingClientRect().left>=hero.getBoundingClientRect().left+hero.clientWidth*.5);})()");
        await web.ExecuteScriptAsync("GaidenUI.moduleState('statistics').period='month'");await web.ExecuteScriptAsync("navigate('stats')");await Task.Delay(250);
        await Check("accentCharts","(()=>{const b=document.querySelector('.bar');return !!b&&getComputedStyle(b).borderTopLeftRadius==='6px'&&getComputedStyle(b).backgroundColor!==getComputedStyle(document.documentElement).getPropertyValue('--control-line').trim();})()");
        foreach(var size in new[]{new Size(820,600),new Size(1280,800),new Size(1920,1080)}){Size=size;foreach(string screen in new[]{"home","library","stats","sessions","settings","computer"}){await web.ExecuteScriptAsync("navigate("+JsonSerializer.Serialize(screen)+")");await Task.Delay(50);await Check("layout-"+size.Width+"-"+screen,"cLayout()");}}
        Size=new Size(1280,800);await web.ExecuteScriptAsync("navigate('home');prefs.opaque=true;applyPrefs()");await Task.Delay(80);await Check("opaque","cSolid()");
        await web.ExecuteScriptAsync("prefs.opaque=false;prefs.reduced=true;applyPrefs()");await Task.Delay(80);await Check("reducedMotion","cSolid()&&!document.getAnimations().some(a=>a.playState==='running')");
        await web.ExecuteScriptAsync("prefs.reduced=false;applyPrefs()");await EmulateAppearanceAsync(("prefers-reduced-transparency","reduce"));await Check("reducedTransparency","cSolid()");
        await EmulateAppearanceAsync(("forced-colors","active"));await Check("forcedColors","cSolid()");await EmulateAppearanceAsync();
        await web.ExecuteScriptAsync("navigate('home');scrollTo(0,200)");await Task.Delay(80);await Check("stickyScroll","document.querySelector('.topbar').getBoundingClientRect().top===0&&document.querySelector('.hero').getBoundingClientRect().top<64");await web.ExecuteScriptAsync("scrollTo(0,0)");
        var shots=new[]{("light-home",1280,800,"home",false),("dark-home",1280,800,"home",true),("light-library",1280,800,"library",false),("dark-detail",1280,800,"detail",true)};
        if(capture)foreach(var (name,width,height,scene,dark) in shots){
            Size=new Size(width,height);await web.ExecuteScriptAsync("prefs.reduced=true;applyPrefs();document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=false;prefs.opaque=false;GaidenTheme.select('"+(dark?"dark":"light")+"');navigate('"+(scene=="detail"?"home":scene)+"');document.activeElement?.blur();"+(scene=="detail"?"openDetail(games[0].id);document.querySelector('#detail-dialog').scrollTop=0;":""));
            await Task.Delay(700);await web.ExecuteScriptAsync("document.activeElement?.blur()");await Check("palette-"+name,"cPalette()");if(scene!="detail")await Check("capture-layout-"+name,"cLayout()");
            using var png=File.Create(Path.Combine(folder,name+".png"));await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,png);
        }
        string log=Environment.GetEnvironmentVariable("GAIDEN_PHASE1D_CHECKS")??Path.Combine(folder,"checks.log");File.WriteAllLines(log,evidence);
    }
}
