using Microsoft.Web.WebView2.Core;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    private async Task CapturePhaseZeroAsync()
    {
        Size=new Size(1280,800);
        var path=Environment.GetEnvironmentVariable("GAIDEN_PHASE0_CAPTURE")??Path.Combine(AppContext.BaseDirectory,"phase0-capture");Directory.CreateDirectory(path);
        await web.ExecuteScriptAsync("prefs.reduced=true;prefs.opaque=false;applyPrefs();document.querySelector('#toast').style.display='none';const style=document.createElement('style');style.textContent='*{caret-color:transparent}';document.head.append(style);");
        var metadata=new JsonObject();
        foreach(bool dark in new[]{false,true})foreach(string scene in new[]{"home","library","stats","settings","detail","add"})
        {
            await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.dark="+(dark?"true":"false")+";applyPrefs();navigate("+System.Text.Json.JsonSerializer.Serialize(scene is "detail" or "add"?"home":scene)+");"+(scene=="detail"?"openDetail(games[0].id);document.querySelector('#detail-dialog').scrollTop=0;":scene=="add"?"document.querySelector('#add-dialog').showModal();":""));
            await Task.Delay(350);
            await web.ExecuteScriptAsync("Promise.all([...document.images].map(i=>i.complete?Promise.resolve():new Promise(r=>{i.addEventListener('load',r,{once:true});i.addEventListener('error',r,{once:true});setTimeout(r,1000)})))");
            await Task.Delay(100);
            string key=(dark?"dark-":"light-")+scene;
            metadata[key]=JsonNode.Parse(await web.ExecuteScriptAsync("({main:document.querySelector('#main').innerHTML,detail:document.querySelector('#detail-content').innerHTML,width:innerWidth,height:innerHeight,scrollY})"));
            using var file=File.Create(Path.Combine(path,key+".png"));await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
        }
        File.WriteAllText(Path.Combine(path,"dom.json"),metadata.ToJsonString());
    }
}
