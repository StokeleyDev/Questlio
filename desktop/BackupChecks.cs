using Microsoft.Data.Sqlite;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal static class BackupChecks
{
    internal static void Run(string package)
    {
        var folder=Path.Combine(Path.GetTempPath(),"Gaiden-backup-check-"+Guid.NewGuid());var result=new JsonObject();
        try{
            var path=Path.Combine(folder,"test.db");var store=new LibraryStore(path);store.Import(File.ReadAllText(package));
            var manual=store.CreateBackup();
            using(var hold=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Pooling=false}.ToString())){
                hold.Open();var games=store.Snapshot()["games"]!.AsArray();games[0]!["notes"]="WAL probe";store.SaveGames(games);
                var walCopy=store.CreateBackup();using var read=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=walCopy,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());read.Open();using var c=read.CreateCommand();c.CommandText="SELECT payload FROM games WHERE id=$id";c.Parameters.AddWithValue("$id",games[0]!["id"]!.GetValue<string>());if(JsonNode.Parse((string)c.ExecuteScalar()!)!["notes"]!.GetValue<string>()!="WAL probe")throw new Exception("WAL data missing");
            }
            var safety=store.RestoreBackup(manual);if(store.Snapshot()["games"]!.AsArray().Any(g=>g!["notes"]?.GetValue<string>()=="WAL probe"))throw new Exception("Restore failed");
            if(!File.Exists(safety))throw new Exception("Safety backup missing");store.RestoreBackup(safety);if(!store.Snapshot()["games"]!.AsArray().Any(g=>g!["notes"]?.GetValue<string>()=="WAL probe"))throw new Exception("Safety restore failed");
            for(int i=0;i<7;i++)store.CreateBackup("auto");if(Directory.GetFiles(store.BackupFolder,"auto-*.db").Length!=5||!File.Exists(manual)||!File.Exists(safety))throw new Exception("Retention deleted wrong backup");
            var before=store.Snapshot().ToJsonString();var bad=Path.Combine(folder,"invalid.db");File.WriteAllText(bad,"not sqlite");try{store.RestoreBackup(bad);throw new Exception("Invalid backup accepted");}catch(SqliteException){}catch(InvalidDataException){}
            if(store.Snapshot().ToJsonString()!=before)throw new Exception("Invalid restore changed data");
            using(var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Pooling=false}.ToString())){db.Open();using var c=db.CreateCommand();c.CommandText="SELECT package FROM imports";if(JsonNode.Parse((string)c.ExecuteScalar()!)!.ToJsonString()!=JsonNode.Parse(File.ReadAllText(package))!.ToJsonString())throw new Exception("Raw package changed");}
            result=new(){["passed"]=true,["walIncluded"]=true,["restore"]=true,["safetyCopyRestorable"]=true,["fiveAutomaticCopies"]=true,["manualCopiesPreserved"]=true,["invalidRestorePreservesDatabase"]=true,["rawImportPreserved"]=true};
        }catch(Exception error){Environment.ExitCode=1;result["passed"]=false;result["error"]=error.ToString();}
        finally{SqliteConnection.ClearAllPools();if(Directory.Exists(folder))Directory.Delete(folder,true);CheckEvidence.Write("backup-test-result.json",result.ToJsonString());}
    }
}
