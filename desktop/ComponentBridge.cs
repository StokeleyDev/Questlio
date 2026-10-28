using System.Text.Json;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    private readonly Dictionary<string,TaskCompletionSource<bool>> uiConfirmations=new();
    private bool uiCloseReady,flushingUiClose;
    private string? phaseThreeClosingProbe;
    internal static bool DeleteNeedsConfirmation(JsonNode message)=>message["confirmed"]?.GetValue<bool>()!=true;
    private async Task<bool> ConfirmUiAsync(string text,string title,string action="Продолжить")
    {
        if(web.CoreWebView2 is null)return false;
        string id=Guid.NewGuid().ToString("N");var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);uiConfirmations.Add(id,completion);
        try{
            await web.ExecuteScriptAsync("GaidenKit.confirm("+JsonSerializer.Serialize(text)+","+JsonSerializer.Serialize(title)+","+JsonSerializer.Serialize(action)+").then(accepted=>window.chrome.webview.postMessage({action:'themeChanged',dark:!!prefs.dark,uiConfirmation:{id:"+JsonSerializer.Serialize(id)+",accepted}}))");
            return await completion.Task;
        }finally{uiConfirmations.Remove(id);}
    }
    private async Task NativeNoticeAsync(string text)
    {
        if(web.CoreWebView2 is not null)await web.ExecuteScriptAsync("notify("+JsonSerializer.Serialize(text)+")");else DiagnosticLog.Write("notice",text);
    }
    private async Task FlushPendingUiDeletesAsync()
    {
        foreach(var completion in uiConfirmations.Values.ToArray())completion.TrySetResult(false);
        if(web.CoreWebView2 is null)return;
        while(webActivityBusy)await Task.Delay(15);
        if(web.CoreWebView2.IsSuspended)web.CoreWebView2.Resume();
        string finalDraft=await web.ExecuteScriptAsync("window.GaidenDetail?.closingDraft()??null");
        if(JsonNode.Parse(finalDraft) is JsonObject finalGame&&store.Snapshot()["games"]!.AsArray().Any(g=>g!["id"]!.GetValue<string>()==finalGame["id"]!.GetValue<string>()))store.SaveGames(new JsonArray(finalGame));
        string json=await web.ExecuteScriptAsync("(()=>{try{return GaidenKit.deletions.flush();}catch{return [];}})()");
        string[] ids=JsonSerializer.Deserialize<string[]>(json)??Array.Empty<string>();
        for(int attempt=0;attempt<200;attempt++){
            var alive=store.Snapshot()["games"]!.AsArray().Select(g=>g!["id"]!.GetValue<string>()).ToHashSet();
            if(!ids.Any(alive.Contains)){
                if(phaseThreeClosingProbe is not null){bool deleted=!alive.Contains(phaseThreeClosingProbe);string folder=Environment.GetEnvironmentVariable("GAIDEN_PHASE3_OUTPUT")??AppContext.BaseDirectory;File.WriteAllText(Path.Combine(folder,"close-check.log"),deleted?"pending-delete on actual FormClosing: SQLite deleted PASS":"FAIL");if(!deleted)throw new InvalidOperationException("Close deletion not committed");phaseThreeClosingProbe=null;}
                return;
            }
            await Task.Delay(25);
        }
        throw new InvalidOperationException("Не удалось завершить удаление. Окно остаётся открытым — повторите выход.");
    }
}
