using System.Diagnostics;
using System.Text.Json.Nodes;

namespace GaidenDesktop;
internal static class EmulatorChecks
{
    internal static void Run(string probe)
    {
        var folder=Path.Combine(Path.GetTempPath(),"Gaiden-emulator-"+Guid.NewGuid());Process? process=null;var results=new JsonObject();
        try {
            var store=new LibraryStore(Path.Combine(folder,"test.db"));var exe="retroarch-probe-"+Guid.NewGuid().ToString("N");
            store.AddEmulator("SNES",exe,"sfc, smc","snes9x_libretro.dll");
            store.AddEmulator("NES",exe,"nes","fceumm_libretro.dll");
            var profiles=store.Snapshot()["emulators"]!.AsArray().Select(p=>p!).ToArray();
            void Check(bool ok,string name){if(!ok)throw new Exception(name);results[name]=true;}
            var command=$"\"C:\\emulators\\{exe}.exe\" -L \"C:\\cores\\snes9x_libretro.dll\" \"C:\\My Games\\Mario (USA) [!].SFC\"";
            var match=EmulatorResolver.Resolve(exe,command,profiles);
            Check(match?.Name=="Mario"&&match.Platform=="SNES","quotedPathUppercaseExtensionTags");
            Check(EmulatorResolver.Resolve(exe,$"{exe}.exe C:\\Mario.sfc",profiles)==null,"missingCoreRejected");
            Check(EmulatorResolver.Resolve(exe,command.Replace("snes9x_libretro.dll","fceumm_libretro.dll"),profiles)==null,"wrongCoreRomCombinationRejected");
            Check(EmulatorResolver.Resolve(exe,command+" C:\\Second.sfc",profiles)==null,"multipleRomsRejected");
            Check(EmulatorResolver.Resolve("retroarch-probe",command,profiles)==null,"executableMatchesExactly");
            store.AddAutomaticEmulator("RetroArch","retroarch-auto.exe");store.AddAutomaticEmulator("PCSX2","pcsx2-qt.exe");
            var automatic=store.Snapshot()["emulators"]!.AsArray().Select(p=>p!).ToArray();
            Check(EmulatorResolver.Resolve("retroarch-auto",@"retroarch-auto.exe -L C:\cores\snes9x_libretro.dll ""C:\My Games\Mario.sfc""",automatic)?.Platform=="SNES","automaticRetroArchCoreAndPlatform");
            Check(EmulatorResolver.Resolve("pcsx2-qt",@"pcsx2-qt.exe ""C:\Games\Game.chd""",automatic)?.Platform=="PlayStation 2","automaticStandalonePlatformAndExtension");
            Check(EmulatorResolver.Resolve("retroarch-auto",@"retroarch-auto.exe C:\Mario.sfc",automatic)==null,"automaticMissingCoreNotGuessed");
            var id=store.EmulatorGame(match!);Check(store.EmulatorGame(match!)==id&&store.Snapshot()["games"]!.AsArray().Count==1,"romAutoAddIsIdempotent");
            var path=Path.Combine(folder,exe+".exe");File.Copy(probe,path);
            var info=new ProcessStartInfo(path){UseShellExecute=false,CreateNoWindow=true};foreach(var a in new[]{"-L",@"C:\cores\snes9x_libretro.dll",@"C:\My Games\Mario (USA) [!].SFC"})info.ArgumentList.Add(a);
            process=Process.Start(info)!;Thread.Sleep(250);var tracker=new GameTracker(store);tracker.Poll();
            Check(tracker.Current?.GameId==id,"liveWmiCommandLineDetection");Thread.Sleep(1100);tracker.Poll();process.Kill();process.WaitForExit();tracker.Poll();
            Check(tracker.Current==null&&store.Snapshot()["sessions"]!.AsArray().Count==1,"emulatedSessionFinalized");
            results["passed"]=true;
        }catch(Exception error){Environment.ExitCode=1;results["passed"]=false;results["error"]=error.ToString();}
        finally{if(process is not null){if(!process.HasExited){process.Kill();process.WaitForExit();}process.Dispose();}Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(folder))Directory.Delete(folder,true);CheckEvidence.Write("emulator-test-result.json",results.ToJsonString());}
    }
}
