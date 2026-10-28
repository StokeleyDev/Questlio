using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    private SteamCoverCache? coverCache;
    private CancellationTokenSource coverCancellation=new();
    private bool coversEnabled=true,coverRefreshRunning,coverRefreshPending,coverRetryRequested;
    private string CoverSetting=>Path.Combine(Path.GetDirectoryName(profileFolder)!,"steam-covers.enabled");
    private void InitializeCovers()
    {
        coverCache=new SteamCoverCache(Path.Combine(Path.GetDirectoryName(profileFolder)!,"covers"));
        coversEnabled=!File.Exists(CoverSetting)||File.ReadAllText(CoverSetting).Trim()!="0";
        web.CoreWebView2.SetVirtualHostNameToFolderMapping("covers.gaiden.local",coverCache.Folder,Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.DenyCors);
        web.CoreWebView2.NavigationCompleted+=async(_,e)=>{if(e.IsSuccess)await RefreshSteamCoversAsync();};
        Disposed+=(_,_)=>{coverCancellation.Cancel();coverCache.Dispose();coverCancellation.Dispose();};
    }
    private JsonObject CoverBootstrap()
    {
        var ids=new JsonArray();foreach(var g in store.Snapshot()["games"]!.AsArray())if(uint.TryParse(LibraryStore.SteamLaunchId(g!),out uint id)&&coverCache?.Has(id)==true)ids.Add(id);
        return new JsonObject{["enabled"]=coversEnabled,["available"]=ids};
    }
    private async Task SetCoverDownloadsAsync(bool enabled)
    {
        coversEnabled=enabled;File.WriteAllText(CoverSetting,enabled?"1":"0");
        if(enabled){coverCache?.RetryFailed();coverRetryRequested=true;}
        if(!enabled){coverCancellation.Cancel();coverCancellation.Dispose();coverCancellation=new();}
        web.CoreWebView2.PostWebMessageAsJson(new JsonObject{["coverPreference"]=enabled}.ToJsonString());await RefreshBootstrapAsync();
    }
    private async Task RefreshSteamCoversAsync()
    {
        if(!coversEnabled||coverCache is null||IsDisposed||Environment.GetCommandLineArgs().Contains("--smoke-test")||!web.CoreWebView2.Source.StartsWith("https://gaiden.local/",StringComparison.Ordinal))return;
        if(coverRefreshRunning){coverRefreshPending=true;return;}coverRefreshRunning=true;
        try{do{
            if(coverRetryRequested){coverCache.RetryFailed();coverRetryRequested=false;}coverRefreshPending=false;var ids=store.Snapshot()["games"]!.AsArray().Select(g=>LibraryStore.SteamLaunchId(g!)).Where(id=>id is not null).Select(id=>uint.Parse(id!)).Distinct().ToArray();
            for(int offset=0;offset<ids.Length&&coversEnabled&&!coverCache.Offline;offset+=2){
                var batch=ids.Skip(offset).Take(2).Select(async id=>{if(!coverCache.Has(id)&&!IsDisposed)web.CoreWebView2.PostWebMessageAsJson(new JsonObject{["coverLoading"]=id}.ToJsonString());bool ready=await coverCache.GetAsync(id,coverCancellation.Token);if(!IsDisposed)web.CoreWebView2.PostWebMessageAsJson(new JsonObject{[ready&&coversEnabled?"coverReady":"coverMissing"]=id}.ToJsonString());});await Task.WhenAll(batch);
            }
        }while(coverRefreshPending&&coversEnabled&&!IsDisposed);}
        catch(OperationCanceledException){}
        catch(ObjectDisposedException)when(IsDisposed||closingSession||exiting){}
        catch(System.Runtime.InteropServices.COMException)when(IsDisposed||closingSession||exiting){}
        finally{coverRefreshRunning=false;}
    }
}
