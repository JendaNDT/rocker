// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private async Task VerifyDesktopRuntimeAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        var checks=new List<string>();
        void Check(bool condition,string name){if(!condition)throw new InvalidOperationException("Desktop: "+name);checks.Add(name);}
        if(audio is null)throw new InvalidOperationException("Desktop check requires the existing host.");
        await audio.StopAsync();var before=audio.GetSnapshot();int hosts=services.Audio.HostsCreated;
        InitializeDesktop();await Task.Delay(100);
        Check(desktopEnabled && Navigation.Visibility==Visibility.Collapsed && documentTabs.XamlRoot is not null,"native-workspace-host");
        Check(desktopRoot.ColumnDefinitions.Count==5 && documentTabs.ActualWidth>0,"library-document-inspector-layout");
        var a=OpenDesktop(new("15","desktop-project-a"));using(documentContext.Enter(a)){studioZoom=31;studioUndo.Clear();}
        var b=OpenDesktop(new("15","desktop-project-b"));using(documentContext.Enter(b)){studioZoom=79;}
        Check(a!=b && desktopDocuments.ContainsKey(a) && desktopDocuments.ContainsKey(b),"two-project-documents");
        using(documentContext.Enter(a))Check(studioZoom==31,"first-project-zoom-preserved");
        using(documentContext.Enter(b))Check(studioZoom==79,"second-project-zoom-independent");
        Check(OpenDesktop(new("15","desktop-project-a"))==a,"duplicate-project-activation");
        var file=System.IO.Path.Combine(AppContext.BaseDirectory,"fixtures","notation-phrases.gp");
        var score=OpenDesktop(new("20"),DesktopDocumentKey.ForCanonicalFile("20",System.IO.Path.GetFullPath(file)));
        object image;UIElement view;double selectedBar;
        using(documentContext.Enter(score)){await LoadScoreAsync(file);Check(preview is not null && ScoreImage.Source is not null,"actual-score-decoded");NotationBar.Value=Math.Min(2,NotationBar.Maximum);selectedBar=NotationBar.Value;image=ScoreImage.Source;view=scoreDocumentView;}
        var tab=desktopDocuments[score];var rootBefore=((FrameworkElement)view).XamlRoot;
        DetachDesktopTab(tab);await Task.Delay(150);
        Check(floatingHosts.ContainsKey(tab) && floatingHosts[tab].Tabs.XamlRoot is not null,"actual-document-window");
        using(documentContext.Enter(score)){Check(ReferenceEquals(image,ScoreImage.Source) && NotationBar.Value==selectedBar,"detach-preserves-score-and-selection");Check(!ReferenceEquals(rootBefore,scoreDocumentView.XamlRoot),"actual-xaml-root-transfer");}
        AttachDesktopTab(tab);await Task.Delay(100);
        using(documentContext.Enter(score))Check(ReferenceEquals(image,ScoreImage.Source) && NotationBar.Value==selectedBar,"reattach-preserves-score-state");
        var tuner=OpenDesktop(new("03"));var tunerTab=TabFor(tuner)!;DetachDesktopTab(tunerTab);await Task.Delay(100);
        Check(OpenDesktop(new("03"))==tuner && TabFor(tuner)==tunerTab,"tool-singleton");
        var tunerHost=floatingHosts[tunerTab];SendMessageW(WinRT.Interop.WindowNative.GetWindowHandle(tunerHost),0x0010,0,0);await Task.Delay(100);
        Check(desktopEnabled && !closed && floatingHosts.ContainsKey(tunerTab),"closing-tool-keeps-application");
        OpenDesktop(new("03"));AttachDesktopTab(tunerTab);
        Check(TabFor(tuner)==tunerTab,"reopening-tool-preserves-instance");
        var after=audio.GetSnapshot();Check(hosts==services.Audio.HostsCreated && hosts==1,"one-native-audio-host");
        Check(before.State==after.State && before.RenderedFrames==after.RenderedFrames,"navigation-does-not-start-or-render-audio");
        Check(!audio.GetMeters().Monitoring,"navigation-does-not-enable-monitoring");
        Check(services.Transport.Owner is null,"opening-score-does-not-claim-media-slot");
        await CloseDesktopTabAsync(desktopDocuments[b]);Check(!b.IsAlive && a.IsAlive,"closing-one-document-keeps-other-session");
        using(documentContext.Enter(a)){Check(studioZoom==31,"other-project-state-survives-close");}
        OpenDesktop(new("01"));
        await CaptureDesktopRuntimeAsync();
        await VerifyDesktopCoverageAsync();
        WriteShellDiagnostic("desktop-runtime.json",new{result="passed",checks=checks.Count,scenarios=checks,actualWindows=true,physicalPointerDragVerified=false,physicalAudioVerified=false,sourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA")});
    }
    private async Task CaptureDesktopRuntimeAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        string? folder=Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR");if(string.IsNullOrEmpty(folder))return;
        await Task.Delay(150);var bitmap=new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();await bitmap.RenderAsync(ShellRoot);
        var buffer=await bitmap.GetPixelsAsync();var pixels=new byte[(int)buffer.Length];using(var source=Windows.Storage.Streams.DataReader.FromBuffer(buffer))source.ReadBytes(pixels);
        using var stream=new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var encoder=await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,stream);
        encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,pixels);await encoder.FlushAsync();stream.Seek(0);
        var bytes=new byte[(int)stream.Size];using var reader=new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));await reader.LoadAsync((uint)bytes.Length);reader.ReadBytes(bytes);await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(folder,"desktop-workspace.png"),bytes);
    }
}
