// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Interop;
using Rocker.Probe.Controls;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private TextBlock? liveProcessedLabel,liveTunerNote,liveTunerCents;private ProgressBar? liveTunerNeedle;
    private LevelMeter? liveInputMeter,liveProcessedMeter,liveMixMeter;
    private TextBlock? liveState,liveDiagnostic;
    private CheckBox? liveMonitor;
    private double liveGain=0.15;
    private bool liveUpdating;
    private FrameworkElement BuildLive()
    {
        var panel=Stack();panel.Children.Add(Text("Live · kytara a poslech"));
        panel.Children.Add(Text("Zvuk běží ve společném nativním jádru a pokračuje při přepnutí obrazovky. Aparát a jeho presety sdílí stejný zvukový graf.",true));
        var devices=new Button{Content="Vybrat kartu a vstup kytary"};devices.Click+=(_,_)=>Navigate(new("29"));panel.Children.Add(devices);
        var start=new Button{Content="Spustit živý vstup"};start.Click+=LiveStart_Click;
        var stop=new Button{Content="Zastavit a ztlumit"};stop.Click+=Stop_Click;
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};buttons.Children.Add(start);buttons.Children.Add(stop);panel.Children.Add(buttons);
        var tuner=Stack();tuner.Children.Add(Text("Ladění · skutečný vstup"));liveTunerNote=Text("—");liveTunerNote.FontSize=64;liveTunerCents=Text("Žádný aktuálně měřený tón.",true);liveTunerNeedle=new ProgressBar{Minimum=0,Maximum=100,Value=50};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(liveTunerNeedle,"Live · odchylka v centech");tuner.Children.Add(liveTunerNote);tuner.Children.Add(liveTunerCents);tuner.Children.Add(liveTunerNeedle);var tune=new Button{Content="Ladění a referenční tóny"};tune.Click+=(_,_)=>Navigate(new("03"));tuner.Children.Add(tune);var left=Stack();left.Children.Add(Card(tuner));var right=Stack();
        right.Children.Add(BuildCompactAmplifier());
        right.Children.Add(BuildCompactMetronome());
        right.Children.Add(Card(BuildRecordingControls()));
        liveMonitor=new CheckBox{Content="Poslouchat kytaru ve sluchátkách (softwarový monitoring)",IsChecked=false};
        liveMonitor.Checked+=LiveMonitoring_Changed;liveMonitor.Unchecked+=LiveMonitoring_Changed;left.Children.Add(liveMonitor);
        var level=new Slider{Header="Hlasitost monitoringu",Minimum=0,Maximum=100,Value=liveGain*100};
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(level,"Hlasitost monitoringu");
        level.ValueChanged+=(_,args)=>{liveGain=args.NewValue/100;ApplyLiveMonitoring();};left.Children.Add(level);
        liveState=Text("Vyber vstup kytary, spusť Live a monitoring zapni výslovně.",true);left.Children.Add(liveState);
        liveInputMeter=new();liveProcessedMeter=new();liveMixMeter=new();
        left.Children.Add(Text("Suchý vstup"));left.Children.Add(liveInputMeter);
        liveProcessedLabel=Text("Za aparátem");left.Children.Add(liveProcessedLabel);left.Children.Add(liveProcessedMeter);
        left.Children.Add(Text("Výsledný mix"));left.Children.Add(liveMixMeter);panel.Children.Add(ResponsiveColumns(left,right));
        liveDiagnostic=Text("",true);panel.Children.Add(new Expander{Header="Podrobnosti zvuku a přebuzení",Content=liveDiagnostic});
        panel.Children.Add(Text("Kytara i sluchátka mají být připojené ke stejné kartě. Direct Monitor na M-Track je další cesta bez efektů; současný přímý a softwarový poslech může vytvořit dvojí zvuk.",true));
        return Card(panel);
    }
    private async void LiveStart_Click(object sender,RoutedEventArgs args)
    {
        if(audio is null || busy || powerTransition || deviceOperationRunning || closingWindow)return;
        if(deviceSelectionUnavailable || appSettings.AudioDevice is not {InputChannel:>=0}){ShowPersistenceError("Nejdřív vyber zvukovou kartu a konkrétní vstup kytary.");Navigate(new("29"));return;}
        try{++playbackIntent;SetBusy(true);await audio.StartLiveAsync();if(!closed && !closingWindow)RefreshState();}
        catch(Exception error){if(!closed)ShowError(error);}
        finally{if(!closed)SetBusy(false);}
    }
    private void LiveMonitoring_Changed(object sender,RoutedEventArgs args){if(!liveUpdating)ApplyLiveMonitoring();}
    private void ApplyLiveMonitoring()
    {
        if(audio is null || closed || closingWindow || powerTransition)return;
        try{audio.SetMonitoring(liveMonitor?.IsChecked==true,liveGain);}
        catch(Exception error){ShowError(error);}
    }
    private void RefreshLive(AudioSnapshot state)
    {
        if(liveInputMeter is null || audio is null)return;
        var meters=audio.GetMeters();bool running=state.State==AudioState.Running;
        liveInputMeter.SetLevel(meters.InputPeak,running && meters.HasInput);
        liveProcessedLabel!.Text=appSettings.AmpEnabled?"Za aparátem":"Za aparátem · bypass";
        liveProcessedMeter!.SetLevel(meters.ProcessedPeak,running && meters.HasInput);
        liveMixMeter!.SetLevel(meters.MixPeak,running);
        liveUpdating=true;liveMonitor!.IsChecked=meters.Monitoring;liveUpdating=false;
        liveState!.Text=running?(meters.HasInput?(meters.Monitoring?"Vstup běží a monitoring je zapnutý.":"Vstup běží. Monitoring je ztlumený."):"Zvuk běží bez vstupu kytary."):"Živý vstup je zastavený. Zařízení je uvolněné.";
        var reading=audio.GetAnalysis();var note=reading.ActiveInput && !reading.Stale && reading.PitchDetected?Rocker.Domain.Music.FrequencyToNote(reading.Frequency*440/(appSettings.ReferenceA??440)):null;liveTunerNote!.Text=note is null?"—":NoteLabel(note.Midi);if(note is null){liveTunerCents!.Text="Žádný aktuálně měřený tón.";liveTunerNeedle!.Value=50;}else{int cents=Rocker.Domain.Music.CentsBetween(reading.Frequency,TunerFrequency(note.Midi));liveTunerCents!.Text=$"{cents:+0;-0;0} centů · {CurrentTuning().Label}";liveTunerNeedle!.Value=Math.Clamp(50+cents,0,100);}
        var clock=audio.GetClock();
        liveDiagnostic!.Text=$"Společné hodiny: {clock.Frame:N0} vzorků · {clock.ElapsedSeconds:0.000} s · epocha {clock.Epoch} · DSP FIR {clock.DspLatencyFrames} vzorků\n"+$"Přebuzení vstupu: {meters.InputClips} · přebuzení výstupu: {meters.OutputClips} · neplatné vzorky: {meters.InvalidSamples}\nAnalytická fronta: {meters.DroppedAnalysisBlocks} zahozených bloků. Tón a kalibrace jsou v Ladičce.";
    }
}
