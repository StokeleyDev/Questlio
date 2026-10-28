using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    private readonly SteamClient steamClient=new();
    private JsonObject? steamPreview;
    private string? steamPreviewToken;
    private bool steamBusy;
    private int steamGeneration;
    private void SteamReply(JsonObject data){if(!IsDisposed&&web.CoreWebView2 is not null)web.CoreWebView2.PostWebMessageAsJson(data.ToJsonString());}
    private async Task SteamActionAsync(JsonNode message)
    {
        string action=message["action"]!.GetValue<string>();
        if(action=="steamCancel"){steamGeneration++;steamPreview=null;steamPreviewToken=null;return;}
        if(steamBusy){SteamReply(new(){["steamError"]="Дождитесь завершения запроса Steam."});return;}
        steamBusy=true;
        try {
            if(action=="steamPreview"){
                steamPreview=null;steamPreviewToken=null;int generation=++steamGeneration;
                var data=await steamClient.Fetch(message["profile"]?.GetValue<string>()??"",message["key"]?.GetValue<string>()??"");
                if(generation!=steamGeneration||IsDisposed)return;
                steamPreview=data;steamPreviewToken=Guid.NewGuid().ToString();
                SteamReply(new(){["steamPreview"]=data.DeepClone(),["token"]=steamPreviewToken});
            }else if(action=="steamImport"){
                if(steamPreview is null||message["token"]?.GetValue<string>()!=steamPreviewToken)throw new InvalidDataException("Сначала загрузите библиотеку Steam заново.");
                store.CreateBackup("before-steam");
                int imported=store.ImportSteam(steamPreview,message["selection"]!.AsArray());
                steamPreview=null;steamPreviewToken=null;
                await RefreshBootstrapAsync();SteamReply(new(){["steamImported"]=imported,["snapshot"]=store.Snapshot()});
            }
        }catch(InvalidDataException error){SteamReply(new(){["steamError"]=error.Message});}
        catch(Exception){SteamReply(new(){["steamError"]="Не удалось выполнить импорт Steam. Библиотека сохранена; попробуйте снова."});}
        finally{steamBusy=false;}
    }
}
