// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private async Task VerifyDesktopCoverageAsync()
    {
        var coverage=new List<object>();string? folder=Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR");if(string.IsNullOrEmpty(folder))return;
        Directory.CreateDirectory(Path.Combine(folder,"desktop-screens"));
        foreach(var view in WorkspaceViews.All)
        {
            var target=new NavigationTarget(view.Id);var route=DesktopRoutes.Resolve(target);string name="desktop-screens/view-"+view.Id+".png";
            FrameworkElement root;
            if(route.Destination==DesktopDestination.Dialog)
            {
                var owner=view.Id=="32"?OpenDesktop(new("14",(await recordingLibrary!.ListAsync()).OrderByDescending(r=>r.CreatedAt).First().Id)):documentContext.Default;
                using var scope=documentContext.Enter(owner);
                var dialog=view.Id=="32"?CreateRecordingExportDialog((await recordingLibrary!.ListAsync()).OrderByDescending(r=>r.CreatedAt).First()).Dialog:new ContentDialog{XamlRoot=DialogRoot,Title=view.Title,Content=new ScrollViewer{Content=BuildWorkspace(target),MaxHeight=480},CloseButtonText="Zavřít"};
                var shown=ShowDialogAsync(dialog);var dialogDeadline=DateTime.UtcNow.AddSeconds(5);while(!dialog.IsLoaded && !shown.IsCompleted && DateTime.UtcNow<dialogDeadline)await Task.Delay(25);if(!dialog.IsLoaded || shown.IsCompleted)throw new InvalidOperationException("Desktop dialog did not open: "+view.Id);
                await CaptureDesktopThemesAsync(dialog,name);dialog.Hide();await shown;root=ShellRoot;
            }
            else
            {
                if(view.Id=="14")OpenDesktop(target);
                var session=view.Id=="14" && desktopDiagnosticRecording is not null?OpenDesktop(new("14",desktopDiagnosticRecording)):view.Id=="15" && desktopDiagnosticProject is not null?OpenDesktop(new("15",desktopDiagnosticProject)):view.Id=="07" && desktopDiagnosticRecording is not null?OpenDesktop(new("07"),DesktopDocumentKey.ForCanonicalFile("07",Rocker.Infrastructure.OwnedPaths.Resolve(userFiles.Root,(await recordingLibrary!.ListAsync()).Single(r=>r.Id==desktopDiagnosticRecording).RelativePath))):view.Id=="20"?OpenDesktop(new("20"),DesktopDocumentKey.ForCanonicalFile("20",Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"fixtures","notation-phrases.gp")))):OpenDesktop(target);await Task.Delay(180);
                if(route.Destination is DesktopDestination.Tool or DesktopDestination.Live or DesktopDestination.Settings)
                {var tab=TabFor(session)??throw new InvalidOperationException("Missing tool host: "+view.Id);DetachDesktopTab(tab);root=HostRoot(session);}
                else root=ShellRoot;
                var deadline=DateTime.UtcNow.AddSeconds(5);
                while((root.XamlRoot is null || root.ActualWidth<=0 || root.ActualHeight<=0) && DateTime.UtcNow<deadline)await Task.Delay(25);
                root.UpdateLayout();await Task.Delay(100);
                if(root.XamlRoot is null || root.ActualWidth<=0 || root.ActualHeight<=0)throw new InvalidOperationException("Desktop view has no actual layout: "+view.Id);
                await CaptureDesktopThemesAsync(root,name);
            }
            coverage.Add(new{viewId=view.Id,view.Title,destination=route.Destination.ToString(),file=name,widthDip=root.ActualWidth,heightDip=root.ActualHeight,rendered=true,lightFile=name.Replace(".png","-light.png"),themes=new[]{"dark","light"}});
        }
        OpenDesktop(new("01"));
        string links=string.Join("",WorkspaceViews.All.Select(v=>$"<article><h2>{System.Net.WebUtility.HtmlEncode(v.Title)}</h2><a href='desktop-screens/view-{v.Id}.png'><img src='desktop-screens/view-{v.Id}.png' alt='{System.Net.WebUtility.HtmlEncode(v.Title)}'></a></article>"));
        await File.WriteAllTextAsync(Path.Combine(folder,"desktop-gallery.html"),"<!doctype html><html lang='cs'><meta charset='utf-8'><title>Rocker — desktop Windows</title><style>body{font:16px system-ui;background:#221e19;color:#eee2d0;margin:24px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:24px}img{width:100%}</style><h1>Pracovní plocha a skutečná Windows okna</h1><p>Automatický kontrolní profil. Snímky nepotvrzují skutečnou zvukovku, poslech ani online služby.</p><main>"+links+"</main></html>");
        WriteShellDiagnostic("desktop-functional-coverage.json",new{result="passed",views=coverage,viewCount=coverage.Count,sourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA"),actualWindows=true,hardwareVerified=false});
    }
    private async Task CaptureDesktopThemesAsync(FrameworkElement root,string name)
    {
        string previous=selectedTheme;
        try{ApplyTheme("dark");root.RequestedTheme=ElementTheme.Dark;await Task.Delay(100);await CaptureDesktopElementAsync(root,name);ApplyTheme("light");root.RequestedTheme=ElementTheme.Light;await Task.Delay(100);await CaptureDesktopElementAsync(root,name.Replace(".png","-light.png"));}
        finally{ApplyTheme(previous);root.RequestedTheme=ShellRoot.RequestedTheme;}
    }
    private async Task CaptureDesktopElementAsync(FrameworkElement root,string name)
    {
        string? folder=Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR");if(string.IsNullOrEmpty(folder))return;
        var bitmap=new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();await bitmap.RenderAsync(root);
        if(bitmap.PixelWidth<=0 || bitmap.PixelHeight<=0)throw new InvalidOperationException("Capture has no pixels: "+name);
        var buffer=await bitmap.GetPixelsAsync();var pixels=new byte[(int)buffer.Length];using(var source=Windows.Storage.Streams.DataReader.FromBuffer(buffer))source.ReadBytes(pixels);
        using var stream=new Windows.Storage.Streams.InMemoryRandomAccessStream();var encoder=await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,stream);
        encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,pixels);await encoder.FlushAsync();stream.Seek(0);
        var bytes=new byte[(int)stream.Size];using var reader=new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));await reader.LoadAsync((uint)bytes.Length);reader.ReadBytes(bytes);await File.WriteAllBytesAsync(Path.Combine(folder,name),bytes);
    }
}
