// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
using Rocker.Interop;
using Rocker.Probe.Controls;
using System.Text.Json;
using System.Diagnostics;
using MetronomeConfig=Rocker.Domain.MetronomeConfig;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private sealed record MetroPreset(string Id,string Name,MetronomeConfig Config);
    private static readonly string[] MetroSounds=["classic","wood","beep","click","hihat"];
    private static readonly string[] MetroSoundNames=["Klasický","Dřevo","Pípnutí","Kliknutí","Hi-hat"];
    private MetronomeConfig metroConfig=MetronomeRules.Sanitize(JsonSerializer.SerializeToElement(new{}));
    private readonly List<MetroPreset> metroPresets=[];
    private readonly List<Action> metroRefresh=[];
    private readonly List<double> metroTaps=[];
    private readonly DispatcherTimer metroApplyTimer=new(){Interval=TimeSpan.FromMilliseconds(100)};
    private bool metroEnabled,metroUpdating,metroTimerInitialized;
    private int metroRevision,metroAppliedRevision;
    private Task? metroApplying;
    private string? metroPresetId;
    private StackPanel? metroBeatPanel;
    private readonly List<WeakReference<MetronomeVisual>> metroVisuals=[];
    private readonly List<Button> metroBeatButtons=[];
    private MetroPreset[] AllMetroPresets()=>catalog!.Items("metronomePresets").Select(value=>new MetroPreset(value.GetProperty("id").GetString()!,value.GetProperty("name").GetString()!,MetronomeRules.Sanitize(value))).Concat(metroPresets).ToArray();
    private FrameworkElement BuildMetronome()
    {
        var panel=Stack();panel.Children.Add(MetroTransport());
        var tempoControls=Stack();
        var presets=new ComboBox{Header="Preset",DisplayMemberPath="Name",HorizontalAlignment=HorizontalAlignment.Stretch};
        metroRefresh.Add(()=>{presets.ItemsSource=AllMetroPresets();presets.SelectedItem=((MetroPreset[])presets.ItemsSource).FirstOrDefault(p=>p.Id==metroPresetId);});
        presets.SelectionChanged+=(_,_)=>{if(!metroUpdating && presets.SelectedItem is MetroPreset preset){metroConfig=preset.Config;metroPresetId=preset.Id;RebuildMetroBeats();QueueMetronome(false);}};tempoControls.Children.Add(presets);
        var tempo=new ValueDial{Label="Tempo (BPM)",Minimum=40,Maximum=240,Value=metroConfig.Bpm};metroRefresh.Add(()=>tempo.Value=metroConfig.Bpm);tempo.ValueChanged+=(_,_)=>ChangeMetro(c=>c with{Bpm=Music.ClampBpm(tempo.Value)});
        tempoControls.Children.Insert(0,tempo);
        var visual=CreateMetroVisual();var visualPane=Stack();visualPane.Children.Add(MetroVisualSelector());visualPane.Children.Add(visual);
        // At narrow widths the instrument stays first, rather than falling below its editors.
        panel.Children.Add(MetroColumns(visualPane,tempoControls,visual));
        var details=Stack();

        details.Children.Add(MetroNumber("Doby v taktu",1,16,()=>metroConfig.BeatsPerBar,v=>{ChangeMetro(c=>c with{BeatsPerBar=(int)v,AccentPattern=Enumerable.Range(0,(int)v).Select(i=>i<c.AccentPattern.Count?c.AccentPattern[i]:i==0?"accent":"normal").ToArray(),SoundPattern=Enumerable.Range(0,(int)v).Select(i=>c.SoundPattern is not null && i<c.SoundPattern.Count?c.SoundPattern[i]:null).ToArray()});RebuildMetroBeats();}));
        details.Children.Add(MetroNumber("Dělení doby (1–4)",1,4,()=>metroConfig.Subdivision,v=>ChangeMetro(c=>c with{Subdivision=(int)v})));
        details.Children.Add(MetroNumber("Swing (%)",50,75,()=>metroConfig.Swing*100,v=>ChangeMetro(c=>c with{Swing=v/100})));
        details.Children.Add(MetroSound("Zvuk",()=>metroConfig.Sound,v=>ChangeMetro(c=>c with{Sound=v!})));
        details.Children.Add(MetroSound("Zvuk mezidob",()=>metroConfig.SubdivisionSound,v=>ChangeMetro(c=>c with{SubdivisionSound=v}),true));
        details.Children.Add(MetroNumber("Hlasitost (%)",0,100,()=>metroConfig.Volume*100,v=>ChangeMetro(c=>c with{Volume=v/100})));
        details.Children.Add(MetroNumber("Síla akcentu (%)",0,100,()=>metroConfig.AccentVolume*100,v=>ChangeMetro(c=>c with{AccentVolume=v/100})));
        details.Children.Add(MetroNumber("Odpočet (takty)",0,2,()=>metroConfig.CountInBars,v=>ChangeMetro(c=>c with{CountInBars=(int)v})));
        details.Children.Add(Text("Akcenty: kliknutím přepni normální / důraz / ticho. Pod každou dobou zvol vlastní zvuk."));
        metroBeatPanel=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};details.Children.Add(new ScrollViewer{Content=metroBeatPanel,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollMode=ScrollMode.Enabled,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});RebuildMetroBeats();
        details.Children.Add(MetroFlag("Postupně zvyšovat tempo",()=>metroConfig.Ramp.Enabled,v=>ChangeMetro(c=>c with{Ramp=c.Ramp with{Enabled=v}})));
        details.Children.Add(MetroNumber("Zvýšit po taktech",1,16,()=>metroConfig.Ramp.EveryBars,v=>ChangeMetro(c=>c with{Ramp=c.Ramp with{EveryBars=(int)v}})));
        details.Children.Add(MetroNumber("Zvýšit o BPM",1,20,()=>metroConfig.Ramp.StepBpm,v=>ChangeMetro(c=>c with{Ramp=c.Ramp with{StepBpm=(int)v}})));
        details.Children.Add(MetroNumber("Cílové BPM",40,240,()=>metroConfig.Ramp.TargetBpm,v=>ChangeMetro(c=>c with{Ramp=c.Ramp with{TargetBpm=(int)v}})));
        details.Children.Add(MetroFlag("Trénink tichých taktů",()=>metroConfig.Gap.Enabled,v=>ChangeMetro(c=>c with{Gap=c.Gap with{Enabled=v}})));
        details.Children.Add(MetroNumber("Slyšitelné takty",1,16,()=>metroConfig.Gap.AudibleBars,v=>ChangeMetro(c=>c with{Gap=c.Gap with{AudibleBars=(int)v}})));
        details.Children.Add(MetroNumber("Tiché takty",1,16,()=>metroConfig.Gap.SilentBars,v=>ChangeMetro(c=>c with{Gap=c.Gap with{SilentBars=(int)v}})));
        details.Children.Add(MetroFlag("Druhá rytmická vrstva",()=>metroConfig.Polyrhythm.Enabled,v=>ChangeMetro(c=>c with{Polyrhythm=c.Polyrhythm with{Enabled=v}})));
        details.Children.Add(MetroNumber("Pulzy druhé vrstvy za dobu",2,7,()=>metroConfig.Polyrhythm.TicksPerBeat,v=>ChangeMetro(c=>c with{Polyrhythm=c.Polyrhythm with{TicksPerBeat=(int)v}})));
        details.Children.Add(MetroSound("Zvuk druhé vrstvy",()=>metroConfig.Polyrhythm.Sound,v=>ChangeMetro(c=>c with{Polyrhythm=c.Polyrhythm with{Sound=v!}})));
        details.Children.Add(MetroNumber("Hlasitost druhé vrstvy (%)",0,100,()=>metroConfig.Polyrhythm.Volume*100,v=>ChangeMetro(c=>c with{Polyrhythm=c.Polyrhythm with{Volume=v/100}})));
        panel.Children.Add(new Expander{Header="Rytmus, zvuky a trénink",Content=details,HorizontalAlignment=HorizontalAlignment.Stretch});
        var save=new Button{Content="Uložit vlastní preset"};save.Click+=SaveMetroPreset_Click;var delete=new Button{Content="Odstranit vlastní preset"};delete.Click+=DeleteMetroPreset_Click;panel.Children.Add(save);panel.Children.Add(delete);
        panel.Children.Add(Text("Odpočet a obě rytmické vrstvy používají stejné zvukové hodiny. Změna taktu nebo dělení začne na další době. Vizualizace čte zvukové jádro; nevytváří kliky. Nahrávání a Looper používají stejné hodiny a svůj výslovný odpočet.",true));RefreshMetronomeControls();return new VintageHousing(panel,"ROCKER / RHYTHM MASTER");
    }
    private FrameworkElement BuildCompactMetronome()
    {
        var panel=Stack();panel.Spacing=6;
        var header=new Grid{ColumnSpacing=8};header.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=new(80)});header.ColumnDefinitions.Add(new(){Width=new(110)});
        var title=Text("Metronom");title.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;title.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(title);
        var tempo=MetroNumber("Tempo (BPM)",40,240,()=>metroConfig.Bpm,v=>ChangeMetro(c=>c with{Bpm=(int)v}));tempo.Header=null;tempo.SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact;Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tempo,"Tempo metronomu (BPM)");ToolTipService.SetToolTip(tempo,"Tempo v BPM");Grid.SetColumn(tempo,1);header.Children.Add(tempo);
        var selector=MetroVisualSelector(compact:true);Grid.SetColumn(selector,2);header.Children.Add(selector);panel.Children.Add(header);
        panel.Children.Add(MetroTransport(compact:true));panel.Children.Add(CreateMetroVisual(compact:true));
        var open=new Button{Content="Úplné nastavení metronomu",FontSize=12,MinHeight=28};open.Click+=(_,_)=>Navigate(new("04"));panel.Children.Add(open);focusSecondary.Add(open);return new VintageHousing(panel,compact:true);
    }
    private ComboBox MetroVisualSelector(bool compact=false)
    {
        var selector=new ComboBox{Header=compact?null:"Vizualizace",ItemsSource=new[]{"Doby","Kyvadlo"},HorizontalAlignment=HorizontalAlignment.Stretch,Tag="metronome-visual-mode"};
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(selector,"Vizualizace metronomu");
        metroRefresh.Add(()=>selector.SelectedIndex=metroConfig.VisualMode=="dots"?0:1);
        selector.SelectedIndex=metroConfig.VisualMode=="dots"?0:1;
        selector.SelectionChanged+=(_,_)=>{if(metroUpdating || selector.SelectedIndex<0)return;metroConfig=metroConfig with{VisualMode=selector.SelectedIndex==0?"dots":"pendulum"};if(persistenceReady)autosave!.Queue(UserData.Document("metronome","default",metroConfig));RefreshMetronomeControls();};
        return selector;
    }
    private static FrameworkElement MetroColumns(FrameworkElement instrument,FrameworkElement editors,MetronomeVisual visual)
    {
        var grid=new Grid{ColumnSpacing=20,RowSpacing=16};grid.Children.Add(instrument);grid.Children.Add(editors);int previousColumns=0;
        void Layout(double width){int columns=width>=600?2:1;visual.SetCondensed(columns==1);if(columns==previousColumns)return;previousColumns=columns;grid.ColumnDefinitions.Clear();grid.RowDefinitions.Clear();for(int i=0;i<columns;++i)grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});for(int i=0;i<(2+columns-1)/columns;++i)grid.RowDefinitions.Add(new(){Height=GridLength.Auto});Grid.SetColumn(editors,columns==2?1:0);Grid.SetRow(editors,columns==2?0:1);}
        Layout(0);grid.SizeChanged+=(_,args)=>Layout(args.NewSize.Width);return grid;
    }
    private MetronomeVisual CreateMetroVisual(bool compact=false)
    {
        var visual=new MetronomeVisual(compact);visual.Configure(metroConfig);metroVisuals.Add(new(visual));return visual;
    }
    private void RefreshMetroVisuals()
    {
        if(audio is null)return;var snapshot=audio.GetMetronomeSnapshot();var clock=audio.GetClock();
        for(int i=metroVisuals.Count-1;i>=0;--i){if(!metroVisuals[i].TryGetTarget(out var visual)){metroVisuals.RemoveAt(i);continue;}if(visual.IsLoaded)visual.Render(snapshot,clock,metroConfig,awaitingStart:metroEnabled && snapshot.Running);}
    }
    private FrameworkElement MetroTransport(bool compact=false)
    {
        var panel=Stack();var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};var start=new Button{Content=compact?"Spustit":"Spustit metronom"};start.Click+=MetroStart_Click;var stop=new Button{Content=compact?"Vypnout":"Vypnout metronom"};stop.Click+=MetroStop_Click;var tap=new Button{Content="Tap tempo"};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(start,"Spustit metronom");Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(stop,"Vypnout metronom");tap.Click+=(_,_)=>{double now=Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency;if(metroTaps.Count>0 && now-metroTaps[^1]>2000)metroTaps.Clear();metroTaps.Add(now);if(metroTaps.Count>6)metroTaps.RemoveAt(0);if(Music.BpmFromTaps(metroTaps) is int bpm)ChangeMetro(c=>c with{Bpm=Music.ClampBpm(bpm)});};buttons.Children.Add(start);buttons.Children.Add(stop);buttons.Children.Add(tap);panel.Children.Add(buttons);return panel;
    }
    private NumberBox MetroNumber(string label,double minimum,double maximum,Func<double> read,Action<double> write)
    {var box=new NumberBox{Header=label,Minimum=minimum,Maximum=maximum,Value=read(),SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};metroRefresh.Add(()=>box.Value=read());box.ValueChanged+=(_,args)=>{if(!metroUpdating && double.IsFinite(args.NewValue))write(Math.Clamp(Math.Floor(args.NewValue+0.5),minimum,maximum));};return box;}
    private CheckBox MetroFlag(string label,Func<bool> read,Action<bool> write)
    {var box=new CheckBox{Content=label,IsChecked=read()};metroRefresh.Add(()=>box.IsChecked=read());box.Checked+=(_,_)=>{if(!metroUpdating)write(true);};box.Unchecked+=(_,_)=>{if(!metroUpdating)write(false);};return box;}
    private ComboBox MetroSound(string label,Func<string?> read,Action<string?> write,bool inherited=false)
    {var box=new ComboBox{Header=label,ItemsSource=inherited?MetroSoundNames.Append("Podle doby").ToArray():MetroSoundNames,SelectedIndex=read() is string value?Array.IndexOf(MetroSounds,value):5};metroRefresh.Add(()=>box.SelectedIndex=read() is string value?Array.IndexOf(MetroSounds,value):5);box.SelectionChanged+=(_,_)=>{if(!metroUpdating && box.SelectedIndex>=0)write(box.SelectedIndex<5?MetroSounds[box.SelectedIndex]:null);};return box;}
    private void ChangeMetro(Func<MetronomeConfig,MetronomeConfig> change){if(metroUpdating)return;int previousBeats=metroConfig.BeatsPerBar;metroConfig=change(metroConfig);if(previousBeats!=metroConfig.BeatsPerBar)RebuildMetroBeats();QueueMetronome();}
    private void RebuildMetroBeats()
    {
        if(metroBeatPanel is null)return;metroBeatPanel.Children.Clear();metroBeatButtons.Clear();
        for(int i=0;i<metroConfig.BeatsPerBar;++i){int index=i;var panel=Stack();var button=new Button{Content=MetroBeatLabel(i)};button.Click+=(_,_)=>{var accents=metroConfig.AccentPattern.ToArray();accents[index]=MetronomeRules.CycleAccent(accents[index]);ChangeMetro(c=>c with{AccentPattern=accents});};metroBeatButtons.Add(button);panel.Children.Add(button);
            var sound=new ComboBox{Header=$"Zvuk doby {i+1}",ItemsSource=MetroSoundNames.Append("Výchozí").ToArray(),SelectedIndex=metroConfig.SoundPattern is not null && i<metroConfig.SoundPattern.Count && metroConfig.SoundPattern[i] is string id?Array.IndexOf(MetroSounds,id):5};sound.SelectionChanged+=(_,_)=>{if(metroUpdating)return;var sounds=Enumerable.Range(0,metroConfig.BeatsPerBar).Select(n=>metroConfig.SoundPattern is not null && n<metroConfig.SoundPattern.Count?metroConfig.SoundPattern[n]:null).ToArray();sounds[index]=sound.SelectedIndex<5 && sound.SelectedIndex>=0?MetroSounds[sound.SelectedIndex]:null;ChangeMetro(c=>c with{SoundPattern=sounds});};panel.Children.Add(sound);metroBeatPanel.Children.Add(panel);}
    }
    private string MetroBeatLabel(int index)=>$"{index+1} · "+(metroConfig.AccentPattern[index] switch{"accent"=>"Důraz","muted"=>"Ticho",_=>"Normální"});
    private MetronomeConfiguration NativeMetronome()=>new(Enabled:metroEnabled,Bpm:(uint)metroConfig.Bpm,Beats:(uint)metroConfig.BeatsPerBar,Subdivision:(uint)metroConfig.Subdivision,Sound:(uint)Array.IndexOf(MetroSounds,metroConfig.Sound),SubSound:metroConfig.SubdivisionSound is string sub?(uint)Array.IndexOf(MetroSounds,sub):5,CountIn:(uint)metroConfig.CountInBars,Ramp:metroConfig.Ramp.Enabled,RampEvery:(uint)metroConfig.Ramp.EveryBars,RampStep:(uint)metroConfig.Ramp.StepBpm,RampTarget:(uint)metroConfig.Ramp.TargetBpm,Gap:metroConfig.Gap.Enabled,AudibleBars:(uint)metroConfig.Gap.AudibleBars,SilentBars:(uint)metroConfig.Gap.SilentBars,Poly:metroConfig.Polyrhythm.Enabled,PolyTicks:(uint)metroConfig.Polyrhythm.TicksPerBeat,PolySound:(uint)Array.IndexOf(MetroSounds,metroConfig.Polyrhythm.Sound),Volume:metroConfig.Volume,AccentVolume:metroConfig.AccentVolume,Swing:metroConfig.Swing,PolyVolume:metroConfig.Polyrhythm.Volume,Accents:metroConfig.AccentPattern.Select(a=>a=="accent"?2u:a=="muted"?0u:1u).ToArray(),Sounds:Enumerable.Range(0,metroConfig.BeatsPerBar).Select(i=>metroConfig.SoundPattern is not null && i<metroConfig.SoundPattern.Count && metroConfig.SoundPattern[i] is string id?(uint)Array.IndexOf(MetroSounds,id):5u).ToArray());
    private void QueueMetronome(bool clearPreset=true)
    {
        if(closed || closingWindow)return;if(clearPreset)metroPresetId=null;++metroRevision;if(persistenceReady)autosave!.Queue(UserData.Document("metronome","default",metroConfig));RefreshMetronomeControls();
        if(!metroTimerInitialized){metroApplyTimer.Tick+=async(_,_)=>{metroApplyTimer.Stop();try{await ApplyMetronomeAsync();}catch(Exception error){if(!closed && !closingWindow)ShowPersistenceError("Metronom nelze nastavit. "+error.Message);}};metroTimerInitialized=true;}metroApplyTimer.Stop();metroApplyTimer.Start();
    }
    private void RefreshMetronomeControls()
    {metroUpdating=true;try{foreach(var refresh in metroRefresh)refresh();for(int i=0;i<metroBeatButtons.Count;++i)metroBeatButtons[i].Content=MetroBeatLabel(i);RefreshMetroVisuals();}finally{metroUpdating=false;}}
    private async Task ApplyMetronomeAsync()
    {if(metroApplying is not null){await metroApplying;return;}if(audio is null || closingWindow || closed)return;async Task Core(){while(metroAppliedRevision<metroRevision && !closed && !closingWindow){int revision=metroRevision;await audio.SetMetronomeAsync(NativeMetronome());metroAppliedRevision=revision;}}metroApplying=Core();try{await metroApplying;}finally{metroApplying=null;}}
    private async void MetroStart_Click(object sender,RoutedEventArgs args)
    {
        if(audio is null || busy || deviceOperationRunning || closingWindow)return;if(deviceSelectionUnavailable){ShowPersistenceError("Nejdřív vyber dostupné zvukové zařízení.");Navigate(new("29"));return;}
        try{SetBusy(true);metroEnabled=true;++metroRevision;await ApplyMetronomeAsync();await audio.StartMetronomeAsync();if(!closed)RefreshState();}catch(Exception error){metroEnabled=false;if(!closed)ShowError(error);}finally{if(!closed)SetBusy(false);}
    }
    private async void MetroStop_Click(object sender,RoutedEventArgs args)
    {if(audio is null || closingWindow)return;try{metroEnabled=false;++metroRevision;await ApplyMetronomeAsync();if(!closed)RefreshState();}catch(Exception error){if(!closed)ShowError(error);}}
    private void RefreshMetronome(AudioSnapshot audioState)
    {
        if(audio is null)return;var state=audio.GetMetronomeSnapshot();if(audioState.State is AudioState.Stopped or AudioState.Faulted)metroEnabled=false;
        bool active=state.Running && state.Enabled;
        for(int i=0;i<metroBeatButtons.Count;++i){metroBeatButtons[i].Content=(active && !state.SilentBar && state.Beat==i?"> ":"")+MetroBeatLabel(i);Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(metroBeatButtons[i],MetroBeatLabel(i)+(active && state.Beat==i?", aktuální doba":""));}
        RefreshMetroVisuals();
        if(metroEnabled && state.Enabled && metroConfig.Ramp.Enabled && metroAppliedRevision==metroRevision && state.Bpm!=metroConfig.Bpm){metroConfig=metroConfig with{Bpm=Music.ClampBpm(state.Bpm)};if(persistenceReady)autosave!.Queue(UserData.Document("metronome","default",metroConfig));RefreshMetronomeControls();}
    }
    private async Task InitializeMetronomeAsync(IReadOnlyList<DataDocument> documents)
    {
        metroApplyTimer.Stop();metroRefresh.Clear();metroBeatButtons.Clear();metroBeatPanel=null;metroVisuals.Clear();metroEnabled=false;
        var settings=documents.FirstOrDefault(d=>d.Kind=="metronome" && d.Id=="default");if(settings is not null)metroConfig=UserData.Read<MetronomeConfig>(settings.Value);metroPresets.Clear();var presets=documents.FirstOrDefault(d=>d.Kind=="metronome-presets" && d.Id=="default");if(presets is not null)metroPresets.AddRange(UserData.Read<MetroPreset[]>(presets.Value));++metroRevision;await ApplyMetronomeAsync();
    }
    private async Task VerifyMetronomeAsync()
    {
        if(audio is null || !persistenceReady)throw new InvalidOperationException("Metronome unavailable.");var preset=AllMetroPresets().First();metroConfig=preset.Config;metroEnabled=false;QueueMetronome();await ApplyMetronomeAsync();var native=audio.GetMetronome();bool match=native.Bpm==metroConfig.Bpm && native.Beats==metroConfig.BeatsPerBar && Math.Abs(native.Swing-metroConfig.Swing)<1e-12 && !native.Enabled;
        var fixture=new MetroPreset("ci-metro-roundtrip","CI metronome",metroConfig);metroPresets.RemoveAll(p=>p.Id==fixture.Id);metroPresets.Add(fixture);autosave!.Queue(UserData.Document("metronome-presets","default",metroPresets.ToArray()));await autosave.FlushAsync();var reopened=await DataRepository.OpenAsync(userFiles);var saved=await reopened.SnapshotAsync();bool restored=UserData.Read<MetronomeConfig>(saved.Documents.Single(d=>d.Kind=="metronome").Value).Bpm==metroConfig.Bpm && UserData.Read<MetroPreset[]>(saved.Documents.Single(d=>d.Kind=="metronome-presets").Value).Any(p=>p.Id==fixture.Id);
        WriteShellDiagnostic("metronome-view.json",new{result=match && restored && metroRefresh.Count>=20?"passed":"failed",nativeParametersVerified=match,persistenceReopened=restored,controlCount=metroRefresh.Count,factoryPresetCount=AllMetroPresets().Length-1,audioPlaybackVerified=false,manualDialogsVerified=false});if(!match || !restored || metroRefresh.Count<20)throw new InvalidOperationException("Metronome UI/persistence contract failed.");
    }
    private async void SaveMetroPreset_Click(object sender,RoutedEventArgs args)
    {if(!persistenceReady || closingWindow)return;try{var name=new TextBox{Header="Název",MaxLength=120};var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Uložit metronomový preset",Content=name,PrimaryButtonText="Uložit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Primary};if(await dialog.ShowAsync()!=ContentDialogResult.Primary || string.IsNullOrWhiteSpace(name.Text))return;var preset=new MetroPreset("user-"+Guid.NewGuid().ToString("N"),name.Text.Trim(),metroConfig);metroPresets.Add(preset);metroPresetId=preset.Id;autosave!.Queue(UserData.Document("metronome-presets","default",metroPresets.ToArray()));QueueMetronome(false);}catch(Exception error){ShowPersistenceError("Preset nelze uložit. "+error.Message);}}
    private async void DeleteMetroPreset_Click(object sender,RoutedEventArgs args)
    {if(!persistenceReady || closingWindow)return;var preset=metroPresets.FirstOrDefault(p=>p.Id==metroPresetId);if(preset is null){ShowPersistenceError("Vyber vlastní preset. Tovární presety se nemažou.");return;}try{var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Odstranit preset?",Content=preset.Name,PrimaryButtonText="Odstranit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;metroPresets.Remove(preset);autosave!.Queue(UserData.Document("metronome-presets","default",metroPresets.ToArray()));QueueMetronome();}catch(Exception error){ShowPersistenceError("Preset nelze odstranit. "+error.Message);}}
}
