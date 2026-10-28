using System.Text.Json.Nodes;

namespace GaidenDesktop;
internal static class StoreChecks
{
    internal static void Run(string package)
    {
        var folder = Path.Combine(Path.GetTempPath(), "Gaiden-store-check-" + Guid.NewGuid());
        var path = Path.Combine(folder, "gaiden.db");
        try {
            var text = File.ReadAllText(package); var store = new LibraryStore(path);
            if (!store.Import(text) || store.Import(text)) throw new Exception("Idempotency failed");
            var snapshot = store.Snapshot();
            if (snapshot["games"]!.AsArray().Count != 14 || snapshot["sessions"]!.AsArray().Count != 248 || snapshot["daily"]!.AsArray().Count != 93) throw new Exception("Counts failed");
            if (snapshot["games"]!.AsArray().Sum(x=>x!["minutes"]!.GetValue<long>()) != 9374 || snapshot["sessions"]!.AsArray().Sum(x=>x!["activeMinutes"]!.GetValue<long>()) != 9371) throw new Exception("Totals changed");
            var incoming = snapshot["games"]!.DeepClone().AsArray(); incoming[0]!["notes"]="Проверка сохранения"; incoming[0]!["minutes"]=0;
            store.SaveGames(incoming);
            var reopened = new LibraryStore(path).Snapshot();
            if (reopened["games"]![0]!["notes"]!.GetValue<string>() != "Проверка сохранения" || reopened["games"]!.AsArray().Sum(x=>x!["minutes"]!.GetValue<long>()) != 9374) throw new Exception("Persistence/immutable counters failed");
            var invalid = incoming.DeepClone().AsArray(); invalid[1]!["status"]="invalid"; invalid[0]!["notes"]="Must roll back";
            try { store.SaveGames(invalid); throw new Exception("Invalid input accepted"); } catch (InvalidDataException) { }
            if (store.Snapshot()["games"]![0]!["notes"]!.GetValue<string>() != "Проверка сохранения") throw new Exception("Rollback failed");
            var edit=store.Snapshot()["games"]!.AsArray();var id=edit[0]!["id"]!.GetValue<string>();
            var originalLegacy=edit[0]!["legacy"]!.ToJsonString();edit[0]!["name"]="Новое название";store.SaveGames(edit);
            store.BindExe(id,"gaiden-editor-probe.exe");store.RemoveExe(id,"gaiden-editor-probe");store.SetCover(id,"data:image/png;base64,TEST");
            var edited=new LibraryStore(path).Snapshot()["games"]!.AsArray().Single(g=>g!["id"]!.GetValue<string>()==id)!;
            if(edited["name"]!.GetValue<string>()!="Новое название"||edited["legacy"]!.ToJsonString()!=originalLegacy||LibraryStore.Exes(edited).Contains("gaiden-editor-probe")||edited["customCover"]!.GetValue<string>()!="data:image/png;base64,TEST")throw new Exception("Editing persistence failed");
            store.SetCover(id,null);store.DeleteGame(id);
            var deleted=store.Snapshot();if(deleted["games"]!.AsArray().Count!=13||deleted["sessions"]!.AsArray().Count!=248)throw new Exception("Delete/history preservation failed");
            CheckEvidence.Write("store-test-result.json", "{\"passed\":true,\"games\":14,\"sessions\":248,\"daily\":93,\"idempotency\":true,\"persistence\":true,\"rollback\":true}");
        } catch (Exception error) { CheckEvidence.Write("store-test-result.json",new JsonObject{["passed"]=false,["error"]=error.ToString()}.ToJsonString()); Environment.ExitCode=1; }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
}
