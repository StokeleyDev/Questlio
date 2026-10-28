using System.Text.Json.Nodes;

namespace GaidenDesktop;
internal sealed partial class LibraryStore
{
    private void EditGame(string id,Action<JsonObject> edit)
    {
        using var db=Open();using var tx=db.BeginTransaction();using var c=db.CreateCommand();c.Transaction=tx;
        c.CommandText="SELECT payload FROM games WHERE id=$id";c.Parameters.AddWithValue("$id",id);
        var game=JsonNode.Parse((string?)c.ExecuteScalar()??throw new InvalidOperationException("Игра не найдена"))!.AsObject();
        if(game["deleted"]?.GetValue<bool>()==true)throw new InvalidOperationException("Игра уже удалена");
        edit(game);c.CommandText="UPDATE games SET payload=$payload WHERE id=$id";c.Parameters.AddWithValue("$payload",game.ToJsonString());c.ExecuteNonQuery();tx.Commit();
    }
    internal void RemoveExe(string id,string name)=>EditGame(id,g=>g["executables"]=new JsonArray(Exes(g).Where(x=>!string.Equals(x,name,StringComparison.OrdinalIgnoreCase)).Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray()));
    internal void DeleteGame(string id)=>EditGame(id,g=>{g["deleted"]=true;g["deletedAt"]=DateTimeOffset.UtcNow.ToString("O");});
    internal void SetCover(string id,string? data)=>EditGame(id,g=>g["customCover"]=data);
}
