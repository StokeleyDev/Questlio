using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
namespace GaidenDesktop;
internal static class ReviewChecks
{
 internal static void Run(string probe){
  string folder=Path.Combine(Path.GetTempPath(),"Gaiden-review-"+Guid.NewGuid());Directory.CreateDirectory(folder);var result=new JsonObject();var owned=new List<Process>();
  void Check(bool value,string name){if(!value)throw new Exception(name);result[name]=true;}
  JsonObject Game(string id,string notes="")=>new(){["id"]=id,["name"]="Review",["platform"]="PC",["status"]="playing",["notes"]=notes};
  try{
   string path=Path.Combine(folder,"review.db"),id=Guid.NewGuid().ToString();var store=new LibraryStore(path);store.SaveGames(new JsonArray(Game(id)));
   for(int i=0;i<10;i++)store.Finish(new ActiveSession{GameId=id,GameName="Review",ActiveSeconds=290},"review");
   var snapshot=store.Snapshot();Check(snapshot["games"]![0]!["minutes"]!.GetValue<long>()==48&&snapshot["games"]![0]!["trackedActiveSeconds"]!.GetValue<double>()==2900,"tenShortSessionsPreserveSeconds");
   var repeated=new ActiveSession{GameId=id,GameName="Review",ActiveSeconds=40};store.Finish(repeated,"review");store.Finish(repeated,"review");Check(store.Snapshot()["games"]![0]!["minutes"]!.GetValue<long>()==49,"fractionCarryAndIdempotency");
   var first=store.CreateAutoBackupIfChanged();Check(first is not null&&File.Exists(first),"firstAutomaticBackup");
   for(int i=0;i<7;i++)Check(store.CreateAutoBackupIfChanged() is null,"idleDoesNotRotateBackups");
   Check(Directory.GetFiles(store.BackupFolder,"auto-*.db").Length==1,"oneBackupDuringIdle");
   store.SaveGames(new JsonArray(Game(id,"Changed note")));Check(store.CreateAutoBackupIfChanged() is not null,"noteChangeCreatesBackup");
   store.Checkpoint(new ActiveSession{GameId=id,GameName="Review",ActiveSeconds=12});Check(store.CreateAutoBackupIfChanged() is not null,"checkpointIncludedInSignature");
   _=new GameTracker(store);
   store.BindExe(id,probe);
   var process=Process.Start(new ProcessStartInfo(probe){UseShellExecute=false,CreateNoWindow=true})!;owned.Add(process);Thread.Sleep(200);var tracker=new GameTracker(store);tracker.Poll();Check(tracker.Current is not null&&tracker.Current.PcId is null,"newSessionsIgnoreRetiredPcFeature");tracker.Stop();
   string leanPath=Path.Combine(folder,"lean.db");var lean=new LibraryStore(leanPath);lean.SaveGames(new JsonArray(Game(Guid.NewGuid().ToString())));
   using(var db=new SqliteConnection("Data Source="+leanPath)){db.Open();using var c=db.CreateCommand();c.CommandText="INSERT INTO sessions VALUES('unread','invalid json');INSERT INTO daily_records VALUES(1,'invalid json')";c.ExecuteNonQuery();}
   Check(lean.TrackerState().Games.Count==1,"trackerStateDoesNotReadHistory");
   using(var db=new SqliteConnection("Data Source="+leanPath)){db.Open();using var c=db.CreateCommand();c.CommandText="DELETE FROM sessions;DELETE FROM daily_records";c.ExecuteNonQuery();}
   string emulator="review-emulator-"+Guid.NewGuid().ToString("N");string exe=Path.Combine(folder,emulator+".exe");File.Copy(probe,exe);lean.AddEmulator("PS2",emulator,"iso","");
   var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true};info.ArgumentList.Add(@"C:\Review\Game.iso");var emulated=Process.Start(info)!;owned.Add(emulated);Thread.Sleep(200);
   int queries=EmulatorResolver.CommandQueryCount;var profiles=lean.TrackerState().Emulators;
   var matches=EmulatorResolver.Running(profiles,out var wmiIssue);EmulatorResolver.Running(profiles,out _);
   Check(matches.Count==1&&EmulatorResolver.CommandQueryCount-queries==1,"wmiQueriedOnceForSameProcess"+(matches.Count==1?"":": "+wmiIssue));
   emulated.Kill();emulated.WaitForExit();EmulatorResolver.Running(profiles,out _);Check(EmulatorResolver.CachedCommandCount==0,"wmiCachePrunesExitedProcesses");
   var steam=SteamClient.ParseOwned(JsonNode.Parse("{\"response\":{\"game_count\":1,\"games\":[{\"appid\":50,\"name\":\"Steam review\",\"playtime_forever\":120,\"rtime_last_played\":1700000000}]}}")!,"76561198000000000");
   store.ImportSteam(steam,new JsonArray(new JsonObject{["appId"]=50,["gameId"]=""}));var imported=store.Snapshot()["games"]!.AsArray().Single(g=>g!["steam"] is not null)!;
   Check(imported["steam"]!["lastPlayedAt"]!.GetValue<string>()==DateTimeOffset.FromUnixTimeSeconds(1700000000).ToString("O"),"steamLastPlayedImported");
   result["passed"]=true;
  }catch(Exception error){result["passed"]=false;result["error"]=error.ToString();Environment.ExitCode=1;}
  finally{foreach(var p in owned){if(!p.HasExited){p.Kill();p.WaitForExit();}p.Dispose();}SqliteConnection.ClearAllPools();CheckEvidence.Write("review-test-result.json",result.ToJsonString());}
 }
}
