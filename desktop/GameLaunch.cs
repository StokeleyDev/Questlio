using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
namespace GaidenDesktop;

internal sealed partial class LibraryStore
{
    internal void RememberExecutable(string id,string file)
    {
        if(!Path.IsPathFullyQualified(file)||!File.Exists(file)||!string.Equals(Path.GetExtension(file),".exe",StringComparison.OrdinalIgnoreCase))return;
        EditGame(id,g=>{var paths=g["launchPaths"] as JsonObject??new JsonObject();paths[Path.GetFileNameWithoutExtension(file)]=Path.GetFullPath(file);if(g["launchPaths"] is null)g["launchPaths"]=paths;});
    }
    internal static string? LaunchPath(JsonNode game)
    {
        if(game["deleted"]?.GetValue<bool>()==true)return null;
        foreach(var name in Exes(game)){
            string? file=game["launchPaths"]?[name]?.GetValue<string>();
            if(file is not null&&Path.IsPathFullyQualified(file)&&string.Equals(Path.GetExtension(file),".exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(file))return Path.GetFullPath(file);
        }
        string? learned=game["launchPath"]?.GetValue<string>();
        return learned is not null&&Path.IsPathFullyQualified(learned)&&File.Exists(learned)&&string.Equals(Path.GetExtension(learned),".exe",StringComparison.OrdinalIgnoreCase)&&Exes(game).Contains(Path.GetFileNameWithoutExtension(learned),StringComparer.OrdinalIgnoreCase)?Path.GetFullPath(learned):null;
    }
    internal bool LearnLaunchPath(JsonNode game,Func<string?> readPath)
    {
        if(game["launchPaths"] is JsonObject chosen&&chosen.Count>0||LaunchPath(game) is not null)return false;
        try{
            string? file=readPath();
            if(file is null||!Path.IsPathFullyQualified(file)||!File.Exists(file)||!string.Equals(Path.GetExtension(file),".exe",StringComparison.OrdinalIgnoreCase)||!Exes(game).Contains(Path.GetFileNameWithoutExtension(file),StringComparer.OrdinalIgnoreCase))return false;
            bool changed=false;EditGame(game["id"]!.GetValue<string>(),latest=>{if(latest["launchPaths"] is JsonObject paths&&paths.Count>0||LaunchPath(latest) is not null)return;latest["launchPath"]=Path.GetFullPath(file);changed=true;});return changed;
        }catch(Exception error)when(error is System.ComponentModel.Win32Exception or UnauthorizedAccessException or InvalidOperationException or IOException or NotSupportedException){return false;}
    }
    internal static string? SteamLaunchId(JsonNode game)
    {
        string text=game["steam"]?["appId"]?.ToString()??"";
        return uint.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out uint id)&&id>0?id.ToString(CultureInfo.InvariantCulture):null;
    }
    internal ProcessStartInfo GameStart(string id,bool reveal=false)
    {
        var game=Snapshot()["games"]!.AsArray().SingleOrDefault(g=>g!["id"]!.GetValue<string>()==id)??throw new InvalidOperationException("Игра не найдена");
        string? file=LaunchPath(game);
        if(reveal){if(file is null)throw new InvalidOperationException("Выберите EXE повторно: полный путь пока неизвестен");var info=new ProcessStartInfo("explorer.exe"){UseShellExecute=true};info.Arguments="/select,\""+file+"\"";return info;}
        if(file is not null)return new ProcessStartInfo(file){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(file)!};
        string? appId=SteamLaunchId(game);if(appId is not null)return new ProcessStartInfo("steam://rungameid/"+appId){UseShellExecute=true};
        throw new InvalidOperationException("Для запуска привяжите EXE через «Файлы игры»");
    }
    internal void CreatePlatformGame(string name,string platform)
    {
        if(!new[]{"PlayStation 2","Nintendo Switch","Другая"}.Contains(platform))throw new InvalidDataException("Неизвестная платформа");
        SaveGames(new JsonArray(new JsonObject{["id"]=Guid.NewGuid().ToString(),["name"]=name,["platform"]=platform,["status"]="backlog",["notes"]=""}));
    }
}
