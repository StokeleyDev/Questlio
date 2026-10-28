using System.Diagnostics;
using System.Text.Json.Nodes;

namespace GaidenDesktop;
internal static class TrackerChecks
{
    internal static void Run(string probeExe)
    {
        var folder=Path.Combine(Path.GetTempPath(),"Gaiden-tracker-check-"+Guid.NewGuid());
        Process? process=null;
        try {
            var path=Path.Combine(folder,"test.db");var store=new LibraryStore(path);var id=Guid.NewGuid().ToString();
            store.SaveGames(new JsonArray(new JsonObject{["id"]=id,["name"]="Тест",["platform"]="PC",["status"]="playing"}));
            store.BindExe(id,probeExe);
            var session=new ActiveSession {GameId=id,GameName="Тест"};session.Advance(10,false);session.Advance(5,true);session.Advance(3600,false);
            if(session.ActiveSeconds!=10||session.IdleSeconds!=5||session.UnobservedSeconds!=3600)throw new Exception("Idle/gap accounting");
            session.Advance(10,false);session.Advance(10,false);session.Advance(10,false);session.Advance(10,false);session.Advance(10,false);session.Advance(5,false);
            store.Checkpoint(session);store.Finish(session,"test");store.Finish(session,"test");
            var snapshot=store.Snapshot();if(snapshot["sessions"]!.AsArray().Count!=1||snapshot["games"]![0]!["minutes"]!.GetValue<long>()!=1||store.Pending()!=null)throw new Exception("Idempotent finalization");
            var pending=new ActiveSession{GameId=id,GameName="Тест",ActiveSeconds=12};store.Checkpoint(pending);
            _=new GameTracker(new LibraryStore(path));
            if(store.Pending()!=null||store.Snapshot()["sessions"]!.AsArray().Count!=2)throw new Exception("Recovery");
            var tracker=new GameTracker(store);
            process=Process.Start(new ProcessStartInfo(probeExe){UseShellExecute=false,CreateNoWindow=true})!;
            Thread.Sleep(200);tracker.Poll();
            if(tracker.Current is null)throw new Exception("Process not detected");
            Thread.Sleep(1100);tracker.Poll();
            if(tracker.Current!.ActiveSeconds+tracker.Current.IdleSeconds<1)throw new Exception("Clock not advanced");
            process.Kill();process.WaitForExit();tracker.Poll();
            if(tracker.Current!=null||store.Pending()!=null||store.Snapshot()["sessions"]!.AsArray().Count!=3)throw new Exception("Process close not finalized");
            tracker.Stop();if(tracker.Enabled)throw new Exception("Stop failed");tracker.Start();if(!tracker.Enabled)throw new Exception("Restart failed");
            CheckEvidence.Write("tracker-test-result.json","{\"passed\":true,\"processDetectionAndClose\":true,\"idleAndLongGap\":true,\"checkpointRecovery\":true,\"idempotentFinish\":true,\"stopRestart\":true}");
        } catch(Exception error){Environment.ExitCode=1;CheckEvidence.Write("tracker-test-result.json",new JsonObject{["passed"]=false,["error"]=error.ToString()}.ToJsonString());}
        finally{if(process is not null){if(!process.HasExited)process.Kill();process.Dispose();}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
}
