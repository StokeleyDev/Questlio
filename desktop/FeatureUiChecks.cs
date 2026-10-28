using Microsoft.Web.WebView2.Core;
namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    private async Task FeatureUiChecksAsync()
    {
        ShowQuickView();if(quickView is null||quickView.Controls[0].Controls.OfType<Button>().Count()!=5)throw new InvalidOperationException("Quick view rows");
        if(!Environment.GetCommandLineArgs().Contains("--no-capture"))using(var image=new Bitmap(quickView.Width,quickView.Height)){quickView.DrawToBitmap(image,new Rectangle(Point.Empty,quickView.Size));image.Save(Path.Combine(AppContext.BaseDirectory,"features-tray.png"));}quickView.Close();
        CheckEvidence.Write("quick-view-result.json","{\"lastFiveGames\":true,\"separateWindow\":true}");
        string checks=await web.ExecuteScriptAsync("""
        (()=>{
          prefs.reduced=true;applyPrefs();const result={};const baseline=structuredClone(window.gaidenBootstrap);
          navigate('settings');result.settings=!!document.querySelector('#tracking-form')&&document.querySelectorAll('details.settings-group').length===5;
          result.diagnostics=!!document.querySelector('[data-export-diagnostics]');
          const profile={id:'4d3c9ff7-cdde-488e-b123-434532ac8c72',name:'Test PCSX2',exe:'pcsx2',extensions:['iso','chd'],core:'',automatic:true};
          const snap=structuredClone(baseline);snap.emulators=[profile];window.applyNativeSnapshot(snap);navigate('settings');
          document.querySelector('[data-edit-emulator]').click();result.emulatorEditor=document.querySelector('#emulator-editor').open&&document.querySelector('#emulator-edit-form [name=extensions]').value==='iso,chd';document.querySelector('#emulator-editor').close();
          const id=games[0].id;const date=(month,day)=>new Date(2026,month,day,12).toISOString();snap.sessions=Array.from({length:16},(_,i)=>({id:'feature-'+i,gameId:id,legacyGameName:games[0].name,startedAt:date(i===15?1:0,i%14+1),activeSeconds:600,idleSeconds:i===0?120:0}));window.applyNativeSnapshot(snap);
          openDetail(id);result.baseline=!!document.querySelector('#baseline-form')&&document.querySelector('#baseline-form [name=minutes]').max==='59';
          document.querySelector('#edit-game-name').value='Draft survives history';document.querySelector('#game-notes').value='Unsent';
          result.historyInitial=document.querySelectorAll('#game-history tbody tr').length===10;document.querySelector('[data-game-history-more]').click();result.historyMore=document.querySelectorAll('#game-history tbody tr').length===16;
          const month=document.querySelector('#game-history-month');month.value='2';month.dispatchEvent(new Event('change',{bubbles:true}));result.historyFilter=document.querySelectorAll('#game-history tbody tr').length===1&&document.querySelector('#game-history').textContent.includes('1 сессий');
          result.historyPreservesDraft=document.querySelector('#edit-game-name').value==='Draft survives history'&&document.querySelector('#game-notes').value==='Unsent';
          document.querySelector('#detail-dialog').close();navigate('stats');document.querySelector('[data-stats-period=year]').click();const year=document.querySelector('#annual-year');year.value='2026';year.dispatchEvent(new Event('change',{bubbles:true}));result.annual=document.querySelectorAll('.annual-column').length===12&&GaidenStatistics.period(GaidenUI.state.snapshot,'year',new Date(),2026).total.count===16;
          const count=GaidenFeatures.year(snap.sessions,2026);result.annualTotals=count.total===9600&&count.months[0]===9000&&count.months[1]===600&&count.days===15;
          document.querySelector('[data-stats-period=week]').click();navigate('library');const sort=document.querySelector('#library-sort');sort.value='name';sort.dispatchEvent(new Event('change',{bubbles:true}));const names=[...document.querySelectorAll('.library-grid .game-name')].map(n=>n.textContent);result.sort=names.every((n,i)=>i===0||names[i-1].localeCompare(n,'ru')<=0);result.filteredTotal=(()=>{const hidden=!document.querySelector('.library-result');search=games[0].name;render();const text=document.querySelector('.library-result')?.textContent||'',total=games.filter(g=>g.name.toLocaleLowerCase('ru').includes(search.toLocaleLowerCase('ru'))).reduce((sum,g)=>sum+(Number(g.minutes)||0),0);search='';render();return hidden&&text.includes(fmt(total*60))&&text.includes('в Questlio');})();
          document.querySelector('#library-sort').value='recent';document.querySelector('#library-sort').dispatchEvent(new Event('change',{bubbles:true}));window.applyNativeSnapshot(baseline);navigate('stats');return result;
        })()
        """);
        CheckEvidence.Write("features-ui-result.json",checks);
        if(System.Text.Json.Nodes.JsonNode.Parse(checks)!.AsObject().Any(x=>x.Value?.GetValue<bool>()!=true))throw new InvalidOperationException("Feature UI check failed: "+checks);
        var chosen=store.Snapshot()["games"]!.AsArray()[0]!;string gameId=chosen["id"]!.GetValue<string>();
        long trackedMinutes=(long)((chosen["trackedActiveSeconds"]?.GetValue<double>()??0)/60);
        long originalBaseline=(chosen["minutes"]?.GetValue<long>()??0)-trackedMinutes;
        await web.ExecuteScriptAsync("openDetail("+System.Text.Json.JsonSerializer.Serialize(gameId)+");document.querySelector('#edit-game-name').value='Bridge draft';document.querySelector('#game-notes').value='Bridge note';");
        await FeatureActionAsync(new System.Text.Json.Nodes.JsonObject{["action"]="setBaseline",["id"]=gameId,["minutes"]=1234L});await Task.Delay(120);
        string bridge=await web.ExecuteScriptAsync("({baselineSaved:games.find(g=>g.id==="+System.Text.Json.JsonSerializer.Serialize(gameId)+").baselineMinutes===1234,baselineDraftPreserved:document.querySelector('#edit-game-name').value==='Bridge draft'&&document.querySelector('#game-notes').value==='Bridge note',detailRemainsOpen:document.querySelector('#detail-dialog').open})");
        if(System.Text.Json.Nodes.JsonNode.Parse(bridge)!.AsObject().Any(x=>x.Value?.GetValue<bool>()!=true))throw new InvalidOperationException("Baseline bridge check failed");
        CheckEvidence.Write("features-bridge-result.json",bridge);
        store.SetBaseline(gameId,originalBaseline);await FeatureActionAsync(new System.Text.Json.Nodes.JsonObject{["action"]="featureSettings"});await Task.Delay(80);
        await web.ExecuteScriptAsync("document.querySelector('#detail-dialog').close();navigate('stats');");
        if(!Environment.GetCommandLineArgs().Contains("--no-capture"))foreach(var scene in new[]{"annual","history","settings"})
        {
            await web.ExecuteScriptAsync(scene=="history"?"openDetail(games[0].id);document.querySelector('#game-history').scrollIntoView({block:'start'});":scene=="settings"?"document.querySelector('#detail-dialog').close();navigate('settings');const d=document.querySelector('#tracking-form').closest('details');d.open=true;d.scrollIntoView({block:'start'});":"navigate('stats');document.querySelector('.annual-panel').scrollIntoView({block:'start'});");
            await Task.Delay(200);using var shot=File.Create(Path.Combine(AppContext.BaseDirectory,"features-"+scene+".png"));await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
        }
    }
}

