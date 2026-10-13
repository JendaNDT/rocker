// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;

namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private static Button PrimaryAction(string label) => new()
    {
        Content = label, MinHeight = 40,
        Style = (Style)Application.Current.Resources["VintageRootButtonStyle"]
    };
    private string WorkspaceDescription(NavigationTarget target) => target.ItemId is not null ? TaskTitle(target) : target.ViewId switch
    {
        "01" => "Tvůj další krok, denní plán a rychlý vstup do hraní.",
        "02" => "Kytara, ladění a zvuk na jednom místě.",
        "03" => "Nejdřív nalaď. Referenční tón spustíš u vybrané struny.",
        "04" => "Udrž tempo. Podrobné rytmy a presety najdeš níže.",
        "05" => "Vyber lekci a pokračuj vlastním tempem.",
        "13" => "Vytvoř si zvuk. Poslech kytary zapni v Live.",
        "14" => "Nahraj nápad, poslechni si jej a porovnej svoje hraní.",
        "15" => "Tvoje skladba, stopy a společné přehrávání.",
        "16" => "Zahraj základ, přidej vrstvy a ulož svou smyčku.",
        "27" => "Co už máš za sebou a kam pokračovat.",
        "28" => "Vzhled, způsob cvičení a volitelné služby.",
        "29" => "Vyber zvukovou kartu, jeden vstup kytary a sluchátka.",
        "30" => "Tvoje data, převod z webu a úplné zálohy.",
        _ => WorkspaceViews.Get(target.ViewId).Group
    };
    private void RefreshWorkspaceChrome()
    {
        if (!shellReady || closed) return;
        PracticeRestNotice.Visibility=focusMode?Visibility.Collapsed:Visibility.Visible;
        ShellNotice.Visibility=focusMode && ShellNotice.Severity is InfoBarSeverity.Informational or InfoBarSeverity.Success?Visibility.Collapsed:Visibility.Visible;
        bool running = practiceSession.Phase == PracticePhase.Running;
        bool paused = practiceSession.Phase == PracticePhase.Paused;
        bool practice = PracticeViews.Contains(navigation.Current.ViewId) || running || paused;
        PracticeRow.Visibility = practice ? Visibility.Visible : Visibility.Collapsed;
        PracticeStart.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        PracticePause.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        PracticeEnd.Visibility = running || paused ? Visibility.Visible : Visibility.Collapsed;
        bool ready = audio is not null && audio.GetTransport().Ready != 0;
        bool active = audio is not null && audio.GetSnapshot().State != Rocker.Interop.AudioState.Stopped;
        bool mediaView = navigation.Current.ViewId is "07" or "14" or "15" or "19" or "20";
        MediaTransportRow.Visibility = ready || mediaView ? Visibility.Visible : Visibility.Collapsed;
        MediaSeekRow.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        GlobalMuteButton.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        MediaPanel.Visibility = practice || ready || mediaView || PlanButtons.Visibility == Visibility.Visible || PracticeRestNotice.IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }
    private async void ToggleMedia_Click(object sender, RoutedEventArgs args)
    {
        if (audio is null || busy || powerTransition || deviceOperationRunning || closingWindow || deviceSelectionUnavailable) return;
        try
        {
            ++playbackIntent;
            if (audio.GetTransport().Playing != 0) await audio.PauseAsync();
            else await audio.PlayAsync();
            if (!closed) RefreshState();
        }
        catch (Exception error) { if (!closed) ShowError(error); }
    }
    private async Task VerifyWorkspaceUxAsync()
    {
        if (audio is null) throw new InvalidOperationException("UX check requires the native host.");
        var previous = navigation.Current;
        // Use the same desktop size as design captures before checking the scale.
        // Startup restores the window to the runner work area, which can be smaller.
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280,940));await Task.Delay(100);
        await audio.StopAsync();await audio.ClearMediaAsync();PracticeRestNotice.IsOpen=false;
        Navigate(new("28"));RefreshState();await Task.Delay(60);
        bool quietSettings = PracticeRow.Visibility==Visibility.Collapsed && MediaPanel.Visibility==Visibility.Collapsed;
        bool navigationLabels = ShellRoot.ActualWidth<900 || Navigation.DisplayMode==NavigationViewDisplayMode.Expanded && Navigation.IsPaneOpen;
        Navigate(new("02"));RefreshState();await Task.Delay(60);
        double liveFooter=MediaPanel.ActualHeight;double liveClientHeight=ShellRoot.ActualHeight;
        var diagnostics=Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR");
        if(!string.IsNullOrEmpty(diagnostics)){Directory.CreateDirectory(Path.Combine(diagnostics,"screens"));await CaptureNativeViewAsync("screens/ux-live.png");}
        double liveScaleBottom=liveTunerNeedle!.TransformToVisual(ShellRoot).TransformPoint(new Windows.Foundation.Point(0,liveTunerNeedle.ActualHeight)).Y;
        bool visibleScale=liveTunerNeedle.ActualWidth>150 && liveScaleBottom<=ShellRoot.ActualHeight-MediaPanel.ActualHeight-24;
        bool focusedLive = PracticeStart.Visibility==Visibility.Visible && PracticePause.Visibility==Visibility.Collapsed && PracticeEnd.Visibility==Visibility.Collapsed && liveStart is {MinHeight:>=40} && liveMonitor is not null && !liveMonitor.IsEnabled;
        bool compactLive = MediaTransportRow.Visibility==Visibility.Collapsed && MediaSeekRow.Visibility==Visibility.Collapsed && liveFooter<90;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(640,720));await Task.Delay(60);
        bool narrow = ShellRoot.ActualWidth>0 && WorkspaceContent.ActualWidth<=ShellRoot.ActualWidth && liveStart!.ActualWidth>0;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280,940));
        await LoadScoreAsync(Path.Combine(AppContext.BaseDirectory,"fixtures","notation-phrases.gp"));
        Navigate(new("20"));RefreshState();await Task.Delay(60);
        bool media = MediaTransportRow.Visibility==Visibility.Visible && MediaSeekRow.Visibility==Visibility.Visible && PlayMediaButton.IsEnabled && PlayMediaButton.Label=="Přehrát";
        Navigate(previous);
        bool passed=quietSettings && navigationLabels && focusedLive && compactLive && visibleScale && narrow && media;
        WriteShellDiagnostic("ux-layout.json",new{result=passed?"passed":"failed",idleSettingsHasNoPlayerOrPracticeBar=quietSettings,desktopNavigationShowsLabels=navigationLabels,liveHasPrimarySetupAndRelevantActions=focusedLive,liveFooterHeightDip=liveFooter,liveTuningScaleVisible=visibleScale,liveTuningScaleBottomDip=liveScaleBottom,clientHeightDip=liveClientHeight,liveScaleHeightDip=liveTunerNeedle.ActualHeight,liveScaleWidthDip=liveTunerNeedle.ActualWidth,emptyMediaDoesNotOccupyLive=compactLive,narrowWorkspaceFits=narrow,loadedMediaHasAccessibleTransport=media,manualUsabilityVerified=false});
        if(!passed)throw new InvalidOperationException("Workspace UX layout/state regression failed.");
    }
}
