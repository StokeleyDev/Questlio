using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Management;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GaidenDesktop;

internal sealed partial class MainWindow
{
    private async Task PhaseFiveChecksAsync()
    {
        string folder = Environment.GetEnvironmentVariable("GAIDEN_PHASE5_OUTPUT") ?? Path.Combine(AppContext.BaseDirectory, "phase5");
        Directory.CreateDirectory(folder);
        // Suspended WebView cannot execute diagnostics. Resume only for the probe,
        // preserving the window's paused state, then suspend again before sampling.
        async Task<string> PausedProbe(string script){while(webActivityBusy)await Task.Delay(15);bool suspended=web.CoreWebView2.IsSuspended;if(suspended)web.CoreWebView2.Resume();try{return await web.ExecuteScriptAsync(script);}finally{if(suspended)await web.CoreWebView2.TrySuspendAsync();}}
        var step5 = new JsonObject();
        var matrix = new JsonObject();
        var keyboard = new JsonObject();
        var aria = new JsonObject();
        var dpi = new JsonObject();
        var summary = new JsonObject();

        await web.ExecuteScriptAsync(PhaseOneAuditScript);
        await web.ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "checks", "phase5-audit.js")));
        await web.ExecuteScriptAsync("prefs.reduced=false;prefs.opaque=false;applyPrefs();GaidenTheme.select('light');document.querySelectorAll('dialog[open]').forEach(d=>d.close());navigate('home');window.__phase5Marker=42;");
        Size = new Size(1280, 800);
        await Task.Delay(600);

        // Step 5: motion surface, transitions and the non-transition fallback.
        step5["viewTransitionsSupport"] = JsonNode.Parse(await web.ExecuteScriptAsync("typeof document.startViewTransition==='function'"));
        step5["viewTransitionCss"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{try{return[...document.styleSheets].some(s=>{try{return[...s.cssRules].some(r=>r.cssText.includes('view-transition'))}catch{return false}})}catch{return false}})()"""));
        step5["posterArm"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{const card=document.querySelector('.game-card .cover');if(!card)return null;const ok=GaidenMotion.armPoster('gaiden-art',card);const name=card.style.viewTransitionName;card.style.viewTransitionName='';return {ok,name};})()"""));
        step5["fallbackWithoutApi"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{const original=document.startViewTransition;let ran=0;document.startViewTransition=undefined;const result=GaidenMotion.transition(()=>{ran++;});document.startViewTransition=original;return {ran,returnedNull:result===null,restored:typeof document.startViewTransition==='function'};})()"""));
        await web.ExecuteScriptAsync("window.__vtOriginal=document.startViewTransition;window.__vtCalls=0;document.startViewTransition=function(...a){window.__vtCalls++;return window.__vtOriginal.apply(this,a);};openDetail(games[0].id);");
        await Task.Delay(450);
        step5["detailTransitionCalled"] = JsonNode.Parse(await web.ExecuteScriptAsync("({called:window.__vtCalls,open:document.querySelector('#detail-dialog').open})"));
        await web.ExecuteScriptAsync("document.startViewTransition=window.__vtOriginal;delete window.__vtOriginal;document.querySelector('#detail-dialog').close();");
        step5["screenAnimation"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{navigate('library');const main=document.querySelector('main'),animations=main.getAnimations().map(a=>({duration:a.effect.getTiming().duration,keyframes:a.effect.getKeyframes().map(k=>({transform:k.transform,opacity:k.opacity}))}));return {count:animations.length,duration:animations[0]?.duration??null,hasTranslate:!!animations[0]?.keyframes?.some(k=>(k.transform||'').includes('8px')),reduced:GaidenMotion.reduced(),paused:document.documentElement.dataset.uiPaused??null,all:document.getAnimations().length,view};})()"""));
        step5["reducedMotion"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{prefs.reduced=true;applyPrefs();navigate('stats');const running=document.getAnimations().filter(a=>a.playState==='running').length;const transitionNull=GaidenMotion.transition(()=>{})===null;prefs.reduced=false;applyPrefs();return {reduced:GaidenMotion.reduced(),running,transitionNull,normal:getComputedStyle(document.documentElement).getPropertyValue('--normal').trim()};})()"""));
        await Task.Delay(250);

        // Step 5: pause on minimized window, CPU in idle / minimized / scrolling.
        await web.ExecuteScriptAsync("navigate('home');window.__phase5Marker=42;GaidenKit.clearToasts();");
        await Task.Delay(700);
        var idle = await MeasureWebViewCpuAsync(2500);
        step5["cpuIdle"] = new JsonObject { ["webviewPercent"] = Math.Round(idle.WebViewPercent, 2), ["rendererPercent"] = Math.Round(idle.RendererPercent, 2), ["seconds"] = Math.Round(idle.Seconds, 3) };

        WindowState = FormWindowState.Minimized;
        await Task.Delay(900);
        step5["pausedState"] = JsonNode.Parse(await PausedProbe("({paused:document.documentElement.dataset.uiPaused==='true',running:document.getAnimations().filter(a=>a.playState==='running').length,reasons:GaidenMotion.diagnostics().reasons,glass:GaidenGlass.diagnostics()})"));
        var minimized = await MeasureWebViewCpuAsync(2500);
        step5["cpuMinimized"] = new JsonObject { ["webviewPercent"] = Math.Round(minimized.WebViewPercent, 2), ["rendererPercent"] = Math.Round(minimized.RendererPercent, 2), ["seconds"] = Math.Round(minimized.Seconds, 3) };

        await PausedProbe("GaidenKit.toast('Phase5 pause probe');");
        await Task.Delay(6800);
        step5["toastHeldWhileMinimized"] = JsonNode.Parse(await PausedProbe("!!document.querySelector('.ui-toast')"));
        WindowState = FormWindowState.Normal;
        Activate();
        await Task.Delay(900);
        step5["resumedState"] = JsonNode.Parse(await web.ExecuteScriptAsync("({paused:document.documentElement.dataset.uiPaused==='true',marker:window.__phase5Marker,view,ready:document.readyState,navigationEntries:performance.getEntriesByType('navigation').length,running:document.getAnimations().filter(a=>a.playState==='running').length})"));
        await web.ExecuteScriptAsync("GaidenKit.clearToasts();");

        await web.ExecuteScriptAsync("navigate('sessions');");
        await Task.Delay(400);
        int sessionsBefore = int.Parse(await web.ExecuteScriptAsync("document.querySelectorAll('#main .session-row').length"));
        WindowState = FormWindowState.Minimized;
        await Task.Delay(700);
        await PausedProbe("window.scrollTo(0,document.body.scrollHeight);");
        await Task.Delay(600);
        int sessionsPaused = int.Parse(await PausedProbe("document.querySelectorAll('#main .session-row').length"));
        WindowState = FormWindowState.Normal;
        Activate();
        await Task.Delay(900);
        await web.ExecuteScriptAsync("window.scrollTo(0,document.body.scrollHeight);");
        await Task.Delay(700);
        int sessionsResumed = int.Parse(await web.ExecuteScriptAsync("document.querySelectorAll('#main .session-row').length"));
        step5["observers"] = new JsonObject { ["before"] = sessionsBefore, ["whilePaused"] = sessionsPaused, ["afterResume"] = sessionsResumed };

        await web.ExecuteScriptAsync("navigate('home');window.scrollTo(0,0);window.__phase5Scroll=true;window.__phase5Frames=0;(function(){const start=performance.now();function tick(t){if(!window.__phase5Scroll)return;window.__phase5Frames++;window.scrollTo(0,(t-start)/2%600);requestAnimationFrame(tick);}requestAnimationFrame(tick);})();");
        var scrolling = await MeasureWebViewCpuAsync(2500);
        int frames = int.Parse(await web.ExecuteScriptAsync("window.__phase5Scroll=false;window.__phase5Frames"));
        await web.ExecuteScriptAsync("window.scrollTo(0,0);");
        step5["cpuScroll"] = new JsonObject { ["webviewPercent"] = Math.Round(scrolling.WebViewPercent, 2), ["rendererPercent"] = Math.Round(scrolling.RendererPercent, 2), ["frames"] = frames };
        step5["timingTokens"] = JsonNode.Parse(await web.ExecuteScriptAsync("({fast:getComputedStyle(document.documentElement).getPropertyValue('--fast').trim(),normal:getComputedStyle(document.documentElement).getPropertyValue('--normal').trim(),slow:getComputedStyle(document.documentElement).getPropertyValue('--slow').trim(),spring:getComputedStyle(document.documentElement).getPropertyValue('--ui-spring').trim()})"));

        // Step 6: matrix of three modes, three sizes and seven screens.
        await web.ExecuteScriptAsync("prefs.reduced=true;prefs.opaque=false;applyPrefs();GaidenTheme.select('light');");
        foreach (string mode in new[] { "light", "dark", "opaque" })
            foreach (var size in new[] { new Size(820, 600), new Size(1280, 800), new Size(1920, 1080) })
                foreach (string scene in new[] { "home", "library", "stats", "sessions", "settings", "detail", "add" })
                {
                    Size = size;
                    await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.opaque=" + (mode == "opaque" ? "true" : "false") + ";GaidenTheme.select(" + JsonSerializer.Serialize(mode == "dark" ? "dark" : "light") + ");filter='all';search='';navigate(" + JsonSerializer.Serialize(scene is "detail" or "add" ? "home" : scene) + ");" + (scene == "detail" ? "openDetail(games[0].id);document.querySelector('#detail-dialog').scrollTop=0;" : scene == "add" ? "document.querySelector('#add-dialog').showModal();" : "") + "requestAnimationFrame(()=>document.activeElement?.blur());");
                    await Task.Delay(220);
                    string key = $"{mode}-{size.Width}x{size.Height}-{scene}";
                    matrix[key] = JsonNode.Parse(await web.ExecuteScriptAsync("phase5Audit.audit(" + JsonSerializer.Serialize(key) + ")"));
                    if(!Environment.GetCommandLineArgs().Contains("--checks-no-captures")){using var image = File.Create(Path.Combine(folder, key + ".png"));
                    await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);}
                }
        Size = new Size(1280, 800);
        await web.ExecuteScriptAsync("prefs.reduced=true;prefs.opaque=false;applyPrefs();GaidenTheme.select('light');document.querySelectorAll('dialog[open]').forEach(d=>d.close());navigate('home');");
        await Task.Delay(250);

        var tokenChecks = new JsonObject();
        foreach (string mode in new[] { "light", "dark", "opaque" })
        {
            await web.ExecuteScriptAsync("prefs.opaque=" + (mode == "opaque" ? "true" : "false") + ";GaidenTheme.select(" + JsonSerializer.Serialize(mode == "dark" ? "dark" : "light") + ");");
            await Task.Delay(120);
            tokenChecks[mode] = JsonNode.Parse(await web.ExecuteScriptAsync("phase5Audit.tokens()"));
        }
        await web.ExecuteScriptAsync("prefs.opaque=false;GaidenTheme.select('light');");
        var tokenFailures = new JsonArray();
        foreach (var entry in tokenChecks)
            foreach (var failure in entry.Value?["failures"]?.AsArray() ?? new JsonArray())
                tokenFailures.Add(new JsonObject { ["mode"] = entry.Key, ["pair"] = failure?["name"]?.DeepClone(), ["ratio"] = failure?["ratio"]?.DeepClone() });
        summary["tokenFailures"] = tokenFailures;

        // Keyboard: full tab pass per screen, Escape return, arrow keys.
        foreach (string scene in new[] { "home", "library", "stats", "sessions", "settings", "detail", "add" })
        {
            string openScene = scene switch
            {
                "detail" => "navigate('home');openDetail(games[0].id);",
                "add" => "navigate('home');document.querySelector('[data-action=\"add\"]').click();",
                "settings" => "navigate('settings');document.querySelectorAll('details').forEach(d=>d.open=true);",
                _ => "navigate(" + JsonSerializer.Serialize(scene) + ");"
            };
            await web.ExecuteScriptAsync("(()=>{document.querySelectorAll('dialog[open]').forEach(d=>d.close());" + openScene + "const open=document.querySelector('dialog[open]');if(open)open.querySelector('button,input:not([type=hidden]),select,textarea')?.focus();else document.activeElement?.blur();})()");
            await Task.Delay(200);
            keyboard[scene] = await TabPassAsync(scene);
        }
        await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());navigate('home');");
        await Task.Delay(150);

        await web.ExecuteScriptAsync("(()=>{const card=document.querySelector('.game-card');card.focus();})()");
        await PressKeyAsync("Enter", "Enter", 13, "\r");
        await Task.Delay(250);
        bool sheetOpened = bool.Parse(await web.ExecuteScriptAsync("document.querySelector('#detail-dialog').open"));
        await PressKeyAsync("Escape", "Escape", 27);
        await Task.Delay(300);
        keyboard["escapeSheet"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{const dialog=document.querySelector('#detail-dialog'),active=document.activeElement;return {opened:""" + (sheetOpened ? "true" : "false") + """,closed:!dialog.open,returned:active?.classList.contains('game-card')===true,active:(active?.getAttribute('aria-label')||active?.className||'').toString().slice(0,60)};})()"""));

        await web.ExecuteScriptAsync("(()=>{const card=document.querySelector('.game-card');card.focus();card.click();})()");
        await Task.Delay(250);
        await web.ExecuteScriptAsync("document.querySelector('#detail-dialog .detail-more').focus();");
        await PressKeyAsync("Enter", "Enter", 13, "\r");
        await Task.Delay(200);
        bool menuOpened = bool.Parse(await web.ExecuteScriptAsync("!!document.querySelector('.ui-menu[role=menu]')"));
        await PressKeyAsync("ArrowDown", "ArrowDown", 40);
        await PressKeyAsync("Escape", "Escape", 27);
        await Task.Delay(200);
        keyboard["escapeMenu"] = JsonNode.Parse(await web.ExecuteScriptAsync("({opened:" + (menuOpened ? "true" : "false") + ",closed:!document.querySelector('.ui-menu'),returned:document.activeElement?.classList.contains('detail-more')===true})"));
        await web.ExecuteScriptAsync("document.querySelector('#detail-dialog').close();");

        keyboard["escapeAdd"] = await EscapeAddAsync();
        keyboard["segmentedArrows"] = await SegmentedArrowsAsync();
        keyboard["selectMenuArrows"] = await SelectMenuArrowsAsync();
        keyboard["chartArrows"] = await ChartArrowsAsync();

        // ARIA roles and labels.
        await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());navigate('settings');document.querySelectorAll('details').forEach(d=>d.open=true);");
        await Task.Delay(200);
        aria["settings"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{const theme=document.querySelector('.ui-theme-segments'),switches=[...document.querySelectorAll('.ui-switch')];return {themeRole:theme?.getAttribute('role'),radioCount:theme?.querySelectorAll('[role=radio]').length,checked:theme?.querySelectorAll('[role=radio][aria-checked=true]').length,switchRole:switches.every(b=>b.getAttribute('role')==='switch'),switchCount:switches.length,switchChecked:switches.every(b=>{const input=document.getElementById(b.dataset.switchFor);return input&&b.getAttribute('aria-checked')===String(input.checked)}),switchLabels:switches.map(b=>document.getElementById(b.getAttribute('aria-labelledby'))?.textContent||b.getAttribute('aria-label'))};})()"""));
        await web.ExecuteScriptAsync("navigate('library');");
        await Task.Delay(150);
        aria["library"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{const filter=document.querySelector('.tools .segmented'),select=document.querySelector('.ui-select');return {filterRole:filter?.getAttribute('role'),filterPressed:filter?.querySelectorAll('[aria-pressed=true]').length,selectExpanded:select?.getAttribute('aria-expanded'),selectHaspopup:select?.getAttribute('aria-haspopup'),selectLabel:select?.getAttribute('aria-label')};})()"""));
        await web.ExecuteScriptAsync("navigate('sessions');");
        await Task.Delay(150);
        aria["sessions"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{const sentinel=document.querySelector('#sessions-sentinel');return {role:sentinel?.getAttribute('role'),live:sentinel?.getAttribute('aria-live'),rows:document.querySelectorAll('#main .session-row').length,headings:document.querySelectorAll('.session-day-heading h2').length};})()"""));
        await web.ExecuteScriptAsync("navigate('stats');");
        await Task.Delay(200);
        aria["chart"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{const chart=document.querySelector('.period-chart');const bins=[...document.querySelectorAll('[data-stat-bin]')];return {role:chart?.getAttribute('role'),label:chart?.getAttribute('aria-label'),bins:bins.length,labelled:bins.every(b=>(b.getAttribute('aria-label')||'').length>3),pressed:bins.filter(b=>b.getAttribute('aria-pressed')==='true').length};})()"""));
        await web.ExecuteScriptAsync("(()=>{navigate('home');const card=document.querySelector('.game-card');card.focus();card.click();})()");
        await Task.Delay(250);
        aria["sheet"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{const d=document.querySelector('#detail-dialog');const title=document.getElementById(d.getAttribute('aria-labelledby'));const titleText=((title?.textContent||'').trim()||title?.querySelector('input')?.value||'').slice(0,40);return {open:d.open,role:d.getAttribute('role'),labelledby:d.getAttribute('aria-labelledby'),titleText,modal:d.getAttribute('aria-modal'),closeLabel:d.querySelector('.close-dialog')?.getAttribute('aria-label')};})()"""));
        await web.ExecuteScriptAsync("document.querySelector('#detail-dialog').close();notify('Phase5 toast probe');");
        await Task.Delay(150);
        aria["toast"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{const host=document.querySelector('#toast'),toast=document.querySelector('.ui-toast');return {hostRole:host?.getAttribute('role'),hostLive:host?.getAttribute('aria-live'),toastRole:toast?.getAttribute('role'),text:toast?.textContent?.slice(0,30)};})()"""));
        await web.ExecuteScriptAsync("(()=>{GaidenKit.clearToasts();const card=document.querySelector('.game-card');card.focus();card.click();})()");
        await Task.Delay(220);
        await web.ExecuteScriptAsync("document.querySelector('#detail-dialog .detail-more').click();");
        await Task.Delay(150);
        aria["menu"] = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{const menu=document.querySelector('.ui-menu'),anchor=document.querySelector('#detail-dialog .detail-more');return {role:menu?.getAttribute('role'),label:menu?.getAttribute('aria-label'),items:menu?.querySelectorAll('[role^=menuitem]').length,expanded:anchor?.getAttribute('aria-expanded'),controls:!!document.getElementById(anchor?.getAttribute('aria-controls'))};})()"""));
        await web.ExecuteScriptAsync("GaidenKit.closeMenu(false);document.querySelector('#detail-dialog').close();");

        // DPI emulation at 100 / 125 / 150%.
        foreach (double scale in new[] { 1.0, 1.25, 1.5 })
        {
            int cssWidth = (int)Math.Round(1280 / scale), cssHeight = (int)Math.Round(800 / scale);
            var payload = new JsonObject { ["width"] = cssWidth, ["height"] = cssHeight, ["deviceScaleFactor"] = scale, ["mobile"] = false, ["screenWidth"] = 1280, ["screenHeight"] = 800 };
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", payload.ToJsonString());
            await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());navigate('library');");
            await Task.Delay(250);
            string label = scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
            dpi[label] = JsonNode.Parse(await web.ExecuteScriptAsync("phase5Audit.audit('dpi-" + label + "')"));
            if(!Environment.GetCommandLineArgs().Contains("--checks-no-captures"))using (var image = File.Create(Path.Combine(folder, "dpi-" + label.Replace('.', '_') + "-library.png")))
                await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
            await web.ExecuteScriptAsync("navigate('settings');");
            await Task.Delay(200);
            dpi[label + "-settings"] = JsonNode.Parse(await web.ExecuteScriptAsync("phase5Audit.audit('dpi-settings')"));
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride", "{}");
            await Task.Delay(120);
        }
        await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=true;applyPrefs();navigate('home');");

        File.WriteAllText(Path.Combine(folder, Environment.GetCommandLineArgs().Contains("--text-results")?"matrix.log":"matrix.json"), matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // Summary: values the report can quote directly.
        static bool Flag(JsonNode? node, bool expected = true) => node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag == expected;
        static bool Any(JsonNode? node) => node is JsonArray array && array.Count > 0;
        var matrixFailures = matrix.Where(p => p.Value is JsonObject o && (Any(o["failures"]) || Any(o["overflow"]) || Any(o["smallTargets"]) || Flag(o["hScroll"]) || !Flag(o["headingVisible"]) || Any(o["clipped"]) || Any(o["overlaps"]))).Select(p => p.Key).ToArray();
        summary["matrixCombinations"] = matrix.Count;
        summary["matrixFailures"] = new JsonArray(matrixFailures.Select(k => (JsonNode)k!).ToArray());
        var keyboardRings = new JsonArray();
        foreach (var entry in keyboard)
            if (entry.Value is JsonObject o && o["missingRing"] is JsonArray missing)
                foreach (var item in missing)
                    keyboardRings.Add(new JsonObject { ["screen"] = entry.Key, ["element"] = item?.DeepClone() });
        summary["keyboardRingsMissing"] = keyboardRings;
        var dpiFailures = new JsonArray();
        foreach (var entry in dpi)
            if (entry.Value is JsonObject o && (Flag(o["hScroll"]) || !Flag(o["headingVisible"]) || Any(o["clipped"]) || Any(o["smallTargets"])))
                dpiFailures.Add(entry.Key);
        summary["dpiFailures"] = dpiFailures;
        summary["step5"] = step5.DeepClone();
        summary["aria"] = aria.DeepClone();

        File.WriteAllText(Path.Combine(folder, Environment.GetCommandLineArgs().Contains("--text-results")?"step5.log":"step5.json"), step5.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(folder, Environment.GetCommandLineArgs().Contains("--text-results")?"matrix.log":"matrix.json"), matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(folder, Environment.GetCommandLineArgs().Contains("--text-results")?"keyboard.log":"keyboard.json"), keyboard.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(folder, Environment.GetCommandLineArgs().Contains("--text-results")?"aria.log":"aria.json"), aria.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(folder, Environment.GetCommandLineArgs().Contains("--text-results")?"dpi.log":"dpi.json"), dpi.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(folder, Environment.GetCommandLineArgs().Contains("--text-results")?"summary.log":"summary.json"), summary.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task<JsonObject> TabPassAsync(string scene)
    {
        int expected = int.Parse(await web.ExecuteScriptAsync("""(()=>{const scope=document.querySelector('dialog[open]')||document.body;return [...scope.querySelectorAll('a[href],button:not([disabled]),input:not([type=hidden]):not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])')].filter(e=>!e.disabled&&e.tabIndex>=0&&e.getClientRects().length&&getComputedStyle(e).visibility!=='hidden').length;})()"""));
        var visited = new JsonArray();
        var missing = new JsonArray();
        var unique = new HashSet<string>();
        string? first = null;
        for (int i = 0; i < expected * 2 + 4; i++)
        {
            await PressKeyAsync("Tab", "Tab", 9);
            var focus = JsonNode.Parse(await web.ExecuteScriptAsync("""(()=>{const a=document.activeElement;if(!a||a===document.body)return null;const s=getComputedStyle(a),r=a.getBoundingClientRect(),k=a.__tabKey||(a.__tabKey='t'+(window.__tabSeq=(window.__tabSeq||0)+1)),labelled=[...(a.getAttribute('aria-labelledby')||'').split(/\s+/).filter(Boolean)].map(id=>document.getElementById(id)?.textContent||'').join(' ');return {key:k,tag:a.tagName,cls:String(a.className).slice(0,40),label:(a.getAttribute('aria-label')||labelled||a.textContent||'').trim().slice(0,40),focusVisible:a.matches(':focus-visible'),outlineStyle:s.outlineStyle,outlineWidth:s.outlineWidth,outlineColor:s.outlineColor,width:Math.round(r.width),height:Math.round(r.height)};})()"""));
            if (focus is null) break;
            string signature = focus["key"]?.GetValue<string>() ?? (focus["tag"] + "|" + focus["cls"] + "|" + focus["label"]);
            first ??= signature;
            bool ring = focus["focusVisible"]?.GetValue<bool>() == true && focus["outlineStyle"]?.GetValue<string>() != "none" && !string.IsNullOrWhiteSpace(focus["outlineWidth"]?.GetValue<string>()) && focus["outlineWidth"]?.GetValue<string>() != "0px";
            if (unique.Add(signature))
            {
                visited.Add(focus.DeepClone());
                if (!ring) missing.Add(focus.DeepClone());
            }
            if (signature == first && unique.Count >= expected) break;
        }
        return new JsonObject { ["scene"] = scene, ["expected"] = expected, ["visited"] = visited.Count, ["elements"] = visited.DeepClone(), ["missingRing"] = missing };
    }

    private async Task<JsonObject> EscapeAddAsync()
    {
        await web.ExecuteScriptAsync("(()=>{document.querySelectorAll('dialog[open]').forEach(d=>d.close());navigate('home');const b=document.querySelector('[data-action=\"add\"]');b.focus();b.click();})()");
        await Task.Delay(200);
        bool opened = bool.Parse(await web.ExecuteScriptAsync("document.querySelector('#add-dialog').open"));
        await PressKeyAsync("Escape", "Escape", 27);
        await Task.Delay(250);
        bool closed = bool.Parse(await web.ExecuteScriptAsync("!document.querySelector('#add-dialog').open"));
        bool returned = bool.Parse(await web.ExecuteScriptAsync("document.activeElement?.matches('[data-action=\"add\"]')===true"));
        await web.ExecuteScriptAsync("document.querySelector('#add-dialog').close();");
        return new JsonObject { ["opened"] = opened, ["closed"] = closed, ["returned"] = returned };
    }

    private async Task<JsonObject> SegmentedArrowsAsync()
    {
        await web.ExecuteScriptAsync("navigate('stats');document.querySelector('[data-stats-period=\"week\"]').focus();");
        await PressKeyAsync("ArrowRight", "ArrowRight", 39);
        bool month = bool.Parse(await web.ExecuteScriptAsync("document.activeElement?.dataset?.statsPeriod==='month'&&document.querySelector('[data-stats-period=month]').getAttribute('aria-pressed')==='true'"));
        await PressKeyAsync("ArrowLeft", "ArrowLeft", 37);
        bool week = bool.Parse(await web.ExecuteScriptAsync("document.activeElement?.dataset?.statsPeriod==='week'&&document.querySelector('[data-stats-period=week]').getAttribute('aria-pressed')==='true'"));
        return new JsonObject { ["right"] = month, ["left"] = week };
    }

    private async Task<JsonObject> SelectMenuArrowsAsync()
    {
        await web.ExecuteScriptAsync("(()=>{navigate('library');const b=document.querySelector('.ui-select');b.focus();})()");
        await PressKeyAsync("ArrowDown", "ArrowDown", 40);
        await Task.Delay(120);
        bool opened = bool.Parse(await web.ExecuteScriptAsync("!!document.querySelector('.ui-menu[role=menu]')"));
        string before = await web.ExecuteScriptAsync("document.querySelector('#library-sort').value");
        await PressKeyAsync("ArrowDown", "ArrowDown", 40);
        await PressKeyAsync("Enter", "Enter", 13, "\r");
        await Task.Delay(180);
        string after = await web.ExecuteScriptAsync("document.querySelector('#library-sort').value");
        bool closed = bool.Parse(await web.ExecuteScriptAsync("!document.querySelector('.ui-menu')"));
        bool returned = bool.Parse(await web.ExecuteScriptAsync("document.activeElement?.classList.contains('ui-select')===true"));
        await web.ExecuteScriptAsync("document.querySelector('#library-sort').value=" + before + ";document.querySelector('#library-sort').dispatchEvent(new Event('change',{bubbles:true}));");
        return new JsonObject { ["opened"] = opened, ["changed"] = before.Trim('"') != after.Trim('"'), ["closed"] = closed, ["returned"] = returned };
    }

    private async Task<JsonObject> ChartArrowsAsync()
    {
        await web.ExecuteScriptAsync("window.p5ChartPeriod=GaidenUI.moduleState('statistics').period;window.p5ChartYear=GaidenUI.moduleState('features').yearValue;GaidenUI.moduleState('statistics').period='year';GaidenUI.moduleState('features').yearValue=Number([...GaidenStatistics.combined(GaidenUI.state.snapshot).keys()].sort().at(-1).slice(0,4));navigate('stats');document.querySelector('[data-stat-bin=\"0\"]').focus();");
        await PressKeyAsync("ArrowRight", "ArrowRight", 39);
        bool right = bool.Parse(await web.ExecuteScriptAsync("document.querySelector('[data-stat-bin=\"1\"]').getAttribute('aria-pressed')==='true'"));
        await PressKeyAsync("ArrowLeft", "ArrowLeft", 37);
        bool left = bool.Parse(await web.ExecuteScriptAsync("document.querySelector('[data-stat-bin=\"0\"]').getAttribute('aria-pressed')==='true'"));
        await web.ExecuteScriptAsync("GaidenUI.moduleState('statistics').period=p5ChartPeriod;GaidenUI.moduleState('features').yearValue=p5ChartYear;render()");
        return new JsonObject { ["right"] = right, ["left"] = left };
    }

    private async Task PressKeyAsync(string key, string code, int vk, string? text = null)
    {
        var down = new JsonObject { ["type"] = text is null ? "rawKeyDown" : "keyDown", ["key"] = key, ["code"] = code, ["windowsVirtualKeyCode"] = vk, ["nativeVirtualKeyCode"] = vk };
        if (text is not null) down["text"] = text;
        await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", down.ToJsonString());
        var up = new JsonObject { ["type"] = "keyUp", ["key"] = key, ["code"] = code, ["windowsVirtualKeyCode"] = vk, ["nativeVirtualKeyCode"] = vk };
        await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", up.ToJsonString());
        await Task.Delay(45);
    }

    private async Task<(double WebViewPercent, double RendererPercent, double Seconds)> MeasureWebViewCpuAsync(int milliseconds)
    {
        await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Performance.enable", "{}");
        var before = WebViewCpuSnapshot();
        double rendererBefore = await RendererTaskDurationAsync();
        var clock = Stopwatch.StartNew();
        await Task.Delay(milliseconds);
        clock.Stop();
        double rendererAfter = await RendererTaskDurationAsync();
        var after = WebViewCpuSnapshot();
        var delta = TimeSpan.Zero;
        foreach (var entry in after)
            delta += entry.Value - (before.TryGetValue(entry.Key, out var value) ? value : TimeSpan.Zero);
        double seconds = delta.TotalSeconds;
        return (seconds / clock.Elapsed.TotalSeconds * 100, (rendererAfter - rendererBefore) / clock.Elapsed.TotalSeconds * 100, seconds);
    }

    private async Task<(double WebViewPercent, double RendererPercent, double Seconds)> MeasureProcessCpuAsync(int milliseconds)
    {
        var before = WebViewCpuSnapshot();
        var clock = Stopwatch.StartNew();
        await Task.Delay(milliseconds);
        clock.Stop();
        var after = WebViewCpuSnapshot();
        var delta = TimeSpan.Zero;
        foreach (var entry in after)
            delta += entry.Value - (before.TryGetValue(entry.Key, out var value) ? value : TimeSpan.Zero);
        double seconds = delta.TotalSeconds;
        return (seconds / clock.Elapsed.TotalSeconds * 100, 0, seconds);
    }

    private Dictionary<int, TimeSpan> WebViewCpuSnapshot()
    {
        var map = new Dictionary<int, TimeSpan>();
        foreach (var entry in WebViewProcessDetails()) map[entry.Key] = entry.Value.Cpu;
        return map;
    }

    private Dictionary<int, (TimeSpan Cpu, string Kind)> WebViewProcessDetails()
    {
        var map = new Dictionary<int, (TimeSpan Cpu, string Kind)>();
        var parents = new Dictionary<int, int>();
        var commands = new Dictionary<int, string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessId,ParentProcessId,CommandLine FROM Win32_Process WHERE Name='msedgewebview2.exe'");
            foreach (ManagementBaseObject item in searcher.Get())
            {
                int pid = Convert.ToInt32(item["ProcessId"]);
                parents[pid] = Convert.ToInt32(item["ParentProcessId"]);
                commands[pid] = item["CommandLine"] as string ?? "";
            }
        }
        catch { return map; }
        int self = Environment.ProcessId;
        bool Descends(int pid)
        {
            var seen = new HashSet<int>();
            for (int cursor = pid; cursor > 0 && seen.Add(cursor);)
            {
                if (cursor == self) return true;
                if (!parents.TryGetValue(cursor, out cursor)) return false;
            }
            return false;
        }
        foreach (var process in Process.GetProcessesByName("msedgewebview2"))
        {
            try
            {
                if (!Descends(process.Id)) continue;
                string command = commands.TryGetValue(process.Id, out var value) ? value : "";
                string kind = command.Contains("--type=gpu-process") ? "gpu" : command.Contains("--type=renderer") ? "renderer" : command.Contains("--type=utility") ? "utility" : "browser";
                map[process.Id] = (process.TotalProcessorTime, kind);
            }
            catch { }
            finally { process.Dispose(); }
        }
        return map;
    }

    private async Task<double> RendererTaskDurationAsync()
    {
        string json = await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Performance.getMetrics", "{}");
        using var document = JsonDocument.Parse(json);
        foreach (var metric in document.RootElement.GetProperty("metrics").EnumerateArray())
            if (metric.GetProperty("name").GetString() == "TaskDuration")
                return metric.GetProperty("value").GetDouble();
        return 0;
    }

    private async Task PhaseFiveFinalAsync()
    {
        string folder = Environment.GetEnvironmentVariable("GAIDEN_PHASE5_FINAL") ?? Path.Combine(AppContext.BaseDirectory, "phase5-final");
        Directory.CreateDirectory(folder);
        WindowState = FormWindowState.Normal;
        Size = new Size(1280, 800);
        Show();
        Activate();
        await Task.Delay(600);
        string[] shots = { "light-home", "dark-home", "light-stats", "dark-stats", "light-sessions", "light-library", "dark-library", "dark-detail", "light-settings", "dark-settings" };
        foreach (string shot in shots)
        {
            bool dark = shot.StartsWith("dark");
            string view = shot.Contains("stats") ? "stats" : shot.Contains("sessions") ? "sessions" : shot.Contains("library") ? "library" : shot.Contains("settings") ? "settings" : "home";
            string prep = "GaidenKit.clearToasts();document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=false;prefs.opaque=false;applyPrefs();GaidenTheme.select('" + (dark ? "dark" : "light") + "');GaidenUI.moduleState('statistics').period='week';GaidenUI.moduleState('statistics').bar=0;filter='all';search='';GaidenUI.moduleState('features').sortMode='recent';navigate('" + view + "');";
            if (shot.EndsWith("-detail")) prep += "openDetail(games[0].id);";
            prep += "scrollTo(0,0)";
            await web.ExecuteScriptAsync(prep);
            await Task.Delay(500);
            await web.ExecuteScriptAsync("document.activeElement?.blur();scrollTo(0,0)");
            await Task.Delay(150);
            if (await web.ExecuteScriptAsync("document.documentElement.scrollWidth>innerWidth+1") == "true") throw new InvalidOperationException("final overflow " + shot);
            using var bitmap = new Bitmap(Width, Height);
            using var graphics = Graphics.FromImage(bitmap);
            IntPtr dc = graphics.GetHdc();
            bool printed;
            try { printed = PrintWindow(Handle, dc, 2); } finally { graphics.ReleaseHdc(dc); }
            if (!printed) graphics.CopyFromScreen(Location, Point.Empty, Size);
            bitmap.Save(Path.Combine(folder, shot + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());navigate('home');");
    }
}
