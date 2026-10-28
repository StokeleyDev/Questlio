using System.Diagnostics;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed record RunningExe(int Pid,long Started,string Name,string Title,string? File,long Memory,string Token);
internal static class RunningPrograms
{
    internal static List<RunningExe> Read()
    {
        var result=new List<RunningExe>();
        foreach(var process in Process.GetProcesses())using(process){
            try {
                long memory=process.WorkingSet64;string name=process.ProcessName+".exe",title="";string? file=null;long started=0;
                try{file=process.MainModule?.FileName;started=process.StartTime.ToUniversalTime().Ticks;title=process.MainWindowTitle;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
                if(file is not null&&!string.Equals(Path.GetExtension(file),".exe",StringComparison.OrdinalIgnoreCase))file=null;
                result.Add(new(process.Id,started,name,title,file,memory,Guid.NewGuid().ToString("N")));
            }catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
        }
        return Sort(result);
    }
    internal static List<RunningExe> Sort(IEnumerable<RunningExe> items)=>items.OrderByDescending(x=>x.Memory).ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.Pid).ToList();
    internal static string Verify(RunningExe choice)
    {
        try {using var process=Process.GetProcessById(choice.Pid);if(process.StartTime.ToUniversalTime().Ticks!=choice.Started||!string.Equals(process.MainModule?.FileName,choice.File,StringComparison.OrdinalIgnoreCase)||choice.File is null||!File.Exists(choice.File))throw new InvalidDataException("Процесс уже завершён или изменился. Обновите список.");return choice.File;}
        catch(ArgumentException){throw new InvalidDataException("Процесс уже завершён. Обновите список.");}
        catch(InvalidOperationException){throw new InvalidDataException("Процесс уже завершён. Обновите список.");}
        catch(System.ComponentModel.Win32Exception){throw new InvalidDataException("Нет доступа к EXE процесса. Выберите файл вручную.");}
    }
}
internal sealed partial class MainWindow
{
    private Dictionary<string,RunningExe> runningChoices=new();
    private async Task ProcessActionAsync(JsonNode message)
    {
        try {
            if(message["action"]?.GetValue<string>()=="processList"){
                var entries=await Task.Run(RunningPrograms.Read);runningChoices=entries.ToDictionary(x=>x.Token);
                var rows=new JsonArray(entries.Select(x=>(JsonNode?)new JsonObject{["token"]=x.Token,["pid"]=x.Pid,["name"]=x.Name,["title"]=x.Title,["memory"]=x.Memory,["available"]=x.File is not null&&x.Started!=0}).ToArray());
                web.CoreWebView2.PostWebMessageAsJson(new JsonObject{["processList"]=rows}.ToJsonString());return;
            }
            string token=message["token"]?.GetValue<string>()??"";
            if(!runningChoices.TryGetValue(token,out var choice))throw new InvalidDataException("Обновите список процессов и выберите программу заново.");
            string file=RunningPrograms.Verify(choice);
            string? id=message["gameId"]?.GetValue<string>();
            if(!string.IsNullOrEmpty(id)){store.BindExe(id,file);await RefreshBootstrapAsync();web.CoreWebView2.PostWebMessageAsJson(new JsonObject{["inlineEdited"]=true,["id"]=id,["snapshot"]=store.Snapshot()}.ToJsonString());}
            else {selectedNewExe=file;web.CoreWebView2.PostWebMessageAsJson(new JsonObject{["exeSelected"]=Path.GetFileName(file),["suggestedName"]=Path.GetFileNameWithoutExtension(file)}.ToJsonString());}
            runningChoices.Clear();
        }catch(Exception error){web.CoreWebView2.PostWebMessageAsJson(new JsonObject{["processError"]=error is InvalidDataException or InvalidOperationException?error.Message:"Не удалось прочитать процессы. Обновите список или выберите файл."}.ToJsonString());}
    }
    private async Task AddExeFromTrayAsync()
    {
        RestoreWindow();
        if(web.CoreWebView2 is null){RestoreWindow();return;}
        await web.ExecuteScriptAsync("window.openAddFromTray?.()");
    }
}
