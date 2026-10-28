using System.Net;
using System.Net.Http;
namespace GaidenDesktop;
internal sealed class SteamCoverCache : IDisposable
{
    internal const int Limit=2*1024*1024;
    private readonly HttpClient client;
    private readonly Dictionary<uint,Task<bool>> requests=new();
    internal string Folder {get;}
    internal bool Offline {get;private set;}
    internal SteamCoverCache(string folder,HttpClient? transport=null)
    {
        Folder=folder;Directory.CreateDirectory(folder);
        client=transport??new HttpClient(new HttpClientHandler{UseCookies=false,AllowAutoRedirect=false});client.Timeout=TimeSpan.FromSeconds(8);
    }
    internal string FileFor(uint id)=>Path.Combine(Folder,id.ToString(System.Globalization.CultureInfo.InvariantCulture)+".jpg");
    internal bool Has(uint id){var file=new FileInfo(FileFor(id));if(!file.Exists||file.Length is <4 or >Limit)return false;try{using var stream=file.OpenRead();return stream.ReadByte()==255&&stream.ReadByte()==216&&stream.ReadByte()==255;}catch(IOException){return false;}}
    internal Task<bool> GetAsync(uint id,CancellationToken cancellation=default)
    {
        if(id==0)return Task.FromResult(false);if(Has(id))return Task.FromResult(true);
        lock(requests){if(requests.TryGetValue(id,out var task))return task;task=FetchAsync(id,cancellation);requests[id]=task;return task;}
    }
    internal void RetryFailed(){lock(requests){foreach(var entry in requests.Where(p=>p.Value.IsCompleted&&(!p.Value.IsCompletedSuccessfully||!p.Value.Result)).ToArray())requests.Remove(entry.Key);Offline=false;}}
    private async Task<bool> FetchAsync(uint id,CancellationToken cancellation)
    {
        if(Offline)return false;
        foreach(string image in new[]{"library_600x900.jpg","header.jpg"}){
            string temp=FileFor(id)+"."+Guid.NewGuid().ToString("N")+".part";
            try{
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(8));var token=timeout.Token;
                using var request=new HttpRequestMessage(HttpMethod.Get,$"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{id}/{image}");
                using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
                if(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)continue;
                if(!response.IsSuccessStatusCode||!string.Equals(response.Content.Headers.ContentType?.MediaType,"image/jpeg",StringComparison.OrdinalIgnoreCase)||response.Content.Headers.ContentLength>Limit)return false;
                await using(var source=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                await using(var target=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,8192,true)){
                    byte[] buffer=new byte[8192];int total=0,count;
                    while((count=await source.ReadAsync(buffer,token).ConfigureAwait(false))>0){total+=count;if(total>Limit)return false;await target.WriteAsync(buffer.AsMemory(0,count),token).ConfigureAwait(false);}
                }
                using(var bitmap=System.Drawing.Image.FromFile(temp)){if(bitmap.RawFormat.Guid!=System.Drawing.Imaging.ImageFormat.Jpeg.Guid||bitmap.Width>4096||bitmap.Height>4096)return false;}
                token.ThrowIfCancellationRequested();File.Move(temp,FileFor(id),true);return true;
            }catch(Exception error)when(error is HttpRequestException or OperationCanceledException){if(!cancellation.IsCancellationRequested)Offline=true;return false;}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException){return false;}
            finally{try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){}}
        }return false;
    }
    public void Dispose()=>client.Dispose();
}
