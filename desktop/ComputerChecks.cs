// Compatibility check for databases/backups made before PC economy was retired.
using Microsoft.Data.Sqlite;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal static class ComputerChecks
{
 internal static void Run(){var result=new JsonObject();try{
  string path=Path.Combine(Path.GetTempPath(),"Gaiden-legacy-pc-check-"+Guid.NewGuid(),"check.db");var store=new LibraryStore(path);string gameId=Guid.NewGuid().ToString(),pcId=Guid.NewGuid().ToString();
  store.SaveGames(new JsonArray(new JsonObject{["id"]=gameId,["name"]="Legacy check",["platform"]="PC",["status"]="playing"}));
  using(var db=new SqliteConnection("Data Source="+path)){db.Open();using var cmd=db.CreateCommand();cmd.CommandText="INSERT INTO pcs VALUES($id,$payload);INSERT INTO settings VALUES('currentPc',$id)";cmd.Parameters.AddWithValue("$id",pcId);cmd.Parameters.AddWithValue("$payload",new JsonObject{["id"]=pcId,["name"]="Legacy PC",["trackedSeconds"]=0}.ToJsonString());cmd.ExecuteNonQuery();}
  var pending=new ActiveSession{GameId=gameId,GameName="Legacy check",PcId=pcId,PcName="Legacy PC",ActiveSeconds=90};store.Checkpoint(pending);_=new GameTracker(store);_=new GameTracker(store);
  var snap=store.Snapshot();if(snap.ContainsKey("pcs")||snap.ContainsKey("currentPcId")||snap["sessions"]!.AsArray().Count!=1)throw new Exception("Retired PC feature exposed or recovery duplicated");
  var copy=store.CreateBackup();LibraryStore.ValidateBackup(copy);
  result=new(){["passed"]=true,["oldPendingSessionRecoveredOnce"]=true,["retiredPcNotExposed"]=true,["oldDatabaseStillBacksUp"]=true};
 }catch(Exception error){result["passed"]=false;result["error"]=error.ToString();Environment.ExitCode=1;}finally{SqliteConnection.ClearAllPools();CheckEvidence.Write("computer-test-result.json",result.ToJsonString());}}
}
