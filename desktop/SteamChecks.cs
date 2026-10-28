using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
namespace GaidenDesktop;
internal static class SteamChecks
{
    private sealed class FakeHandler:HttpMessageHandler
    {
        internal List<Uri> Requests=new();internal HttpStatusCode Status=HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){Requests.Add(request.RequestUri!);string json=request.RequestUri!.AbsolutePath.Contains("ResolveVanityURL")?"{\"response\":{\"success\":1,\"steamid\":\"76561198000000000\"}}":"{\"response\":{\"game_count\":2,\"games\":[{\"appid\":10,\"name\":\"Existing\",\"playtime_forever\":90},{\"appid\":20,\"name\":\"New Steam game\",\"playtime_forever\":0}]}}";return Task.FromResult(new HttpResponseMessage(Status){Content=new StringContent(json,Encoding.UTF8,"application/json")});}
    }
    internal static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"Gaiden-steam-check-"+Guid.NewGuid());Directory.CreateDirectory(folder);var result=new JsonObject();
        void Assert(bool value,string message){if(!value)throw new Exception(message);}
        try {
            var handler=new FakeHandler();using var http=new HttpClient(handler);var client=new SteamClient(http);
            string key=new string('a',32);
            var preview=client.Fetch("https://steamcommunity.com/id/test_user",key).GetAwaiter().GetResult();
            Assert(handler.Requests.Count==2&&handler.Requests.All(x=>x.Host=="api.steampowered.com"),"Fixed Steam host/vanity resolution");
            Assert(SteamClient.ProfileToken("https://steamcommunity.com/profiles/76561198000000000")=="76561198000000000","Numeric profile");
            try{SteamClient.ProfileToken("https://example.com/id/test");throw new Exception("Foreign URL accepted");}catch(InvalidDataException){}
            try{SteamClient.ParseOwned(JsonNode.Parse("{\"response\":{}}")!,"76561198000000000");throw new Exception("Private profile accepted");}catch(InvalidDataException){}
            handler.Status=HttpStatusCode.Forbidden;
            try{client.Fetch("76561198000000000",key).GetAwaiter().GetResult();throw new Exception("Bad key accepted");}catch(InvalidDataException e){Assert(!e.Message.Contains(key),"Key leaked into error");}
            string path=Path.Combine(folder,"test.db");var store=new LibraryStore(path);string existingId=Guid.NewGuid().ToString();
            store.SaveGames(new JsonArray(new JsonObject{["id"]=existingId,["name"]="Existing",["platform"]="PC",["status"]="playing",["notes"]="Keep my note"}));
            using(var db=new SqliteConnection("Data Source="+path)){db.Open();using var c=db.CreateCommand();c.CommandText="UPDATE games SET payload=json_set(payload,'$.minutes',47,'$.executables',json('[\"existing\"]'))";c.ExecuteNonQuery();}
            var selection=new JsonArray(new JsonObject{["appId"]=10,["gameId"]=existingId},new JsonObject{["appId"]=20,["gameId"]=""});
            store.ImportSteam(preview,selection);var snapshot=store.Snapshot();Assert(snapshot["games"]!.AsArray().Count==2,"Import game count");
            var existing=snapshot["games"]!.AsArray().First(x=>x!["id"]!.GetValue<string>()==existingId)!;
            Assert(existing["minutes"]!.GetValue<int>()==47&&existing["notes"]!.GetValue<string>()=="Keep my note"&&existing["executables"]![0]!.GetValue<string>()=="existing","Local values changed");
            string newId=snapshot["games"]!.AsArray().First(x=>x!["id"]!.GetValue<string>()!=existingId)!["id"]!.GetValue<string>();selection[1]!["gameId"]=newId;
            store.ImportSteam(preview,selection);Assert(store.Snapshot()["games"]!.AsArray().Count==2,"Repeated import duplicated games");
            preview["games"]![0]!["minutes"]=120;store.ImportSteam(preview,selection);
            var updated=store.Snapshot()["games"]!.AsArray().First(x=>x!["id"]!.GetValue<string>()==existingId)!;Assert(updated["steam"]!["minutes"]!.GetValue<int>()==120&&updated["minutes"]!.GetValue<int>()==47,"Steam time added instead of replaced");
            string before=store.Snapshot().ToJsonString();
            preview["games"]![0]!["minutes"]=999;
            var invalid=new JsonArray(new JsonObject{["appId"]=10,["gameId"]=existingId},new JsonObject{["appId"]=999,["gameId"]=""});
            try{store.ImportSteam(preview,invalid);throw new Exception("Unknown app accepted");}catch(InvalidDataException){}
            Assert(store.Snapshot().ToJsonString()==before,"Failed import was not rolled back");
            Assert(store.Snapshot()["sessions"]!.AsArray().Count==0,"Fabricated sessions");
            Assert(File.Exists(store.CreateBackup("before-steam")),"Steam safety backup");
            SqliteConnection.ClearAllPools();
            Assert(!File.ReadAllText(path).Contains(key),"API key persisted");
            result=new JsonObject{["passed"]=true,["vanityAndNumericProfile"]=true,["privateProfileRejected"]=true,["foreignHostRejected"]=true,["keyNotInErrorsOrDatabase"]=true,["localDataPreserved"]=true,["repeatImportNoDuplicates"]=true,["hoursReplacedNotAdded"]=true,["failedBatchRolledBack"]=true,["noInventedSessions"]=true,["network"]="mock Steam responses"};
        }catch(Exception error){result["passed"]=false;result["error"]=error.ToString();Environment.ExitCode=1;}
        finally{SqliteConnection.ClearAllPools();CheckEvidence.Write("steam-test-result.json",result.ToJsonString());}
    }
}
