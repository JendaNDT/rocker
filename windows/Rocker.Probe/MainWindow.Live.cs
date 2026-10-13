// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Interop;
using Rocker.Probe.Controls;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private TextBlock? liveTunerNote,liveTunerCents;private PitchGauge? liveTunerNeedle;
    private LevelMeter? liveInputMeter,liveProcessedMeter,liveMixMeter;
    private TextBlock? liveState,liveDiagnostic,liveMixReason;
    private Button? liveEnableMonitor;
    private CheckBox? liveMonitor;
    private Button? liveStart,liveStop;private TextBlock? liveDevice;
    private double liveGain=0.15;
    private bool liveUpdating;
    private FrameworkElement BuildLive()
    {
        focusSecondary.Clear();var panel=Stack();
        liveDevice=Text("Vyber kartu a jeden vstup pro kytaru.",true);
        liveStart=PrimaryAction("Nastavit zvuk");liveStart.Click+=(_,_)=>{if(appSettings.AudioDevice is not{InputChannel:>=0} || deviceSelectionUnavailable)Navigate(new("29"));else LiveStart_Click(liveStart,new());};
        liveStop=new Button{Content="Zastavit zvuk",MinHeight=40,IsEnabled=false};liveStop.Click+=Stop_Click;
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};buttons.Children.Add(liveStart);buttons.Children.Add(liveStop);panel.Children.Add(buttons);panel.Children.Add(liveDevice);
        var left=Stack();var right=Stack();
        var monitoring=Stack();monitoring.Spacing=6;
        liveMonitor=new CheckBox{Content="Poslouchat kytaru ve sluchátkách",IsChecked=false,IsEnabled=false};liveMonitor.Checked+=LiveMonitoring_Changed;liveMonitor.Unchecked+=LiveMonitoring_Changed;monitoring.Children.Add(liveMonitor);
        var level=new Slider{Header="Hlasitost monitoringu",Minimum=0,Maximum=100,Value=liveGain*100};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(level,"Hlasitost monitoringu");level.ValueChanged+=(_,args)=>{liveGain=args.NewValue/100;ApplyLiveMonitoring();};monitoring.Children.Add(level);
        liveInputMeter=new(){Label="Vstup kytary"};monitoring.Children.Add(liveInputMeter);
        liveState=Text("Spusť Live a potom zapni poslech kytary.",true);monitoring.Children.Add(liveState);left.Children.Add(new VintageHousing(monitoring,compact:true));
        var tuner=Stack();
        TextBlock FaceText(string label){var text=Text(label);text.Style=(Style)Application.Current.Resources["VintageInstrumentTextStyle"];return text;}
        var tunerLabel=FaceText("LADIČKA");tunerLabel.FontSize=11;tunerLabel.CharacterSpacing=160;tuner.Children.Add(tunerLabel);liveTunerNote=FaceText("—");liveTunerNote.FontSize=40;liveTunerNote.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;liveTunerCents=FaceText("Žádný aktuálně měřený tón.");
        liveTunerNeedle=new PitchGauge{Value=50,Height=70};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(liveTunerNeedle,"Live · odchylka v centech");var readout=new Grid{ColumnSpacing=12};readout.ColumnDefinitions.Add(new(){Width=new GridLength(88)});readout.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});liveTunerNote.VerticalAlignment=VerticalAlignment.Center;readout.Children.Add(liveTunerNote);Grid.SetColumn(liveTunerNeedle,1);readout.Children.Add(liveTunerNeedle);tuner.Children.Add(readout);tuner.Children.Add(liveTunerCents);
        var tune=new Button{Content="Otevřít ladičku",RequestedTheme=ElementTheme.Light};tune.Click+=(_,_)=>Navigate(new("03"));tuner.Children.Add(tune);focusSecondary.Add(tune);left.Children.Add(new VintageHousing(tuner,compact:true,instrument:true));
        right.Children.Add(BuildCompactMetronome());right.Children.Add(BuildRecordingControls(compact:true));right.Children.Add(BuildCompactAmplifier());
        panel.Children.Add(ResponsiveColumns(left,right));
        var details=Stack();liveProcessedMeter=new(){Label="Za aparátem",Analog=true};liveMixMeter=new(){Label="Výsledný mix",Analog=true};details.Children.Add(liveProcessedMeter);details.Children.Add(liveMixMeter);liveMixReason=Text("Poslech kytary je vypnutý.",true);details.Children.Add(liveMixReason);liveEnableMonitor=new Button{Content="Zapnout poslech kytary"};liveEnableMonitor.Click+=(_,_)=>{if(liveMonitor is{IsEnabled:true}){liveMonitor.IsChecked=true;ApplyLiveMonitoring();}};details.Children.Add(liveEnableMonitor);liveDiagnostic=Text("",true);details.Children.Add(liveDiagnostic);
        details.Children.Add(Text("Kytaru i sluchátka připoj ke stejné kartě. Pro poslech softwarových efektů nepřimíchávej současně Direct Monitor karty.",true));
        var devices=new Button{Content="Změnit zvukovou kartu a vstup"};devices.Click+=(_,_)=>Navigate(new("29"));details.Children.Add(devices);
        var more=new Expander{Header="Zvuková karta, měřáky a podrobnosti",Content=details,HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(more);focusSecondary.Add(more);return panel;
    }
    private async void LiveStart_Click(object sender,RoutedEventArgs args)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(audio is null || busy || powerTransition || deviceOperationRunning || closingWindow)return;
        if(deviceSelectionUnavailable || appSettings.AudioDevice is not {InputChannel:>=0}){ShowPersistenceError("Nejdřív vyber zvukovou kartu a konkrétní vstup kytary.");Navigate(new("29"));return;}
        try{if(desktopEnabled && CurrentTarget.ViewId is "06" or "09" or "12")await ClaimDesktopGraderAsync();++playbackIntent;SetBusy(true);await audio.StartLiveAsync();if(!closed && !closingWindow)RefreshState();}
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
        bool configured=appSettings.AudioDevice is{InputChannel:>=0} && !deviceSelectionUnavailable;
        liveStart!.Content=configured?"Spustit Live":"Nastavit zvuk";liveStart.IsEnabled=!busy && !powerTransition && !deviceOperationRunning && (!running || !meters.HasInput);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(liveStart,(string)liveStart.Content);
        liveStop!.IsEnabled=state.State!=AudioState.Stopped || captureDraft is not null;
        liveDevice!.Text=configured && appSettings.AudioDevice is{ } device?device.Output+" · vstup "+(device.InputChannel+1):"Nejdřív vyber zvukovou kartu a vstup kytary.";
        liveMonitor!.IsEnabled=running && meters.HasInput && !powerTransition && !deviceOperationRunning;
        liveInputMeter.SetLevel(meters.InputPeak,running && meters.HasInput);
        liveProcessedMeter!.Label=appSettings.AmpEnabled?"Za aparátem":"Za aparátem · bypass";
        liveProcessedMeter!.SetLevel(meters.ProcessedPeak,running && meters.HasInput);
        liveMixMeter!.SetLevel(meters.MixPeak,running);
        if(liveMixReason is not null){liveMixReason.Text=!running?"Zvukové zařízení je zastavené.":!meters.HasInput?"Zvuk běží bez vstupu kytary.":!meters.Monitoring?"Poslech kytary je vypnutý. Vstup a aparát mohou měřit signál; do výsledného mixu se kytara přidá až po zapnutí poslechu.":liveGain==0?"Poslech je zapnutý, ale jeho hlasitost je nula.":"Poslech kytary je zapnutý; ostatní zdroje se přimíchávají samostatně.";liveEnableMonitor!.Visibility=running && meters.HasInput && !meters.Monitoring?Visibility.Visible:Visibility.Collapsed;liveEnableMonitor.IsEnabled=liveMonitor!.IsEnabled;}
        liveUpdating=true;liveMonitor!.IsChecked=meters.Monitoring;liveUpdating=false;
        liveState!.Text=running?(meters.HasInput?(meters.Monitoring?"Vstup běží a monitoring je zapnutý.":"Vstup běží. Monitoring je ztlumený."):"Zvuk běží bez vstupu kytary."):"Živý vstup je zastavený. Zařízení je uvolněné.";
        var reading=audio.GetAnalysis();var note=reading.ActiveInput && !reading.Stale && reading.PitchDetected?Rocker.Domain.Music.FrequencyToNote(reading.Frequency*440/(appSettings.ReferenceA??440)):null;liveTunerNote!.Text=note is null?"—":NoteLabel(note.Midi);if(note is null){liveTunerCents!.Text="Žádný aktuálně měřený tón.";liveTunerNeedle!.HasReading=false;liveTunerNeedle.Value=50;}else{int cents=Rocker.Domain.Music.CentsBetween(reading.Frequency,TunerFrequency(note.Midi));liveTunerCents!.Text=$"{cents:+0;-0;0} centů · {CurrentTuning().Label}";liveTunerNeedle!.HasReading=true;liveTunerNeedle.Value=Math.Clamp(50+cents,0,100);}
        var clock=audio.GetClock();
        liveDiagnostic!.Text=$"Společné hodiny: {clock.Frame:N0} vzorků · {clock.ElapsedSeconds:0.000} s · epocha {clock.Epoch} · DSP FIR {clock.DspLatencyFrames} vzorků\n"+$"Přebuzení vstupu: {meters.InputClips} · přebuzení výstupu: {meters.OutputClips} · neplatné vzorky: {meters.InvalidSamples}\nAnalytická fronta: {meters.DroppedAnalysisBlocks} zahozených bloků. Tón a kalibrace jsou v Ladičce.";
    }
}
