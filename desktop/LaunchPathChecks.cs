using System.Diagnostics;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal static class LaunchPathChecks
{
    internal static void Run(string probe)
    {
        string folder=Path.Combine(Path.GetTempPath(),"Gaiden-learn-"+Guid.NewGuid());Directory.CreateDirectory(folder);Process? process=null;
        try{
            string exe=Path.Combine(folder,"learn-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(probe,exe);var store=new LibraryStore(Path.Combine(folder,"test.db"));string id=Guid.NewGuid().ToString();
            store.SaveGames(new JsonArray(new JsonObject{["id"]=id,["name"]="Learning probe",["platform"]="PC",["status"]="playing"}));store.BindExe(id,Path.GetFileName(exe));
            process=Process.Start(new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true});Thread.Sleep(250);var tracker=new GameTracker(store);tracker.Poll();
            JsonNode Game()=>store.Snapshot()["games"]!.AsArray().Single(g=>g!["id"]!.GetValue<string>()==id)!;
            if(tracker.Current?.GameId!=id||Game()["launchPath"]?.GetValue<string>()!=exe||store.GameStart(id).FileName!=exe)throw new Exception("Matched process path not learned");
            string chosenFolder=Path.Combine(folder,"chosen");Directory.CreateDirectory(chosenFolder);string chosen=Path.Combine(chosenFolder,Path.GetFileName(exe));File.Copy(exe,chosen);store.RememberExecutable(id,chosen);tracker.Poll();
            if(store.GameStart(id).FileName!=chosen||store.LearnLaunchPath(Game(),()=>exe))throw new Exception("User-selected path replaced");
            string blocked=Guid.NewGuid().ToString();store.SaveGames(new JsonArray(new JsonObject{["id"]=blocked,["name"]="Protected probe",["platform"]="PC",["status"]="backlog"}));store.BindExe(blocked,"protected-check.exe");var g=store.Snapshot()["games"]!.AsArray().Single(g=>g!["id"]!.GetValue<string>()==blocked)!;
            if(store.LearnLaunchPath(g,()=>throw new System.ComponentModel.Win32Exception(5))||store.LearnLaunchPath(g,()=>exe))throw new Exception("Access denied or wrong executable accepted");tracker.Poll();tracker.Stop();
            CheckEvidence.Write("launch-path-result.json","{\"matchedProcessLearned\":true,\"userChoicePreserved\":true,\"accessDeniedEmulated\":true,\"wrongNameRejected\":true,\"trackerAlive\":true}");
        }finally{if(process is not null){if(!process.HasExited)process.Kill();process.WaitForExit(3000);process.Dispose();}}
    }
}
