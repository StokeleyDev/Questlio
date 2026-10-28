using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace GaidenDesktop;

internal sealed class SteamClient
{
    private readonly HttpClient http;
    internal SteamClient(HttpClient? client=null) { http=client??new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(20),MaxResponseContentBufferSize=8*1024*1024}; }
    internal static string ProfileToken(string profile)
    {
        profile=profile.Trim();
        if(Regex.IsMatch(profile,@"^\d{17}$"))return profile;
        if(Uri.TryCreate(profile,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host.Equals("steamcommunity.com",StringComparison.OrdinalIgnoreCase)&&uri.IsDefaultPort&&string.IsNullOrEmpty(uri.UserInfo)){
            var parts=uri.AbsolutePath.Trim('/').Split('/');
            if(parts.Length==2&&parts[0]=="profiles"&&Regex.IsMatch(parts[1],@"^\d{17}$"))return parts[1];
            if(parts.Length==2&&parts[0]=="id"&&Regex.IsMatch(parts[1],@"^[A-Za-z0-9_-]{1,100}$"))return parts[1];
        }
        throw new InvalidDataException("Введите SteamID64 или ссылку https://steamcommunity.com/id/имя либо /profiles/SteamID64.");
    }
    private async Task<JsonNode> Request(string route,string query)
    {
        try {
            using var response=await http.GetAsync("https://api.steampowered.com/"+route+"?"+query);
            if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)throw new InvalidDataException("Steam отклонил API-ключ. Проверьте ключ.");
            if(response.StatusCode==HttpStatusCode.TooManyRequests)throw new InvalidDataException("Слишком много запросов. Попробуйте позже.");
            if(!response.IsSuccessStatusCode)throw new InvalidDataException("Steam временно недоступен. Попробуйте позже.");
            return JsonNode.Parse(await response.Content.ReadAsStringAsync())??throw new InvalidDataException("Steam вернул пустой ответ.");
        }catch(TaskCanceledException){throw new InvalidDataException("Steam не ответил вовремя. Повторите запрос.");}
        catch(HttpRequestException){throw new InvalidDataException("Не удалось подключиться к Steam. Проверьте интернет.");}
        catch(System.Text.Json.JsonException){throw new InvalidDataException("Не удалось прочитать ответ Steam.");}
    }
    internal async Task<JsonObject> Fetch(string profile,string key)
    {
        var token=ProfileToken(profile);key=key.Trim();
        if(!Regex.IsMatch(key,@"^[a-fA-F0-9]{32}$"))throw new InvalidDataException("API-ключ Steam должен содержать 32 шестнадцатеричных символа.");
        string steamId=token;
        if(!Regex.IsMatch(token,@"^\d{17}$")) {
            var resolved=await Request("ISteamUser/ResolveVanityURL/v1/","key="+key+"&vanityurl="+Uri.EscapeDataString(token));
            steamId=resolved["response"]?["steamid"]?.GetValue<string>()??"";
            if(!Regex.IsMatch(steamId,@"^\d{17}$"))throw new InvalidDataException("Профиль Steam не найден.");
        }
        var data=await Request("IPlayerService/GetOwnedGames/v1/","key="+key+"&steamid="+steamId+"&include_appinfo=true&include_played_free_games=true");
        return ParseOwned(data,steamId);
    }
    internal static JsonObject ParseOwned(JsonNode data,string steamId)
    {
        var response=data["response"] as JsonObject??throw new InvalidDataException("Не удалось прочитать библиотеку Steam.");
        if(response["game_count"] is null)throw new InvalidDataException("Библиотека недоступна. Откройте «Доступ к игровой информации» и показ общего времени в настройках приватности Steam.");
        var count=response["game_count"]!.GetValue<int>();
        if(count<0||count>10000)throw new InvalidDataException("Слишком большая библиотека Steam.");
        var games=response["games"] as JsonArray??new JsonArray();
        if(games.Count!=count)throw new InvalidDataException("Steam вернул неполную библиотеку. Повторите запрос.");
        var result=new JsonArray();var ids=new HashSet<int>();
        foreach(var item in games){
            int id=item?["appid"]?.GetValue<int>()??0;
            long minutes=item?["playtime_forever"]?.GetValue<long>()??0;
            string name=item?["name"]?.GetValue<string>()??("Steam #"+id);
            if(id<=0||!ids.Add(id)||minutes<0||minutes>1000000000||name.Length>500)throw new InvalidDataException("Некорректные данные игры Steam.");
            long lastPlayed=item?["rtime_last_played"]?.GetValue<long>()??0;
            var entry=new JsonObject{["appId"]=id,["name"]=name,["minutes"]=minutes};
            if(lastPlayed>0){try{entry["lastPlayedAt"]=DateTimeOffset.FromUnixTimeSeconds(lastPlayed).ToString("O");}catch(ArgumentOutOfRangeException){throw new InvalidDataException("Некорректная дата последнего запуска Steam.");}}
            result.Add(entry);
        }
        return new JsonObject{["steamId"]=steamId,["games"]=result};
    }
}

internal sealed partial class LibraryStore
{
    internal int ImportSteam(JsonObject preview,JsonArray selection)
    {
        if(selection.Count is <1 or >10000)throw new InvalidDataException("Выберите игры для импорта.");
        var source=preview["games"]!.AsArray().ToDictionary(x=>x!["appId"]!.GetValue<int>(),x=>x!.AsObject());
        string account=preview["steamId"]!.GetValue<string>();
        using var db=Open();using var tx=db.BeginTransaction();
        using var command=db.CreateCommand();command.Transaction=tx;command.CommandText="SELECT payload FROM games";
        var library=new List<JsonObject>();using(var reader=command.ExecuteReader())while(reader.Read())library.Add(JsonNode.Parse(reader.GetString(0))!.AsObject());
        var usedApps=new HashSet<int>();var usedGames=new HashSet<string>();
        foreach(var picked in selection){
            int appId=picked!["appId"]!.GetValue<int>();
            if(!source.TryGetValue(appId,out var incoming)||!usedApps.Add(appId))throw new InvalidDataException("Повторяющаяся или неизвестная игра Steam.");
            var linked=library.FirstOrDefault(g=>g["deleted"]?.GetValue<bool>()!=true&&g["steam"]?["account"]?.GetValue<string>()==account&&g["steam"]?["appId"]?.GetValue<int>()==appId);
            string requested=picked["gameId"]?.GetValue<string>()??"";
            if(linked is not null&&requested!=linked["id"]!.GetValue<string>())throw new InvalidDataException("Игра уже связана со Steam. Обновите предпросмотр.");
            JsonObject game;
            if(requested.Length>0){
                game=library.FirstOrDefault(g=>g["id"]?.GetValue<string>()==requested&&g["deleted"]?.GetValue<bool>()!=true)??throw new InvalidDataException("Игра для сопоставления не найдена.");
                if(game["platform"]?.GetValue<string>()!="PC")throw new InvalidDataException("Сопоставьте игру с платформой PC.");
                if(game["steam"] is JsonObject old&&(old["account"]?.GetValue<string>()!=account||old["appId"]?.GetValue<int>()!=appId))throw new InvalidDataException("Эта игра уже связана с другой записью Steam.");
            }else{
                string name=incoming["name"]!.GetValue<string>();if(name.Length>80)name=name[..80];
                game=new JsonObject{["id"]=Guid.NewGuid().ToString(),["name"]=name,["title"]=name,["platform"]="PC",["minutes"]=0,["sessions"]=0,["status"]="backlog",["notes"]="",["genre"]="Библиотека",["createdAt"]=DateTimeOffset.UtcNow.ToString("O"),["art"]="blue"};library.Add(game);
            }
            string id=game["id"]!.GetValue<string>();if(!usedGames.Add(id))throw new InvalidDataException("Две игры Steam нельзя связать с одной игрой Questlio.");
            game["steam"]=new JsonObject{["account"]=account,["appId"]=appId,["minutes"]=incoming["minutes"]!.DeepClone(),["name"]=incoming["name"]!.DeepClone(),["updatedAt"]=DateTimeOffset.UtcNow.ToString("O")};
            if(incoming["lastPlayedAt"] is not null)game["steam"]!["lastPlayedAt"]=incoming["lastPlayedAt"]!.DeepClone();
            command.Parameters.Clear();command.CommandText="INSERT INTO games(id,payload) VALUES($id,$payload) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload";command.Parameters.AddWithValue("$id",id);command.Parameters.AddWithValue("$payload",game.ToJsonString());command.ExecuteNonQuery();
        }
        tx.Commit();return selection.Count;
    }
}
