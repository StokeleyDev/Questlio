using System.Management;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal static class HardwareInfo
{
    private static List<Dictionary<string,string>> Query(string query,params string[] fields)
    {
        var rows=new List<Dictionary<string,string>>();
        try {
            using var search=new ManagementObjectSearcher(new ManagementScope("root\\cimv2"),new ObjectQuery(query),new System.Management.EnumerationOptions{Timeout=TimeSpan.FromSeconds(3),ReturnImmediately=false});
            using var values=search.Get();foreach(ManagementObject value in values)using(value){var row=new Dictionary<string,string>();foreach(var field in fields)row[field]=Convert.ToString(value[field],System.Globalization.CultureInfo.InvariantCulture)?.Trim()??"";rows.Add(row);}
        }catch(SystemException){ }
        return rows;
    }
    internal static JsonObject Read()
    {
        var os=Query("SELECT Caption,Version,BuildNumber FROM Win32_OperatingSystem","Caption","Version","BuildNumber").FirstOrDefault();
        var cpu=Query("SELECT Name,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor","Name","NumberOfCores","NumberOfLogicalProcessors");
        var gpu=Query("SELECT Name FROM Win32_VideoController","Name");
        var memory=Query("SELECT Capacity FROM Win32_PhysicalMemory","Capacity");
        var board=Query("SELECT Manufacturer,Product FROM Win32_BaseBoard","Manufacturer","Product");
        string Join(IEnumerable<string> values){var result=string.Join(" · ",values.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct());return result.Length>0?result:"Не удалось определить";}
        var bytes=memory.Sum(row=>double.TryParse(row["Capacity"],out var n)?n:0);
        return new JsonObject{
            ["windows"]=os is null?RuntimeInformation.OSDescription:$"{os["Caption"]} · {os["Version"]} · сборка {os["BuildNumber"]}",
            ["architecture"]=RuntimeInformation.OSArchitecture.ToString(),
            ["processor"]=Join(cpu.Select(row=>$"{row["Name"]} · {row["NumberOfCores"]} ядер / {row["NumberOfLogicalProcessors"]} потоков")),
            ["graphics"]=Join(gpu.Select(row=>row["Name"])),
            ["ram"]=bytes>0?$"{bytes/1073741824:0.#} ГБ":"Не удалось определить",
            ["motherboard"]=Join(board.Select(row=>Join(new[]{row["Manufacturer"],row["Product"]}))),
            ["detectedAt"]=DateTimeOffset.Now.ToString("O")
        };
    }
}

