// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Interop;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private SurgicalState? surgicalState { get => documentContext.Current.Get<SurgicalState?>("surgicalState", () => default); set => documentContext.Current.Set("surgicalState",value); }
    private SurgicalNoteGate surgicalGate { get => documentContext.Current.Get<SurgicalNoteGate>("surgicalGate", () => new()); }
    private bool surgicalListening { get => documentContext.Current.Get<bool>("surgicalListening", () => default); set => documentContext.Current.Set("surgicalListening",value); }
    private bool surgicalStartOperation { get => documentContext.Current.Get<bool>("surgicalStartOperation", () => default); set => documentContext.Current.Set("surgicalStartOperation",value); }
    private uint surgicalEpoch { get => documentContext.Current.Get<uint>("surgicalEpoch", () => default); set => documentContext.Current.Set("surgicalEpoch",value); }
    private TextBlock? surgicalReading { get => documentContext.Current.Get<TextBlock?>("surgicalReading", () => default); set => documentContext.Current.Set("surgicalReading",value); }
    private TextBlock? surgicalTarget { get => documentContext.Current.Get<TextBlock?>("surgicalTarget", () => default); set => documentContext.Current.Set("surgicalTarget",value); }
    private TextBlock? surgicalProgress { get => documentContext.Current.Get<TextBlock?>("surgicalProgress", () => default); set => documentContext.Current.Set("surgicalProgress",value); }
    private ListView? surgicalNotes { get => documentContext.Current.Get<ListView?>("surgicalNotes", () => default); set => documentContext.Current.Set("surgicalNotes",value); }
    private Button? surgicalStart { get => documentContext.Current.Get<Button?>("surgicalStart", () => default); set => documentContext.Current.Set("surgicalStart",value); }
    private Button? surgicalStop { get => documentContext.Current.Get<Button?>("surgicalStop", () => default); set => documentContext.Current.Set("surgicalStop",value); }
    private PlayedNoteObservation SurgicalObservation(AnalysisSnapshot value)
    {
        int? midi=value.PitchDetected?Music.FrequencyToNote(value.Frequency*440/(appSettings.ReferenceA??440))?.Midi:null;
        return new(value.Epoch,value.Frame,value.SampleRate,midi,value.ActiveInput && !value.Stale,!value.PitchDetected && (value.Rms*3<appSettings.NoiseGate || value.Rms<1e-7),value.HitCount,value.OnsetFrame,value.WindowSeconds);
    }
    private FrameworkElement BuildSurgical(string? id)
    {var capturedDocument = documentContext.Current;
        var panel=Stack();panel.Children.Add(Text("Přesný trénink · jednotlivé tóny"));
        var exercises=catalog!.Items("surgicalExercises").Select(UserData.Read<SurgicalExercise>).Concat(catalog.Items("riffs").Select(UserData.Read<PracticeRiff>).Select(r=>LibraryPractice.RiffExercise(catalog,r)).OfType<SurgicalExercise>()).ToArray();var choose=new ComboBox{Header="Cvičení",DisplayMemberPath="Name",ItemsSource=exercises};
        choose.SelectionChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(choose.SelectedItem is SurgicalExercise exercise){surgicalListening=false;if(surgicalState?.Exercise.Id!=exercise.Id)surgicalState=SurgicalEngine.Start(exercise);UpdateSurgical();}};panel.Children.Add(choose);
        surgicalTarget=Text("Vyber cvičení.");surgicalTarget.FontSize=32;panel.Children.Add(surgicalTarget);surgicalProgress=Text("",true);panel.Children.Add(surgicalProgress);surgicalReading=Text("Vyber vstup kytary a spusť hodnocení.",true);panel.Children.Add(surgicalReading);
        surgicalNotes=new ListView{Header="Tóny a chybová místa",SelectionMode=ListViewSelectionMode.Single};panel.Children.Add(surgicalNotes);
        surgicalStart=new Button{Content="Spustit / pokračovat v hodnocení"};surgicalStart.Click+=async(_,_)=>
        {using var documentScope = documentContext.Enter(capturedDocument);
            if(surgicalStartOperation || audio is null || surgicalState is null || surgicalState.Phase==SurgicalPhase.Complete)return;var exerciseId=surgicalState.Exercise.Id;surgicalStartOperation=true;UpdateSurgical();
            try{await ClaimDesktopGraderAsync();if(exerciseId.StartsWith("riff:",StringComparison.Ordinal) && catalog!.ResolveRiff(exerciseId[5..]) is { } riff && riff.GetProperty("tuning").GetString()!=CurrentTuning().Id)throw new InvalidOperationException("Vybraný riff vyžaduje jiné ladění; přepni nastavení a nalaď kytaru.");if(deviceSelectionUnavailable || appSettings.AudioDevice is not{InputChannel:>=0})throw new InvalidOperationException("Vyber skutečný vstup kytary v nastavení zvuku.");await audio.StartLiveAsync();var observed=audio.GetAnalysis();for(int wait=0;wait<50 && (!observed.ActiveInput || observed.Stale || observed.Epoch!=audio.GetClock().Epoch);++wait){await Task.Delay(20);observed=audio.GetAnalysis();}if(!observed.ActiveInput || observed.Stale || observed.Epoch!=audio.GetClock().Epoch)throw new InvalidOperationException("Vstup zatím nedodává aktuální měření. Zkus hodnocení znovu.");if(CurrentTarget.ViewId!="09" || surgicalState?.Exercise.Id!=exerciseId)return;surgicalEpoch=observed.Epoch;surgicalGate.Reset(SurgicalObservation(observed));surgicalListening=true;UpdateSurgical();}catch(Exception error){ShowError(error);}finally{surgicalStartOperation=false;UpdateSurgical();}
        };panel.Children.Add(surgicalStart);
        surgicalStop=new Button{Content="Pozastavit hodnocení"};surgicalStop.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);surgicalListening=false;UpdateSurgical();};panel.Children.Add(surgicalStop);
        var restart=new Button{Content="Začít cvičení znovu"};restart.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);surgicalListening=false;if(surgicalState is not null)surgicalState=SurgicalEngine.Start(surgicalState.Exercise);UpdateSurgical();};panel.Children.Add(restart);
        var listen=new Button{Content="Poslechnout frázi / problémové místo"};listen.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(surgicalState is null)return;surgicalListening=false;UpdateSurgical();var notes=surgicalState.Phase==SurgicalPhase.Drill?SurgicalEngine.DrillNotes(surgicalState):surgicalState.Exercise.Notes.ToArray();await PlayTeachingAsync(notes.Take(128).Select((midi,i)=>new TeachingNote(i*.35,.3,midi)).ToArray());};panel.Children.Add(listen);
        panel.Children.Add(Text("Hodnocení rozlišuje pomalé jednotlivé tóny a nové úhozy. Jeden držený tón se počítá jednou; pro opakování stejného tónu jej odděl tichem nebo novým rozpoznaným úhozem. Rychlé hraní přesahující měřicí okno trénuj ve smyčce skladby.",true));
        var songMode=new Button{Content="Trénovat A/B úsek skladby bez tónového hodnocení"};songMode.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);Navigate(new("07"));};panel.Children.Add(songMode);panel.Children.Add(BuildRecordingControls());
        var selected=exercises.FirstOrDefault(e=>e.Id==id)??exercises.FirstOrDefault(e=>e.Id==surgicalState?.Exercise.Id)??exercises[0];choose.SelectedItem=selected;UpdateSurgical();return Card(panel);
    }
    private void UpdateSurgical()
    {
        if(surgicalState is null || surgicalTarget is null)return;var state=surgicalState;var target=SurgicalEngine.CurrentTarget(state);surgicalTarget.Text=target is int midi?"Zahraj "+NoteLabel(midi):"Cvičení zvládnuto";
        surgicalProgress!.Text=state.Phase==SurgicalPhase.Run?$"Průchod · {state.Position} / {state.Exercise.Notes.Count} · chyby {state.ErrorCounts.Sum()}":state.Phase==SurgicalPhase.Drill?$"Drill · pozice {state.DrillPosition+1} / {state.DrillLength} · čistá opakování {state.CleanReps} / {SurgicalEngine.RequiredCleanReps}":"Hotovo. Původní chybová místa zůstávají v přehledu.";
        surgicalReading!.Text=surgicalListening?"Poslouchám zvolený vstup kytary.":"Hodnocení je pozastavené. Čtení, poslech a nahrávání fungují samostatně.";
        surgicalNotes!.ItemsSource=state.Exercise.Notes.Select((midi,i)=>$"{i+1}: {NoteLabel(midi)} · chyby {state.ErrorCounts[i]}"+(state.Phase==SurgicalPhase.Drill && i>=state.DrillStart && i<state.DrillStart+state.DrillLength?" · drill":"")).ToArray();surgicalNotes.SelectedIndex=state.Phase==SurgicalPhase.Run?Math.Min(state.Position,state.Exercise.Notes.Count-1):state.Phase==SurgicalPhase.Drill?state.DrillStart+state.DrillPosition:-1;
        surgicalStart!.IsEnabled=!surgicalStartOperation && !surgicalListening && state.Phase!=SurgicalPhase.Complete;surgicalStop!.IsEnabled=surgicalListening;
    }
    private void RefreshSurgical(AnalysisSnapshot analysis)
    {
        if(!surgicalListening || surgicalState is null || CurrentTarget.ViewId!="09")return;
        if(analysis.Epoch!=surgicalEpoch || !analysis.ActiveInput || analysis.Stale){surgicalListening=false;UpdateSurgical();surgicalReading!.Text="Vstup se změnil nebo přestal dodávat data. Cvičení je zachované; spusť hodnocení znovu.";return;}
        if(surgicalGate.Observe(SurgicalObservation(analysis)) is not int midi)return;
        surgicalState=SurgicalEngine.FeedNote(surgicalState,midi);if(surgicalState.Phase==SurgicalPhase.Complete)surgicalListening=false;UpdateSurgical();
    }
    private void VerifySurgical()
    {
        Navigate(new("09","spider"));var exercise=new SurgicalExercise("ci-exercise","Kontrolní fráze","",[40,41,42,43]);var state=SurgicalEngine.Start(exercise);state=SurgicalEngine.FeedNote(state,44);foreach(var midi in exercise.Notes)state=SurgicalEngine.FeedNote(state,midi);surgicalState=state;UpdateSurgical();bool nativeDrill=surgicalProgress!.Text.Contains("Drill",StringComparison.Ordinal);int start=state.DrillStart,length=state.DrillLength;for(int round=0;round<3;++round)for(int i=0;i<length;++i)state=SurgicalEngine.FeedNote(state,exercise.Notes[start+i]);surgicalState=state;UpdateSurgical();bool pass=nativeDrill && surgicalTarget!.Text=="Cvičení zvládnuto" && !surgicalStart!.IsEnabled && state.Phase==SurgicalPhase.Complete && state.CleanReps==3 && surgicalNotes is not null && surgicalStart is not null;
        WriteShellDiagnostic("surgical-view.json",new{result=pass?"passed":"failed",nativeNoteList=true,wrongRunEntersDrill=true,cleanRepeats=state.CleanReps,physicalGuitarVerified=false});if(!pass)throw new InvalidOperationException("Native surgical view failed.");
    }
}
