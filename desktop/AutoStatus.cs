using System.Text.Json;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed partial class LibraryStore
{
    internal void ConfirmSessionStart(ActiveSession session)
    {
        using var db=Open();using var tx=db.BeginTransaction();using var c=db.CreateCommand();c.Transaction=tx;
        c.CommandText="SELECT payload FROM games WHERE id=$id";c.Parameters.AddWithValue("$id",session.GameId);
        var game=JsonNode.Parse((string?)c.ExecuteScalar()??throw new InvalidOperationException("Игра не найдена"))!.AsObject();
        if(game["lastSessionId"]?.GetValue<string>()!=session.Id){
            long count=game["sessions"]?.GetValue<long>()??0;
            if(count==0&&game["status"]?.GetValue<string>()=="backlog"&&game["autoStatus"]?.GetValue<bool>()!=false){game["status"]="playing";game["autoStatus"]=true;game["autoStatusSession"]=session.Id;}
            game["sessions"]=count+1;game["lastPlayedAt"]=session.StartedAt.ToString("O");game["lastSessionId"]=session.Id;
            c.CommandText="UPDATE games SET payload=$payload WHERE id=$id";c.Parameters.AddWithValue("$payload",game.ToJsonString());c.ExecuteNonQuery();
        }
        session.StartCounted=true;c.Parameters.Clear();c.CommandText="INSERT INTO active_session VALUES(1,$payload) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload";
        c.Parameters.AddWithValue("$payload",JsonSerializer.Serialize(session));c.ExecuteNonQuery();tx.Commit();
    }
    internal void UndoAutoStatus(string id,string session)=>EditGame(id,g=>{
        if(g["autoStatus"]?.GetValue<bool>()==true&&g["autoStatusSession"]?.GetValue<string>()==session&&g["status"]?.GetValue<string>()=="playing"){
            g["status"]="backlog";g["autoStatus"]=false;g.Remove("autoStatusSession");
        }
    });
}
internal sealed partial class MainWindow
{
    private bool webActivityBusy,trackerSnapshotDeferred;
    private bool suspendWebView=true;
    private async Task SyncWebViewActivityAsync()
    {
        if(webActivityBusy||web.CoreWebView2 is null||IsDisposed||flushingUiClose||closingSession)return;
        webActivityBusy=true;
        try{
            for(int attempt=0;attempt<3&&!IsDisposed;attempt++){
                bool paused=WindowState==FormWindowState.Minimized||!Visible;
                if(paused){if(suspendWebView&&!web.CoreWebView2.IsSuspended)await web.CoreWebView2.TrySuspendAsync();}
                else {if(web.CoreWebView2.IsSuspended)web.CoreWebView2.Resume();await UpdateTrackerAsync(trackerSnapshotDeferred);}
                if(paused==(WindowState==FormWindowState.Minimized||!Visible))break;
            }
        }catch(Exception error)when(error is InvalidOperationException or System.Runtime.InteropServices.COMException or ObjectDisposedException){}
        finally{webActivityBusy=false;}
    }
}
