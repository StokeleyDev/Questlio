using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
namespace GaidenDesktop;
internal static class ProcessChecks
{
 internal static void Run(){
  var result=new JsonObject();
  void Check(bool value,string message){if(!value)throw new Exception(message);}
  try{
   var entries=RunningPrograms.Read();Check(entries.Count>0,"Empty process list");Check(entries.Zip(entries.Skip(1),(a,b)=>a.Memory>=b.Memory).All(x=>x),"Memory sort");
   var fake=RunningPrograms.Sort(new[]{new RunningExe(1,1,"small.exe","",null,10,"a"),new RunningExe(2,1,"large.exe","",null,900,"b")});Check(fake[0].Pid==2,"Largest memory first");
   var current=entries.First(x=>x.Pid==Environment.ProcessId);string file=RunningPrograms.Verify(current);Check(string.Equals(file,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase),"Resolved wrong executable");
   try{RunningPrograms.Verify(current with{Started=current.Started-1});throw new Exception("PID reuse accepted");}catch(InvalidDataException){}
   var store=new LibraryStore(Path.Combine(Path.GetTempPath(),"Gaiden-process-check-"+Guid.NewGuid(),"check.db"));string id=Guid.NewGuid().ToString();store.SaveGames(new JsonArray(new JsonObject{["id"]=id,["name"]="Process probe",["platform"]="PC",["status"]="playing",["notes"]="Keep draft"}));store.BindExe(id,file);
   var game=store.Snapshot()["games"]![0]!;Check(LibraryStore.Exes(game).Contains(Path.GetFileNameWithoutExtension(file)),"Process EXE not bound");Check(game["notes"]!.GetValue<string>()=="Keep draft","Notes changed");
   result=new(){["passed"]=true,["realProcessList"]=entries.Count,["largestMemoryFirst"]=true,["liveExeResolved"]=true,["pidReuseRejected"]=true,["exeBoundOnTestDatabase"]=true,["notesPreserved"]=true};
  }catch(Exception error){result["passed"]=false;result["error"]=error.ToString();Environment.ExitCode=1;}
  finally{SqliteConnection.ClearAllPools();CheckEvidence.Write("process-test-result.json",result.ToJsonString());}
 }
}
