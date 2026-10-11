// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Interop;
using Rocker.Probe.Controls;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private TextBlock? liveProcessedLabel;
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
        panel.Children.Add(BuildCompactAmplifier());
        panel.Children.Add(BuildCompactMetronome());
        liveMonitor=new CheckBox{Content="Poslouchat kytaru ve sluchátkách (softwarový monitoring)",IsChecked=false};
        liveMonitor.Checked+=LiveMonitoring_Changed;liveMonitor.Unchecked+=LiveMonitoring_Changed;panel.Children.Add(liveMonitor);
        var level=new Slider{Header="Hlasitost monitoringu",Minimum=0,Maximum=100,Value=liveGain*100};
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(level,"Hlasitost monitoringu");
        level.ValueChanged+=(_,args)=>{liveGain=args.NewValue/100;ApplyLiveMonitoring();};panel.Children.Add(level);
        liveState=Text("Vyber vstup kytary, spusť Live a monitoring zapni výslovně.",true);panel.Children.Add(liveState);
        liveInputMeter=new();liveProcessedMeter=new();liveMixMeter=new();
        panel.Children.Add(Text("Suchý vstup"));panel.Children.Add(liveInputMeter);
        liveProcessedLabel=Text("Za aparátem");panel.Children.Add(liveProcessedLabel);panel.Children.Add(liveProcessedMeter);
        panel.Children.Add(Text("Výsledný mix"));panel.Children.Add(liveMixMeter);
        liveDiagnostic=Text("",true);panel.Children.Add(liveDiagnostic);
        panel.Children.Add(Text("Kytara i sluchátka mají být připojené ke stejné kartě. Direct Monitor na M-Track je další cesta bez efektů; současný přímý a softwarový poslech může vytvořit dvojí zvuk.",true));
        return Card(panel);
    }
    private async void LiveStart_Click(object sender,RoutedEventArgs args)
    {
        if(audio is null || busy || deviceOperationRunning || closingWindow)return;
        if(deviceSelectionUnavailable || appSettings.AudioDevice is not {InputChannel:>=0}){ShowPersistenceError("Nejdřív vyber zvukovou kartu a konkrétní vstup kytary.");Navigate(new("29"));return;}
        try{++playbackIntent;SetBusy(true);await audio.StartLiveAsync();if(!closed && !closingWindow)RefreshState();}
        catch(Exception error){if(!closed)ShowError(error);}
        finally{if(!closed)SetBusy(false);}
    }
    private void LiveMonitoring_Changed(object sender,RoutedEventArgs args){if(!liveUpdating)ApplyLiveMonitoring();}
    private void ApplyLiveMonitoring()
    {
        if(audio is null || closed || closingWindow)return;
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
        var clock=audio.GetClock();
        liveDiagnostic!.Text=$"Společné hodiny: {clock.Frame:N0} vzorků · {clock.ElapsedSeconds:0.000} s · epocha {clock.Epoch} · DSP FIR {clock.DspLatencyFrames} vzorků\n"+$"Přebuzení vstupu: {meters.InputClips} · přebuzení výstupu: {meters.OutputClips} · neplatné vzorky: {meters.InvalidSamples}\nAnalytická fronta: {meters.DroppedAnalysisBlocks} zahozených bloků. Tón a kalibrace jsou v Ladičce.";
    }
}
