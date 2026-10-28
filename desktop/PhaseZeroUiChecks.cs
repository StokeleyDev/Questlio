namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    private async Task<string> PhaseZeroUiChecksAsync(bool persist=true)
    {
        var result=await web.ExecuteScriptAsync("""
        (()=>{
          const stable={render,navigate,openDetail,media,renderSettings,renderSessions};const base=structuredClone(GaidenUI.state.snapshot),checks={};
          document.querySelectorAll('dialog[open]').forEach(d=>d.close());filter='all';search='';navigate('library');
          let input=document.querySelector('#library-search');input.value='a';input.dispatchEvent(new Event('input',{bubbles:true}));input.focus();input.setSelectionRange(0,1);window.scrollTo(0,140);
          const original=input,rootChild=document.querySelector('#main').firstElementChild;window.applyNativeSnapshot(base);
          checks.unchangedSnapshotKeepsNodes=original===document.querySelector('#library-search')&&rootChild===document.querySelector('#main').firstElementChild;
          const changed=structuredClone(base),game=changed.games.find(g=>g.name.toLocaleLowerCase('ru').includes('a'));game.name+=' updated';const beforeScroll=scrollY;window.applyNativeSnapshot(changed);input=document.querySelector('#library-search');
          checks.snapshotPreservesSearch=input.value==='a';checks.snapshotPreservesFocus=document.activeElement===input;checks.snapshotPreservesSelection=input.selectionStart===0&&input.selectionEnd===1;checks.snapshotPreservesScroll=scrollY===beforeScroll;
          openDetail(games[0].id);document.querySelector('#edit-game-name').value='Phase zero draft';document.querySelector('#game-notes').value='Phase zero note';document.querySelector('#game-notes').focus();const detailField=document.querySelector('#game-notes');document.querySelector('#detail-dialog').scrollTop=120;const detailScroll=document.querySelector('#detail-dialog').scrollTop;
          window.applyNativeSnapshot(base);checks.detailNodeAndDraftStay=detailField===document.querySelector('#game-notes')&&detailField.value==='Phase zero note'&&document.querySelector('#edit-game-name').value==='Phase zero draft';checks.detailFocusAndScrollStay=document.activeElement===detailField&&document.querySelector('#detail-dialog').scrollTop===detailScroll;
          document.querySelector('#detail-dialog').close();navigate('settings');const threshold=document.querySelector('#tracking-form [name=idleMinutes]');threshold.closest('details').open=true;threshold.value='27';threshold.focus();const expanded=document.querySelector('#tracking-form').closest('details');
          const profiles=structuredClone(base);profiles.emulators=[{id:'4d3c9ff7-cdde-488e-b123-434532ac8c72',name:'Snapshot probe',exe:'pcsx2',extensions:['iso'],automatic:true,core:''}];window.applyNativeSnapshot(profiles);
          checks.settingsDraftAndExpansionStay=document.querySelector('#tracking-form [name=idleMinutes]').value==='27'&&document.querySelector('#tracking-form').closest('details').open;
          const previousTheme=prefs.dark;prefs.dark=!previousTheme;applyPrefs();render();checks.preferenceControlsFollowState=document.querySelector('[data-theme-mode]').value===(prefs.dark?'dark':'light');prefs.dark=previousTheme;applyPrefs();render();
          checks.globalsNeverReassigned=Object.entries(stable).every(([key,value])=>eval(key)===value);
          checks.singleAuthoritativeState=GaidenUI.state.games===games&&GaidenUI.state.prefs===prefs&&GaidenUI.state.snapshot.sessions===window.gaidenSessions;
          checks.versionFromBootstrap=!!window.gaidenBootstrap.version&&document.querySelector('[data-app-version]').textContent==='Questlio '+window.gaidenBootstrap.version&&document.querySelector('.sidebar-footer').textContent==='Questlio Desktop '+window.gaidenBootstrap.version;
          checks.noDemoMarkup=!document.querySelector('.demo-badge')&&!document.body.textContent.includes('Gaiden Preview')&&!document.body.textContent.includes('Демо-данные');
          const empty={games:[],sessions:[],daily:[],emulators:[]};window.applyNativeSnapshot(empty);navigate('home');checks.emptyLibraryHasNoFakeGames=games.length===0&&!document.querySelector('.hero')&&document.querySelector('#library-count').textContent==='0';
          window.applyNativeSnapshot(base);filter='all';search='';navigate('home');return checks;
        })()
        """);
        if(persist)CheckEvidence.Write("phase0-ui-result.json",result);
        if(System.Text.Json.Nodes.JsonNode.Parse(result)!.AsObject().Any(p=>p.Value?.GetValue<bool>()!=true))throw new InvalidOperationException("Phase 0 UI checks failed: "+result);
        return result;
    }
}
