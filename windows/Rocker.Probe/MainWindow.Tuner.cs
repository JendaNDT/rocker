// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Rocker.Domain;
using Rocker.Infrastructure;
using Rocker.Interop;
using Rocker.Probe.Controls;
using System.Diagnostics;
using System.Text.Json;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private readonly List<CustomTuning> customTunings=[];
    private bool tunerUpdating,tunerChromatic,noiseCalibrationPending;
    private int tunerString;
    private ComboBox? tunerTuningBox;
    private CheckBox? tunerAutoBox;
    private StackPanel? tunerStringsPanel;
    private TextBlock? tunerNote,tunerStatus,tunerCentText,tunerAnalysisText;
    private PitchGauge? tunerNeedle;
    private LevelMeter? tunerLevel;
    private readonly List<Button> tunerStringButtons=[];
    private double? tunerStableSince;
    private string? tunerConfirmedKey;
    private uint tunerLastEpoch;
    private ulong noiseCalibrationToken;
    private CustomTuning[] AllTunings()=>catalog!.Items("tunings").Select(UserData.Read<CustomTuning>).Concat(customTunings).ToArray();
    private CustomTuning CurrentTuning()=>AllTunings().FirstOrDefault(t=>t.Id==appSettings.TuningId)??AllTunings()[0];
    private double TunerFrequency(int midi)=>Music.MidiToFrequency(midi)*(appSettings.ReferenceA??440)/440;
    private string NoteLabel(int midi)=>Music.MidiToLabel(midi,appSettings.Notation=="german"?NoteNotation.German:NoteNotation.English);
    private FrameworkElement BuildTuner()
    {
        var panel=Stack();panel.Children.Add(Text("Ladička · Vintage"));
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};var start=new Button{Content="Spustit vstup pro ladění"};start.Click+=LiveStart_Click;var stop=new Button{Content="Zastavit zvuk"};stop.Click+=Stop_Click;var device=new Button{Content="Karta a vstup"};device.Click+=(_,_)=>Navigate(new("29"));buttons.Children.Add(start);buttons.Children.Add(stop);buttons.Children.Add(device);panel.Children.Add(buttons);
        tunerNote=Text("—");tunerNote.FontSize=48;tunerCentText=Text("Žádný aktuálně měřený tón.");tunerNeedle=new PitchGauge{Value=50};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tunerNeedle,"Odchylka ladění v centech");var instrument=Stack();tunerNote.Style=tunerCentText.Style=(Style)Application.Current.Resources["VintageInstrumentTextStyle"];tunerNote.FontSize=56;tunerNote.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;instrument.Children.Add(tunerNote);instrument.Children.Add(tunerCentText);instrument.Children.Add(tunerNeedle);panel.Children.Add(new VintageHousing(instrument,compact:true,instrument:true));
        var mode=new ComboBox{Header="Režim",ItemsSource=new[]{"Vedené ladění šesti strun","Chromatická ladička"},SelectedIndex=tunerChromatic?1:0};mode.SelectionChanged+=(_,_)=>{tunerChromatic=mode.SelectedIndex==1;tunerStableSince=null;tunerConfirmedKey=null;};panel.Children.Add(mode);
        tunerTuningBox=new ComboBox{Header="Ladění",DisplayMemberPath="Label",HorizontalAlignment=HorizontalAlignment.Stretch};tunerTuningBox.SelectionChanged+=(_,_)=>{if(tunerUpdating || tunerTuningBox.SelectedItem is not CustomTuning tuning)return;appSettings=appSettings with{TuningId=tuning.Id};tunerString=0;SaveSettings();RebuildTunerStrings();};panel.Children.Add(tunerTuningBox);RefreshTuningSelector();
        var automatic=new CheckBox{Content="Vybrat nejbližší strunu automaticky",IsChecked=tunerAutoString};tunerAutoBox=automatic;automatic.Checked+=(_,_)=>tunerAutoString=true;automatic.Unchecked+=(_,_)=>tunerAutoString=false;panel.Children.Add(automatic);
        tunerStringsPanel=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};panel.Children.Add(new ScrollViewer{Content=tunerStringsPanel,HorizontalScrollMode=ScrollMode.Enabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});RebuildTunerStrings();
        tunerStatus=Text("Vyber kytarový vstup a spusť ladění.",true);panel.Children.Add(tunerStatus);tunerLevel=new LevelMeter{Analog=true,Label="Špička skutečného vstupu"};panel.Children.Add(tunerLevel);tunerAnalysisText=Text("",true);panel.Children.Add(tunerAnalysisText);
        var notation=new ComboBox{Header="Značení not",ItemsSource=new[]{"Anglické (B)","Německé (H / B)"},SelectedIndex=appSettings.Notation=="german"?1:0};notation.SelectionChanged+=(_,_)=>{appSettings=appSettings with{Notation=notation.SelectedIndex==1?"german":"english"};SaveSettings();RebuildTunerStrings();};panel.Children.Add(notation);
        var reference=new NumberBox{Header="Referenční A (Hz)",Minimum=430,Maximum=450,Value=appSettings.ReferenceA??440,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};reference.ValueChanged+=(_,args)=>{if(!double.IsFinite(args.NewValue))return;appSettings=appSettings with{ReferenceA=Math.Clamp(args.NewValue,430,450)};SaveSettings();tunerStableSince=null;};panel.Children.Add(reference);
        var volume=new Slider{Header="Hlasitost referenčních tónů (%)",Minimum=0,Maximum=100,Value=appSettings.ReferenceToneVolume*100};volume.ValueChanged+=(_,args)=>{appSettings=appSettings with{ReferenceToneVolume=args.NewValue/100};SaveSettings();};panel.Children.Add(volume);
        var confirm=new CheckBox{Content="Potvrdit naladění krátkým zvukem",IsChecked=appSettings.TunerConfirmSound};confirm.Checked+=(_,_)=>{appSettings=appSettings with{TunerConfirmSound=true};SaveSettings();};confirm.Unchecked+=(_,_)=>{appSettings=appSettings with{TunerConfirmSound=false};SaveSettings();};panel.Children.Add(confirm);
        tunerGateBox=new NumberBox{Header="Práh šumu (0–50 % stejné stupnice jako měřák)",Minimum=0,Maximum=50,Value=appSettings.NoiseGate*100,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};tunerGateBox.ValueChanged+=(_,args)=>{if(tunerUpdating || !double.IsFinite(args.NewValue))return;appSettings=appSettings with{NoiseGate=Math.Clamp(args.NewValue/100,0,.5)};SaveSettings();try{audio?.SetNoiseGate(appSettings.NoiseGate);}catch(Exception error){ShowError(error);}};panel.Children.Add(tunerGateBox);
        var calibrate=new Button{Content="Změřit šum po dobu 2 sekund"};calibrate.Click+=(_,_)=>{try{if(audio is null)throw new InvalidOperationException("Zvukové jádro není dostupné.");noiseCalibrationToken=audio.CalibrateNoise();noiseCalibrationPending=true;}catch(Exception error){ShowPersistenceError("Kalibrace potřebuje běžící vstup. "+error.Message);}};panel.Children.Add(calibrate);
        var custom=new Button{Content="Vytvořit vlastní ladění"};custom.Click+=SaveTuning_Click;var delete=new Button{Content="Odstranit vlastní ladění"};delete.Click+=DeleteTuning_Click;panel.Children.Add(custom);panel.Children.Add(delete);
        panel.Children.Add(Text("Referenční tón: drž tlačítko myší; klávesnice a čtečka spustí krátký náhled. Tón se sám uvolní nejpozději po 5 sekundách zvuku. Referenční tóny a potvrzení hrají ve společném jádru, mimo aparát. Bez vstupu je nevydáváme za detekovanou kytaru.",true));
        panel.Children.Add(Text("Analýza používá okno přibližně 171 ms (při 8 kHz 256 ms); nízké tóny potřebují více period. Tato délka není zpoždění monitoringu. Nejistý nebo příliš slabý signál nemá aktuální notu. Jas tónu je pomůcka pro kalibrovaný palm mute, ne univerzální rozpoznání techniky.",true));return Card(panel);
    }
    private bool tunerAutoString=true;
    private NumberBox? tunerGateBox;
    private void RefreshTuningSelector()
    {if(tunerTuningBox is null)return;tunerUpdating=true;try{tunerTuningBox.ItemsSource=AllTunings();tunerTuningBox.SelectedItem=((CustomTuning[])tunerTuningBox.ItemsSource).FirstOrDefault(t=>t.Id==CurrentTuning().Id);}finally{tunerUpdating=false;}}
    private void RebuildTunerStrings()
    {
        if(tunerStringsPanel is null)return;tunerStringsPanel.Children.Clear();tunerStringButtons.Clear();var tuning=CurrentTuning();tunerString=Math.Clamp(tunerString,0,5);
        for(int i=0;i<6;++i){int index=i;var box=Stack();var choose=new Button{Content=$"{6-i} · {NoteLabel(tuning.Strings[i].Midi)}"};choose.Click+=(_,_)=>{tunerString=index;tunerAutoString=false;if(tunerAutoBox is not null)tunerAutoBox.IsChecked=false;tunerStableSince=null;tunerConfirmedKey=null;};box.Children.Add(choose);tunerStringButtons.Add(choose);
            var reference=new Button{Content="Referenční tón"};reference.PointerPressed+=async(_,args)=>{if(!args.GetCurrentPoint(reference).Properties.IsLeftButtonPressed)return;args.Handled=true;if(!reference.CapturePointer(args.Pointer))return;await PlayTunerReferenceAsync(tuning.Strings[index].Midi);};reference.PointerReleased+=async(_,args)=>{args.Handled=true;reference.ReleasePointerCapture(args.Pointer);await StopTunerReferenceAsync();};reference.PointerCanceled+=async(_,_)=>await StopTunerReferenceAsync();reference.PointerCaptureLost+=async(_,_)=>await StopTunerReferenceAsync();reference.Click+=async(_,_)=>await PlayTunerReferenceAsync(tuning.Strings[index].Midi);box.Children.Add(reference);tunerStringsPanel.Children.Add(box);}
    }
    private async Task PlayTunerReferenceAsync(int midi)
    {if(audio is null || closingWindow || deviceSelectionUnavailable || deviceOperationRunning)return;try{await audio.PlayReferenceAsync(TunerFrequency(midi),appSettings.ReferenceToneVolume);if(!closed)RefreshState();}catch(Exception error){if(!closed)ShowError(error);}}
    private async Task StopTunerReferenceAsync(){if(audio is null || closingWindow)return;try{await audio.StopReferenceAsync();}catch(Exception error){if(!closed)ShowError(error);}}
    private void RefreshTuner(AnalysisSnapshot reading)
    {
        if(tunerNote is null || audio is null)return;bool input=reading.ActiveInput && !reading.Stale;tunerLevel!.SetLevel(audio.GetMeters().InputPeak,input);
        tunerAnalysisText!.Text=$"Analýza: {reading.AgeSeconds*1000:0} ms od posledních dat · okno {reading.WindowSeconds*1000:0} ms · confidence {reading.Clarity:0.000}\nJas: {reading.Centroid:0} Hz · úhozy: {reading.HitCount} · tělo posledního úhozu: {reading.HitCentroid:0} Hz";
        if(noiseCalibrationPending && reading.CalibrationToken==noiseCalibrationToken && reading.CalibrationComplete){noiseCalibrationPending=false;appSettings=appSettings with{NoiseGate=reading.RecommendedGate};SaveSettings();audio.SetNoiseGate(appSettings.NoiseGate);tunerUpdating=true;tunerGateBox!.Value=appSettings.NoiseGate*100;tunerUpdating=false;}
        if(noiseCalibrationPending && (reading.CalibrationInterrupted || !input)){noiseCalibrationPending=false;ShowPersistenceError("Kalibrace šumu byla přerušena. Spusť vstup a zkus ji znovu.");}
        if(tunerLastEpoch!=reading.Epoch){tunerStableSince=null;tunerConfirmedKey=null;tunerLastEpoch=reading.Epoch;}
        if(!input || !reading.PitchDetected){tunerNote.Text="—";tunerCentText!.Text="Žádný aktuálně měřený tón.";tunerNeedle!.HasReading=false;tunerNeedle.Value=50;tunerStableSince=null;tunerConfirmedKey=null;tunerStatus!.Text=!input?"Vstup neběží nebo je měření staré.":reading.Calibrating?$"Měřím šum: {reading.CalibrationProgress:P0}. Nehraj.":!reading.Ready?"Připravuji měřicí okno…":reading.Rms*3<appSettings.NoiseGate?"Ticho nebo příliš slabý signál.":"Tón není dostatečně jistý. Zahraj jednu strunu.";return;}
        var tuning=CurrentTuning();if(!tunerChromatic && tunerAutoString)tunerString=Enumerable.Range(0,6).MinBy(i=>Math.Abs(Music.CentsBetween(reading.Frequency,TunerFrequency(tuning.Strings[i].Midi))));
        var note=Music.FrequencyToNote(reading.Frequency*440/(appSettings.ReferenceA??440));if(note is null)return;int midi=tunerChromatic?note.Midi:tuning.Strings[tunerString].Midi;int cents=Music.CentsBetween(reading.Frequency,TunerFrequency(midi));bool inTune=Math.Abs(cents)<=5;
        tunerNote.Text=NoteLabel(midi);tunerCentText!.Text=$"{reading.Frequency:0.00} Hz · {cents:+0;-0;0} centů · "+(inTune?"Naladěno":cents<0?"Pod tónem · dotáhni":"Nad tónem · povol");tunerNeedle!.HasReading=true;tunerNeedle.Value=50+Math.Clamp(cents,-50,50);tunerStatus!.Text=reading.Calibrating?$"Měřím šum: {reading.CalibrationProgress:P0}. Nehraj.":reading.ReferencePlaying?"Hraje referenční tón. Zobrazené měření pochází ze vstupu kytary.":"Měřím jednu strunu ze zvoleného vstupu.";
        for(int i=0;i<tunerStringButtons.Count;++i)tunerStringButtons[i].Content=(!tunerChromatic && i==tunerString?"> ":"")+$"{6-i} · {NoteLabel(tuning.Strings[i].Midi)}";
        string key=$"{reading.Epoch}:{midi}";double now=Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency;
        if(tunerConfirmedKey is not null && tunerConfirmedKey!=key){tunerStableSince=null;tunerConfirmedKey=null;}
        if(!inTune){tunerStableSince=null;tunerConfirmedKey=null;}else{tunerStableSince??=now;if(appSettings.TunerConfirmSound && now-tunerStableSince>=400 && tunerConfirmedKey!=key){tunerConfirmedKey=key;PlayTunerConfirmation();}}
    }
    private async void PlayTunerConfirmation(){try{if(audio is not null && !closingWindow)await audio.PlayReferenceAsync(1320,.6,true);}catch(Exception error){if(!closed)ShowError(error);}}
    private void InitializeTuner(IReadOnlyList<DataDocument> documents)
    {var tunings=documents.FirstOrDefault(d=>d.Kind=="custom-tunings" && d.Id=="default");customTunings.Clear();if(tunings is not null)customTunings.AddRange(UserData.Read<CustomTuning[]>(tunings.Value));audio?.SetNoiseGate(Math.Clamp(appSettings.NoiseGate,0,.5));tunerTuningBox=null;tunerStringsPanel=null;tunerNote=null;tunerStringButtons.Clear();tunerStableSince=null;tunerConfirmedKey=null;}
    private async void SaveTuning_Click(object sender,RoutedEventArgs args)
    {
        if(!persistenceReady || closingWindow)return;if(customTunings.Count>=20){ShowPersistenceError("Můžeš uložit nejvýše 20 vlastních ladění.");return;}
        try{var panel=Stack();var name=new TextBox{Header="Název",MaxLength=120};panel.Children.Add(name);var notes=CurrentTuning().Strings.Select((s,i)=>new NumberBox{Header=$"Struna {6-i}, MIDI (24–84)",Minimum=24,Maximum=84,Value=s.Midi,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline}).ToArray();foreach(var note in notes)panel.Children.Add(note);var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Vlastní ladění",Content=new ScrollViewer{Content=panel},PrimaryButtonText="Uložit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Primary};if(await dialog.ShowAsync()!=ContentDialogResult.Primary || string.IsNullOrWhiteSpace(name.Text))return;if(notes.Any(n=>!double.IsFinite(n.Value))){ShowPersistenceError("Všechny struny musí mít platné MIDI číslo.");return;}
            var tuning=new CustomTuning("custom-"+Guid.NewGuid().ToString("N"),name.Text.Trim(),notes.Select(n=>{int midi=(int)Math.Clamp(Math.Floor(n.Value+.5),24,84);return new TuningString(Music.MidiToLabel(midi),midi);}).ToArray());customTunings.Add(tuning);appSettings=appSettings with{TuningId=tuning.Id};SaveSettings();autosave!.Queue(UserData.Document("custom-tunings","default",customTunings.ToArray()));RefreshTuningSelector();RebuildTunerStrings();
        }catch(Exception error){ShowPersistenceError("Ladění nelze uložit. "+error.Message);}
    }
    private async void DeleteTuning_Click(object sender,RoutedEventArgs args)
    {
        if(!persistenceReady || closingWindow)return;var tuning=customTunings.FirstOrDefault(t=>t.Id==appSettings.TuningId);if(tuning is null){ShowPersistenceError("Vyber vlastní ladění. Tovární se nemažou.");return;}
        try{var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Odstranit ladění?",Content=tuning.Label,PrimaryButtonText="Odstranit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;customTunings.Remove(tuning);appSettings=appSettings with{TuningId="standard"};SaveSettings();autosave!.Queue(UserData.Document("custom-tunings","default",customTunings.ToArray()));RefreshTuningSelector();RebuildTunerStrings();}catch(Exception error){ShowPersistenceError("Ladění nelze odstranit. "+error.Message);}
    }
    private async Task VerifyTunerAsync()
    {
        if(audio is null || tunerStringsPanel is null || !persistenceReady)throw new InvalidOperationException("Tuner unavailable.");var fixture=new CustomTuning("custom-ci-native-tuner","CI tuning",CurrentTuning().Strings);customTunings.RemoveAll(t=>t.Id==fixture.Id);customTunings.Add(fixture);autosave!.Queue(UserData.Document("custom-tunings","default",customTunings.ToArray()));appSettings=appSettings with{ReferenceA=442,Notation="german"};SaveSettings();await autosave.FlushAsync();var reopened=await DataRepository.OpenAsync(userFiles);var saved=await reopened.SnapshotAsync();bool restored=UserData.Read<CustomTuning[]>(saved.Documents.Single(d=>d.Kind=="custom-tunings").Value).Any(t=>t.Id==fixture.Id) && UserData.Read<AppSettings>(saved.Documents.Single(d=>d.Kind=="settings").Value).ReferenceA==442;
        WriteShellDiagnostic("tuner-view.json",new{result=restored && tunerStringButtons.Count==6?"passed":"failed",sixStringControls=tunerStringButtons.Count,persistenceReopened=restored,factoryTunings=AllTunings().Length-customTunings.Count,nativeAnalysisQueried=true,noInputInventsNoNote=audio.GetAnalysis().Frequency==0,audioPlaybackVerified=false,manualTuningDialogsVerified=false});if(!restored || tunerStringButtons.Count!=6)throw new InvalidOperationException("Tuner persistence/controls failed.");
    }
}
