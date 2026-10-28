using System.Text.Json.Nodes;
using System.IO.Compression;
namespace GaidenDesktop;
internal static class FeatureChecks
{
    internal static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"Gaiden-features-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        try
        {
            var store=new LibraryStore(Path.Combine(folder,"check.db"));var options=new TrackingOptions(true,2);store.SaveTracking(options);
            if(store.TrackingSettings()!=options||options.IsIdle(119999)||!options.IsIdle(120000)||new TrackingOptions(false,1).IsIdle(uint.MaxValue))throw new Exception("Idle boundary/settings persistence");
            try{store.SaveTracking(new(true,0));throw new Exception("Invalid threshold accepted");}catch(InvalidDataException){}
            string id=Guid.NewGuid().ToString();store.SaveGames(new JsonArray(new JsonObject{["id"]=id,["name"]="Test",["status"]="playing",["platform"]="PC"}));
            store.BindExe(id,Environment.ProcessPath!);if(store.Snapshot()["games"]![0]!["exeIcon"]?.GetValue<string>()?.StartsWith("data:image/png;base64,")!=true)throw new Exception("EXE icon extraction");
            var session=new ActiveSession{GameId=id,GameName="Test",ActiveSeconds=95};store.Checkpoint(session);store.Finish(session,"test");
            store.SetBaseline(id,125);var snapshot=store.Snapshot();
            if(snapshot["games"]![0]!["minutes"]!.GetValue<long>()!=126||snapshot["games"]![0]!["trackedActiveSeconds"]!.GetValue<double>()!=95||snapshot["sessions"]!.AsArray().Count!=1)throw new Exception("Baseline must not rewrite recorded sessions");
            store.SetBaseline(id,0);if(store.Snapshot()["games"]![0]!["minutes"]!.GetValue<long>()!=1)throw new Exception("Baseline zero must retain tracked minute");
            try{store.SetBaseline(id,-1);throw new Exception("Negative baseline accepted");}catch(InvalidDataException){}
            store.AddAutomaticEmulator("PS2","pcsx2.exe");var profile=store.Snapshot()["emulators"]![0]!;string pid=profile["id"]!.GetValue<string>();
            store.UpdateEmulator(pid,"PlayStation 2",".ISO,chd,iso","",false,"pcsx2-qt.exe");profile=store.Snapshot()["emulators"]![0]!;
            if(profile["id"]!.GetValue<string>()!=pid||profile["exe"]!.GetValue<string>()!="pcsx2-qt"||profile["extensions"]!.AsArray().Count!=2)throw new Exception("Emulator identity/normalization");
            var match=EmulatorResolver.Resolve("pcsx2-qt","pcsx2-qt.exe \"C:\\Games\\Example.iso\"",store.Snapshot()["emulators"]!.AsArray().Select(x=>x!));if(match?.Platform!="PlayStation 2")throw new Exception("Edited emulator did not resolve");
            var before=profile.ToJsonString();try{store.UpdateEmulator(pid,"Bad","../bad","",false);throw new Exception("Bad extension accepted");}catch(InvalidDataException){}
            if(store.Snapshot()["emulators"]![0]!.ToJsonString()!=before)throw new Exception("Invalid editor save changed profile");
            string backup=store.CreateBackup();store.SaveTracking(new(false,20));store.RestoreBackup(backup);if(store.TrackingSettings()!=options)throw new Exception("Settings backup roundtrip");
            if(StartupRegistration.Command(@"C:\Program Files\Gaiden\Gaiden.exe")!="\"C:\\Program Files\\Gaiden\\Gaiden.exe\" --background")throw new Exception("Startup command quoting");
            DiagnosticLog.Folder=Path.Combine(folder,"logs");DiagnosticLog.Write("check","diagnostic-probe");string zip=Path.Combine(folder,"diag.zip");DiagnosticLog.Export(zip,new JsonObject{["version"]="0.19",["games"]=1});
            using(var archive=ZipFile.OpenRead(zip))if(archive.Entries.Any(e=>e.Name.EndsWith(".db"))||archive.GetEntry("summary.json") is null||archive.GetEntry("app.log") is null)throw new Exception("Diagnostic export content");
            var report=new JsonObject{["exeIconExtraction"]=true,["settings"]=true,["idleThresholdBoundary"]=true,["baselinePreservesSessions"]=true,["baselineZero"]=true,["emulatorEditorStableId"]=true,["editedEmulatorResolvesRom"]=true,["validationRollback"]=true,["settingsBackupRoundtrip"]=true,["startupQuoted"]=true,["diagnosticNoDatabase"]=true};
            CheckEvidence.Write("features-test-result.json",report.ToJsonString());
        }
        catch(Exception error){CheckEvidence.Write("features-test-result.json",new JsonObject{["error"]=error.ToString()}.ToJsonString());Environment.ExitCode=1;}
    }
}
