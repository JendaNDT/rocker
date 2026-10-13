// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
using Rocker.Interop;
using Windows.Storage.Pickers;
using Path=System.IO.Path;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private RecordingLibrary? recordingLibrary;
    private CaptureDraft? captureDraft;
    private bool recordingOperation;
    private ListView? recordingList;
    private readonly List<TextBlock> recordingLabels=[];
    private readonly List<Button> recordingStarts=[],recordingStops=[];
    private RecordingData? playingRecording;
    private bool exportOperation;private CancellationTokenSource? exportCancellation;
    private readonly List<Marker> captureMarkers=[];
    private uint recordingBus,recordingCountIn;
    private string recordingName="Moje kytara";
    private async Task InitializeRecordingsAsync()
    {
        recordingLibrary=new(userFiles,database!);var messages=await recordingLibrary.RecoverAsync();
        recordingLabels.Clear();recordingStarts.Clear();recordingStops.Clear();recordingList=null;playingRecording=null;
        if(messages.Count>0){ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Warning;ShellNotice.Message=string.Join("\n",messages);}
    }
    private FrameworkElement BuildRecordingControls(bool compact=false)
    {
        var panel=Stack();var settings=compact?Stack():panel;if(compact){var title=Text("Nahrávání");title.FontSize=18;title.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;panel.Children.Add(title);}var name=new TextBox{Header="Název nahrávky",Text=recordingName,MaxLength=256};name.TextChanged+=(_,_)=>recordingName=name.Text;settings.Children.Add(name);
        var bus=new ComboBox{Header="Co nahrávat",ItemsSource=new[]{"Suchá kytara · mono","Kytara za aparátem · mono","Výsledný mix · stereo"},SelectedIndex=(int)recordingBus};bus.SelectionChanged+=(_,_)=>{if(bus.SelectedIndex>=0)recordingBus=(uint)bus.SelectedIndex;};settings.Children.Add(bus);
        var count=new ComboBox{Header="Předtakt",ItemsSource=new[]{"Bez předtaktu","1 takt","2 takty"},SelectedIndex=(int)recordingCountIn};count.SelectionChanged+=(_,_)=>{if(count.SelectedIndex>=0)recordingCountIn=(uint)count.SelectedIndex;};settings.Children.Add(count);
        settings.Children.Add(Text("Předtakt používá tempo a takt metronomu. Výsledný mix obsahuje jen zvuk Rockeru; YouTube se nenahrává.",true));
        var start=new Button{Content=compact?"Nahrávat kytaru":"Spustit nahrávání",MinHeight=40,Tag=compact?"compact-recording":null};start.Click+=async(_,_)=>await StartCaptureAsync();recordingStarts.Add(start);panel.Children.Add(start);
        var stop=new Button{Content=compact?"Zastavit a uložit":"Zastavit a uložit nahrávku",MinHeight=40,Tag=compact?"compact-recording":null,IsEnabled=captureDraft is not null};stop.Click+=async(_,_)=>await StopCaptureAsync();recordingStops.Add(stop);panel.Children.Add(stop);
        var marker=new Button{Content="Přidat značku během nahrávání…"};marker.Click+=async(_,_)=>{if(captureDraft is null || audio is null)return;double time=audio.GetRecording().CapturedFrames/(double)captureDraft.Rate;var label=await AskRecordingNameAsync("Nová značka","Značka");if(label is not null)captureMarkers.Add(new(Guid.NewGuid().ToString("N"),time,label));};settings.Children.Add(marker);
        var status=Text("Nahrávání je zastavené.",true);recordingLabels.Add(status);panel.Children.Add(status);if(compact){var more=new Expander{Header="Název, zdroj a předtakt",Content=settings,HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(more);focusSecondary.Add(more);}return Card(panel);
    }
    private FrameworkElement BuildRecordings()
    {
        var panel=Stack();var libraryPanel=Stack();
        recordingList=new ListView{Header="Knihovna nahrávek",DisplayMemberPath="LibraryLabel",SelectionMode=ListViewSelectionMode.Multiple,IsItemClickEnabled=true,MaxHeight=240};
        recordingList.ItemClick+=async(_,e)=>{if(e.ClickedItem is RecordingData recording){try{await PlayRecordingAsync(recording);}catch(Exception error){ShowPersistenceError(error.Message);}}};libraryPanel.Children.Add(recordingList);
        Button Action(string label,Func<RecordingData,Task> action){var button=new Button{Content=label};button.Click+=async(_,_)=>{if(recordingList?.SelectedItem is RecordingData selected)try{await action(selected);}catch(Exception error){ShowPersistenceError(error.Message);}};panel.Children.Add(button);return button;}
        var play=Action("Přehrát vybranou nahrávku",PlayRecordingAsync);panel.Children.Remove(play);libraryPanel.Children.Add(play);
        var preview=Action("Zobrazit průběh bez přehrávání",PreviewRecordingAsync);panel.Children.Remove(preview);libraryPanel.Children.Add(preview);
        panel.Children.Add(ResponsiveColumns(Card(libraryPanel),BuildRecordingWaveform()));
        panel.Children.Add(BuildRecordingControls(compact:true));
        var compareA=new Button{Content="Porovnat A · první ze dvou vybraných"};var compareB=new Button{Content="Porovnat B · druhá ze dvou vybraných"};
        async Task Compare(int index){if(recordingList.SelectedItems.Count!=2 || busy)return;var pair=recordingList.SelectedItems.Cast<RecordingData>().ToArray();double position=audio?.GetTransport().PositionSeconds*currentSpeed??0;await PlayRecordingAsync(pair[index]);if(currentSpeed!=1)await ChangeSongSpeedAsync(1);if(audio is not null)await audio.SeekAsync(Math.Min(position,pair[index].DurationSec));}
        compareA.Click+=async(_,_)=>{try{await Compare(0);}catch(Exception error){ShowPersistenceError(error.Message);}};compareB.Click+=async(_,_)=>{try{await Compare(1);}catch(Exception error){ShowPersistenceError(error.Message);}};panel.Children.Add(compareA);panel.Children.Add(compareB);
        var removeMany=new Button{Content="Odstranit vybrané nahrávky…"};removeMany.Click+=async(_,_)=>{try{var chosen=recordingList.SelectedItems.Cast<RecordingData>().ToArray();if(chosen.Length==0)return;var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Odstranit vybrané nahrávky?",Content=$"Počet: {chosen.Length}. Celou poslední skupinu odstranění lze vrátit. Zvukové soubory zůstávají zachované.",PrimaryButtonText="Odstranit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;await recordingLibrary!.RemoveManyAsync(chosen);await ReloadRecordingsAsync();}catch(Exception error){ShowPersistenceError(error.Message);}};panel.Children.Add(removeMany);
        Action("Přejmenovat…",async r=>{var name=await AskRecordingNameAsync("Přejmenovat nahrávku",r.Name);if(name is not null){await recordingLibrary!.SaveAsync(r with{Name=name});await ReloadRecordingsAsync();}});
        Action("Značky a poznámky…",EditRecordingMarkersAsync);
        Action("Exportovat původní soubor…",ExportRecordingAsync);
        Action("Odstranit z knihovny…",async r=>{var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Odstranit nahrávku?",Content=r.Name+"\nPoslední odstranění lze vrátit. Zvuk zůstává pro použití v projektech.",PrimaryButtonText="Odstranit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await dialog.ShowAsync()==ContentDialogResult.Primary){await recordingLibrary!.RemoveAsync(r);await ReloadRecordingsAsync();}});
        var undo=new Button{Content="Vrátit poslední odstranění"};undo.Click+=async(_,_)=>{try{await recordingLibrary!.UndoAsync();await ReloadRecordingsAsync();}catch(Exception error){ShowPersistenceError(error.Message);}};panel.Children.Add(undo);
        panel.Children.Add(Text("Kliknutím nahrávku přehraješ; posuvník dole mění pozici. Neúplné a obnovené záznamy jsou označené v přehledu.",true));_ = ReloadRecordingsAsync();return panel;
    }
    private async Task ReloadRecordingsAsync()
    {
        if(recordingLibrary is null || recordingList is null)return;
        try{var values=await recordingLibrary.ListAsync();if(closed || recordingList is null)return;recordingList.ItemsSource=values;if(playingRecording is not null){playingRecording=values.FirstOrDefault(r=>r.Id==playingRecording.Id);DrawRecordingWaveform();}var summary=string.Join("\n",values.Select(r=>$"{r.Name} · {FormatTime(r.DurationSec)}"+(r.Capture?.Partial==true?" · neúplná / obnovená":"")));Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(recordingList,summary);}
        catch(Exception error){if(!closed)ShowPersistenceError("Knihovnu nelze načíst. "+error.Message);}
    }
    private async Task<string?> AskRecordingNameAsync(string title,string current)
    {
        var text=new TextBox{Text=current,MaxLength=256,Header="Název"};var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title=title,Content=text,PrimaryButtonText="Uložit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Primary};
        return await dialog.ShowAsync()==ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(text.Text)?text.Text.Trim():null;
    }
    private async Task StartCaptureAsync(ulong projectTicket=0)
    {
        if(recordingOperation || captureDraft is not null || audio is null || recordingLibrary is null || closingWindow || busy)return;
        if(deviceSelectionUnavailable || deviceOperationRunning || (recordingBus<2 && appSettings.AudioDevice is not{InputChannel:>=0})){ShowPersistenceError("Vyber konkrétní vstup kytary v nastavení zvuku.");return;}
        var selectedBus=recordingBus;var selectedCountIn=recordingCountIn;var selectedName=recordingName;
        recordingOperation=true;try
        {
            if(audio.GetSnapshot().State!=AudioState.Running)await audio.StartLiveAsync();var details=await audio.GetDeviceDetailsAsync();
            var draft=await recordingLibrary.BeginAsync(selectedName,checked((int)Math.Round(details.SampleRate)),selectedBus==2?2:1,(int)selectedBus);
            captureDraft=draft;captureMarkers.Clear();RegisterCloseGuard("recording",new(true,false),StopCaptureAsync,()=>{});
            await audio.StartRecordingAsync(recordingLibrary.PendingPath(draft.Id),selectedBus,selectedCountIn,projectTicket);
        }catch(Exception error){ShowPersistenceError("Nahrávání se nespustilo. "+error.Message);}
        finally{recordingOperation=false;RefreshRecording();}
    }
    private async Task<bool> StopCaptureAsync()
    {
        if(recordingOperation)return false;if(captureDraft is null)return true;if(audio is null || recordingLibrary is null)return false;
        recordingOperation=true;try
        {
            var draft=captureDraft;await audio.StopRecordingAsync();var state=audio.GetRecording();
            if(state.Active)throw new IOException("Zvukový soubor se stále dokončuje.");
            var result=new CaptureMetadata(draft.Rate,draft.Channels,draft.Bus,checked((long)state.WrittenFrames),checked((long)state.FirstFrame),state.Epoch,state.DspLatencyFrames,(state.Flags&16)!=0,!state.Complete,state.Complete?null:$"Nahrávání bylo přerušeno (stav {state.Failure}).");
            var recording=await recordingLibrary.FinishAsync(draft,result);
            if(recording is not null && captureMarkers.Count>0){var markers=captureMarkers.Where(m=>m.TimeSec<=recording.DurationSec).ToArray();await recordingLibrary.SaveAsync(recording with{Markers=markers});}
            var alignment=audio.GetRecordingAlignment();if(recording is not null)await AttachStudioCaptureAsync(recording,alignment);if(studioCaptureBacking)await audio.PauseAsync();studioCaptureBacking=false;
            captureDraft=null;captureMarkers.Clear();RemoveCloseGuard("recording");await ReloadRecordingsAsync();return true;
        }catch(Exception error){ShowPersistenceError("Nahrávku nelze dokončit; rozpracovaný soubor zůstává zachovaný. "+error.Message);return false;}
        finally{recordingOperation=false;RefreshRecording();}
    }
    private void RefreshRecording()
    {
        if(audio is null)return;try
        {
            RefreshRecordingWaveform();var state=audio.GetRecording();string status=captureDraft is null?"Nahrávání je zastavené.":$"{captureDraft.Name} · {FormatTime(state.Duration)} · "+(state.State==1?"Předtakt…":state.State==3?"Dokončuji zápis…":state.State==5?"Přerušeno; zachovávám dostupný zvuk.":"Nahrávání");
            foreach(var label in recordingLabels)label.Text=status;foreach(var start in recordingStarts){start.IsEnabled=captureDraft is null && !recordingOperation && persistenceReady;if(start.Tag as string=="compact-recording")start.Visibility=captureDraft is null?Visibility.Visible:Visibility.Collapsed;}foreach(var stop in recordingStops){stop.IsEnabled=captureDraft is not null && !recordingOperation;if(stop.Tag as string=="compact-recording")stop.Visibility=captureDraft is not null?Visibility.Visible:Visibility.Collapsed;}
            if(captureDraft is not null && !recordingOperation && state.State is 4 or 5)_=StopCaptureAsync();
        }catch(Exception error){if(!closed)ShowPersistenceError("Stav nahrávání není dostupný. "+error.Message);}
    }
    private async Task PlayRecordingAsync(RecordingData value)
    {
        if(await PreviewRecordingAsync(value) && audio is not null && playingRecording?.Id==value.Id && RecordingWaveformReady())await audio.PlayAsync();
    }
    private async Task EditRecordingMarkersAsync(RecordingData value)
    {
        var panel=Stack();var markers=value.Markers.ToList();var list=new ListView{ItemsSource=markers,DisplayMemberPath="Label",SelectionMode=ListViewSelectionMode.Single};panel.Children.Add(list);
        var label=new TextBox{Header="Popisek značky",MaxLength=256};panel.Children.Add(label);var time=new NumberBox{Header="Čas (sekundy)",Minimum=0,Maximum=value.DurationSec,Value=playingRecording?.Id==value.Id && audio is not null?Math.Min(value.DurationSec,audio.GetTransport().PositionSeconds*currentSpeed):0,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};panel.Children.Add(time);
        var add=new Button{Content="Přidat značku"};add.Click+=(_,_)=>{if(string.IsNullOrWhiteSpace(label.Text)||!double.IsFinite(time.Value)||markers.Count>=1000)return;markers.Add(new(Guid.NewGuid().ToString("N"),Math.Clamp(time.Value,0,value.DurationSec),label.Text.Trim()));list.ItemsSource=markers.OrderBy(m=>m.TimeSec).ToArray();};panel.Children.Add(add);
        var remove=new Button{Content="Odstranit vybranou značku"};remove.Click+=(_,_)=>{if(list.SelectedItem is Marker selected){markers.Remove(selected);list.ItemsSource=markers.OrderBy(m=>m.TimeSec).ToArray();}};panel.Children.Add(remove);
        var seek=new Button{Content="Přejít na vybranou značku"};seek.Click+=async(_,_)=>{if(list.SelectedItem is Marker selected){try{if(playingRecording?.Id!=value.Id)await PlayRecordingAsync(value);if(audio is not null)await audio.SeekAsync(selected.TimeSec/currentSpeed);}catch(Exception error){ShowPersistenceError(error.Message);}}};panel.Children.Add(seek);
        var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title=value.Name,Content=panel,PrimaryButtonText="Uložit značky",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Primary};if(await dialog.ShowAsync()==ContentDialogResult.Primary){await recordingLibrary!.SaveAsync(value with{Markers=Practice.SortMarkers(markers)});await ReloadRecordingsAsync();}
    }
    private async Task ExportRecordingAsync(RecordingData value)
    {
        if(exportOperation)return;
        var options=Stack();options.Children.Add(Text(value.Capture is { } capture?new[]{"Suchá kytara","Kytara za aparátem","Výsledný mix"}[capture.Bus]:"Původní importovaná nahrávka"));
        var format=new ComboBox{Header="Formát",ItemsSource=new[]{"Původní soubor beze změny","WAV · PCM 24 bit"},SelectedIndex=0};options.Children.Add(format);
        var start=new NumberBox{Header="Od (sekundy)",Minimum=0,Maximum=value.DurationSec,Value=0,IsEnabled=false};var end=new NumberBox{Header="Do (sekundy)",Minimum=0,Maximum=value.DurationSec,Value=value.DurationSec,IsEnabled=false};options.Children.Add(start);options.Children.Add(end);format.SelectionChanged+=(_,_)=>start.IsEnabled=end.IsEnabled=format.SelectedIndex==1;
        var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Export nahrávky",Content=options,PrimaryButtonText="Vybrat soubor…",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Primary};if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
        if(format.SelectedIndex==1 && (!double.IsFinite(start.Value)||!double.IsFinite(end.Value)||end.Value<=start.Value))throw new InvalidDataException("Vyber platný rozsah exportu.");
        var picker=new FileSavePicker{SuggestedFileName=value.Name};picker.FileTypeChoices.Add(format.SelectedIndex==0?"Původní zvuk":"WAV PCM24",new[]{format.SelectedIndex==0?Path.GetExtension(value.RelativePath):".wav"});WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var file=await picker.PickSaveFileAsync();if(file is null)return;
        var dataRoot=userFiles.Root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(Path.GetFullPath(file.Path).StartsWith(dataRoot,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Export zvol mimo datovou složku Rockeru.");
        exportOperation=true;exportCancellation=new();var cancellation=exportCancellation.Token;
        try{if(format.SelectedIndex==0)await recordingLibrary!.ExportOriginalAsync(value,file.Path,cancellation);else{if(!await new OwnedMedia(userFiles).VerifyAsync(new(value.RelativePath,value.Sha256,value.Bytes),cancellation))throw new InvalidDataException("Nahrávka chybí nebo je změněná.");var progress=new Progress<double>(fraction=>{if(!closed){ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Informational;ShellNotice.Message=$"Export WAV · {fraction:P0}. Zrušení je dostupné v pohledu Export.";}});await MediaExport.WavAsync(OwnedPaths.Resolve(userFiles.Root,value.RelativePath),file.Path,start.Value,end.Value,progress,cancellation);}if(!closed){ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Success;ShellNotice.Message="Nahrávka byla exportována: "+file.Name;}}
        catch(OperationCanceledException){if(!closed)ShowPersistenceError("Export byl zrušen. Původní cílový soubor je zachovaný.");}finally{exportOperation=false;exportCancellation.Dispose();exportCancellation=null;}
    }
    private FrameworkElement BuildRecordingExport()
    {
        var panel=Stack();panel.Children.Add(Text("Export nahrávky"));var open=new Button{Content="Vybrat nahrávku v knihovně"};open.Click+=(_,_)=>Navigate(new("14"));panel.Children.Add(open);var cancel=new Button{Content="Zrušit probíhající export"};cancel.Click+=(_,_)=>exportCancellation?.Cancel();panel.Children.Add(cancel);panel.Children.Add(Text("V knihovně vyber nahrávku a použij Exportovat původní soubor. Původní nahrávky z webu se exportují beze změny formátu.",true));return Card(panel);
    }
    private async Task VerifyRecordingsAsync()
    {
        if(recordingLibrary is null || database is null)throw new InvalidOperationException("Recording library did not initialize.");
        Navigate(new("14"));await ReloadRecordingsAsync();
        var draft=await recordingLibrary.BeginAsync("Kontrolní nahrávka",48000,1,0);
        var path=recordingLibrary.PendingPath(draft.Id);byte[] wave=new byte[44+480*3];"RIFF"u8.CopyTo(wave);"WAVEfmt "u8.CopyTo(wave.AsSpan(8));"data"u8.CopyTo(wave.AsSpan(36));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(wave.AsSpan(4),36+1440);System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(wave.AsSpan(16),16);System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(20),1);System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(22),1);System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(wave.AsSpan(24),48000);System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(wave.AsSpan(28),144000);System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(32),3);System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(34),24);System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(wave.AsSpan(40),1440);await File.WriteAllBytesAsync(path,wave);
        var result=await recordingLibrary.FinishAsync(draft,new(48000,1,0,480,0,1,0,false,false));await ReloadRecordingsAsync();
        var reopened=await DataRepository.OpenAsync(userFiles);var persisted=(await new RecordingLibrary(userFiles,reopened).ListAsync()).Single(r=>r.Id==draft.Id);
        bool passed=result is not null && persisted.Capture?.Frames==480 && recordingList?.Items.Count>0 && recordingStarts.Count>=1;
        WriteShellDiagnostic("recordings-view.json",new{result=passed?"passed":"failed",persistenceReopened=persisted.Capture?.Frames==480,nativeLibraryList=recordingList is not null,nativeRecordingControls=recordingStarts.Count,hardwareCaptureVerified=false,manualDialogsVerified=false});if(!passed)throw new InvalidOperationException("Recording form/persistence failed.");
    }

}
