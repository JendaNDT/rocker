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
    private SongSession? song { get => documentContext.Current.Get<SongSession?>("song", () => default); set => documentContext.Current.Set("song",value); }
    private string? appliedSongLoopId { get => documentContext.Current.Get<string?>("appliedSongLoopId", () => default); set => documentContext.Current.Set("appliedSongLoopId",value); } private double appliedSongLoopStart { get => documentContext.Current.Get<double>("appliedSongLoopStart", () => default); set => documentContext.Current.Set("appliedSongLoopStart",value); }
    private double appliedSongLoopEnd { get => documentContext.Current.Get<double>("appliedSongLoopEnd", () => default); set => documentContext.Current.Set("appliedSongLoopEnd",value); }
    private ComboBox? songSelector { get => documentContext.Current.Get<ComboBox?>("songSelector", () => default); set => documentContext.Current.Set("songSelector",value); }
    private ListView? songSections { get => documentContext.Current.Get<ListView?>("songSections", () => default); set => documentContext.Current.Set("songSections",value); }
    private NumberBox? songA { get => documentContext.Current.Get<NumberBox?>("songA", () => default); set => documentContext.Current.Set("songA",value); }
    private NumberBox? songB { get => documentContext.Current.Get<NumberBox?>("songB", () => default); set => documentContext.Current.Set("songB",value); }
    private NumberBox? songSpeed { get => documentContext.Current.Get<NumberBox?>("songSpeed", () => default); set => documentContext.Current.Set("songSpeed",value); }
    private Canvas? songWaveform { get => documentContext.Current.Get<Canvas?>("songWaveform", () => default); set => documentContext.Current.Set("songWaveform",value); }
    private TextBlock? songReading { get => documentContext.Current.Get<TextBlock?>("songReading", () => default); set => documentContext.Current.Set("songReading",value); }
    private Button? songCancel { get => documentContext.Current.Get<Button?>("songCancel", () => default); set => documentContext.Current.Set("songCancel",value); }
    private float[] songPeaks { get => documentContext.Current.Get<float[]>("songPeaks", () => []); set => documentContext.Current.Set("songPeaks",value); }
    private bool songUpdating { get => documentContext.Current.Get<bool>("songUpdating", () => default); set => documentContext.Current.Set("songUpdating",value); }
    private bool songLoadCancelled { get => documentContext.Current.Get<bool>("songLoadCancelled", () => default); set => documentContext.Current.Set("songLoadCancelled",value); }
    private Task? songRampTask { get => documentContext.Current.Get<Task?>("songRampTask", () => default); set => documentContext.Current.Set("songRampTask",value); }private double? songRampQueuedSpeed { get => documentContext.Current.Get<double?>("songRampQueuedSpeed", () => default); set => documentContext.Current.Set("songRampQueuedSpeed",value); }
    private DateTime songLastSave { get => documentContext.Current.Get<DateTime>("songLastSave", () => DateTime.MinValue); set => documentContext.Current.Set("songLastSave",value); }
    private void InitializeSongs(IReadOnlyList<DataDocument> documents)
    {
        songs.Clear();foreach(var item in documents.Where(d=>d.Kind=="song")){var entry=UserData.Read<SongSession>(item.Value);songs[entry.Id]=entry;}
        song=null;songPeaks=[];songSelector=null;songSections=null;songReading=null;songWaveform=null;
    }
    private FrameworkElement BuildSong()
    {var capturedDocument = documentContext.Current;
        songUpdating=true;
        var panel=Stack();panel.AllowDrop=true;
        panel.DragOver+=(_,e)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(e.DataView.Contains(StandardDataFormats.StorageItems))e.AcceptedOperation=DataPackageOperation.Copy;};
        panel.Drop+=async(_,e)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;e.Handled=true;try{if(e.DataView.Contains(StandardDataFormats.StorageItems) && (await e.DataView.GetStorageItemsAsync()).OfType<StorageFile>().FirstOrDefault() is { } file)await OpenSongFileAsync(file.Path,file.Name);}catch(Exception error){ShowError(error);}};
        var open=new Button{Content="Otevřít skladbu…"};open.Click+=(__sender,__args)=>{using var documentScope=documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;OpenMedia_Click(__sender,__args);};panel.Children.Add(open);
        songCancel=new Button{Content="Zrušit načítání",IsEnabled=busy};songCancel.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;songLoadCancelled=true;audio?.CancelMediaPreparation();MediaStatus.Text="Ruším načítání a obnovuji předchozí zdroj…";};panel.Children.Add(songCancel);
        songSelector=new ComboBox{Header="Naposledy otevřené skladby",DisplayMemberPath="Name",ItemsSource=songs.Values.OrderBy(s=>s.Name).ToArray(),HorizontalAlignment=HorizontalAlignment.Stretch};
        songSelector.SelectionChanged+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(!songUpdating && !busy && songSelector.SelectedItem is SongSession selected){try{if(File.Exists(selected.SourcePath))await OpenSongFileAsync(selected.SourcePath,selected.Name);else await RelocateSongAsync(selected);}catch(Exception error){ShowError(error);}}};panel.Children.Add(songSelector);
        songReading=Text(song is null?"Vyber skladbu nebo sem přetáhni zvukový soubor.":song.Name,true);panel.Children.Add(songReading);
        songWaveform=new Canvas{Height=140,HorizontalAlignment=HorizontalAlignment.Stretch};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(songWaveform,"Zvuková časová osa; pozici lze měnit také posuvníkem přehrávače.");
        songWaveform.SizeChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;DrawSongWaveform();};songWaveform.PointerPressed+=async(_,e)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(song is null || busy || audio is null || songWaveform.ActualWidth<=0)return;try{var seconds=Math.Clamp(e.GetCurrentPoint(songWaveform).Position.X/songWaveform.ActualWidth,0,1)*song.Duration;await audio.SeekAsync(seconds/currentSpeed);}catch(Exception error){ShowError(error);}};panel.Children.Add(Card(songWaveform));
        var range=Stack();songA=SongNumber("Začátek A (sekundy ve skladbě)",0,0,song?.Duration??86400,.1);songB=SongNumber("Konec B (sekundy ve skladbě)",song?.Duration??1,0,song?.Duration??86400,.1);range.Children.Add(songA);range.Children.Add(songB);
        songA.ValueChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;DrawSongWaveform();};songB.ValueChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;DrawSongWaveform();};
        var markA=new Button{Content="Nastavit A na aktuální pozici"};markA.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(audio is not null)songA.Value=audio.GetTransport().PositionSeconds*currentSpeed;};range.Children.Add(markA);
        var markB=new Button{Content="Nastavit B na aktuální pozici"};markB.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(audio is not null)songB.Value=audio.GetTransport().PositionSeconds*currentSpeed;};range.Children.Add(markB);
        var loop=new Button{Content="Opakovat úsek A–B"};loop.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;try{await ApplySongLoopAsync();}catch(Exception error){ShowError(error);}};range.Children.Add(loop);
        var whole=new Button{Content="Vypnout opakování"};whole.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;try{if(audio is not null){if(desktopEnabled)await ClaimDesktopSourceAsync(capturedDocument);await audio.SetLoopAsync(0,0,false);}}catch(Exception error){ShowError(error);}};range.Children.Add(whole);
        var save=new Button{Content="Uložit pojmenovaný úsek…"};save.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;try{await SaveSongSectionAsync();}catch(Exception error){ShowError(error);}};range.Children.Add(save);panel.Children.Add(Card(range));
        songSections=new ListView{Header="Uložené úseky",DisplayMemberPath="Label",IsItemClickEnabled=true,SelectionMode=ListViewSelectionMode.Single,ItemsSource=song?.Sections??[]};
        songSections.ItemClick+=async(_,e)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(e.ClickedItem is SongSection section){songA.Value=section.Start;songB.Value=section.End;try{await ApplySongLoopAsync();}catch(Exception error){ShowError(error);}}};panel.Children.Add(songSections);
        var remove=new Button{Content="Odstranit vybraný úsek"};remove.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(song is null || songSections.SelectedItem is not SongSection selected)return;var dialog=new ContentDialog{XamlRoot=DialogRoot,Title="Odstranit úsek?",Content=selected.Label,PrimaryButtonText="Odstranit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await ShowDialogAsync(dialog)==ContentDialogResult.Primary){song=song with{Sections=song.Sections!.Where(s=>s.Id!=selected.Id).ToArray()};SaveSong();songSections.ItemsSource=song.Sections;}};panel.Children.Add(remove);
        songSpeed=SongNumber("Rychlost bez změny výšky",currentSpeed,.25,2,.05);songSpeed.ValueChanged+=async(_,e)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(songUpdating || busy || !double.IsFinite(e.NewValue))return;try{await ChangeSongSpeedAsync(e.NewValue);}catch(Exception error){ShowError(error);}};panel.Children.Add(songSpeed);
        var countIn=SongNumber("Předtakt (takty)",song?.CountInBars??0,0,2,1);var bpm=SongNumber("Tempo předtaktu (BPM)",song?.Bpm??100,40,240,1);var beats=SongNumber("Doby v taktu",song?.Beats??4,1,16,1);
        void SavePractice(){if(song is null || songUpdating || !double.IsFinite(countIn.Value) || !double.IsFinite(bpm.Value) || !double.IsFinite(beats.Value))return;song=song with{CountInBars=(uint)countIn.Value,Bpm=Math.Round(bpm.Value),Beats=(uint)beats.Value};SaveSong();}
        countIn.ValueChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;SavePractice();};bpm.ValueChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;SavePractice();};beats.ValueChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;SavePractice();};panel.Children.Add(countIn);panel.Children.Add(bpm);panel.Children.Add(beats);
        var ramp=new ToggleSwitch{Header="Postupné zrychlování A–B",IsOn=song?.Ramp??false};var rampStep=SongNumber("Přírůstek rychlosti",song?.RampStep??.05,.01,.25,.01);var rampEvery=SongNumber("Zrychlit po opakováních",song?.RampEvery??4,1,32,1);var rampTarget=SongNumber("Cílová rychlost",song?.RampTarget??1,.25,2,.05);
        void SaveRamp(){if(song is null || songUpdating || !double.IsFinite(rampStep.Value) || !double.IsFinite(rampEvery.Value) || !double.IsFinite(rampTarget.Value))return;song=song with{Ramp=ramp.IsOn,RampStep=rampStep.Value,RampEvery=(uint)rampEvery.Value,RampTarget=rampTarget.Value};SaveSong();}
        ramp.Toggled+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;SaveRamp();if(!ramp.IsOn){try{await CancelSongRampAsync();}catch(Exception error){ShowError(error);}}};rampStep.ValueChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;SaveRamp();};rampEvery.ValueChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;SaveRamp();};rampTarget.ValueChanged+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;SaveRamp();};panel.Children.Add(ramp);panel.Children.Add(rampStep);panel.Children.Add(rampEvery);panel.Children.Add(rampTarget);
        var play=new Button{Content="Přehrát s předtaktem"};play.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(song is null || audio is null || busy)return;try{if(desktopEnabled)await ClaimDesktopSourceAsync(capturedDocument);SavePractice();var config=audio.GetMetronome() with{Enabled=false,Ramp=false};await audio.SetMetronomeAsync(config);await audio.SetMetronomeAsync(config with{Enabled=true,Bpm=(uint)song.Bpm,Beats=song.Beats,CountIn=song.CountInBars});await audio.PlayWithCountInAsync(song.CountInBars,song.Beats,song.Bpm);}catch(Exception error){ShowError(error);}};panel.Children.Add(play);
        var pause=new Button{Content="Pozastavit skladbu"};pause.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;try{if(audio is not null && (!desktopEnabled || services.Transport.Owner==capturedDocument))await audio.PauseAsync();}catch(Exception error){ShowError(error);}};panel.Children.Add(pause);
        panel.Children.Add(Text("Skladba a monitoring kytary mají samostatné ovládání. Tlačítko Zastavit ve spodním panelu zastaví všechny zdroje. Soubory zůstávají na původním místě; záloha uchová jejich pozice a úseky, původní zvuk připojené skladby zálohuj zvlášť.",true));
        songUpdating=false;DrawSongWaveform();return desktopEnabled?ComposeDesktopSong(panel):panel;
    }
    private static NumberBox SongNumber(string label,double value,double minimum,double maximum,double step)=>new(){Header=label,Minimum=minimum,Maximum=maximum,Value=value,SmallChange=step,LargeChange=step*10,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact};
    private async Task RelocateSongAsync(SongSession missing)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        var dialog=new ContentDialog{XamlRoot=DialogRoot,Title="Soubor skladby chybí",Content=missing.Name,PrimaryButtonText="Najít soubor…",CloseButtonText="Zrušit"};if(await ShowDialogAsync(dialog)!=ContentDialogResult.Primary)return;
        var picker=new FileOpenPicker();WinRT.Interop.InitializeWithWindow.Initialize(picker,DialogWindowHandle);foreach(var ext in new[]{".wav",".mp3",".ogg",".opus",".webm",".m4a",".mp4"})picker.FileTypeFilter.Add(ext);var file=await picker.PickSingleFileAsync();if(file is not null)await OpenSongFileAsync(file.Path,missing.Name,missing);
    }
    private async Task WaitForSongAsync(double speed)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        for(int i=0;i<200;++i){if(closed || songLoadCancelled)throw new OperationCanceledException();var transport=audio!.GetTransport();if(transport.Ready!=0 && (transport.Reserved&16)==0 && Math.Abs(transport.Speed-speed)<1e-8)return;await Task.Delay(10);}
        throw new InvalidOperationException("Přehrávač zatím nepřevzal novou skladbu.");
    }
    private async Task RestoreSongPlaybackAsync(string? path,byte[]? midi,double speed,Rocker.Interop.TransportSnapshot state,bool project)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(audio is null || closed || powerTransition)return;
        // Restore the source as well as the labels after a cancellation that
        // races publication. A restored source remains paused deliberately.
        await audio.PauseAsync();songLoadCancelled=false;
        if(project && studioProject is not null)await PrepareStudioAsync();
        else if(path is not null)await audio.PrepareFileAsync(path,speed);
        else if(midi is not null)await audio.PrepareMidiAsync(midi,speed);
        else{await audio.ClearMediaAsync();return;}
        await WaitForSongAsync(project?1:speed);
        songPeaks=await audio.GetWaveformAsync();
        await audio.SeekAsync(Math.Min(state.PositionSeconds,audio.GetTransport().DurationSeconds));
        if((state.Reserved&1)!=0){if(path is not null && song is not null)await ApplySongLoopAsync();else if(midi is not null && notationLoop is { } loop)await audio.SetLoopAsync(loop.Start/speed,loop.End/speed,true);}
    }
    private async Task OpenSongFileAsync(string path,string name,SongSession? relocated=null)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(desktopEnabled){await OpenDesktopSongAsync(path,name,relocated);return;}
        if(busy || powerTransition || audio is null)return;var previous=song;var oldTitle=MediaTitle.Text;var oldPath=mediaPath;var oldMidi=mediaMidi;var oldSpeed=currentSpeed;var oldState=audio.GetTransport();bool oldStudio=studioOwnsPlayback;
        try{
            SetBusy(true);await CancelSongRampAsync();songLoadCancelled=false;if(songCancel is not null)songCancel.IsEnabled=true;MediaStatus.Text="Připravuji skladbu a zvukovou časovou osu…";
            var existing=relocated??songs.Values.FirstOrDefault(s=>string.Equals(s.SourcePath,path,StringComparison.OrdinalIgnoreCase));var speed=existing?.Speed??currentSpeed;
            await audio.PrepareFileAsync(path,speed);if(closed)return;if(songLoadCancelled)throw new OperationCanceledException();await WaitForSongAsync(speed);if(songCancel is not null)songCancel.IsEnabled=false;
            studioOwnsPlayback=false;studioTicket=0;
            var transport=audio.GetTransport();songPeaks=await audio.GetWaveformAsync();currentSpeed=speed;mediaPath=path;mediaMidi=null;
            var duration=transport.DurationSeconds*speed;
            song=SongRules.Validate((existing??new SongSession(Guid.NewGuid().ToString("D"),name,path,duration)) with{SourcePath=path,Duration=duration,Speed=speed,Position=Math.Min(existing?.Position??0,duration),Sections=(existing?.Sections??[]).Where(s=>s.End<=duration).ToArray()});
            await audio.SeekAsync(song.Position/speed);RememberFile(path);MediaTitle.Text=song.Name;SaveSong();workspaceCache.Remove(new("07"));ShowWorkspace();MediaStatus.Text="Skladba je připravená; monitoring zůstal samostatný. Přehrávání spustíš tlačítkem.";
        }catch(OperationCanceledException){song=previous;MediaTitle.Text=oldTitle;mediaPath=oldPath;mediaMidi=oldMidi;currentSpeed=oldSpeed;await RestoreSongPlaybackAsync(oldPath,oldMidi,oldSpeed,oldState,oldStudio);MediaStatus.Text="Načítání bylo zrušeno. Předchozí zdroj je zachovaný a pozastavený.";}
        catch{song=previous;MediaTitle.Text=oldTitle;mediaPath=oldPath;mediaMidi=oldMidi;currentSpeed=oldSpeed;await RestoreSongPlaybackAsync(oldPath,oldMidi,oldSpeed,oldState,oldStudio);throw;}
        finally{if(!closed){SetBusy(false);if(songCancel is not null)songCancel.IsEnabled=false;}}
    }
    private async Task ChangeSongSpeedAsync(double speed)
    {
        using var scope=documentContext.Enter(documentContext.Current);
        if(desktopEnabled && services.Transport.Owner==documentContext.Current)
        {
            using var operation=services.Operations.Begin(documentContext.Current.Id,"source-update","Změna rychlosti skladby",cancellable:false);
            await services.Transport.UpdateOwnerAsync(documentContext.Current,()=>ChangeSongSpeedCoreAsync(speed));
        }
        else await ChangeSongSpeedCoreAsync(speed);
    }
    private async Task ChangeSongSpeedCoreAsync(double speed)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(desktopEnabled && services.Transport.Owner!=documentContext.Current){currentSpeed=speed;if(song is not null){song=song with{Speed=speed};SaveSong();}return;}
        if(audio is null || powerTransition || busy || (mediaPath is null && mediaMidi is null) || speed==currentSpeed)return;
        var previous=currentSpeed;var transport=audio.GetTransport();var sourcePosition=transport.PositionSeconds*previous;
        try{
            SetBusy(true);await CancelSongRampAsync();songLoadCancelled=false;if(songCancel is not null)songCancel.IsEnabled=true;MediaStatus.Text="Připravuji novou rychlost; živý vstup pokračuje…";
            if(mediaPath is not null)await audio.PrepareFileAsync(mediaPath,speed);else await audio.PrepareMidiAsync(mediaMidi!,speed);if(closed)return;if(songLoadCancelled)throw new OperationCanceledException();await WaitForSongAsync(speed);if(songCancel is not null)songCancel.IsEnabled=false;currentSpeed=speed;
            await audio.SeekAsync(Math.Min(sourcePosition/speed,audio.GetTransport().DurationSeconds));songPeaks=await audio.GetWaveformAsync();
            if(song is not null && mediaPath is not null){song=song with{Speed=speed,Position=Math.Min(sourcePosition,song.Duration)};SaveSong();if((transport.Reserved&1)!=0)await ApplySongLoopAsync();}
            if(notationLoop is { } loop && mediaMidi is not null && ReferenceEquals(mediaMidi,notationPlayingMidi))await audio.SetLoopAsync(loop.Start/speed,loop.End/speed,true);
            if(transport.Playing!=0)await audio.PlayAsync();MediaStatus.Text="Rychlost byla změněna bez změny výšky tónu.";
        }catch(OperationCanceledException){currentSpeed=previous;await RestoreSongPlaybackAsync(mediaPath,mediaMidi,previous,transport,false);MediaStatus.Text="Změna rychlosti byla zrušena. Zdroj je pozastavený.";}
        catch{currentSpeed=previous;await RestoreSongPlaybackAsync(mediaPath,mediaMidi,previous,transport,false);throw;}
        finally{if(!closed){songUpdating=true;SpeedBox.SelectedIndex=Array.IndexOf(new[]{.25,.5,.75,1.0,1.5,2.0},currentSpeed);if(songSpeed is not null)songSpeed.Value=currentSpeed;songUpdating=false;SetBusy(false);if(songCancel is not null)songCancel.IsEnabled=false;DrawSongWaveform();}}
    }
    private async Task ApplySongLoopAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(song is null || audio is null || songA is null || songB is null)return;
        if(desktopEnabled)await ClaimDesktopSourceAsync(documentContext.Current);
        if(!double.IsFinite(songA.Value) || !double.IsFinite(songB.Value) || songA.Value<0 || songB.Value<=songA.Value || songB.Value>song.Duration)throw new InvalidOperationException("Konec B musí následovat po začátku A a ležet ve skladbě.");
        await audio.SetLoopAsync(songA.Value/currentSpeed,songB.Value/currentSpeed,true);appliedSongLoopId=song.Id;appliedSongLoopStart=songA.Value;appliedSongLoopEnd=songB.Value;DrawSongWaveform();
    }
    private async Task SaveSongSectionAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(song is null || songA is null || songB is null)return;await ApplySongLoopAsync();
        var name=new TextBox{Header="Název úseku",MaxLength=128};var dialog=new ContentDialog{XamlRoot=DialogRoot,Title="Uložit úsek",Content=name,PrimaryButtonText="Uložit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Primary};
        if(await ShowDialogAsync(dialog)!=ContentDialogResult.Primary || string.IsNullOrWhiteSpace(name.Text))return;
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
        if(song is not null && mediaPath is not null && !busy && !backupOperationRunning && DateTime.UtcNow-songLastSave>TimeSpan.FromSeconds(3)){songLastSave=DateTime.UtcNow;song=song with{Position=Math.Clamp(transport.PositionSeconds*currentSpeed,0,song.Duration)};SaveSong();}
    }
    private async Task CancelSongRampAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(audio is null)return;
        if(songRampTask is {IsCompleted:false}){audio.CancelMediaPreparation();await songRampTask;}
        await audio.CancelRampAsync();songRampQueuedSpeed=null;
    }
    private async Task PrepareSongRampAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

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
        using var invocationScope=documentContext.Enter(documentContext.Current);

        var entry=new SongSession(Guid.NewGuid().ToString("D"),"Synthetic source",Path.Combine(userFiles.Root,"missing-source.wav"),60,12,.75,[new("intro","Úvod",5,15)],1);
        song=SongRules.Validate(entry);SaveSong();await autosave!.FlushAsync();var reopened=await DataRepository.OpenAsync(userFiles);var snapshot=await reopened.SnapshotAsync();var restored=UserData.Read<SongSession>(snapshot.Documents.Single(d=>d.Kind=="song" && d.Id==entry.Id).Value);
        _=BuildSong();var pass=restored.Position==12 && restored.Sections![0].End==15 && songWaveform is not null && songA is not null && songB is not null;
        WriteShellDiagnostic("songs-view.json",new{result=pass?"passed":"failed",nativeWaveformCanvas=songWaveform is not null,persistenceReopened=pass,savedSectionCount=restored.Sections!.Count,missingSourcePreserved=!File.Exists(restored.SourcePath),physicalPlaybackVerified=false});song=null;workspaceCache.Remove(new("07"));if(!pass)throw new InvalidOperationException("Song workspace failed.");
    }
}
