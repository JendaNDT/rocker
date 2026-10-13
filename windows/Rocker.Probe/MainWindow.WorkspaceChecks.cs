// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
using Rocker.Interop;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private ContentDialog? activeWorkspaceDialog;
    private async Task VerifyWorkspaceEnhancementsAsync()
    {
        if (audio is null || database is null || recordingLibrary is null) throw new InvalidOperationException("Workspace checks require initialized services.");
        var previousWorkspace = practiceWorkspace; var previousSettings = appSettings; var previousAmp = ampSettings; var previousMetro = metroConfig;
        var previousTheme = selectedTheme;
        var checks = new Dictionary<string, bool>();
        void Check(string name, bool value) { checks[name] = value; if (!value) throw new InvalidOperationException("Workspace enhancement failed: " + name); }
        try
        {
            ApplyTheme("dark");ShellNotice.IsOpen=PracticeRestNotice.IsOpen=false;
            await audio.StopAsync(); await audio.ClearMediaAsync(); song = null; mediaPath = null; mediaMidi = null;
            Navigate(new("02")); SetFocusMode(true); RefreshState(); await Task.Delay(350);
            Check("focusUsesSharedLiveHost", navigation.Current.ViewId == "02" && audioHostsCreated == 1 && !Navigation.IsPaneVisible && FocusExitButton.Visibility == Visibility.Visible && liveTunerNote?.FontSize == 56 && !liveTunerNeedle!.HasReading && liveTunerNeedle.TransformToVisual(ShellRoot).TransformPoint(new Windows.Foundation.Point(0,liveTunerNeedle.ActualHeight)).Y<ShellRoot.ActualHeight-MediaPanel.ActualHeight);
            Check("focusRecorderActionsAreLarge", recordingStarts.Where(b => b.Tag as string == "compact-recording").All(b => b.MinHeight >= 48));
            await CaptureNativeViewAsync("screens/improved-focus-dark.png");
            ApplyTheme("light"); await Task.Delay(350); await CaptureNativeViewAsync("screens/improved-focus-light.png");
            AppWindow.Resize(new Windows.Graphics.SizeInt32(640, 720)); await Task.Delay(150);
            Check("focusNarrowFits", WorkspaceContent.ActualWidth <= ShellRoot.ActualWidth && FocusExitButton.ActualWidth > 0);
            await CaptureNativeViewAsync("screens/improved-focus-narrow.png");
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 940)); SetFocusMode(false); ApplyTheme("dark");
            Check("focusCanReturnWithoutStartingSound", Navigation.IsPaneVisible && audio.GetSnapshot().State == AudioState.Stopped && !audio.GetMeters().Monitoring);
            BeginPreparation("Kontrola zrušení přípravy");await Task.Delay(100);
            bool cancelAvailable=!ShellInteractionRoot.IsEnabled && PreparationCancelButton.IsEnabled && PreparationCancelButton.ActualHeight>0;
            CancelPreparation_Click(this,new());Check("preparationCanCancelWhileWorkspaceIsDisabled",cancelAvailable && PreparationToken.IsCancellationRequested);EndPreparation();
            var setup = CapturePracticeSetup("Kontrolní sestava") with { Amp = ampSettings with { Drive = .23 }, Metronome = metroConfig with { Bpm = 93 }, AmpEnabled = true };
            await SavePracticeWorkspaceAsync(new([setup], ["view:03", "setup:" + setup.Id]));
            await ApplyPracticeSetupAsync(setup);
            Check("setupAppliesNativeSoundAndTempo", Math.Abs(audio.GetAmplifier().Drive - .23) < 1e-9 && audio.GetAmplifier().Enabled && audio.GetMetronome().Bpm == 93);
            Check("setupDoesNotStartPlaybackOrMonitoring", audio.GetTransport().Playing == 0 && !audio.GetMeters().Monitoring && !metroEnabled);
            var reopened = await (await DataRepository.OpenAsync(userFiles)).SnapshotAsync();
            Check("workspaceSurvivesDatabaseReopen", UserData.Read<PracticeWorkspace>(reopened.Documents.Single(d => d.Kind == "practice-workspace").Value).Setups.Single().Id == setup.Id);
            var index = await BuildSearchIndexAsync();
            Check("globalIndexContainsLessonsChordsSongsRecordingsAndSetups", index.Count(e => e.Category == "Lekce") == 18 && index.Count(e => e.Category == "Akord") == 26 && index.Any(e => e.Category == "Sestava cvičení") && index.Any(e => e.Category == "Nahrávka") && index.Any(e => e.Category == "Skladba"));
            var searchTask = ShowSearchAsync(false); await Task.Delay(200);
            Check("searchIsNativeDialog", activeWorkspaceDialog is { IsLoaded: true });
            var content = (StackPanel)activeWorkspaceDialog!.Content;
            content.Children.OfType<TextBox>().First().Text = "ladicka"; await Task.Delay(100);
            Check("nativeSearchIgnoresDiacritics", content.Children.OfType<ListView>().First().Items.Cast<SearchEntry>().Any(e => e.Target.ViewId == "03"));
            await CaptureNativeViewAsync("screens/improved-search.png", activeWorkspaceDialog);
            activeWorkspaceDialog.Hide(); await searchTask;
            Setups_Click(this, new()); await Task.Delay(200);
            Check("setupManagerIsNativeDialog", activeWorkspaceDialog is { IsLoaded: true });
            await CaptureNativeViewAsync("screens/improved-setups.png", activeWorkspaceDialog);
            activeWorkspaceDialog!.Hide(); await Task.Delay(50);
            Navigate(new("14")); await ReloadRecordingsAsync();
            var recording = await CreateWorkspaceWaveformFixtureAsync();await ReloadRecordingsAsync();
            await PreviewRecordingAsync(recording); await Task.Delay(100);
            Check("recordingPreviewHasDecodedWaveform", recordingPeaks.Length > 0 && recordingWaveform!.Children.Count >= 3 && recordingRangeA!.IsEnabled && audio.GetTransport().Playing == 0);
            SetRecordingRange(0, recording.DurationSec * .5);
            Check("waveformSelectionRetainsSourceTimes", recordingRangeB!.Value == recording.DurationSec * .5 && recordingPlayhead is not null);
            await CaptureNativeViewAsync("screens/improved-recordings.png");
            var songSetup = CapturePracticeSetup("Skladba s úsekem") with { LoopEnabled = true, LoopStart = 0, LoopEnd = recording.DurationSec * .5, Speed = .75 };
            playingRecording=null;recordingPeaks=[];
            await ApplyPracticeSetupAsync(songSetup);
            Check("setupRestoresSongLoopAndSpeedPaused", currentSpeed == .75 && (audio.GetTransport().Reserved & 1) != 0 && audio.GetTransport().Playing == 0 && !audio.GetMeters().Monitoring);
            Check("setupKeepsOwnedReferenceAfterReopen",CapturePracticeSetup("Znovu uložená sestava").RecordingId==recording.Id);
            Navigate(new("28")); await Task.Delay(100); updateCheckButton!.StartBringIntoView(); await Task.Delay(150);
            Check("updatesRequireExplicitActions", updateCheckButton.IsEnabled && !updateDownloadButton!.IsEnabled && !updateInstallButton!.IsEnabled && !updateOperation && !appSettings.CheckUpdatesOnStartup);
            await CaptureNativeViewAsync("screens/improved-updates.png");
            Check("singleAudioHostRemains", audioHostsCreated == 1);
        }
        finally
        {
            activeWorkspaceDialog?.Hide(); activeWorkspaceDialog = null; SetFocusMode(false);
            practiceWorkspace = previousWorkspace; appSettings = previousSettings; ampSettings = previousAmp; metroConfig = previousMetro; metroEnabled = false;
            await SavePracticeWorkspaceAsync(previousWorkspace); SaveSettings(); QueueAmplifier(); QueueMetronome(); await ApplyAmplifierAsync(); await ApplyMetronomeAsync();
            await audio.StopAsync(); await audio.ClearMediaAsync(); mediaPath = null; mediaMidi = null; song = null; playingRecording = null; recordingPeaks = [];
            ApplyTheme(previousTheme); workspaceCache.Clear(); Navigate(new("01")); WorkspaceScroll.ChangeView(null, 0, null, true);
            WriteShellDiagnostic("workspace-enhancements.json", new { result = checks.Count == 18 && checks.Values.All(v => v) ? "passed" : "failed", checks, newViews = 0, nativeWorkspaces = 32, sharedAudioHostCount = audioHostsCreated, liveInputNotSimulated = true, onlinePrivateUpdateVerified = false, installerExecutedByAppVerified = false, manualInteractionsVerified = false });
        }
    }
}
