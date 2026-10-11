// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Rocker.Domain;
using Rocker.Infrastructure;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Path=System.IO.Path;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private readonly Dictionary<string,SongSession> songs=[];
    private SongSession? song;
    private ComboBox? songSelector;
    private ListView? songSections;
    private NumberBox? songA,songB,songSpeed;
    private Canvas? songWaveform;
    private TextBlock? songReading;
    private Button? songCancel;
    private float[] songPeaks=[];
    private bool songUpdating,songLoadCancelled;
    private Task? songRampTask;private double? songRampQueuedSpeed;
    private DateTime songLastSave=DateTime.MinValue;
    private void InitializeSongs(IReadOnlyList<DataDocument> documents)
    {
        songs.Clear();foreach(var item in documents.Where(d=>d.Kind=="song")){var entry=UserData.Read<SongSession>(item.Value);songs[entry.Id]=entry;}
        song=null;songPeaks=[];songSelector=null;songSections=null;songReading=null;songWaveform=null;
    }
    private FrameworkElement BuildSong()
    {
        songUpdating=true;
        var panel=Stack();panel.AllowDrop=true;
        panel.DragOver+=(_,e)=>{if(e.DataView.Contains(StandardDataFormats.StorageItems))e.AcceptedOperation=DataPackageOperation.Copy;};
        panel.Drop+=async(_,e)=>{try{if(e.DataView.Contains(StandardDataFormats.StorageItems) && (await e.DataView.GetStorageItemsAsync()).OfType<StorageFile>().FirstOrDefault() is { } file)await OpenSongFileAsync(file.Path,file.Name);}catch(Exception error){ShowError(error);}};
        var open=new Button{Content="Otevřít skladbu…"};open.Click+=OpenMedia_Click;panel.Children.Add(open);
        songCancel=new Button{Content="Zrušit načítání",IsEnabled=busy};songCancel.Click+=(_,_)=>{songLoadCancelled=true;audio?.CancelMediaPreparation();MediaStatus.Text="Načítání bylo zrušeno. Předchozí skladba zůstává dostupná.";};panel.Children.Add(songCancel);
        songSelector=new ComboBox{Header="Naposledy otevřené skladby",DisplayMemberPath="Name",ItemsSource=songs.Values.OrderBy(s=>s.Name).ToArray(),HorizontalAlignment=HorizontalAlignment.Stretch};
        songSelector.SelectionChanged+=async(_,_)=>{if(!songUpdating && !busy && songSelector.SelectedItem is SongSession selected){try{if(File.Exists(selected.SourcePath))await OpenSongFileAsync(selected.SourcePath,selected.Name);else await RelocateSongAsync(selected);}catch(Exception error){ShowError(error);}}};panel.Children.Add(songSelector);
        songReading=Text(song is null?"Vyber skladbu nebo sem přetáhni zvukový soubor.":song.Name,true);panel.Children.Add(songReading);
        songWaveform=new Canvas{Height=140,HorizontalAlignment=HorizontalAlignment.Stretch};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(songWaveform,"Zvuková časová osa; pozici lze měnit také posuvníkem přehrávače.");
        songWaveform.SizeChanged+=(_,_)=>DrawSongWaveform();songWaveform.PointerPressed+=async(_,e)=>{if(song is null || busy || audio is null || songWaveform.ActualWidth<=0)return;try{var seconds=Math.Clamp(e.GetCurrentPoint(songWaveform).Position.X/songWaveform.ActualWidth,0,1)*song.Duration;await audio.SeekAsync(seconds/currentSpeed);}catch(Exception error){ShowError(error);}};panel.Children.Add(Card(songWaveform));
        var range=Stack();songA=SongNumber("Začátek A (sekundy ve skladbě)",0,0,song?.Duration??86400,.1);songB=SongNumber("Konec B (sekundy ve skladbě)",song?.Duration??1,0,song?.Duration??86400,.1);range.Children.Add(songA);range.Children.Add(songB);
        songA.ValueChanged+=(_,_)=>DrawSongWaveform();songB.ValueChanged+=(_,_)=>DrawSongWaveform();
        var markA=new Button{Content="Nastavit A na aktuální pozici"};markA.Click+=(_,_)=>{if(audio is not null)songA.Value=audio.GetTransport().PositionSeconds*currentSpeed;};range.Children.Add(markA);
        var markB=new Button{Content="Nastavit B na aktuální pozici"};markB.Click+=(_,_)=>{if(audio is not null)songB.Value=audio.GetTransport().PositionSeconds*currentSpeed;};range.Children.Add(markB);
        var loop=new Button{Content="Opakovat úsek A–B"};loop.Click+=async(_,_)=>{try{await ApplySongLoopAsync();}catch(Exception error){ShowError(error);}};range.Children.Add(loop);
        var whole=new Button{Content="Vypnout opakování"};whole.Click+=async(_,_)=>{try{if(audio is not null)await audio.SetLoopAsync(0,0,false);}catch(Exception error){ShowError(error);}};range.Children.Add(whole);
        var save=new Button{Content="Uložit pojmenovaný úsek…"};save.Click+=async(_,_)=>{try{await SaveSongSectionAsync();}catch(Exception error){ShowError(error);}};range.Children.Add(save);panel.Children.Add(Card(range));
        songSections=new ListView{Header="Uložené úseky",DisplayMemberPath="Label",IsItemClickEnabled=true,SelectionMode=ListViewSelectionMode.Single,ItemsSource=song?.Sections??[]};
        songSections.ItemClick+=async(_,e)=>{if(e.ClickedItem is SongSection section){songA.Value=section.Start;songB.Value=section.End;try{await ApplySongLoopAsync();}catch(Exception error){ShowError(error);}}};panel.Children.Add(songSections);
        var remove=new Button{Content="Odstranit vybraný úsek"};remove.Click+=async(_,_)=>{if(song is null || songSections.SelectedItem is not SongSection selected)return;var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Odstranit úsek?",Content=selected.Label,PrimaryButtonText="Odstranit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await dialog.ShowAsync()==ContentDialogResult.Primary){song=song with{Sections=song.Sections!.Where(s=>s.Id!=selected.Id).ToArray()};SaveSong();songSections.ItemsSource=song.Sections;}};panel.Children.Add(remove);
        songSpeed=SongNumber("Rychlost bez změny výšky",currentSpeed,.25,2,.05);songSpeed.ValueChanged+=async(_,e)=>{if(songUpdating || busy || !double.IsFinite(e.NewValue))return;try{await ChangeSongSpeedAsync(e.NewValue);}catch(Exception error){ShowError(error);}};panel.Children.Add(songSpeed);
        var countIn=SongNumber("Předtakt (takty)",song?.CountInBars??0,0,2,1);var bpm=SongNumber("Tempo předtaktu (BPM)",song?.Bpm??100,40,240,1);var beats=SongNumber("Doby v taktu",song?.Beats??4,1,16,1);
        void SavePractice(){if(song is null || songUpdating || !double.IsFinite(countIn.Value) || !double.IsFinite(bpm.Value) || !double.IsFinite(beats.Value))return;song=song with{CountInBars=(uint)countIn.Value,Bpm=Math.Round(bpm.Value),Beats=(uint)beats.Value};SaveSong();}
        countIn.ValueChanged+=(_,_)=>SavePractice();bpm.ValueChanged+=(_,_)=>SavePractice();beats.ValueChanged+=(_,_)=>SavePractice();panel.Children.Add(countIn);panel.Children.Add(bpm);panel.Children.Add(beats);
        var ramp=new ToggleSwitch{Header="Postupné zrychlování A–B",IsOn=song?.Ramp??false};var rampStep=SongNumber("Přírůstek rychlosti",song?.RampStep??.05,.01,.25,.01);var rampEvery=SongNumber("Zrychlit po opakováních",song?.RampEvery??4,1,32,1);var rampTarget=SongNumber("Cílová rychlost",song?.RampTarget??1,.25,2,.05);
        void SaveRamp(){if(song is null || songUpdating || !double.IsFinite(rampStep.Value) || !double.IsFinite(rampEvery.Value) || !double.IsFinite(rampTarget.Value))return;song=song with{Ramp=ramp.IsOn,RampStep=rampStep.Value,RampEvery=(uint)rampEvery.Value,RampTarget=rampTarget.Value};SaveSong();}
        ramp.Toggled+=async(_,_)=>{SaveRamp();if(!ramp.IsOn){try{await CancelSongRampAsync();}catch(Exception error){ShowError(error);}}};rampStep.ValueChanged+=(_,_)=>SaveRamp();rampEvery.ValueChanged+=(_,_)=>SaveRamp();rampTarget.ValueChanged+=(_,_)=>SaveRamp();panel.Children.Add(ramp);panel.Children.Add(rampStep);panel.Children.Add(rampEvery);panel.Children.Add(rampTarget);
        var play=new Button{Content="Přehrát s předtaktem"};play.Click+=async(_,_)=>{if(song is null || audio is null || busy)return;try{SavePractice();var config=audio.GetMetronome() with{Enabled=false,Ramp=false};await audio.SetMetronomeAsync(config);await audio.SetMetronomeAsync(config with{Enabled=true,Bpm=(uint)song.Bpm,Beats=song.Beats,CountIn=song.CountInBars});await audio.PlayWithCountInAsync(song.CountInBars,song.Beats,song.Bpm);}catch(Exception error){ShowError(error);}};panel.Children.Add(play);
        var pause=new Button{Content="Pozastavit skladbu"};pause.Click+=async(_,_)=>{try{if(audio is not null)await audio.PauseAsync();}catch(Exception error){ShowError(error);}};panel.Children.Add(pause);
        panel.Children.Add(Text("Skladba a monitoring kytary mají samostatné ovládání. Escape zastaví všechny zdroje. Soubory zůstávají na původním místě; záloha uchová jejich pozice a úseky, původní zvuk připojené skladby zálohuj zvlášť.",true));
        songUpdating=false;DrawSongWaveform();return panel;
    }
    private static NumberBox SongNumber(string label,double value,double minimum,double maximum,double step)=>new(){Header=label,Minimum=minimum,Maximum=maximum,Value=value,SmallChange=step,LargeChange=step*10,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact};
    private async Task RelocateSongAsync(SongSession missing)
    {
        var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Soubor skladby chybí",Content=missing.Name,PrimaryButtonText="Najít soubor…",CloseButtonText="Zrušit"};if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
        var picker=new FileOpenPicker();WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));foreach(var ext in new[]{".wav",".mp3",".ogg",".opus",".webm",".m4a",".mp4"})picker.FileTypeFilter.Add(ext);var file=await picker.PickSingleFileAsync();if(file is not null)await OpenSongFileAsync(file.Path,missing.Name,missing);
    }
    private async Task WaitForSongAsync(double speed)
    {
        for(int i=0;i<200;++i){if(closed || songLoadCancelled)throw new OperationCanceledException();var transport=audio!.GetTransport();if(transport.Ready!=0 && (transport.Reserved&16)==0 && Math.Abs(transport.Speed-speed)<1e-8)return;await Task.Delay(10);}
        throw new InvalidOperationException("Přehrávač zatím nepřevzal novou skladbu.");
    }
    private async Task OpenSongFileAsync(string path,string name,SongSession? relocated=null)
    {
        if(busy || audio is null)return;var previous=song;
        try{
            SetBusy(true);await CancelSongRampAsync();songLoadCancelled=false;if(songCancel is not null)songCancel.IsEnabled=true;MediaStatus.Text="Připravuji skladbu a zvukovou časovou osu…";
            var existing=relocated??songs.Values.FirstOrDefault(s=>string.Equals(s.SourcePath,path,StringComparison.OrdinalIgnoreCase));var speed=existing?.Speed??currentSpeed;
            await audio.PrepareFileAsync(path,speed);if(closed || songLoadCancelled)return;await WaitForSongAsync(speed);
            studioOwnsPlayback=false;studioTicket=0;
            var transport=audio.GetTransport();songPeaks=await audio.GetWaveformAsync();currentSpeed=speed;mediaPath=path;mediaMidi=null;
            var duration=transport.DurationSeconds*speed;
            song=SongRules.Validate((existing??new SongSession(Guid.NewGuid().ToString("D"),name,path,duration)) with{SourcePath=path,Duration=duration,Speed=speed,Position=Math.Min(existing?.Position??0,duration),Sections=(existing?.Sections??[]).Where(s=>s.End<=duration).ToArray()});
            await audio.SeekAsync(song.Position/speed);MediaTitle.Text=song.Name;SaveSong();workspaceCache.Remove(new("07"));ShowWorkspace();MediaStatus.Text="Skladba je připravená; monitoring zůstal samostatný. Přehrávání spustíš tlačítkem.";
        }catch(OperationCanceledException){song=previous;}
        finally{if(!closed){SetBusy(false);if(songCancel is not null)songCancel.IsEnabled=false;}}
    }
    private async Task ChangeSongSpeedAsync(double speed)
    {
        if(audio is null || busy || (mediaPath is null && mediaMidi is null) || speed==currentSpeed)return;
        var previous=currentSpeed;var transport=audio.GetTransport();var sourcePosition=transport.PositionSeconds*previous;
        try{
            SetBusy(true);await CancelSongRampAsync();songLoadCancelled=false;if(songCancel is not null)songCancel.IsEnabled=true;MediaStatus.Text="Připravuji novou rychlost; živý vstup pokračuje…";
            if(mediaPath is not null)await audio.PrepareFileAsync(mediaPath,speed);else await audio.PrepareMidiAsync(mediaMidi!,speed);if(closed || songLoadCancelled)return;await WaitForSongAsync(speed);currentSpeed=speed;
            await audio.SeekAsync(Math.Min(sourcePosition/speed,audio.GetTransport().DurationSeconds));songPeaks=await audio.GetWaveformAsync();
            if(song is not null && mediaPath is not null){song=song with{Speed=speed,Position=Math.Min(sourcePosition,song.Duration)};SaveSong();if((transport.Reserved&1)!=0)await ApplySongLoopAsync();}
            if(notationLoop is { } loop && mediaMidi is not null && ReferenceEquals(mediaMidi,notationPlayingMidi))await audio.SetLoopAsync(loop.Start/speed,loop.End/speed,true);
            if(transport.Playing!=0)await audio.PlayAsync();MediaStatus.Text="Rychlost byla změněna bez změny výšky tónu.";
        }catch{currentSpeed=previous;throw;}
        finally{if(!closed){songUpdating=true;SpeedBox.SelectedIndex=Array.IndexOf(new[]{.25,.5,.75,1.0,1.5,2.0},currentSpeed);if(songSpeed is not null)songSpeed.Value=currentSpeed;songUpdating=false;SetBusy(false);if(songCancel is not null)songCancel.IsEnabled=false;DrawSongWaveform();}}
    }
    private async Task ApplySongLoopAsync()
    {
        if(song is null || audio is null || songA is null || songB is null)return;
        if(!double.IsFinite(songA.Value) || !double.IsFinite(songB.Value) || songA.Value<0 || songB.Value<=songA.Value || songB.Value>song.Duration)throw new InvalidOperationException("Konec B musí následovat po začátku A a ležet ve skladbě.");
        await audio.SetLoopAsync(songA.Value/currentSpeed,songB.Value/currentSpeed,true);DrawSongWaveform();
    }
    private async Task SaveSongSectionAsync()
    {
        if(song is null || songA is null || songB is null)return;await ApplySongLoopAsync();
        var name=new TextBox{Header="Název úseku",MaxLength=128};var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Uložit úsek",Content=name,PrimaryButtonText="Uložit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Primary};
        if(await dialog.ShowAsync()!=ContentDialogResult.Primary || string.IsNullOrWhiteSpace(name.Text))return;
        song=SongRules.Validate(song with{Sections=[..song.Sections??[],new SongSection(Guid.NewGuid().ToString("N"),name.Text.Trim(),songA.Value,songB.Value)]});SaveSong();if(songSections is not null)songSections.ItemsSource=song.Sections;
    }
    private void SaveSong(){if(song is null || !persistenceReady)return;song=SongRules.Validate(song);songs[song.Id]=song;autosave!.Queue(UserData.Document("song",song.Id,song));}
    private void RefreshSong(Rocker.Interop.TransportSnapshot transport)
    {
        if(song is not null && mediaPath is not null && !busy && transport.Ready!=0 && Math.Abs(transport.Speed-currentSpeed)>1e-8){
            currentSpeed=transport.Speed;songRampQueuedSpeed=null;songUpdating=true;if(songSpeed is not null)songSpeed.Value=currentSpeed;SpeedBox.SelectedIndex=Array.IndexOf(new[]{.25,.5,.75,1.0,1.5,2.0},currentSpeed);songUpdating=false;song=song with{Speed=currentSpeed};SaveSong();
        }
        if(song?.Ramp==true && mediaPath is not null && audio is not null && !busy && (transport.Reserved&1)!=0 && (transport.Reserved&(16|32))==0 && transport.Playing!=0 && currentSpeed<song.RampTarget-1e-8 && songRampQueuedSpeed is null && (songRampTask is null || songRampTask.IsCompleted))songRampTask=PrepareSongRampAsync();
        if(transport.Playing==0 && songRampQueuedSpeed is not null && (transport.Reserved&32)==0)songRampQueuedSpeed=null;
        if(songReading is not null && song is not null)songReading.Text=$"{song.Name} · {FormatTime(transport.PositionSeconds*currentSpeed)} / {FormatTime(song.Duration)} · {currentSpeed:0.##}×"+((transport.Reserved&8)!=0?" · předtakt":(transport.Reserved&4)!=0?" · chyba čtení cache":(transport.Reserved&2)!=0?" · čekám na zvuková data":"");
        if(song is not null && mediaPath is not null && !busy && DateTime.UtcNow-songLastSave>TimeSpan.FromSeconds(3)){songLastSave=DateTime.UtcNow;song=song with{Position=Math.Clamp(transport.PositionSeconds*currentSpeed,0,song.Duration)};SaveSong();}
    }
    private async Task CancelSongRampAsync()
    {
        if(audio is null)return;
        if(songRampTask is {IsCompleted:false}){audio.CancelMediaPreparation();await songRampTask;}
        await audio.CancelRampAsync();songRampQueuedSpeed=null;
    }
    private async Task PrepareSongRampAsync()
    {
        if(song is null || audio is null || mediaPath is null)return;
        var expectedSong=song.Id;var next=Math.Min(song.RampTarget,Math.Round(currentSpeed+song.RampStep,6));songRampQueuedSpeed=next;
        try{await audio.PrepareFileForLoopAsync(mediaPath,next,audio.GetClock().LoopCycles+song.RampEvery);}
        catch(Exception error){songRampQueuedSpeed=null;if(!closed && audio.GetTransport().Playing!=0 && song?.Id==expectedSong){song=song with{Ramp=false};SaveSong();MediaStatus.Text="Postupné zrychlování se zastavilo. "+error.Message;}}
    }
    private void DrawSongWaveform()
    {
        if(songWaveform is null)return;songWaveform.Children.Clear();if(song is null || songPeaks.Length==0)return;var width=Math.Max(1,songWaveform.ActualWidth);var brush=songReading?.Foreground;
        var line=new Polyline{Stroke=brush,StrokeThickness=1,IsHitTestVisible=false};for(int i=0;i<songPeaks.Length;++i){var x=i*width/(songPeaks.Length-1);var value=Math.Clamp(songPeaks[i],0,1)*60;line.Points.Add(new(x,70-value));line.Points.Add(new(x,70+value));}songWaveform.Children.Add(line);
        foreach(var (seconds,label) in new[]{(songA?.Value??0,"A"),(songB?.Value??song.Duration,"B")}){var x=Math.Clamp(seconds/song.Duration,0,1)*width;var mark=new Line{X1=x,X2=x,Y1=0,Y2=140,Stroke=brush,StrokeThickness=2,IsHitTestVisible=false};songWaveform.Children.Add(mark);var text=Text(label);Canvas.SetLeft(text,Math.Clamp(x+3,0,Math.Max(0,width-20)));songWaveform.Children.Add(text);}
    }
    private async Task VerifySongsAsync()
    {
        var entry=new SongSession(Guid.NewGuid().ToString("D"),"Synthetic source",Path.Combine(userFiles.Root,"missing-source.wav"),60,12,.75,[new("intro","Úvod",5,15)],1);
        song=SongRules.Validate(entry);SaveSong();await autosave!.FlushAsync();var reopened=await DataRepository.OpenAsync(userFiles);var snapshot=await reopened.SnapshotAsync();var restored=UserData.Read<SongSession>(snapshot.Documents.Single(d=>d.Kind=="song" && d.Id==entry.Id).Value);
        _=BuildSong();var pass=restored.Position==12 && restored.Sections![0].End==15 && songWaveform is not null && songA is not null && songB is not null;
        WriteShellDiagnostic("songs-view.json",new{result=pass?"passed":"failed",nativeWaveformCanvas=songWaveform is not null,persistenceReopened=pass,savedSectionCount=restored.Sections!.Count,missingSourcePreserved=!File.Exists(restored.SourcePath),physicalPlaybackVerified=false});song=null;workspaceCache.Remove(new("07"));if(!pass)throw new InvalidOperationException("Song workspace failed.");
    }
}
