using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal static class CheckEvidence
{
    internal static void Write(string name,string value)
    {
        if(!Environment.GetCommandLineArgs().Contains("--text-results")){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,name),value);return;}
        var node=JsonNode.Parse(value);if(node is JsonValue v&&v.TryGetValue<string>(out var nested))node=JsonNode.Parse(nested);
        var lines=new List<string>();void Walk(JsonNode? n,string key){if(n is JsonObject o){foreach(var p in o)Walk(p.Value,key.Length==0?p.Key:key+"."+p.Key);}else if(n is JsonArray a){for(int i=0;i<a.Count;i++)Walk(a[i],key+"["+i+"]");}else lines.Add(key+" = "+n?.ToString());}Walk(node,"");
        string folder=Environment.GetEnvironmentVariable("GAIDEN_CHECK_OUTPUT")??AppContext.BaseDirectory;Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,Path.ChangeExtension(name,"log")),lines);
    }
}
