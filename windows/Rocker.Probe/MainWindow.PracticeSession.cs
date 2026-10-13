// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Rocker.Domain;
using Rocker.Interop;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private PracticeSession practiceSession=>services.Practice.Session;
    private ulong practiceAudioFrame { get=>services.Practice.AudioFrame; set=>services.Practice.AudioFrame=value; }
    private uint practiceAudioEpoch { get=>services.Practice.AudioEpoch; set=>services.Practice.AudioEpoch=value; }
    private double practiceLastFlush { get=>services.Practice.LastFlush; set=>services.Practice.LastFlush=value; }
    private double practicePreviewUntil { get=>services.Practice.PreviewUntil; set=>services.Practice.PreviewUntil=value; }
    private double practiceLastUiInteraction { get=>services.Practice.LastInteraction; set=>services.Practice.LastInteraction=value; }
    private static readonly HashSet<string> PracticeViews=["02","03","04","05","06","07","08","09","10","11","12","13","14","15","16","17","18","19","20","21","22"];
    private static double MonotonicMs=>System.Diagnostics.Stopwatch.GetTimestamp()*1000.0/System.Diagnostics.Stopwatch.Frequency;
    private void InitializePracticeTracking()
    {
        ShellRoot.AddHandler(UIElement.PointerPressedEvent,new PointerEventHandler((_,args)=>TrackPracticeInteraction(args.OriginalSource as DependencyObject)),true);
        ShellRoot.AddHandler(UIElement.KeyDownEvent,new KeyEventHandler((_,args)=>TrackPracticeInteraction(args.OriginalSource as DependencyObject)),true);
    }
    private void TrackPracticeInteraction(DependencyObject? element)
    {
        if(!PracticeViews.Contains(CurrentTarget.ViewId))return;if(desktopEnabled){practiceLastUiInteraction=MonotonicMs;return;}for(var current=element;current is not null;current=VisualTreeHelper.GetParent(current))if(ReferenceEquals(current,WorkspaceContent) || ReferenceEquals(current,NotationPanel) || ReferenceEquals(current,VideoPanel)){practiceLastUiInteraction=MonotonicMs;return;}
    }
    private void RefreshPractice(ClockSnapshot clock,TransportSnapshot transport,AnalysisSnapshot analysis)
    {
        double now=MonotonicMs;bool frame=clock.Running && clock.Epoch==practiceAudioEpoch && clock.Frame>practiceAudioFrame;practiceAudioEpoch=clock.Epoch;practiceAudioFrame=clock.Frame;
        bool input=frame && analysis.Epoch==clock.Epoch && analysis.ActiveInput && !analysis.Stale && double.IsFinite(analysis.AgeSeconds) && analysis.AgeSeconds<=.25 && analysis.Rms>appSettings.NoiseGate;
        bool listening=frame && (transport.Playing!=0 || now<practicePreviewUntil || analysis.ReferencePlaying || analysis.ConfirmationPlaying || CurrentTarget.ViewId=="16" && audio?.GetLoop().State is 3 or 4 or 5);
        bool interaction=PracticeViews.Contains(CurrentTarget.ViewId) && (now-practiceLastUiInteraction<=15000 || recentExerciseInteraction is { } recent && (DateTimeOffset.UtcNow-recent).TotalSeconds is >=0 and <=15);
        bool video=CurrentTarget.ViewId=="08" && videoPractice.Ready && videoPractice.State==1 && (DateTimeOffset.UtcNow-videoProgressObserved).TotalSeconds<=1;
        ObservePractice(now,DateTimeOffset.UtcNow,input || listening || interaction || video);
        FinishPracticeObservation(now);
    }
    private void RefreshPracticeWithoutAudio()
    {
        double now=MonotonicMs;bool interaction=PracticeViews.Contains(CurrentTarget.ViewId) && (now-practiceLastUiInteraction<=15000 || recentExerciseInteraction is { } recent && (DateTimeOffset.UtcNow-recent).TotalSeconds is >=0 and <=15);
        bool video=CurrentTarget.ViewId=="08" && videoPractice.Ready && videoPractice.State==1 && (DateTimeOffset.UtcNow-videoProgressObserved).TotalSeconds is >=0 and <=1;
        ObservePractice(now,DateTimeOffset.UtcNow,interaction || video);FinishPracticeObservation(now);
    }
    private void FinishPracticeObservation(double now){if(now-practiceLastFlush>=1000){practiceLastFlush=now;FlushPractice();}RefreshPracticeControls();}
    private void ObservePractice(double stamp,DateTimeOffset wall,bool active)
    {
        var result=practiceSession.Observe(stamp,wall,practiceTimeZone,active);if(result.Remind){PracticeRestNotice.IsOpen=true;PracticeRestNotice.Message="Cvičíš už 45 aktivních minut. Dopřej rukám krátkou pauzu; při bolesti hru přeruš.";}
    }
    private void FlushPractice()
    {
        if(!persistenceReady)return;var changes=practiceSession.Drain();if(changes.Count==0)return;var minutes=new Dictionary<string,double>(progressData.DailyMinutes);foreach(var(day,value) in changes)minutes[day]=minutes.GetValueOrDefault(day)+value;progressData=progressData with{DailyMinutes=minutes};SaveProgressAndAchievements();
    }
    private void SaveProgressAndAchievements()
    {
        if(!persistenceReady || catalog is null)return;var newly=ProgressRules.EvaluateAchievements(catalog,progressData,completedLessons).Except(progressData.UnlockedAchievements,StringComparer.Ordinal).ToArray();if(newly.Length>0){progressData=progressData with{UnlockedAchievements=progressData.UnlockedAchievements.Concat(newly).Distinct(StringComparer.Ordinal).ToArray()};if(!appSettings.ZenMode){PracticeRestNotice.IsOpen=true;PracticeRestNotice.Message="Nová odměna: "+string.Join(", ",catalog.Items("achievements").Where(a=>newly.Contains(a.GetProperty("id").GetString(),StringComparer.Ordinal)).Select(a=>a.GetProperty("title").GetString()));}}autosave!.Queue(UserData.Document("progress","default",progressData));
    }
    private void PracticeStart_Click(object sender,RoutedEventArgs args)
    {
        if(!persistenceReady)return;FlushPractice();if(practiceSession.Phase==PracticePhase.Paused)practiceSession.Resume();else if(practiceSession.Phase!=PracticePhase.Running)practiceSession.Start(MonotonicMs,DateTimeOffset.UtcNow);RefreshPracticeControls();
    }
    private void PracticePause_Click(object sender,RoutedEventArgs args){practiceSession.Pause();FlushPractice();RefreshPracticeControls();}
    private async void PracticeEnd_Click(object sender,RoutedEventArgs args){practiceSession.End();FlushPractice();try{if(autosave is not null)await autosave.FlushAsync();}catch(Exception){ShowPersistenceError("Čas cvičení se zatím nepodařilo uložit; zůstává v paměti.");}RefreshPracticeControls();}
    private void RefreshPracticeControls()
    {
        UpdateWakeLock();
        PracticeStart.IsEnabled=persistenceReady && practiceSession.Phase!=PracticePhase.Running;PracticeStart.Content=practiceSession.Phase==PracticePhase.Paused?"Pokračovat ve cvičení":"Zahájit cvičení";PracticePause.IsEnabled=practiceSession.Phase==PracticePhase.Running;PracticeEnd.IsEnabled=practiceSession.Phase is PracticePhase.Running or PracticePhase.Paused;PracticeStatus.Text=(practiceSession.Phase switch{PracticePhase.Running=>"Cvičení běží; počítá se jen aktivita",PracticePhase.Paused=>"Cvičení pozastavené",PracticePhase.Ended=>"Cvičení ukončené",_=>"Cvičení není zahájené"})+" · aktivně "+FormatTime(practiceSession.TotalMs/1000);
        RefreshWorkspaceChrome();
    }
}
