// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Rocker.Video;
using Rocker.Domain;
using Rocker.Infrastructure;
using System.Text.Json;

namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private (string Id,SavedVideoLoop? Loop)? queuedDesktopVideo { get=>documentContext.Current.Get<(string Id,SavedVideoLoop? Loop)?>("queued-video",()=>null);set=>documentContext.Current.Set("queued-video",value); }
    private bool videoBridgeReady { get => documentContext.Current.Get<bool>("videoBridgeReady", () => default); set => documentContext.Current.Set("videoBridgeReady",value); }
    private bool videoReady { get => documentContext.Current.Get<bool>("videoReady", () => default); set => documentContext.Current.Set("videoReady",value); }
    private bool changingVideoRates { get => documentContext.Current.Get<bool>("changingVideoRates", () => default); set => documentContext.Current.Set("changingVideoRates",value); }
    private double videoPosition { get => documentContext.Current.Get<double>("videoPosition", () => default); set => documentContext.Current.Set("videoPosition",value); }private DateTimeOffset videoProgressObserved { get => documentContext.Current.Get<DateTimeOffset>("videoProgressObserved", () => default); set => documentContext.Current.Set("videoProgressObserved",value); }
    private VideoPractice videoPractice { get => documentContext.Current.Get<VideoPractice>("videoPractice", () => new()); }
    private readonly List<SavedVideoLoop> savedVideoLoops=[];
    private SavedVideoLoop? pendingVideoLoop { get => documentContext.Current.Get<SavedVideoLoop?>("pendingVideoLoop", () => default); set => documentContext.Current.Set("pendingVideoLoop",value); }
    private SavedVideoLoop? deletedVideoLoop { get => documentContext.Current.Get<SavedVideoLoop?>("deletedVideoLoop", () => default); set => documentContext.Current.Set("deletedVideoLoop",value); }
    private async Task InitializeVideoAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);
var capturedDocument = documentContext.Current;
        try
        {
            var profile = userFiles.DirectoryFor("WebView2");
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, profile, null);
            if (closed) return;
            await VideoView.EnsureCoreWebView2Async(environment);
            if (closed) return;
            var core = VideoView.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.SetVirtualHostNameToFolderMapping(new Uri(VideoProtocol.Origin).Host,
                Path.Combine(AppContext.BaseDirectory, "Video"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.AddWebResourceRequestedFilter("https://www.youtube.com/*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, args) => {using var documentScope = documentContext.Enter(capturedDocument);args.Request.Headers.SetHeader("Referer", VideoProtocol.Origin + "/");};
            core.NavigationStarting += (_, args) =>
            {using var documentScope = documentContext.Enter(capturedDocument);
                if (args.Uri != VideoProtocol.Page && args.Uri != "about:blank") args.Cancel = true;
            };
            core.NewWindowRequested += (_, args) =>
            {using var documentScope = documentContext.Enter(capturedDocument);
                args.Handled = true;
                if (!closed) VideoStatus.Text = "Odkazy z přehrávače lze otevřít tlačítkem V prohlížeči.";
            };
            core.PermissionRequested += (_, args) => {using var documentScope = documentContext.Enter(capturedDocument);args.State = CoreWebView2PermissionState.Deny;};
            core.ProcessFailed += (_, _) =>
            {using var documentScope = documentContext.Enter(capturedDocument);
                videoBridgeReady = videoReady = false;
                if (!closed) { SetVideoControls(false); VideoStatus.Text = "Přehrávač se ukončil. Aplikaci lze dál používat bez videa; zkus znovu otevřít okno."; }
            };
            core.WebMessageReceived += (_, args) =>
            {using var documentScope = documentContext.Enter(capturedDocument);
                if (closed) return;
                var message = VideoProtocol.ReadEvent(args.Source, args.WebMessageAsJson);
                if (message is null || (message.Kind!="bridge-ready" && !videoPractice.Observe(message))) return;
                switch (message.Kind)
                {
                    case "bridge-ready":
                        videoBridgeReady = true;
                        if(queuedDesktopVideo is{ } queued){queuedDesktopVideo=null;LoadVideoCore(queued.Id,queued.Loop);}
                        VideoLoadButton.IsEnabled = true;
                        VideoStatus.Text = "Vlož odkaz na YouTube. Načtení videa vyžaduje internet.";
                        WriteVideoReport(new { result = "passed", localBridgeVerified = true, runtimeVersion = core.Environment.BrowserVersionString,
                            appId = VideoProtocol.AppId, onlineVideoVerified = false, audioCoexistenceVerified = false });
                        break;
                    case "ready":
                        videoReady = true; SetVideoControls(true);if(CurrentTarget.ViewId!="08")SendVideo(new{op="pause"});
                        VideoStatus.Text = "Video je připravené. Zvuk YouTube používá sdílený výstup Windows.";
                        break;
                    case "state":
                        VideoStatus.Text = message.Code switch { 1 => "Video hraje.", 2 => "Video je pozastavené.", 3 => "Video se načítá…", 0 => "Video skončilo.", _ => "Video je připravené." };
                        break;
                    case "progress":
                        videoPosition = message.Position;videoProgressObserved=DateTimeOffset.UtcNow;VideoSeek.Maximum=Math.Max(.01,message.Duration);if(!videoSeeking)VideoSeek.Value=message.Position;TryRestoreVideoLoop();
                        VideoPosition.Text = $"{FormatTime(message.Position)} / {FormatTime(message.Duration)} · {message.Rate:0.##}×";
                        if (VideoRates.ItemsSource is double[] values && values.Contains(message.Rate))
                        { changingVideoRates = true; VideoRates.SelectedItem = message.Rate; changingVideoRates = false; }
                        break;
                    case "rates":
                        changingVideoRates = true;
                        VideoRates.ItemsSource = message.Rates;
                        VideoRates.SelectedItem = message.Rates?.Contains(1) == true ? 1.0 : message.Rates?.FirstOrDefault();
                        changingVideoRates = false;TryRestoreVideoLoop();
                        break;
                    case "error":
                        videoReady = false; SetVideoControls(false);
                        VideoStatus.Text = VideoProtocol.DescribeError(message.Code);
                        break;
                }
            };
            core.Navigate(VideoProtocol.Page);
        }
        catch (Exception error)
        {
            if (closed) return;
            SetVideoControls(false);
            VideoStatus.Text = "YouTube není dostupné. Chybí WebView2 nebo jej nelze otevřít. " + error.Message;
            WriteVideoReport(new { result = "failed", localBridgeVerified = false, error = error.ToString(), onlineVideoVerified = false });
        }
    }
    private void SetVideoControls(bool value)
    {
        VideoPlayButton.IsEnabled = VideoPauseButton.IsEnabled = VideoStopButton.IsEnabled = VideoRates.IsEnabled = VideoSeek.IsEnabled = VideoMarkA.IsEnabled = VideoMarkB.IsEnabled = VideoApplyLoop.IsEnabled = VideoLoopEnabled.IsEnabled = value;
    }
    private void VideoLoad_Click(object sender, RoutedEventArgs e)
    {
        if (!videoBridgeReady) return;
        var id = VideoProtocol.ExtractId(VideoUrl.Text);
        if (id is null) { VideoStatus.Text = "Vlož platný odkaz na YouTube nebo ID videa."; return; }
        LoadVideo(id);
    }
    private void LoadVideo(string id,SavedVideoLoop? loop=null)
    {
        if(desktopEnabled && CurrentTarget.ItemId!=id){OpenDesktopVideo(id,loop);return;}
        LoadVideoCore(id,loop);
    }
    private void OpenDesktopVideo(string id,SavedVideoLoop? loop=null)
    {
        var session=OpenDesktop(new("08",id));using var scope=documentContext.Enter(session);
        if(videoBridgeReady)LoadVideoCore(id,loop);else queuedDesktopVideo=(id,loop);
    }
    private void LoadVideoCore(string id,SavedVideoLoop? loop=null)
    {
        videoPractice.Begin(id);pendingVideoLoop=loop;videoReady=false;videoPosition=0;SetVideoControls(false);VideoUrl.Text="https://www.youtube.com/watch?v="+id;VideoLoopEnabled.IsChecked=false;VideoLoopA.Value=loop?.Start??0;VideoLoopB.Value=loop?.End??0;VideoLoopLabel.Text=loop?.Label??"";VideoStatus.Text="Načítám video…";VideoLoopStatus.Text="Smyčka je vypnutá. Přehrávání spustíš výslovně.";SendVideo(new{op="load",id});
    }
    private void SendVideo(object command)
    {
        if (closed || !videoBridgeReady) return;
        try { VideoView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(WithVideoToken(command))); }
        catch (Exception error) { VideoStatus.Text = "Přehrávač neodpovídá. " + error.Message; }
    }
    private Dictionary<string,object?> WithVideoToken(object command)
    {
        var values=JsonSerializer.SerializeToElement(command).EnumerateObject().ToDictionary(p=>p.Name,p=>(object?)p.Value);values["token"]=videoPractice.Token;return values;
    }
    private async void VideoPlay_Click(object sender, RoutedEventArgs e) {using var scope=documentContext.Enter(documentContext.Current);if(desktopEnabled)await ClaimDesktopVideoAsync();else if(videoReady)SendVideo(new {op="play"});}
    private void VideoPause_Click(object sender, RoutedEventArgs e) { if (videoReady) SendVideo(new { op = "pause" }); }
    private void VideoStop_Click(object sender, RoutedEventArgs e) { if (videoReady) SendVideo(new { op = "stop" }); }
    private void VideoRate_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (videoReady && !changingVideoRates && VideoRates.SelectedItem is double rate) SendVideo(new { op = "rate", rate });
    }
    private async void VideoExternal_Click(object sender, RoutedEventArgs e)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        var id = VideoProtocol.ExtractId(VideoUrl.Text);
        if (id is null) { VideoStatus.Text = "Nejdřív vlož platný odkaz na video."; return; }
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.youtube.com/watch?v=" + id)); }
        catch (Exception error) { if (!closed) VideoStatus.Text = "Prohlížeč nelze otevřít. " + error.Message; }
    }
    private static void WriteVideoReport(object report)
    {
        var directory = Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR");
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "video-bridge.json");
        File.WriteAllText(destination + ".tmp", JsonSerializer.Serialize(report));
        File.Move(destination + ".tmp", destination, true);
    }
}
