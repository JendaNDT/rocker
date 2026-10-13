// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.AppCore;
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private string? desktopDiagnosticProject;
    private string? desktopDiagnosticRecording;
    private async Task VerifyDesktopInteractionsAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);
        if(audio is null || services.Storage is null)throw new InvalidOperationException("Desktop checks require application services.");
        var checks=new List<string>();void Check(bool pass,string name){if(!pass)throw new InvalidOperationException("Desktop interaction: "+name);checks.Add(name);}
        await audio.StopAsync();await audio.ClearMediaAsync();if(services.Transport.Owner is{ } oldOwner)services.Transport.Release(oldOwner);
        var fixture=await CreateWorkspaceWaveformFixtureAsync();string path=OwnedPaths.Resolve(userFiles.Root,fixture.RelativePath);
        desktopDiagnosticRecording=fixture.Id;
        await OpenDesktopSongAsync(path,"První syntetický zdroj",null);var first=OpenDesktop(new("07"),DesktopDocumentKey.ForCanonicalFile("07",path));
        using(documentContext.Enter(first)){await ClaimDesktopSourceAsync(first);Check(audio.GetTransport().Ready!=0 && audio.GetTransport().Playing==0,"explicit-source-preparation-remains-paused");}
        ulong ticket=await audio.GetMediaSourceTicketAsync();
        await OpenDesktopRecordingAsync(fixture);var second=OpenDesktop(new("14",fixture.Id));
        await Task.Delay(100);ShellRoot.UpdateLayout();
        using(documentContext.Enter(second))
        {
            Check(desktopDocuments[second].Content is Desktop.DocumentSurface && recordingWaveform!.ActualHeight>=180,"recording-editor-uses-a-bounded-resizable-waveform");
            var bottom=recordingRangeB!.TransformToVisual(ShellRoot).TransformPoint(new Windows.Foundation.Point(0,recordingRangeB.ActualHeight));
            Check(recordingRangeA!.ActualWidth>0 && recordingRangeB.ActualWidth>0 && bottom.Y<=ShellRoot.ActualHeight,"recording-selection-a-and-b-remain-in-the-visible-editor");
        }
        using(documentContext.Enter(second)){Check(recordingPeaks.Length==1024 && RecordingWaveformAvailable(),"recording-inspection-works-without-source-takeover");SetRecordingRange(.25,1);Check(recordingRangeA!.Value==.25 && recordingRangeB!.Value==1,"offline-recording-selection-retains-source-seconds");}
        Check(services.Transport.Owner==first && await audio.GetMediaSourceTicketAsync()==ticket,"opening-another-document-preserves-adopted-native-source");
        services.Transport.Capturing=true;
        bool refused=false;try{await services.Transport.TakeAsync(second,()=>Task.CompletedTask);}catch(InvalidOperationException){refused=true;}finally{services.Transport.Capturing=false;}
        Check(refused && services.Transport.Owner==first && await audio.GetMediaSourceTicketAsync()==ticket,"capture-conflict-preserves-source-and-owner");
        var projectA=new ProjectManifest(1,Guid.NewGuid().ToString("D"),"Projekt A",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,48000,[]);var projectB=projectA with{Id=Guid.NewGuid().ToString("D"),Name="Projekt B"};
        desktopDiagnosticProject=projectA.Id;var media=await services.Storage.Projects.AttachMediaAsync(projectA.Id,new(fixture.RelativePath,fixture.Sha256,fixture.Bytes));
        projectA=projectA with{Tracks=[new(Guid.NewGuid().ToString("N"),"Kytara · syntetická ukázka",media,fixture.Sha256,fixture.DurationSec,SourceSampleRate:48000,SourceFrames:96000),new(Guid.NewGuid().ToString("N"),"Druhá stopa · ukázka posunu",media,fixture.Sha256,fixture.DurationSec,Gain:.7,OffsetFrames:48000,SourceSampleRate:48000,SourceFrames:96000)]};
        await services.Storage.Projects.SaveAsync(projectA);await services.Storage.Projects.SaveAsync(projectB);
        var a=OpenDesktop(new("15",projectA.Id));var b=OpenDesktop(new("15",projectB.Id));await LoadDesktopProjectAsync(a,projectA.Id);await LoadDesktopProjectAsync(b,projectB.Id);
        using(documentContext.Enter(a)){studioProject=projectA;studioUndo.Clear();RememberStudio();studioProject=projectA with{Name="Projekt A · editace"};studioZoom=42;}
        using(documentContext.Enter(b)){studioProject=projectB;studioUndo.Clear();studioZoom=87;}
        documentContext.Select(b);await ExecuteDesktopCommandAsync("file.save",a);
        Check((await services.Storage.Projects.LoadAsync(projectA.Id)).Project.Name=="Projekt A · editace" && (await services.Storage.Projects.LoadAsync(projectB.Id)).Project.Name=="Projekt B","save-command-uses-captured-document-after-focus-change");
        await ExecuteDesktopCommandAsync("edit.undo",a);Check((await services.Storage.Projects.LoadAsync(projectA.Id)).Project.Name=="Projekt A","undo-restores-only-the-originating-project");
        await ExecuteDesktopCommandAsync("edit.redo",a);Check((await services.Storage.Projects.LoadAsync(projectA.Id)).Project.Name=="Projekt A · editace" && (await services.Storage.Projects.LoadAsync(projectB.Id)).Project.Name=="Projekt B","redo-and-project-histories-remain-independent");
        DetachDesktopTab(desktopDocuments[a]);var host=floatingHosts[desktopDocuments[a]];MoveDesktopTab(desktopDocuments[b],host.Tabs,host);await Task.Delay(150);
        Check(host.Tabs.TabItems.Count==2 && HostWindow(a)==HostWindow(b),"real-document-window-hosts-a-group-of-tabs");
        using(documentContext.Enter(a))Check(DialogWindowHandle==WinRT.Interop.WindowNative.GetWindowHandle(host) && DialogRoot==host.Root.XamlRoot,"detached-document-resolves-its-own-hwnd-and-xaml-root");
        var dialogA=new ContentDialog{Title="Dokument A",Content="Kontrola vlastníka dialogu",CloseButtonText="Zavřít"};Task<ContentDialogResult> shownA;
        using(documentContext.Enter(a))shownA=ShowDialogAsync(dialogA);await Task.Delay(150);
        Check(dialogA.IsLoaded && dialogA.XamlRoot==host.Root.XamlRoot,"owned-content-dialog-opens-over-the-detached-document");
        dialogA.Hide();await shownA;
        host.Tabs.SelectedItem=desktopDocuments[b];await SaveDesktopLayoutAsync();var layout=new WorkspaceLayoutStore(userFiles).Load()!;
        Check(layout.Windows.Any(w=>w.Role==DesktopWindowRole.Document && w.Documents.Count==2 && w.SelectedDocumentIndex==1),"window-group-and-selected-tab-are-persisted");
        Check(layout.ViewPreferences!.Any(v=>v.Key.Equals(a.Key) && v.Zoom==42) && layout.ViewPreferences!.Any(v=>v.Key.Equals(b.Key) && v.Zoom==87),"project-view-preferences-are-persisted-per-document");
        await CloseDesktopTabAsync(desktopDocuments[b]);await CloseDesktopTabAsync(desktopDocuments[a]);await RestoreDesktopLayoutAsync();
        var restoredA=services.Documents.All.Single(s=>s.Key?.Equals(a.Key)==true);var restoredB=services.Documents.All.Single(s=>s.Key?.Equals(b.Key)==true);
        Check(HostWindow(restoredA)==HostWindow(restoredB) && HostWindow(restoredA) is Desktop.DesktopWindow restoredHost && ReferenceEquals(restoredHost.Tabs.SelectedItem,desktopDocuments[restoredB]),"restoration-recreates-one-window-with-the-saved-tab-group");
        using(documentContext.Enter(restoredA))Check(studioZoom==42,"restored-first-project-zoom");using(documentContext.Enter(restoredB))Check(studioZoom==87,"restored-second-project-zoom");
        Check(audio.GetTransport().Playing==0 && !audio.GetMeters().Monitoring,"layout-restoration-never-starts-playback-or-monitoring");
        AttachDesktopTab(desktopDocuments[restoredA]);AttachDesktopTab(desktopDocuments[restoredB]);
        var lesson=OpenDesktop(new("06",catalog!.Lessons[0].Id));services.Transport.SetGrader(lesson);var practice=practiceSession;practice.Start(MonotonicMs,DateTimeOffset.UtcNow);
        OpenDesktop(new("17"));Check(services.Transport.Grader==lesson && ReferenceEquals(practice,practiceSession) && practiceSession.Phase==PracticePhase.Running,"switching-document-retains-one-grader-and-practice-session");practice.End();services.Transport.SetGrader(null);
        int windowsBefore=desktopWindows.Count,documentsBefore=services.Documents.All.Count,hosts=services.Audio.HostsCreated;
        for(int i=0;i<12;++i){var cycle=OpenDesktop(new("21","cycle-"+i));var tab=desktopDocuments[cycle];DetachDesktopTab(tab);await Task.Delay(20);AttachDesktopTab(tab);await CloseDesktopTabAsync(tab);}
        Check(desktopWindows.Count==windowsBefore && services.Documents.All.Count==documentsBefore && services.Audio.HostsCreated==hosts,"repeated-open-transfer-close-releases-window-and-document-registries");
        var settings=OpenDesktop(new("29"));OpenDesktop(new("28"));Check(OpenDesktop(new("29"))==settings,"settings-categories-reuse-the-same-window-and-device-panel");
        var previousSettings=appSettings;appSettings=appSettings with{SetupStep=0};var wizard=ShowDesktopDialogAsync(new("31"));var deadline=DateTime.UtcNow.AddSeconds(5);while(desktopFirstRunDialog?.IsLoaded!=true && !wizard.IsCompleted && DateTime.UtcNow<deadline)await Task.Delay(25);
        Check(desktopFirstRunDialog?.IsLoaded==true,"first-run-opens-one-owned-desktop-dialog");
        var oldContent=desktopFirstRunDialog!.Content;SetSetupStep(1);Check(appSettings.SetupStep==1 && !ReferenceEquals(oldContent,desktopFirstRunDialog.Content),"first-run-advances-within-the-existing-dialog");DismissDesktopFirstRun();await wizard;appSettings=previousSettings;SaveSettings();
        await StopForPowerTransitionAsync();Check(audio.GetSnapshot().State==Rocker.Interop.AudioState.Stopped && !audio.GetMeters().Monitoring && services.Transport.Grader is null,"one-power-transition-stops-the-shared-session-in-all-windows");
        WriteShellDiagnostic("desktop-interactions.json",new{result="passed",checks=checks.Count,scenarios=checks,sourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA"),actualWindows=true,syntheticAudioFiles=true,physicalAudioVerified=false,physicalPickerInteractionVerified=false});
    }
}
