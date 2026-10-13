// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
using Rocker.Interop;
using Rocker.Probe.Controls;

namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private async Task VerifyMetronomeVisualizationAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if (audio is null) throw new InvalidOperationException("Native clock unavailable.");
        var previous = metroConfig; string theme = selectedTheme;
        var checks = new Dictionary<string,bool>();
        void Check(string name,bool value) { checks[name]=value; if(!value)throw new InvalidOperationException("Metronome visualization: "+name); }
        MetronomeVisual Current()=>AuthoredElements(WorkspaceContent).OfType<MetronomeVisual>().Single();
        bool AboveFooter(FrameworkElement element)=>element.ActualHeight>0 && element.TransformToVisual(ShellRoot).TransformPoint(new Windows.Foundation.Point(0,element.ActualHeight)).Y<=MediaPanel.TransformToVisual(ShellRoot).TransformPoint(new Windows.Foundation.Point()).Y;
        try
        {
            await audio.StopAsync();await audio.ClearMediaAsync();metroEnabled=false;
            metroConfig=previous with{VisualMode="pendulum"};RefreshMetronomeControls();
            ApplyTheme("dark");ShellNotice.IsOpen=PracticeRestNotice.IsOpen=false;
            Navigate(new("04"));RefreshState();WorkspaceScroll.ChangeView(null,0,null,true);await Task.Delay(200);
            Check("standaloneInstrumentVisibleAtTop",Current().PendulumVisible && AboveFooter(Current()));
            await CaptureNativeViewAsync("screens/metronome-top-dark.png");
            var mode=AuthoredElements(WorkspaceContent).OfType<ComboBox>().Single(c=>c.Tag as string=="metronome-visual-mode");
            int revision=metroRevision;ulong frame=audio.GetClock().Frame;mode.SelectedIndex=0;
            Check("modeChangeDoesNotReconfigureOrStartSound",metroRevision==revision && audio.GetClock().Frame==frame && audio.GetSnapshot().State==AudioState.Stopped && !Current().PendulumVisible);
            await autosave!.FlushAsync();var persisted=await (await DataRepository.OpenAsync(userFiles)).SnapshotAsync();
            Check("displayChoiceSurvivesDatabaseReopen",UserData.Read<MetronomeConfig>(persisted.Documents.Single(d=>d.Kind=="metronome").Value).VisualMode=="dots");
            mode.SelectedIndex=1;
            ApplyTheme("light");await Task.Delay(150);await CaptureNativeViewAsync("screens/metronome-top-light.png");
            Navigate(new("02"));RefreshState();await Task.Delay(200);
            Check("liveInstrumentVisibleAtTop",Current().PendulumVisible && AboveFooter(Current()));
            await CaptureNativeViewAsync("screens/metronome-live.png");
            SetFocusMode(true);RefreshState();await Task.Delay(200);
            Check("focusKeepsInstrumentVisible",Current().PendulumVisible && AboveFooter(Current()) && liveTunerNeedle is not null && AboveFooter(liveTunerNeedle));
            await CaptureNativeViewAsync("screens/metronome-focus.png");
            SetFocusMode(false);ApplyTheme("dark");AppWindow.Resize(new Windows.Graphics.SizeInt32(640,720));Navigate(new("04"));RefreshState();WorkspaceScroll.ChangeView(null,0,null,true);await Task.Delay(200);
            await CaptureNativeViewAsync("screens/metronome-narrow.png");
            WriteShellDiagnostic("metronome-narrow-layout.json",new{instrumentBottom=Current().TransformToVisual(ShellRoot).TransformPoint(new Windows.Foundation.Point(0,Current().ActualHeight)).Y,footerTop=MediaPanel.TransformToVisual(ShellRoot).TransformPoint(new Windows.Foundation.Point()).Y,instrumentWidth=Current().ActualWidth,workspaceWidth=WorkspaceContent.ActualWidth});
            Check("narrowInstrumentComesBeforeEditors",AboveFooter(Current()) && Current().ActualWidth<=WorkspaceContent.ActualWidth);
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1280,940));
            var visual=new MetronomeVisual();var config=previous with{BeatsPerBar=3,AccentPattern=new[]{"accent","muted","normal"},VisualMode="pendulum"};
            var state=new MetronomeSnapshot{Flags=9,Beat=2,Bpm=100,Bar=0,Phase=.99};
            var clock=new ClockSnapshot{Flags=1,Beat=2,BeatPhase=0};
            visual.Render(state,clock,config,allowMotion:true);Check("nativeBeatAndPhaseDriveArm",visual.ActiveBeat==2 && Math.Abs(visual.Angle+28)<.001);
            double unchanged=visual.Angle;await Task.Delay(80);visual.Render(state,clock,config,allowMotion:true);Check("sameClockSnapshotDoesNotDrift",visual.Angle==unchanged);
            clock.BeatPhase=1;visual.Render(state,clock,config,allowMotion:true);double end=visual.Angle;
            clock.Beat=3;clock.BeatPhase=0;state.Beat=0;state.Bar=1;visual.Render(state,clock,config,allowMotion:true);
            Check("oddMeterBoundaryIsContinuous",Math.Abs(end-visual.Angle)<.001 && visual.ActiveBeat==0);
            state.Flags=11;state.Bar=-1;visual.Render(state,clock,config,allowMotion:true);
            Check("countInIsLabelledAndKeepsClockParity",visual.StatusText.Contains("Odpočet") && Math.Abs(visual.Angle-28)<.001);
            state.Flags=13;state.Bar=0;state.Beat=1;clock.BeatPhase=.25;visual.Render(state,clock,config,allowMotion:true);
            Check("silentBarsStillShowTiming",visual.Silent && visual.ActiveBeat==1 && visual.StatusText.Contains("Tichý takt") && Math.Abs(visual.Angle)>1);
            state.Flags=9;visual.Render(state,clock,config,allowMotion:true);Check("mutedBeatIsExplicit",visual.StatusText.Contains("bez kliku"));
            visual.Render(state,clock,config,allowMotion:false);Check("reducedMotionKeepsBeatWithoutSwing",visual.ActiveBeat==1 && visual.Angle==0 && visual.StatusText.Contains("bez animace"));
            clock.BeatPhase=double.NaN;visual.Render(state,clock,config,allowMotion:true);Check("invalidPhaseHasNoActiveBeat",visual.ActiveBeat==-1 && visual.Angle==0);
            clock.BeatPhase=.25;state.Beat=16;visual.Render(state,clock,config,allowMotion:true);Check("invalidBeatHasNoActiveBeat",visual.ActiveBeat==-1 && visual.Angle==0);
            state.Flags=0;state.Beat=0;visual.Render(state,clock,config,allowMotion:true);Check("disabledMetronomeRestsWhileOtherSoundRuns",visual.ActiveBeat==-1 && visual.Angle==0 && visual.StatusText=="Zastaveno");
            state.Flags=8;state.Beat=-1;visual.Render(state,clock,config,allowMotion:true,awaitingStart:true);Check("waitingDoesNotInventFirstBeat",visual.ActiveBeat==-1 && visual.StatusText.Contains("Čekám"));
            state.Flags=9;state.Beat=15;var sixteen=config with{BeatsPerBar=16,AccentPattern=Enumerable.Repeat("normal",16).ToArray(),VisualMode="dots"};visual.Render(state,clock,sixteen,allowMotion:true);
            Check("sixteenBeatsAndDotsModeAreSupported",visual.BeatCount==16 && visual.ActiveBeat==15 && !visual.PendulumVisible);
            visual.Configure(config with{VisualMode=null});Check("legacyUnsetModeDefaultsToPendulum",visual.PendulumVisible);
            // The demonstration is isolated from the live registry and cannot create audio.
            Navigate(new("04"));var demo=Stack();demo.Children.Add(new InfoBar{IsOpen=true,IsClosable=false,Title="Ukázka vizualizace",Message="Připravené snímky zvukových hodin pro kontrolu grafiky; žádný zvuk se nepřehrává.",Severity=InfoBarSeverity.Informational});demo.Children.Add(visual);WorkspaceContent.Content=new VintageHousing(demo,"ROCKER / METRONOM");
            state.Beat=0;state.Bar=0;state.Flags=9;clock.Beat=1;clock.BeatPhase=0;visual.Render(state,clock,config,allowMotion:true);await Task.Delay(100);await CaptureNativeViewAsync("screens/metronome-demo.png");
            for(int i=0;i<16;++i){clock.Beat=(ulong)(i/8);clock.BeatPhase=(i%8)/8.0;state.Beat=i/8;visual.Render(state,clock,config,allowMotion:true);await Task.Delay(25);await CaptureNativeViewAsync($"screens/metronome-frame-{i:00}.png",visual);}
            Check("visualizationNeverStartsAudio",audio.GetSnapshot().State==AudioState.Stopped && audioHostsCreated==1);
        }
        finally
        {
            SetFocusMode(false);AppWindow.Resize(new Windows.Graphics.SizeInt32(1280,940));metroConfig=previous;metroEnabled=false;QueueMetronome();await ApplyMetronomeAsync();await audio.StopAsync();ApplyTheme(theme);Navigate(new("01"));WorkspaceScroll.ChangeView(null,0,null,true);
            WriteShellDiagnostic("metronome-visualization.json",new{result=checks.Count==20 && checks.Values.All(v=>v)?"passed":"failed",checks,sharedAudioHostCount=audioHostsCreated,clockSource="native sample clock",noIndependentAnimationTimer=true,demonstrationUsesLabelledSnapshots=true,actualAudioOutputVerified=false,physicalHighContrastVerified=false});
        }
    }
}
