using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
namespace GaidenDesktop;
internal static class CoverChecks
{
    private sealed class Fake(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler
    {
        internal int Count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Count++;if(request.Headers.Contains("Cookie")||request.Headers.Contains("User-Agent")||request.RequestUri?.Host!="shared.akamai.steamstatic.com")throw new Exception("Unexpected headers/host");return Task.FromResult(response(request));}
    }
    private sealed class UnknownLength(byte[] bytes):HttpContent
    {
        protected override bool TryComputeLength(out long length){length=0;return false;}
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context)=>stream.WriteAsync(bytes).AsTask();
    }
    internal static byte[] Jpeg(Color color,string title="Gaiden")
    {
        using var bitmap=new Bitmap(600,900);using var graphics=Graphics.FromImage(bitmap);graphics.Clear(color);
        using var gradient=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0,0,600,900),color,Color.FromArgb(8,15,28),70);graphics.FillRectangle(gradient,0,0,600,900);
        using var pen=new Pen(Color.FromArgb(100,255,255,255),3);for(int i=0;i<4;i++)graphics.DrawEllipse(pen,80+i*30,100+i*55,440-i*60,440-i*60);
        using var font=new Font("Segoe UI",36,FontStyle.Bold);using var small=new Font("Segoe UI",14);graphics.DrawString(title,font,Brushes.White,new RectangleF(48,650,504,180));graphics.DrawString("GAIDEN  ·  LOCAL FIXTURE",small,Brushes.White,48,846);
        using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Jpeg);return stream.ToArray();
    }
    internal static async Task RunAsync()
    {
        string folder=Path.Combine(Path.GetTempPath(),"Gaiden-cover-check-"+Guid.NewGuid());byte[] jpeg=Jpeg(Color.DarkSlateBlue);var log=new List<string>();
        void Assert(bool value,string name){if(!value)throw new Exception(name);log.Add(name+" PASS");}
        HttpResponseMessage Image(byte[] bytes,string type="image/jpeg",bool unknown=false){var response=new HttpResponseMessage(HttpStatusCode.OK){Content=unknown?new UnknownLength(bytes):new ByteArrayContent(bytes)};response.Content.Headers.ContentType=new MediaTypeHeaderValue(type);return response;}
        var good=new Fake(_=>Image(jpeg));using(var cache=new SteamCoverCache(folder,new HttpClient(good))){Assert(await cache.GetAsync(1),"JPEG downloaded using fake HTTP");Assert(await cache.GetAsync(1)&&good.Count==1,"cache reused without HTTP");Assert(File.Exists(cache.FileFor(1)),"numeric cache filename");}
        var offline=new Fake(_=>throw new HttpRequestException("offline"));using(var cache=new SteamCoverCache(folder,new HttpClient(offline)))Assert(await cache.GetAsync(1)&&offline.Count==0,"disk cache reused offline after restart");
        var fallback=new Fake(r=>r.RequestUri!.AbsolutePath.EndsWith("library_600x900.jpg")?new HttpResponseMessage(HttpStatusCode.NotFound):Image(jpeg));using(var cache=new SteamCoverCache(folder,new HttpClient(fallback)))Assert(await cache.GetAsync(2)&&fallback.Count==2,"header fallback after missing poster");
        var wrong=new Fake(_=>Image(jpeg,"text/html"));using(var cache=new SteamCoverCache(folder,new HttpClient(wrong)))Assert(!await cache.GetAsync(3)&&!cache.Has(3),"wrong content type rejected");
        var tooBig=new Fake(_=>Image(new byte[SteamCoverCache.Limit+1]));using(var cache=new SteamCoverCache(folder,new HttpClient(tooBig)))Assert(!await cache.GetAsync(4)&&!cache.Has(4),"declared size limit");
        var chunked=new Fake(_=>Image(new byte[SteamCoverCache.Limit+1],unknown:true));using(var cache=new SteamCoverCache(folder,new HttpClient(chunked)))Assert(!await cache.GetAsync(5)&&!cache.Has(5),"stream size limit without Content-Length");
        var malformed=new Fake(_=>Image(new byte[]{255,216,255,0,0}));using(var cache=new SteamCoverCache(folder,new HttpClient(malformed)))Assert(!await cache.GetAsync(6),"invalid JPEG rejected");
        Assert(!Directory.EnumerateFiles(folder,"*.part").Any(),"temporary files cleaned");
        string output=Environment.GetEnvironmentVariable("GAIDEN_CHECK_OUTPUT")??AppContext.BaseDirectory;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"cover-checks.log"),log);
    }
}
