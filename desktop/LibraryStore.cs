using Microsoft.Data.Sqlite;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

namespace GaidenDesktop;

internal sealed partial class LibraryStore
{
    private readonly string path;
    internal LibraryStore(string path)
    {
        this.path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS imports(hash TEXT PRIMARY KEY, package TEXT NOT NULL, imported_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS games(id TEXT PRIMARY KEY, payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY, payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS daily_records(id INTEGER PRIMARY KEY, payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS active_session(id INTEGER PRIMARY KEY CHECK(id=1), payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS emulator_profiles(id TEXT PRIMARY KEY, payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS pcs(id TEXT PRIMARY KEY, payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            PRAGMA user_version=1;
            """;
        cmd.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()); db.Open(); return db; }
    internal static JsonObject ParsePackage(string text)
    {
        var p = JsonNode.Parse(text)?.AsObject() ?? throw new InvalidDataException("Пустой пакет");
        if (p["format"]?.GetValue<string>() != "gaiden-legacy-import" || p["formatVersion"]?.GetValue<int>() != 1)
            throw new InvalidDataException("Неподдерживаемый формат импорта");
        var games = p["normalized"]!["games"]!.AsArray();
        var sessions = p["normalized"]!["sessions"]!.AsArray();
        _ = p["rawTables"]!["daily_playtime"]!.AsArray();
        if (games.Select(g => g!["id"]!.GetValue<string>()).Distinct().Count() != games.Count ||
            sessions.Select(s => s!["id"]!.GetValue<string>()).Distinct().Count() != sessions.Count)
            throw new InvalidDataException("Повторяющиеся ID в пакете");
        foreach (var g in games)
            if (string.IsNullOrWhiteSpace(g!["name"]?.GetValue<string>()) || g["playMinutes"]!.GetValue<long>() < 0)
                throw new InvalidDataException("Некорректная игра");
        return p;
    }
    internal bool Import(string text)
    {
        var p = ParsePackage(text);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        using var db = Open();
        using var tx = db.BeginTransaction();
        using var query = db.CreateCommand(); query.Transaction = tx;
        query.CommandText = "SELECT COUNT(*) FROM imports WHERE hash=$hash"; query.Parameters.AddWithValue("$hash", hash);
        if ((long)query.ExecuteScalar()! > 0) return false;
        query.CommandText = "SELECT COUNT(*) FROM imports"; query.Parameters.Clear();
        if ((long)query.ExecuteScalar()! > 0) throw new InvalidOperationException("Библиотека уже импортирована. Замена другим пакетом пока не поддерживается.");
        foreach (var g in p["normalized"]!["games"]!.AsArray())
        {
            var game = new JsonObject {
                ["id"] = g!["id"]!.DeepClone(), ["name"] = g["name"]!.DeepClone(),
                ["platform"] = g["platform"]?.DeepClone() ?? JsonValue.Create("PC"),
                ["minutes"] = g["playMinutes"]!.DeepClone(), ["sessions"] = g["legacySessionCount"]?.DeepClone() ?? JsonValue.Create(0),
                ["status"] = g["status"]?.DeepClone() ?? JsonValue.Create("backlog"),
                ["title"] = g["name"]!.DeepClone(), ["genre"] = "Библиотека", ["art"] = "blue", ["notes"] = "",
                ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"), ["legacy"] = g.DeepClone()
            };
            Insert(db, tx, "games", g["id"]!.GetValue<string>(), game.ToJsonString());
        }
        foreach (var s in p["normalized"]!["sessions"]!.AsArray()) Insert(db, tx, "sessions", s!["id"]!.GetValue<string>(), s.ToJsonString());
        int i = 0;
        foreach (var d in p["rawTables"]!["daily_playtime"]!.AsArray()) Insert(db, tx, "daily_records", (++i).ToString(), d!.ToJsonString());
        query.CommandText = "INSERT INTO imports VALUES($hash,$package,$date)";
        query.Parameters.AddWithValue("$hash", hash); query.Parameters.AddWithValue("$package", text); query.Parameters.AddWithValue("$date", DateTimeOffset.UtcNow.ToString("O")); query.ExecuteNonQuery();
        tx.Commit(); return true;
    }
    private static void Insert(SqliteConnection db, SqliteTransaction tx, string table, string id, string payload)
    {
        using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = $"INSERT INTO {table}(id,payload) VALUES($id,$payload)";
        cmd.Parameters.AddWithValue("$id", id); cmd.Parameters.AddWithValue("$payload", payload); cmd.ExecuteNonQuery();
    }
    internal JsonObject Snapshot()
    {
        using var db = Open();
        JsonArray Read(string table) { using var c = db.CreateCommand(); c.CommandText = $"SELECT payload FROM {table} ORDER BY id"; using var r = c.ExecuteReader(); var a = new JsonArray(); while(r.Read()) {var item=JsonNode.Parse(r.GetString(0));if(table=="games"&&item?["deleted"]?.GetValue<bool>()==true)continue;if(table=="games"&&item is not null){item["launchAvailable"]=LaunchPath(item) is not null||SteamLaunchId(item) is not null;item["revealAvailable"]=LaunchPath(item) is not null;}a.Add(item);} return a; }
        return new JsonObject { ["games"] = Read("games"), ["sessions"] = Read("sessions"), ["daily"] = Read("daily_records"),["emulators"]=Read("emulator_profiles") };
    }
    internal void SaveGames(JsonArray incoming)
    {
        if (incoming.Count > 10000) throw new InvalidDataException("Слишком много игр");
        using var db = Open(); using var tx = db.BeginTransaction();
        foreach (var item in incoming)
        {
            var id = item!["id"]!.GetValue<string>();
            if (!Guid.TryParse(id, out _)) throw new InvalidDataException("Некорректный ID");
            var status = item["status"]!.GetValue<string>();
            if (!new[] { "playing", "finished", "backlog", "hold", "dropped", "forever" }.Contains(status)) throw new InvalidDataException("Некорректный статус");
            var notes = item["notes"]?.GetValue<string>() ?? "";
            if (notes.Length > 3000) throw new InvalidDataException("Слишком длинная заметка");
            using var c = db.CreateCommand(); c.Transaction = tx; c.CommandText = "SELECT payload FROM games WHERE id=$id"; c.Parameters.AddWithValue("$id", id);
            var old = c.ExecuteScalar() as string;
            JsonObject game;
            if (old is not null) { game = JsonNode.Parse(old)!.AsObject();if(game["deleted"]?.GetValue<bool>()==true)throw new InvalidOperationException("Игра удалена из библиотеки"); }
            else {
                var name = item["name"]!.GetValue<string>().Trim();
                var platform = item["platform"]!.GetValue<string>();
                if (name.Length is < 1 or > 80 || platform.Length > 100) throw new InvalidDataException("Некорректное название");
                game = new JsonObject { ["id"]=id,["name"]=name,["platform"]=platform,["minutes"]=0,["sessions"]=0,["title"]=name,["art"]="sand",["genre"]="Библиотека",["createdAt"]=DateTimeOffset.UtcNow.ToString("O") };
            }
            if(old is not null&&item["statusEdited"]?.GetValue<bool>()==false)status=game["status"]!.GetValue<string>();
            var editedName=item["name"]!.GetValue<string>().Trim();
            if(editedName.Length is <1 or >80)throw new InvalidDataException("Название должно содержать от 1 до 80 символов");
            game["name"]=editedName;game["title"]=editedName;
            if(old is not null&&(game["status"]?.GetValue<string>()!=status||item["statusEdited"]?.GetValue<bool>()==true)){game["autoStatus"]=false;game.Remove("autoStatusSession");}
            game["status"] = status; game["notes"] = notes;
            c.CommandText = "INSERT INTO games VALUES($id,$payload) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload";
            c.Parameters.AddWithValue("$payload", game.ToJsonString()); c.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
