// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Rocker.Video;
using System.Text.Json;

namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private bool videoBridgeReady, videoReady, changingVideoRates;
    private double videoPosition;
    private async Task InitializeVideoAsync()
    {
        try
        {
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rocker", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, profile);
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
            core.WebResourceRequested += (_, args) => args.Request.Headers.SetHeader("Referer", VideoProtocol.Origin + "/");
            core.NavigationStarting += (_, args) =>
            {
                if (args.Uri != VideoProtocol.Page && args.Uri != "about:blank") args.Cancel = true;
            };
            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                if (!closed) VideoStatus.Text = "Odkazy z přehrávače lze otevřít tlačítkem V prohlížeči.";
            };
            core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
            core.ProcessFailed += (_, _) =>
            {
                videoBridgeReady = videoReady = false;
                if (!closed) { SetVideoControls(false); VideoStatus.Text = "Přehrávač se ukončil. Aplikaci lze dál používat bez videa; zkus znovu otevřít okno."; }
            };
            core.WebMessageReceived += (_, args) =>
            {
                if (closed) return;
                var message = VideoProtocol.ReadEvent(args.Source, args.WebMessageAsJson);
                if (message is null) return;
                switch (message.Kind)
                {
                    case "bridge-ready":
                        videoBridgeReady = true;
                        VideoLoadButton.IsEnabled = true;
                        VideoStatus.Text = "Vlož odkaz na YouTube. Načtení videa vyžaduje internet.";
                        WriteVideoReport(new { result = "passed", localBridgeVerified = true, runtimeVersion = core.Environment.BrowserVersionString,
                            appId = VideoProtocol.AppId, onlineVideoVerified = false, audioCoexistenceVerified = false });
                        break;
                    case "ready":
                        videoReady = true; SetVideoControls(true);
                        VideoStatus.Text = "Video je připravené. Zvuk YouTube používá sdílený výstup Windows.";
                        break;
                    case "state":
                        VideoStatus.Text = message.Code switch { 1 => "Video hraje.", 2 => "Video je pozastavené.", 3 => "Video se načítá…", 0 => "Video skončilo.", _ => "Video je připravené." };
                        break;
                    case "progress":
                        videoPosition = message.Position;
                        VideoPosition.Text = $"{FormatTime(message.Position)} / {FormatTime(message.Duration)} · {message.Rate:0.##}×";
                        if (VideoRates.ItemsSource is double[] values && values.Contains(message.Rate))
                        { changingVideoRates = true; VideoRates.SelectedItem = message.Rate; changingVideoRates = false; }
                        break;
                    case "rates":
                        changingVideoRates = true;
                        VideoRates.ItemsSource = message.Rates;
                        VideoRates.SelectedItem = message.Rates?.Contains(1) == true ? 1.0 : message.Rates?.FirstOrDefault();
                        changingVideoRates = false;
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
        VideoPlayButton.IsEnabled = VideoPauseButton.IsEnabled = VideoStopButton.IsEnabled = VideoRates.IsEnabled = value;
    }
    private void VideoLoad_Click(object sender, RoutedEventArgs e)
    {
        if (!videoBridgeReady) return;
        var id = VideoProtocol.ExtractId(VideoUrl.Text);
        if (id is null) { VideoStatus.Text = "Vlož platný odkaz na YouTube nebo ID videa."; return; }
        videoReady = false; SetVideoControls(false);
        VideoStatus.Text = "Načítám video…";
        SendVideo(new { op = "load", id });
    }
    private void SendVideo(object command)
    {
        if (closed || !videoBridgeReady) return;
        try { VideoView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(command)); }
        catch (Exception error) { VideoStatus.Text = "Přehrávač neodpovídá. " + error.Message; }
    }
    private void VideoPlay_Click(object sender, RoutedEventArgs e) { if (videoReady) SendVideo(new { op = "play" }); }
    private void VideoPause_Click(object sender, RoutedEventArgs e) { if (videoReady) SendVideo(new { op = "pause" }); }
    private void VideoStop_Click(object sender, RoutedEventArgs e) { if (videoReady) SendVideo(new { op = "stop" }); }
    private void VideoRate_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (videoReady && !changingVideoRates && VideoRates.SelectedItem is double rate) SendVideo(new { op = "rate", rate });
    }
    private async void VideoExternal_Click(object sender, RoutedEventArgs e)
    {
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
