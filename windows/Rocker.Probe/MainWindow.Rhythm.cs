// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Interop;
using Rocker.Probe.Controls;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private ProgressData progressData=ProgressRules.Empty;
    private InstrumentProfile instrumentProfile=InstrumentProfile.Default;
    private PowerChordExercise? powerExercise;private GallopExercise? gallopExercise;
    private string? rhythmView,rhythmExerciseId;private ulong rhythmTicket,rhythmSavedRounds;
    private int rhythmUiGeneration;private readonly Dictionary<string,List<Control>> rhythmEditors=[];
    private bool rhythmRunning,rhythmOperation;private Task? rhythmStopTask;
    private readonly Dictionary<string,TextBlock> rhythmReadings=[],rhythmTargets=[];
    private readonly Dictionary<string,ChordDiagram> rhythmDiagrams=[];
    private readonly Dictionary<string,ProgressBar> rhythmRoundBars=[];private readonly Dictionary<string,string> rhythmChordKeys=[];private readonly Dictionary<string,TextBlock> rhythmCells=[];
    private void InitializeTraining(IReadOnlyList<DataDocument> documents)
    {
        progressData=documents.FirstOrDefault(d=>d.Kind=="progress" && d.Id=="default") is { } progress?UserData.Read<ProgressData>(progress.Value):ProgressRules.Empty;
        instrumentProfile=documents.FirstOrDefault(d=>d.Kind=="instrument" && d.Id=="default") is { } profile?UserData.Read<InstrumentProfile>(profile.Value):InstrumentProfile.Default;
        rhythmRunning=false;rhythmView=null;rhythmTicket=0;rhythmEditors.Clear();surgicalListening=false;surgicalState=null;rhythmReadings.Clear();rhythmTargets.Clear();rhythmDiagrams.Clear();rhythmChordKeys.Clear();rhythmRoundBars.Clear();rhythmCells.Clear();palmPickup=appSettings.PalmMute?.Pickup??"bridge";ResetPalmRuntime();ResetLibraryGames();
    }
    private void RecordTempo(string id,double bpm)
    {
        if(!persistenceReady || !double.IsFinite(bpm) || bpm is <40 or >240 || progressData.BpmRecords.GetValueOrDefault(id)>=bpm)return;
        var records=new Dictionary<string,double>(progressData.BpmRecords){[id]=bpm};progressData=progressData with{BpmRecords=records};SaveProgressAndAchievements();
    }
    private FrameworkElement BuildRhythmTrainer(string view,string? id)
    {
        bool power=view=="10";var panel=Stack();panel.Children.Add(Text(power?"Power chord · přesuny v tempu":"Gallop · rytmická buňka"));
        var exercises=power?catalog!.Items("powerChordExercises").Select(UserData.Read<PowerChordExercise>).Select(e=>new ListEntry(e.Id,e.Name,e.Description)).ToArray():catalog!.Items("gallopExercises").Select(UserData.Read<GallopExercise>).Select(e=>new ListEntry(e.Id,e.Name,e.Description)).ToArray();
        var selector=new ComboBox{Header="Cvičení",ItemsSource=exercises,DisplayMemberPath="Title"};var tempo=new NumberBox{Header="Tempo BPM",Minimum=40,Maximum=240,Value=power?70:90,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};var speed=new CheckBox{Content="Zvýšit tempo o 5 % po dokončeném kole",IsChecked=false};var countIn=new ComboBox{Header="Předtakt",ItemsSource=new[]{"Bez předtaktu","1 takt","2 takty"},SelectedIndex=1};
        selector.SelectionChanged+=async(_,_)=>{if(selector.SelectedItem is not ListEntry selected)return;if(rhythmRunning)await StopRhythmUiAsync();if(power){powerExercise=UserData.Read<PowerChordExercise>(catalog!.Find("powerChordExercises",selected.Id)!.Value);tempo.Value=powerExercise.StartBpm;}else{gallopExercise=UserData.Read<GallopExercise>(catalog!.Find("gallopExercises",selected.Id)!.Value);tempo.Value=gallopExercise.StartBpm;}ShowRhythmChord(view,0);};
        panel.Children.Add(selector);panel.Children.Add(tempo);panel.Children.Add(speed);panel.Children.Add(countIn);var target=Text("Vyber cvičení.");target.FontSize=28;rhythmTargets[view]=target;panel.Children.Add(target);var diagram=new ChordDiagram();rhythmDiagrams[view]=diagram;panel.Children.Add(diagram);var reading=Text("Zastaveno.",true);rhythmReadings[view]=reading;panel.Children.Add(reading);
        rhythmEditors[view]=[selector,tempo,speed,countIn];
        var bar=new ProgressBar{Minimum=0,Maximum=100,Value=0};rhythmRoundBars[view]=bar;panel.Children.Add(bar);if(!power){var cell=Text("",true);rhythmCells[view]=cell;panel.Children.Add(cell);}
        var start=new Button{Content="Začít kolo"};start.Click+=async(_,_)=>
        {
            if(rhythmOperation || audio is null || !double.IsFinite(tempo.Value))return;rhythmOperation=true;start.IsEnabled=false;
            try{if(deviceSelectionUnavailable)throw new InvalidOperationException("Vyber dostupné výstupní zařízení.");await StopRhythmUiAsync();var generation=rhythmUiGeneration;await audio.PauseAsync();var tuning=CurrentTuning().Strings.Select(s=>s.Midi).ToArray();IReadOnlyList<TeachingNote> notes;double pattern,round;
                if(power){var exercise=powerExercise??throw new InvalidOperationException("Vyber cvičení.");notes=RhythmPreview.Power(exercise,tuning);pattern=round=RhythmTrainers.RoundBeats(exercise);rhythmExerciseId=exercise.Id;}else{var exercise=gallopExercise??throw new InvalidOperationException("Vyber cvičení.");notes=RhythmPreview.Gallop(exercise,tuning);pattern=exercise.Cell.Sum();round=pattern*RhythmTrainers.CellsPerRound;rhythmExerciseId=exercise.Id;}
                var chordIds=power?powerExercise!.Sequence:new[]{gallopExercise!.ChordId};foreach(var chordId in chordIds){var chord=MusicShapes.GetPowerChord(chordId,tuning);if(chord is null || RhythmPreview.Shape(chord,tuning) is null)throw new InvalidOperationException("Pro toto ladění nemáme vhodný hmat vybraného cvičení. Zvol jiné ladění nebo cvičení.");}
                rhythmView=view;rhythmSavedRounds=0;rhythmTicket=await audio.StartRhythmAsync(notes.Select(n=>new PreviewNote(n.Start,n.Duration,TunerFrequency(n.Midi),n.Volume)).ToArray(),pattern,round,(uint)Music.ClampBpm(tempo.Value),speed.IsChecked==true,(uint)Math.Max(0,countIn.SelectedIndex)*4);if(generation!=rhythmUiGeneration || navigation.Current.ViewId!=view){await audio.StopRhythmAsync();return;}rhythmRunning=true;selector.IsEnabled=tempo.IsEnabled=speed.IsEnabled=countIn.IsEnabled=false;
            }catch(Exception error){ShowError(error);}finally{rhythmOperation=false;start.IsEnabled=true;}
        };panel.Children.Add(start);
        var stop=new Button{Content="Zastavit kolo"};stop.Click+=async(_,_)=>{await StopRhythmUiAsync();selector.IsEnabled=tempo.IsEnabled=speed.IsEnabled=countIn.IsEnabled=true;};panel.Children.Add(stop);
        var listen=new Button{Content="Poslechnout aktuální power chord"};listen.Click+=async(_,_)=>{await StopRhythmUiAsync();selector.IsEnabled=tempo.IsEnabled=speed.IsEnabled=countIn.IsEnabled=true;var shape=CurrentRhythmChord(view,0);if(shape is not null)await PlayTeachingAsync(MusicShapes.PowerChordMidiNotes(shape).Select(m=>new TeachingNote(0,1,m)).ToArray());};panel.Children.Add(listen);
        panel.Children.Add(Text("Rekord znamená absolvované tempo celého kola. Správnost akordu ani přesnost tvých úhozů tento trenažér automaticky nehodnotí. Doprovod a změny tempa řídí zvukové jádro.",true));panel.Children.Add(BuildRecordingControls());
        var amp=new Button{Content="Aparát a efekty"};amp.Click+=(_,_)=>Navigate(new("13"));panel.Children.Add(amp);selector.SelectedItem=exercises.FirstOrDefault(e=>e.Id==id)??exercises[0];return Card(panel);
    }
    private PowerChord? CurrentRhythmChord(string view,double elapsed)
    {
        string? id=view=="10" && powerExercise is { } power?power.Sequence[(int)(Math.Floor(elapsed/power.BeatsPerChord)%power.Sequence.Count)]:gallopExercise?.ChordId;
        return id is null?null:MusicShapes.GetPowerChord(id,CurrentTuning().Strings.Select(s=>s.Midi).ToArray());
    }
    private void ShowRhythmChord(string view,double elapsed)
    {
        var chord=CurrentRhythmChord(view,elapsed);if(chord is null || !rhythmTargets.TryGetValue(view,out var target))return;var key=chord.Name+"/"+CurrentTuning().Id+"/"+appSettings.Handedness;if(rhythmChordKeys.GetValueOrDefault(view)==key)return;rhythmChordKeys[view]=key;var shape=RhythmPreview.Shape(chord,CurrentTuning().Strings.Select(s=>s.Midi).ToArray());target.Text=chord.Name+" · "+(chord.RootStringIndex==0?"6.":"5.")+" struna, pražec "+chord.RootFret;
        if(shape is { } valid){rhythmDiagrams[view].Visibility=Visibility.Visible;rhythmDiagrams[view].ShowChord(valid,appSettings.Handedness=="left");}else{rhythmDiagrams[view].Visibility=Visibility.Collapsed;target.Text+=" · běžný hmat pro toto ladění není vhodný";}
    }
    private async Task StopRhythmUiAsync()
    {
        ++rhythmUiGeneration;rhythmRunning=false;if(audio is null)return;
        if(rhythmStopTask is not null){await rhythmStopTask;return;}
        async Task Stop()
        {
            try{await audio.StopRhythmAsync();for(int wait=0;wait<50;++wait){var value=audio.GetRhythm();if(!value.Active || value.Token!=rhythmTicket)break;await Task.Delay(10);}StoreRhythmRecord(audio.GetRhythm());}
            catch(Exception error){if(!closed)ShowError(error);}
            finally{if(!rhythmRunning)foreach(var controls in rhythmEditors.Values)foreach(var control in controls)control.IsEnabled=true;}
        }
        rhythmStopTask=Stop();try{await rhythmStopTask;}finally{rhythmStopTask=null;}
    }
    private void StoreRhythmRecord(RhythmSnapshot value)
    {
        if(value.Token!=rhythmTicket || rhythmExerciseId is null || value.Rounds<=rhythmSavedRounds || value.LastCompletedBpm<40)return;
        rhythmSavedRounds=value.Rounds;RecordTempo(rhythmExerciseId,value.LastCompletedBpm);
    }
    private void RefreshRhythm()
    {
        if(audio is null || rhythmView is null)return;var value=audio.GetRhythm();if(value.Token!=rhythmTicket)return;StoreRhythmRecord(value);
        if(rhythmReadings.TryGetValue(rhythmView,out var reading)){reading.Text=value.State==1?"Předtakt…":$"{value.Bpm} BPM · dokončená kola {value.Rounds} · "+(value.State==2?"Doprovod běží.":"Zastaveno.");}
        if(value.State==2 && navigation.Current.ViewId==rhythmView){if(rhythmView=="19")RefreshRiff(value);else ShowRhythmChord(rhythmView,value.ElapsedBeats);if(rhythmRoundBars.TryGetValue(rhythmView,out var bar))bar.Value=100*(value.ElapsedBeats%value.RoundBeats)/value.RoundBeats;if(rhythmView=="11" && gallopExercise is { } exercise && rhythmCells.TryGetValue("11",out var cell)){double phase=value.ElapsedBeats%exercise.Cell.Sum(),edge=0;int active=0;for(int i=0;i<exercise.Cell.Count;++i){edge+=exercise.Cell[i];if(phase<edge){active=i;break;}}cell.Text="Rytmická buňka: "+string.Join(" · ",exercise.Cell.Select((beats,i)=>(i==active?"[":"")+beats.ToString("0.##")+(i==active?"]":"")))+" doby";}}
        if(!value.Active){rhythmRunning=false;foreach(var controls in rhythmEditors.Values)foreach(var control in controls)control.IsEnabled=true;}
    }
    private string InstrumentFingerprint()=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(instrumentProfile,UserData.Json))));
    private string PalmDeviceFingerprint()=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(appSettings.AudioDevice,UserData.Json))));
}
