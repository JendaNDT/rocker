// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
using Rocker.Interop;
using Rocker.Probe.Controls;
using System.Text.Json;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private AmpSettings ampSettings=AmpRules.Sanitize(JsonSerializer.SerializeToElement(new{}));
    private readonly List<AmpPreset> userAmpPresets=[];
    private readonly Dictionary<string,ValueDial> ampDials=[];
    private readonly DispatcherTimer ampApplyTimer=new(){Interval=TimeSpan.FromMilliseconds(100)};
    private bool ampTimerInitialized,ampUiUpdating;
    private int ampRevision,appliedAmpRevision;
    private Task? ampApplying;
    private ComboBox? ampPresetsBox,liveAmpPresetsBox,ampDistortionBox,ampCabinetBox;
    private CheckBox? ampEnabledBox,liveAmpEnabledBox;
    private TextBlock? ampStatus;
    private AmpPreset[] AllAmpPresets()=>catalog is null?userAmpPresets.ToArray():catalog.Items("ampPresets").Select(UserData.Read<AmpPreset>).Concat(userAmpPresets).ToArray();
    private FrameworkElement BuildAmplifier()
    {
        var panel=Stack();var sectionLabel=Text("ZVUKOVÝ ŘETĚZEC");sectionLabel.Style=(Style)Application.Current.Resources["VintageSectionTextStyle"];panel.Children.Add(sectionLabel);
        ampEnabledBox=new CheckBox{Content="Zapnout aparát",IsChecked=appSettings.AmpEnabled};ampEnabledBox.Checked+=AmplifierEnabled_Changed;ampEnabledBox.Unchecked+=AmplifierEnabled_Changed;panel.Children.Add(VintageHousing.PowerSwitch(ampEnabledBox));
        ampPresetsBox=PresetSelector();panel.Children.Add(ampPresetsBox);
        var parameters=new[]{("compression","Komprese"),("drive","Drive"),("bass","Basy"),("mid","Středy"),("treble","Výšky"),("delay","Delay"),("reverb","Reverb"),("chorus","Chorus"),("level","Výstup")};
        var controls=new List<FrameworkElement>();ampDials.Clear();
        foreach(var (id,label) in parameters)
        {
            var dial=new ValueDial{Label=label+" (%)",Minimum=id is "bass" or "mid" or "treble"?-100:0,Maximum=100,Value=AmpValue(id)*100};ampDials.Add(id,dial);
            dial.ValueChanged+=(_,_)=>{if(ampUiUpdating)return;SetAmpValue(id,dial.Value/100);QueueAmplifier();};controls.Add(dial);
        }
        panel.Children.Add(ResponsiveInstrumentColumns(controls.ToArray()));
        ampDistortionBox=new ComboBox{Header="Zkreslení",ItemsSource=new[]{"Overdrive","Distortion","Fuzz"},SelectedIndex=ampSettings.Distortion=="overdrive"?0:ampSettings.Distortion=="fuzz"?2:1};
        ampDistortionBox.SelectionChanged+=(_,_)=>{if(!ampUiUpdating){ampSettings=ampSettings with{Distortion=ampDistortionBox.SelectedIndex==0?"overdrive":ampDistortionBox.SelectedIndex==2?"fuzz":"distortion",PresetId=null};QueueAmplifier();}};panel.Children.Add(ampDistortionBox);
        ampCabinetBox=new ComboBox{Header="Kabinet",ItemsSource=new[]{"Moderní","Vintage","Otevřený"},SelectedIndex=ampSettings.Cabinet=="vintage"?1:ampSettings.Cabinet=="open"?2:0};
        ampCabinetBox.SelectionChanged+=(_,_)=>{if(!ampUiUpdating){ampSettings=ampSettings with{Cabinet=ampCabinetBox.SelectedIndex==1?"vintage":ampCabinetBox.SelectedIndex==2?"open":"modern",PresetId=null};QueueAmplifier();}};panel.Children.Add(ampCabinetBox);
        var save=new Button{Content="Uložit vlastní preset"};save.Click+=SaveAmpPreset_Click;var remove=new Button{Content="Odstranit vybraný vlastní preset"};remove.Click+=DeleteAmpPreset_Click;panel.Children.Add(save);panel.Children.Add(remove);
        ampStatus=Text("Presety a změny se průběžně ukládají. Monitoring zapínáš samostatně v Live.",true);panel.Children.Add(ampStatus);
        panel.Children.Add(Text("Vypnutý aparát vrací přesně suchý vstup. Komprese nepoužívá lookahead; 2× oversampling zkreslení přidá 15 vzorků FIR prodlevy. Odezvu celé karty tím neměříme. Nativní reverb a ostatní efekty mají vlastní zvuk; význam ovladačů odpovídá původním presetům.",true));return new VintageHousing(panel,"ROCKER / KYTAROVÝ APARÁT",speaker:true);
    }
    private FrameworkElement BuildCompactAmplifier()
    {
        var panel=Stack();liveAmpEnabledBox=new CheckBox{Content="Aparát",IsChecked=appSettings.AmpEnabled};liveAmpEnabledBox.Checked+=AmplifierEnabled_Changed;liveAmpEnabledBox.Unchecked+=AmplifierEnabled_Changed;panel.Children.Add(VintageHousing.PowerSwitch(liveAmpEnabledBox));
        liveAmpPresetsBox=PresetSelector();panel.Children.Add(liveAmpPresetsBox);var open=new Button{Content="Ovladače aparátu"};open.Click+=(_,_)=>Navigate(new("13"));panel.Children.Add(open);focusSecondary.Add(open);return new VintageHousing(panel,compact:true);
    }
    private ComboBox PresetSelector()
    {
        var box=new ComboBox{Header="Preset",ItemsSource=AllAmpPresets(),DisplayMemberPath="Name",HorizontalAlignment=HorizontalAlignment.Stretch};box.SelectedItem=((AmpPreset[])box.ItemsSource).FirstOrDefault(p=>p.Id==ampSettings.PresetId);
        box.SelectionChanged+=(_,_)=>{if(!ampUiUpdating && box.SelectedItem is AmpPreset preset){ampSettings=preset.Settings with{PresetId=preset.Id};QueueAmplifier();}};return box;
    }
    private void AmplifierEnabled_Changed(object sender,RoutedEventArgs args)
    {if(ampUiUpdating)return;appSettings=appSettings with{AmpEnabled=(sender as CheckBox)?.IsChecked==true};SaveSettings();QueueAmplifier();}
    private double AmpValue(string id)=>id switch{"compression"=>ampSettings.Compression,"drive"=>ampSettings.Drive,"bass"=>ampSettings.Bass,"mid"=>ampSettings.Mid,"treble"=>ampSettings.Treble,"delay"=>ampSettings.Delay,"reverb"=>ampSettings.Reverb,"chorus"=>ampSettings.Chorus,_=>ampSettings.Level};
    private void SetAmpValue(string id,double value)
    {
        ampSettings=(id switch{"compression"=>ampSettings with{Compression=value},"drive"=>ampSettings with{Drive=value},"bass"=>ampSettings with{Bass=value},"mid"=>ampSettings with{Mid=value},"treble"=>ampSettings with{Treble=value},"delay"=>ampSettings with{Delay=value},"reverb"=>ampSettings with{Reverb=value},"chorus"=>ampSettings with{Chorus=value},_=>ampSettings with{Level=value}}) with{PresetId=null};
    }
    private AmplifierConfiguration NativeAmplifier()=>new(appSettings.AmpEnabled,ampSettings.Distortion=="overdrive"?0u:ampSettings.Distortion=="fuzz"?2u:1u,ampSettings.Cabinet=="vintage"?1u:ampSettings.Cabinet=="open"?2u:0u,ampSettings.Compression,ampSettings.Drive,ampSettings.Bass,ampSettings.Mid,ampSettings.Treble,ampSettings.Delay,ampSettings.Reverb,ampSettings.Chorus,ampSettings.Level);
    private void QueueAmplifier()
    {
        if(closed || closingWindow)return;++ampRevision;
        if(persistenceReady)autosave!.Queue(UserData.Document("amp","default",ampSettings));
        RefreshAmplifierControls();
        if(!ampTimerInitialized){ampApplyTimer.Tick+=async(_,_)=>{ampApplyTimer.Stop();try{await ApplyAmplifierAsync();}catch(Exception error){if(!closed && !closingWindow)ShowPersistenceError("Aparát nelze nastavit. "+error.Message);}};ampTimerInitialized=true;}
        ampApplyTimer.Stop();ampApplyTimer.Start();
    }
    private void RefreshAmplifierControls()
    {
        ampUiUpdating=true;
        try{
            if(ampEnabledBox is not null)ampEnabledBox.IsChecked=appSettings.AmpEnabled;if(liveAmpEnabledBox is not null)liveAmpEnabledBox.IsChecked=appSettings.AmpEnabled;
            foreach(var entry in ampDials)entry.Value.Value=AmpValue(entry.Key)*100;
            if(ampDistortionBox is not null)ampDistortionBox.SelectedIndex=ampSettings.Distortion=="overdrive"?0:ampSettings.Distortion=="fuzz"?2:1;
            if(ampCabinetBox is not null)ampCabinetBox.SelectedIndex=ampSettings.Cabinet=="vintage"?1:ampSettings.Cabinet=="open"?2:0;
            foreach(var box in new[]{ampPresetsBox,liveAmpPresetsBox}.OfType<ComboBox>()){box.ItemsSource=AllAmpPresets();box.SelectedItem=((AmpPreset[])box.ItemsSource).FirstOrDefault(p=>p.Id==ampSettings.PresetId);}
        }finally{ampUiUpdating=false;}
    }
    private async Task ApplyAmplifierAsync()
    {
        if(ampApplying is not null){await ampApplying;return;}
        if(audio is null || closed || closingWindow)return;
        async Task ApplyCore(){while(appliedAmpRevision<ampRevision && !closed && !closingWindow){int revision=ampRevision;await audio.SetAmplifierAsync(NativeAmplifier());appliedAmpRevision=revision;}}
        ampApplying=ApplyCore();try{await ampApplying;if(ampStatus is not null && !closed)ampStatus.Text="Aparát je nastavený. Změny a presety se průběžně ukládají.";}finally{ampApplying=null;}
    }
    private async Task InitializeAmplifierAsync(IReadOnlyList<DataDocument> documents)
    {
        var settings=documents.FirstOrDefault(d=>d.Kind=="amp" && d.Id=="default");if(settings is not null)ampSettings=UserData.Read<AmpSettings>(settings.Value);
        userAmpPresets.Clear();var presets=documents.FirstOrDefault(d=>d.Kind=="amp-presets" && d.Id=="default");if(presets is not null)userAmpPresets.AddRange(UserData.Read<AmpPreset[]>(presets.Value));
        ++ampRevision;await ApplyAmplifierAsync();RefreshAmplifierControls();
    }
    private async Task VerifyAmplifierAsync()
    {
        if(!persistenceReady || audio is null)throw new InvalidOperationException("Amp persistence unavailable.");
        var factory=AllAmpPresets().First();ampSettings=factory.Settings with{PresetId=factory.Id};appSettings=appSettings with{AmpEnabled=false};SaveSettings();QueueAmplifier();await ApplyAmplifierAsync();
        var expected=NativeAmplifier();if(audio.GetAmplifier()!=expected)throw new InvalidOperationException("Factory preset did not reach native DSP.");
        var fixture=new AmpPreset("ci-amp-roundtrip","CI amp",ampSettings with{PresetId=null});userAmpPresets.RemoveAll(p=>p.Id==fixture.Id);userAmpPresets.Add(fixture);
        autosave!.Queue(UserData.Document("amp-presets","default",userAmpPresets.ToArray()));await autosave.FlushAsync();
        var reopened=await DataRepository.OpenAsync(userFiles);var saved=await reopened.SnapshotAsync();
        bool restored=UserData.Read<AmpSettings>(saved.Documents.Single(d=>d.Kind=="amp").Value)==ampSettings && UserData.Read<AmpPreset[]>(saved.Documents.Single(d=>d.Kind=="amp-presets").Value).Any(p=>p==fixture);
        WriteShellDiagnostic("amplifier-view.json",new{result=restored && ampDials.Count==9?"passed":"failed",nativeParametersVerified=true,persistenceReopened=restored,nativeDialCount=ampDials.Count,factoryPresetCount=catalog!.Items("ampPresets").Count(),audioPlaybackVerified=false,manualPresetDialogsVerified=false});
        if(!restored || ampDials.Count!=9)throw new InvalidOperationException("Amp UI or persistence contract failed.");
    }
    private async void SaveAmpPreset_Click(object sender,RoutedEventArgs args)
    {
        if(!persistenceReady || closingWindow)return;
        try{
            var name=new TextBox{Header="Název",MaxLength=120};var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Uložit preset",Content=name,PrimaryButtonText="Uložit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Primary};
            if(await dialog.ShowAsync()!=ContentDialogResult.Primary || string.IsNullOrWhiteSpace(name.Text))return;
            var preset=new AmpPreset("user-"+Guid.NewGuid().ToString("N"),name.Text.Trim(),ampSettings with{PresetId=null});userAmpPresets.Add(preset);ampSettings=ampSettings with{PresetId=preset.Id};
            autosave!.Queue(UserData.Document("amp-presets","default",userAmpPresets.ToArray()));QueueAmplifier();
        }catch(Exception error){ShowPersistenceError("Preset nelze uložit. "+error.Message);}
    }
    private async void DeleteAmpPreset_Click(object sender,RoutedEventArgs args)
    {
        var preset=userAmpPresets.FirstOrDefault(p=>p.Id==ampSettings.PresetId);if(preset is null){ShowPersistenceError("Vyber vlastní preset. Tovární presety se nemažou.");return;}
        if(!persistenceReady || closingWindow)return;
        try{
            var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Odstranit preset?",Content=preset.Name,PrimaryButtonText="Odstranit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};
            if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;userAmpPresets.Remove(preset);ampSettings=ampSettings with{PresetId=null};autosave!.Queue(UserData.Document("amp-presets","default",userAmpPresets.ToArray()));QueueAmplifier();
        }catch(Exception error){ShowPersistenceError("Preset nelze odstranit. "+error.Message);}
    }
}
