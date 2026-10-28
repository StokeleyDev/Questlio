using System.Diagnostics;
using System.Text.Json.Nodes;

namespace GaidenDesktop;
internal static class TrackerEdgeChecks
{
    internal static void Run(string probe)
    {
        string folder=Path.Combine(Path.GetTempPath(),"Gaiden-edge-"+Guid.NewGuid());
        var owned=new List<Process>();var results=new JsonObject();
        try {
            string path=Path.Combine(folder,"test.db");var store=new LibraryStore(path);string id=Guid.NewGuid().ToString();
            store.SaveGames(new JsonArray(new JsonObject{["id"]=id,["name"]="Edge probe",["platform"]="PC",["status"]="playing"}));
            string exe=Path.Combine(folder,"edge-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(probe,exe);store.BindExe(id,exe);
            Process Start(string target){var p=Process.Start(new ProcessStartInfo(target){UseShellExecute=false,CreateNoWindow=true})!;owned.Add(p);return p;}
            void Check(bool condition,string name){if(!condition)throw new Exception(name);results[name]=true;}
            var first=Start(exe);var second=Start(exe);Thread.Sleep(200);var tracker=new GameTracker(store);tracker.Poll();
            string sessionId=tracker.Current?.Id??throw new Exception("Not detected");
            first.Kill();first.WaitForExit();tracker.Poll();Check(tracker.Current?.Id==sessionId,"oneOfTwoProcessesClosed");
            Thread.Sleep(1100);tracker.Poll();tracker.Stop();Check(!tracker.Enabled&&store.Pending()==null,"stopWhileGameRunning");
            tracker.Poll();Check(tracker.Current==null,"stoppedDoesNotRestartItself");
            tracker.Start();tracker.Poll();Check(tracker.Current!=null&&tracker.Current.Id!=sessionId,"manualRestartCreatesNewSession");
            tracker.Suspend();Check(!tracker.Enabled&&store.Pending()==null,"suspendFinalizesCheckpoint");
            tracker.Resume();tracker.Poll();Check(tracker.Current!=null,"resumeDetectsRunningGame");
            tracker.Stop();tracker.Suspend();tracker.Resume();Check(!tracker.Enabled,"resumeRespectsManualPause");
            var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};info.ArgumentList.Add("--checkpoint-worker");info.ArgumentList.Add(path);
            var worker=Process.Start(info)!;owned.Add(worker);
            var limit=Stopwatch.StartNew();ActiveSession? pending=null;
            while(limit.Elapsed.TotalSeconds<10){Thread.Sleep(150);pending=store.Pending();if(pending is not null&&pending.ActiveSeconds+pending.IdleSeconds>=1)break;}
            if(pending is null||pending.ActiveSeconds+pending.IdleSeconds<1)throw new Exception("Worker checkpoint missing");
            worker.Kill();worker.WaitForExit();var before=store.Snapshot()["sessions"]!.AsArray().Count;
            _=new GameTracker(new LibraryStore(path));
            var sessions=store.Snapshot()["sessions"]!.AsArray();var recovered=sessions.Single(s=>s!["id"]!.GetValue<string>()==pending.Id)!;
            Check(store.Pending()==null&&sessions.Count==before+1&&recovered["endReason"]!.GetValue<string>()=="recovered-after-interruption","forcedProcessKillRecovery");
            Check(Math.Abs(recovered["activeSeconds"]!.GetValue<double>()-pending.ActiveSeconds)<0.001,"recoveryDoesNotAddOfflineTime");
            _=new GameTracker(new LibraryStore(path));Check(store.Snapshot()["sessions"]!.AsArray().Count==sessions.Count,"secondRecoveryDoesNotDuplicate");
            second.Kill();second.WaitForExit();var clean=new GameTracker(store);clean.Poll();Check(clean.Current==null,"noSessionWithoutProcess");
            results["passed"]=true;
        } catch(Exception error){Environment.ExitCode=1;results["passed"]=false;results["error"]=error.ToString();}
        finally {
            foreach(var process in owned){if(!process.HasExited){process.Kill();process.WaitForExit();}process.Dispose();}
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            // Only this run's explicitly created directory is removed.
            if(Directory.Exists(folder))Directory.Delete(folder,true);
            CheckEvidence.Write("tracker-edge-results.json",results.ToJsonString());
        }
    }
}
