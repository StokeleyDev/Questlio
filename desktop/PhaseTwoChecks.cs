using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
namespace GaidenDesktop;
internal sealed partial class MainWindow
{
    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    private async Task PhaseTwoChecksAsync(string smoke)
    {
        string folder=Environment.GetEnvironmentVariable("GAIDEN_PHASE2_OUTPUT")??Path.Combine(AppContext.BaseDirectory,"phase2");Directory.CreateDirectory(folder);var log=new List<string>();
        void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);log.Add(name+" PASS");}
        Check(Text=="Questlio"&&Icon is not null&&tray.Icon is not null&&ShowInTaskbar&&ShowIcon,"nativeCaptionIcons");
        Check(SendMessage(Handle,0x7f,new IntPtr(1),IntPtr.Zero)!=IntPtr.Zero&&SendMessage(Handle,0x7f,IntPtr.Zero,IntPtr.Zero)!=IntPtr.Zero,"nativeBigSmallIconsTaskbarAltTab");
        Check(!SupportsMica(new Version(10,0,19045))&&SupportsMica(new Version(10,0,22000)),"Win10VersionBranch");
        Check(!TryDwm(()=>unchecked((int)0x80070057))&&!TryDwm(()=>throw new EntryPointNotFoundException())&&!TryDwm(()=>throw new DllNotFoundException()),"unsupportedDwmNoThrow");
        var primary=new Rectangle(0,0,1920,1080);Check(NativeShellSettings.SafeBounds(new Rectangle(-9000,-9000,1280,800),MinimumSize,new[]{primary},primary).Left>=0,"removedMonitorFallback");
        string probe=Path.Combine(Path.GetTempPath(),"Gaiden-shell-check-"+Guid.NewGuid());Directory.CreateDirectory(probe);
        try{
            var a=new NativeShellSettings(probe){Theme="dark",Bounds=new Rectangle(100,120,1280,800),Maximized=true};a.Save();var b=new NativeShellSettings(probe);Check(b.Theme=="dark"&&b.Dark&&b.Bounds==a.Bounds&&b.Maximized,"nativeSettingsRoundtrip");
            a.Theme="light";a.Save();Check(!new NativeShellSettings(probe).Dark,"explicitLightRoundtrip");a.Theme="auto";a.Save();Check(new NativeShellSettings(probe).Dark==NativeShellSettings.SystemDark(),"autoUsesWindowsRegistry");
            File.WriteAllText(Path.Combine(probe,"window.ini"),"invalid");Check(new NativeShellSettings(probe).Theme=="auto","invalidSettingsFallback");
        }finally{Directory.Delete(probe,true);}
        await PhaseOneDesktopChecksAsync(smoke,false);log.Add("WebView smoke + PhaseZero 14/14 + PhaseOne layout/material/modes PASS");
        await FeatureUiChecksAsync();log.Add("FeatureUiChecks PASS");
        await web.ExecuteScriptAsync(PhaseOneAuditScript);
        foreach(string mode in new[]{"light","dark"})foreach(string view in new[]{"home","library"}){
            await web.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());prefs.reduced=false;prefs.opaque=false;GaidenTheme.select('"+mode+"');navigate('"+view+"');document.activeElement?.blur()");await Task.Delay(700);
            Check(BackColor==(mode=="dark"?Color.FromArgb(15,20,27):Color.FromArgb(245,245,247))&&web.DefaultBackgroundColor==BackColor,"nativeTheme-"+mode+"-"+view);
            var audit=JsonNode.Parse(await web.ExecuteScriptAsync("phaseOneAudit.collect('phase2')"))!;
            Check(audit["panelsOpaque"]!.GetValue<bool>()&&audit["overflow"]!.AsArray().Count==0&&audit["smallTargets"]!.AsArray().Count==0,"PhaseOne-"+mode+"-"+view);
            Check(audit["failures"]!.AsArray().Count==0,"PhaseOneContrast-"+mode+"-"+view+": "+audit["failures"]);
            var persisted=new NativeShellSettings(Path.GetDirectoryName(profileFolder)!);Check(persisted.Theme==mode,"persistedTheme-"+mode+"-"+view);
        }
        Size=new Size(1280,800);WindowState=FormWindowState.Normal;Show();Activate();BringToFront();
        if(!Environment.GetCommandLineArgs().Contains("--phase2-no-capture"))foreach(string mode in new[]{"light","dark"})foreach(string view in new[]{"home","library"}){
            await web.ExecuteScriptAsync("prefs.reduced=false;prefs.opaque=false;GaidenTheme.select('"+mode+"');navigate('"+view+"');document.activeElement?.blur()");await Task.Delay(700);
            using var bitmap=new Bitmap(Width,Height);using var graphics=Graphics.FromImage(bitmap);IntPtr dc=graphics.GetHdc();bool printed;try{printed=PrintWindow(Handle,dc,2);}finally{graphics.ReleaseHdc(dc);}
            if(!printed)graphics.CopyFromScreen(Location,Point.Empty,Size);
            bitmap.Save(Path.Combine(folder,mode+"-"+view+".png"),System.Drawing.Imaging.ImageFormat.Png);log.Add(mode+"-"+view+" native 1280x800 "+(printed?"PrintWindow":"CopyFromScreen"));
        }
        int backdrop;int hr=DwmGetWindowAttribute(Handle,38,out backdrop,sizeof(int));log.Add("OS "+Environment.OSVersion.Version+"; DWM38 HRESULT="+hr+" value="+backdrop);
        File.WriteAllLines(Path.Combine(folder,"checks.log"),log);
    }
}
