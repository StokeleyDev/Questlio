using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GaidenDesktop;
internal sealed record EmulatorMatch(string ProfileId,string Platform,string Rom,string Name);
internal static class EmulatorResolver
{
    [DllImport("shell32.dll",SetLastError=true,CharSet=CharSet.Unicode)]private static extern IntPtr CommandLineToArgvW(string command,out int count);
    [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr value);
    internal static string[] Arguments(string command)
    {
        var pointer=CommandLineToArgvW(command,out int count);if(pointer==IntPtr.Zero)throw new InvalidDataException("Не удалось разобрать командную строку");
        try{return Enumerable.Range(0,count).Select(i=>Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer,i*IntPtr.Size))??"").ToArray();}finally{LocalFree(pointer);}
    }
    internal static EmulatorMatch? Resolve(string exe,string command,IEnumerable<JsonNode> profiles)
    {
        var args=Arguments(command);var matches=new List<EmulatorMatch>();
        var selected=profiles.Where(p=>string.Equals(p["exe"]!.GetValue<string>(),exe,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(selected.Any(p=>p["automatic"]?.GetValue<bool>()==true))selected=selected.Where(p=>p["automatic"]?.GetValue<bool>()==true).ToArray();
        foreach(var p in selected) {
            bool automatic=p["automatic"]?.GetValue<bool>()==true;
            var core=p["core"]?.GetValue<string>()??"";
            bool retro=exe.Contains("retroarch",StringComparison.OrdinalIgnoreCase);
            string? autoCore=null;
            if(automatic&&retro){for(int i=1;i<args.Length;i++){if((args[i]=="-L"||args[i]=="--libretro")&&i+1<args.Length)autoCore=args[i+1];if(args[i].StartsWith("--libretro="))autoCore=args[i][11..];}if(string.IsNullOrWhiteSpace(autoCore))continue;}
            if(retro&&core.Length==0&&!automatic)continue;
            if(core.Length>0) {
                string? actual=null;
                for(int i=1;i<args.Length;i++) {if((args[i]=="-L"||args[i]=="--libretro")&&i+1<args.Length)actual=args[i+1];if(args[i].StartsWith("--libretro="))actual=args[i][11..];}
                if(!string.Equals(Path.GetFileName(actual),core,StringComparison.OrdinalIgnoreCase))continue;
            }
            var extensions=p["extensions"]!.AsArray().Select(x=>x!.GetValue<string>()).ToArray();
            var roms=args.Skip(1).Where(a=>!a.StartsWith('-')&&extensions.Contains(Path.GetExtension(a).TrimStart('.'),StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(roms.Length!=1)continue;
            var name=Regex.Replace(Path.GetFileNameWithoutExtension(roms[0]),@"\([^)]*\)|\[[^\]]*\]","").Trim();if(name.Length==0)continue;
            var platform=automatic?AutoPlatform(exe,autoCore):p["name"]!.GetValue<string>();
            matches.Add(new(p["id"]!.GetValue<string>(),platform,roms[0],name));
        }
        return matches.Count==1?matches[0]:null;
    }
    internal static string AutoPlatform(string exe,string? core)
    {
        string value=(exe+" "+core).ToLowerInvariant();
        foreach(var (token,platform) in new[]{("pcsx2","PlayStation 2"),("ppsspp","PSP"),("duckstation","PlayStation"),("dolphin","GameCube / Wii"),("rpcs3","PlayStation 3"),("snes9x","SNES"),("bsnes","SNES"),("fceumm","NES"),("nestopia","NES"),("mgba","Game Boy Advance"),("gambatte","Game Boy / Color"),("mupen","Nintendo 64"),("parallel_n64","Nintendo 64"),("genesis_plus","Mega Drive / Sega CD"),("melonds","Nintendo DS"),("desmume","Nintendo DS"),("flycast","Dreamcast")})if(value.Contains(token))return platform;
        return exe.Contains("retroarch",StringComparison.OrdinalIgnoreCase)?"RetroArch":"Эмуляция";
    }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(int Pid,long Started),string> CommandCache=new();
    private static int commandQueries;
    internal static int CommandQueryCount=>System.Threading.Volatile.Read(ref commandQueries);
    internal static int CachedCommandCount=>CommandCache.Count;
    internal static List<EmulatorMatch> Running(JsonArray profiles,out string? issue)
    {
        issue=null;var result=new List<EmulatorMatch>();var all=profiles.Select(p=>p!).ToArray();
        if(all.Length==0)return result;
        var liveKeys=new HashSet<(int Pid,long Started)>();
        foreach(var exe in all.Select(p=>p["exe"]!.GetValue<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
        foreach(var process in Process.GetProcessesByName(exe))using(process)try {
            long started=0;try{started=process.StartTime.ToUniversalTime().Ticks;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
            var key=(process.Id,started);liveKeys.Add(key);
            if(!CommandCache.TryGetValue(key,out var command)){
                System.Threading.Interlocked.Increment(ref commandQueries);
                using var query=new ManagementObjectSearcher(new ManagementScope("root\\cimv2"),new ObjectQuery($"SELECT CommandLine FROM Win32_Process WHERE ProcessId={process.Id}"),new System.Management.EnumerationOptions{Timeout=TimeSpan.FromSeconds(2),ReturnImmediately=false});
                using var rows=query.Get();foreach(ManagementObject row in rows)using(row)command=row["CommandLine"] as string;
                if(!string.IsNullOrEmpty(command)&&started!=0)CommandCache[key]=command;
            }
            if(string.IsNullOrEmpty(command)){issue="Нет доступа к командной строке эмулятора";continue;}
            var match=Resolve(exe,command,all);if(match is not null)result.Add(match);else issue="Эмулятор открыт: ROM или ядро не определены";
        }catch(Exception error)when(error is ManagementException or System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException or TimeoutException or System.Runtime.InteropServices.COMException){issue="Не удалось прочитать процесс эмулятора";}
        foreach(var key in CommandCache.Keys)if(!liveKeys.Contains(key))CommandCache.TryRemove(key,out _);
        if(CommandCache.Count>256)CommandCache.Clear();return result;
    }
}
internal sealed partial class LibraryStore
{
    internal void AddEmulator(string name,string exe,string extensions,string core,bool automatic=false)
    {
        name=name.Trim();exe=Path.GetFileNameWithoutExtension(exe.Trim());core=Path.GetFileName(core.Trim());
        var ext=extensions.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(e=>e.TrimStart('.').ToLowerInvariant()).Distinct().ToArray();
        if(name.Length is <1 or >80||exe.Length is <1 or >100||ext.Length==0||ext.Any(e=>!Regex.IsMatch(e,@"^[a-z0-9]{1,12}$")))throw new InvalidDataException("Проверьте платформу, EXE и расширения ROM");
        if(exe.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidDataException("Укажите имя EXE без пути и специальных символов");
        if(exe.Contains("retroarch",StringComparison.OrdinalIgnoreCase)&&core.Length==0&&!automatic)throw new InvalidDataException("Для RetroArch укажите DLL ядра");
        var existing=Snapshot()["emulators"]!.AsArray();if(existing.Any(p=>string.Equals(p!["exe"]!.GetValue<string>(),exe,StringComparison.OrdinalIgnoreCase)&&string.Equals(p["core"]!.GetValue<string>(),core,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Такой EXE и ядро уже настроены");
        var p=new JsonObject{["id"]=Guid.NewGuid().ToString(),["name"]=name,["exe"]=exe,["core"]=core,["automatic"]=automatic,["extensions"]=new JsonArray(ext.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray())};
        using var db=Open();using var tx=db.BeginTransaction();Insert(db,tx,"emulator_profiles",p["id"]!.GetValue<string>(),p.ToJsonString());tx.Commit();
    }
    internal void AddAutomaticEmulator(string name,string exe)=>AddEmulator(name,exe,"iso,chd,cso,bin,cue,gcm,rvz,wbfs,wad,nes,sfc,smc,gba,gb,gbc,n64,z64,v64,nds,3ds,cia,pbp,md,gen,sms,gg,zip,7z,m3u,cdi,gdi,dol,elf", "",true);
    internal string EmulatorGame(EmulatorMatch match)
    {
        string identity=match.ProfileId+":"+match.Rom.Replace('/','\\').ToLowerInvariant();
        var existing=Snapshot()["games"]!.AsArray().FirstOrDefault(g=>g!["romIdentity"]?.GetValue<string>()==identity);if(existing is not null)return existing["id"]!.GetValue<string>();
        var id=Guid.NewGuid().ToString();var game=new JsonObject{["id"]=id,["name"]=match.Name,["title"]=match.Name,["platform"]=match.Platform,["status"]="playing",["minutes"]=0,["sessions"]=0,["art"]="blue",["genre"]="Эмуляция",["notes"]="",["romIdentity"]=identity,["emulatorProfileId"]=match.ProfileId};
        using var db=Open();using var tx=db.BeginTransaction();Insert(db,tx,"games",id,game.ToJsonString());tx.Commit();return id;
    }
    internal void RemoveEmulator(string id){using var db=Open();using var c=db.CreateCommand();c.CommandText="DELETE FROM emulator_profiles WHERE id=$id";c.Parameters.AddWithValue("$id",id);c.ExecuteNonQuery();}
}
