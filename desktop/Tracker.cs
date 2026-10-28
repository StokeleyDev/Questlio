using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GaidenDesktop;

internal sealed class ActiveSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GameId { get; set; } = "";
    public string GameName { get; set; } = "";
    public bool StartCounted { get; set; }
    public string? PcId { get; set; }
    public string? PcName { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastObservedAt { get; set; } = DateTimeOffset.UtcNow;
    public double ActiveSeconds { get; set; }
    public double IdleSeconds { get; set; }
    public double UnobservedSeconds { get; set; }
    public void Advance(double seconds, bool idle)
    {
        if (seconds < 0 || !double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (seconds > 15) { UnobservedSeconds += seconds; return; }
        if (idle) IdleSeconds += seconds; else ActiveSeconds += seconds;
    }
}

internal sealed partial class LibraryStore
{
    internal void CreateWithExe(string name,string path)
    {
        name=name.Trim();var exe=Path.GetFileNameWithoutExtension(path);
        if(name.Length is <1 or >80 || string.IsNullOrWhiteSpace(exe)||exe.Contains(','))throw new InvalidDataException("Проверьте название и EXE");
        using var db=Open();using var tx=db.BeginTransaction();using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT payload FROM games";
        using(var r=c.ExecuteReader())while(r.Read())if(Exes(JsonNode.Parse(r.GetString(0))!).Contains(exe,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("Этот EXE уже связан с другой игрой");
        var id=Guid.NewGuid().ToString();var game=new JsonObject{["id"]=id,["name"]=name,["platform"]="PC",["minutes"]=0,["sessions"]=0,["status"]="backlog",["notes"]="",["art"]="sand",["title"]=name,["genre"]="Библиотека",["executables"]=new JsonArray(JsonValue.Create(exe))};
        game["createdAt"]=DateTimeOffset.UtcNow.ToString("O");
        Insert(db,tx,"games",id,game.ToJsonString());tx.Commit();RememberExecutable(id,path);CaptureExeIcon(id,path);
    }
    internal void BindExe(string id, string exe)
    {
        var name = Path.GetFileNameWithoutExtension(exe).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Contains(',')) throw new InvalidDataException("Некорректное имя EXE");
        using var db = Open(); using var tx = db.BeginTransaction();
        using var c = db.CreateCommand(); c.Transaction = tx; c.CommandText = "SELECT id,payload FROM games";
        var entries = new List<(string Id, JsonObject Game)>();
        using (var r = c.ExecuteReader()) while (r.Read()) entries.Add((r.GetString(0),JsonNode.Parse(r.GetString(1))!.AsObject()));
        if (entries.Any(e=>e.Id!=id && Exes(e.Game).Contains(name,StringComparer.OrdinalIgnoreCase))) throw new InvalidOperationException("Этот EXE уже связан с другой игрой");
        var game=entries.Single(e=>e.Id==id).Game;
        if (game["platform"]?.GetValue<string>() != "PC") throw new InvalidOperationException("Этот этап трекера поддерживает только PC-игры");
        game["executables"] = new JsonArray(Exes(game).Append(name).Distinct(StringComparer.OrdinalIgnoreCase).Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray());
        c.CommandText="UPDATE games SET payload=$payload WHERE id=$id";c.Parameters.AddWithValue("$payload",game.ToJsonString());c.Parameters.AddWithValue("$id",id);c.ExecuteNonQuery();tx.Commit();RememberExecutable(id,exe);CaptureExeIcon(id,exe);
    }
    internal static string[] Exes(JsonNode g) => g["deleted"]?.GetValue<bool>()==true ? [] : g["executables"] is JsonArray a ? a.Select(x=>x!.GetValue<string>()).ToArray()
        : (g["legacy"]?["executableName"]?.GetValue<string>()??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(Path.GetFileNameWithoutExtension).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x!).ToArray();
    internal ActiveSession? Pending()
    {
        using var db=Open();using var c=db.CreateCommand();c.CommandText="SELECT payload FROM active_session WHERE id=1";
        return c.ExecuteScalar() is string text ? JsonSerializer.Deserialize<ActiveSession>(text) : null;
    }
    internal void Checkpoint(ActiveSession session)
    {
        using var db=Open();using var c=db.CreateCommand();c.CommandText="INSERT INTO active_session VALUES(1,$payload) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload";c.Parameters.AddWithValue("$payload",JsonSerializer.Serialize(session));c.ExecuteNonQuery();
    }
    internal (JsonArray Games,JsonArray Emulators) TrackerState()
    {
        using var db=Open();
        JsonArray Read(string table){using var c=db.CreateCommand();c.CommandText=$"SELECT payload FROM {table} ORDER BY id";using var reader=c.ExecuteReader();var result=new JsonArray();while(reader.Read()){var item=JsonNode.Parse(reader.GetString(0));if(table=="games"&&item?["deleted"]?.GetValue<bool>()==true)continue;result.Add(item);}return result;}
        return(Read("games"),Read("emulator_profiles"));
    }
    internal void Finish(ActiveSession session, string reason)
    {
        using var db=Open();using var tx=db.BeginTransaction();using var c=db.CreateCommand();c.Transaction=tx;
        c.CommandText="SELECT COUNT(*) FROM sessions WHERE id=$id";c.Parameters.AddWithValue("$id",session.Id);
        if ((long)c.ExecuteScalar()! == 0) {
            c.CommandText="SELECT payload FROM games WHERE id=$game";c.Parameters.AddWithValue("$game",session.GameId);
            var game=JsonNode.Parse((string?)c.ExecuteScalar()??throw new InvalidDataException("Игра активной сессии не найдена"))!.AsObject();
            double prevTracked=game["trackedActiveSeconds"]?.GetValue<double>()??0;
            double nextTracked=prevTracked+session.ActiveSeconds;
            long deltaMinutes=(long)(nextTracked/60)-(long)(prevTracked/60);
            long minutes=(long)(session.ActiveSeconds/60);
            game["minutes"]=(game["minutes"]?.GetValue<long>()??0)+deltaMinutes;
            if(!session.StartCounted)game["sessions"]=(game["sessions"]?.GetValue<long>()??0)+1;
            game["lastPlayedAt"]=session.LastObservedAt.ToString("O");
            game["trackedActiveSeconds"]=nextTracked;
            game["trackedIdleSeconds"]=(game["trackedIdleSeconds"]?.GetValue<double>()??0)+session.IdleSeconds;
            c.CommandText="UPDATE games SET payload=$payload WHERE id=$game";c.Parameters.AddWithValue("$payload",game.ToJsonString());c.ExecuteNonQuery();
            var payload=new JsonObject { ["id"]=session.Id,["gameId"]=session.GameId,["legacyGameName"]=session.GameName,["startedAt"]=session.StartedAt.ToString("O"),["endedAt"]=session.LastObservedAt.ToString("O"),["activeMinutes"]=minutes,["activeSeconds"]=session.ActiveSeconds,["idleSeconds"]=session.IdleSeconds,["unobservedSeconds"]=session.UnobservedSeconds,["endReason"]=reason,["source"]="gaiden-tracker" };
            Insert(db,tx,"sessions",session.Id,payload.ToJsonString());
            if(session.PcId is not null){
                payload["pcId"]=session.PcId;payload["pcName"]=session.PcName;
                using var pcCommand=db.CreateCommand();pcCommand.Transaction=tx;pcCommand.CommandText="SELECT payload FROM pcs WHERE id=$pc";pcCommand.Parameters.AddWithValue("$pc",session.PcId);
                if(pcCommand.ExecuteScalar() is string pcText){var pc=JsonNode.Parse(pcText)!.AsObject();pc["trackedSeconds"]=(pc["trackedSeconds"]?.GetValue<double>()??0)+session.ActiveSeconds;
                    pcCommand.CommandText="UPDATE pcs SET payload=$payload WHERE id=$pc";pcCommand.Parameters.AddWithValue("$payload",pc.ToJsonString());pcCommand.ExecuteNonQuery();}
                c.CommandText="UPDATE sessions SET payload=$session WHERE id=$id";c.Parameters.AddWithValue("$session",payload.ToJsonString());c.ExecuteNonQuery();
            }
        }
        c.CommandText="DELETE FROM active_session WHERE id=1";c.ExecuteNonQuery();tx.Commit();
    }
}

internal sealed class GameTracker
{
    [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size; public uint Tick; }
    [DllImport("user32.dll", SetLastError=true)] private static extern bool GetLastInputInfo(ref LastInput info);
    private readonly LibraryStore store;
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private double previous;
    private bool resumeAfterSuspend;
    private string? emulatorIssue;
    internal bool Enabled { get; private set; } = true;
    internal ActiveSession? Current { get; private set; }
    internal string Status => !Enabled ? "Трекер остановлен" : Current is null ? emulatorIssue??"Ожидание игры" : "Игра: "+Current.GameName;
    internal GameTracker(LibraryStore store) { this.store=store; if(store.Pending() is { } pending) store.Finish(pending,"recovered-after-interruption"); }
    private static bool Running(string exe)
    {
        var processes=Process.GetProcessesByName(exe);
        try { return processes.Length>0; } finally { foreach(var p in processes)p.Dispose(); }
    }
    private bool LearnMatchedPath(JsonNode game)=>store.LearnLaunchPath(game,()=>{
        foreach(string exe in LibraryStore.Exes(game)){
            var processes=Process.GetProcessesByName(exe);
            try{foreach(var process in processes){try{string? path=process.MainModule?.FileName;if(path is not null&&File.Exists(path)&&string.Equals(Path.GetFileNameWithoutExtension(path),exe,StringComparison.OrdinalIgnoreCase))return path;}catch(Exception error)when(error is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException or NotSupportedException){}}}
            finally{foreach(var process in processes)process.Dispose();}
        }return null;
    });
    internal bool Poll()
    {
        double now=clock.Elapsed.TotalSeconds; double elapsed=now-previous;previous=now;
        if(!Enabled)return false;
        var (gamesArray,emulatorsArray)=store.TrackerState();var candidates=gamesArray.OrderByDescending(g=>Latest(g!)).ThenBy(g=>g!["id"]!.GetValue<string>(),StringComparer.Ordinal).ToArray();
        var emulated=EmulatorResolver.Running(emulatorsArray,out emulatorIssue);
        bool EmulatorAlive(JsonNode game)=>emulated.Any(m=>game["romIdentity"]?.GetValue<string>()==m.ProfileId+":"+m.Rom.Replace('/','\\').ToLowerInvariant());
        if(Current is not null) {
            var game=candidates.FirstOrDefault(g=>g!["id"]!.GetValue<string>()==Current.GameId);
            if(game is null || !(game["romIdentity"] is not null?EmulatorAlive(game):LibraryStore.Exes(game).Any(Running))) { Current.Advance(elapsed,ReadIdle());Current.LastObservedAt=DateTimeOffset.UtcNow;Stop("process-closed",false);return true; }
            Current.GameName=game["name"]!.GetValue<string>();
            Current.Advance(elapsed,ReadIdle());
            Current.LastObservedAt=DateTimeOffset.UtcNow;
            store.Checkpoint(Current);return LearnMatchedPath(game);
        } else {
            JsonNode? game;
            if(emulated.FirstOrDefault() is { } match){var id=store.EmulatorGame(match);game=store.TrackerState().Games.Single(g=>g!["id"]!.GetValue<string>()==id);}
            else game=candidates.FirstOrDefault(g=>g!["platform"]?.GetValue<string>()=="PC"&&LibraryStore.Exes(g).Any(Running));
            if(game is not null) {LearnMatchedPath(game);Current=new ActiveSession {GameId=game["id"]!.GetValue<string>(),GameName=game["name"]!.GetValue<string>()};store.ConfirmSessionStart(Current);return true; }
        }
        return false;
    }
    internal void Stop(string reason="stopped",bool disable=true) { ObserveBoundary();if(Current is not null){store.Finish(Current,reason);Current=null;} if(disable)Enabled=false;previous=clock.Elapsed.TotalSeconds; }
    private static DateTimeOffset Latest(JsonNode game)=>new[]{game["lastPlayedAt"],game["legacy"]?["lastPlayedAt"],game["steam"]?["lastPlayedAt"]}.Select(x=>DateTimeOffset.TryParse(x?.GetValue<string>(),out var d)?d:DateTimeOffset.MinValue).Max();
    private bool ReadIdle()
    {
        var options=store.TrackingSettings();if(!options.ExcludeIdle)return false;
        var input=new LastInput{Size=(uint)Marshal.SizeOf<LastInput>()};
        if(!GetLastInputInfo(ref input))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return options.IsIdle(unchecked((uint)Environment.TickCount-input.Tick));
    }
    internal void ObserveBoundary()
    {
        double now=clock.Elapsed.TotalSeconds;double seconds=now-previous;
        if(Current is not null){Current.Advance(seconds,ReadIdle());Current.LastObservedAt=DateTimeOffset.UtcNow;store.Checkpoint(Current);}
        previous=now;
    }
    internal void Start() { Enabled=true;previous=clock.Elapsed.TotalSeconds; }
    internal void Suspend() { resumeAfterSuspend=Enabled;Stop("suspend"); }
    internal void Resume() { if(resumeAfterSuspend)Start();resumeAfterSuspend=false;previous=clock.Elapsed.TotalSeconds; }
}
