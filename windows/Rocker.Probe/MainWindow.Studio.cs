// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
using Rocker.Interop;
using Rocker.Probe.Controls;
using Windows.Storage.Pickers;
using Path=System.IO.Path;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private ProjectManifest? studioProject;
    private ComboBox? studioProjects,studioRecordings;
    private ListView? studioTrackList;
    private StackPanel? studioTrackEditor;
    private TextBlock? studioReading;
    private Canvas? studioTimeline;
    private bool studioOperation,studioUpdating,studioOwnsPlayback,studioLoadCancelled;
    private string? studioCaptureProject;private bool studioCaptureBacking;private int studioInputDelay,studioOutputDelay;private double studioManualDelayMs;
    private ulong studioTicket;private double studioZoom=10,studioExportStart,studioExportEnd;
    private readonly Stack<ProjectManifest> studioUndo=[];
    private readonly Dictionary<string,ProjectTrack> studioPendingEdits=[];private Task? studioEditTask;private bool studioPendingTimeline;
    private void ResetStudioWorkspace(){studioProject=null;studioProjects=null;studioRecordings=null;studioTrackList=null;studioTrackEditor=null;studioReading=null;studioTimeline=null;studioOwnsPlayback=false;studioTicket=0;studioCaptureProject=null;studioCaptureBacking=false;studioUndo.Clear();studioPendingEdits.Clear();studioPendingTimeline=false;studioEditTask=null;}
    private FrameworkElement BuildStudio()
    {
        var panel=Stack();panel.Children.Add(Text("Studio · projekt na disku"));
        void Action(string label,Func<Task> work){var button=new Button{Content=label};button.Click+=async(_,_)=>{if(powerTransition || fileOperation || busy || backupOperationRunning || deviceOperationRunning || captureDraft is not null || studioOperation || studioEditTask is{IsCompleted:false} || database is null || audio is null)return;studioOperation=true;SetStudioEditorEnabled(false);try{await work();}catch(OperationCanceledException){ShowPersistenceError("Operace Studia byla zrušena.");}catch(Exception error){ShowPersistenceError("Studio: "+error.Message);}finally{studioOperation=false;SetStudioEditorEnabled(true);RefreshStudio();}};panel.Children.Add(button);}
        Action("Nový projekt…",async()=>{var name=await AskRecordingNameAsync("Nový projekt","Moje skladba");if(name is null)return;await InvalidateStudioPlaybackAsync();var now=DateTimeOffset.UtcNow;studioProject=new(1,Guid.NewGuid().ToString(),name,now,now,48000,[]);studioUndo.Clear();await SaveStudioAsync();await ReloadStudioListsAsync();});
        studioProjects=new ComboBox{Header="Projekty",DisplayMemberPath="Name",HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(studioProjects);
        Action("Otevřít vybraný projekt",async()=>{if(studioProjects.SelectedItem is not ProjectIndex selected)return;await InvalidateStudioPlaybackAsync();var loaded=await new ProjectRepository(userFiles,database!).LoadAsync(selected.Id);if(loaded.MissingMedia.Count>0)throw new InvalidDataException("Projekt má chybějící nebo změněné stopy. Původní manifest je zachovaný.");studioProject=loaded.Project;studioUndo.Clear();studioOwnsPlayback=false;studioTicket=0;RefreshStudio();});
        Action("Importovat přenesený projekt…",async()=>{var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".rockerproj");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var file=await picker.PickSingleFileAsync();if(file is null)return;var imported=await new ProjectRepository(userFiles,database!).ImportAsync(file.Path);await InvalidateStudioPlaybackAsync();studioProject=imported;studioUndo.Clear();await ReloadStudioListsAsync();RefreshStudio();});
        Action("Uložit kopii projektu pro přenos…",async()=>{if(studioProject is null)return;await SaveStudioAsync();var picker=new FolderPicker();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var folder=await picker.PickSingleFolderAsync();if(folder is null)return;var exported=await new ProjectRepository(userFiles,database!).ExportDirectoryAsync(studioProject.Id,folder.Path);ShellNotice.IsOpen=true;ShellNotice.Message="Přenášej celou složku včetně médií: "+exported;});
        Action("Přejmenovat projekt…",async()=>{if(studioProject is null)return;var name=await AskRecordingNameAsync("Název projektu",studioProject.Name);if(name is null)return;RememberStudio();studioProject=studioProject with{Name=name};await SaveStudioAsync();await ReloadStudioListsAsync();});
        studioRecordings=new ComboBox{Header="Nahrávka z knihovny",DisplayMemberPath="LibraryLabel",HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(studioRecordings);
        Action("Připojit vybranou nahrávku",async()=>{if(studioProject is null || studioRecordings.SelectedItem is not RecordingData recording)return;await AttachStudioRecordingAsync(recording);});
        Action("Importovat zvukovou stopu…",ImportStudioTrackAsync);
        studioTrackList=new ListView{Header="Stopy",DisplayMemberPath="Name",SelectionMode=ListViewSelectionMode.Single};studioTrackList.SelectionChanged+=(_,_)=>BuildStudioTrackEditor();panel.Children.Add(studioTrackList);studioTrackEditor=Stack();panel.Children.Add(Card(studioTrackEditor));
        var zoom=new Slider{Header="Časová osa · pixely za sekundu",Minimum=1,Maximum=200,Value=studioZoom};zoom.ValueChanged+=(_,e)=>{studioZoom=e.NewValue;DrawStudioTimeline();};panel.Children.Add(zoom);
        studioTimeline=new Canvas{Height=100,MinWidth=300};studioTimeline.PointerPressed+=async(_,e)=>{if(!studioOwnsPlayback || audio is null || studioOperation)return;try{await audio.SeekAsync(e.GetCurrentPoint(studioTimeline).Position.X/studioZoom);}catch(Exception error){ShowPersistenceError(error.Message);}};panel.Children.Add(new ScrollViewer{Content=studioTimeline,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollMode=ScrollMode.Enabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=400});
        Action("Připravit a přehrát projekt",async()=>{await PrepareStudioAsync();if(!studioLoadCancelled && audio is not null)await audio.PlayAsync();});
        Action("Pozastavit přehrávání projektu",()=>audio!.PauseAsync());
        var cancel=new Button{Content="Zrušit přípravu projektu"};cancel.Click+=(_,_)=>{studioLoadCancelled=true;audio?.CancelMediaPreparation();};panel.Children.Add(cancel);
        Action("Vrátit poslední editaci",async()=>{if(studioUndo.Count==0)return;await InvalidateStudioPlaybackAsync();studioProject=studioUndo.Pop();await SaveStudioAsync();studioOwnsPlayback=false;studioTicket=0;RefreshStudio();});
        Action("Uložit projekt",SaveStudioAsync);
        var exportStart=new NumberBox{Header="Export od (sekundy)",Minimum=0,Value=studioExportStart};exportStart.ValueChanged+=(_,e)=>{if(double.IsFinite(e.NewValue))studioExportStart=e.NewValue;};panel.Children.Add(exportStart);
        var exportEnd=new NumberBox{Header="Export do (sekundy; 0 = celý zbytek)",Minimum=0,Value=studioExportEnd};exportEnd.ValueChanged+=(_,e)=>{if(double.IsFinite(e.NewValue))studioExportEnd=e.NewValue;};panel.Children.Add(exportEnd);
        Action("Exportovat stereo WAV…",ExportStudioAsync);var cancelExport=new Button{Content="Zrušit export"};cancelExport.Click+=(_,_)=>exportCancellation?.Cancel();panel.Children.Add(cancelExport);
        panel.Children.Add(BuildRecordingControls());
        var compensation=new NumberBox{Header="Ruční korekce nahrané stopy (ms; kladná ji posune dříve)",Minimum=-2000,Maximum=2000,Value=studioManualDelayMs,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};compensation.ValueChanged+=(_,e)=>{if(double.IsFinite(e.NewValue))studioManualDelayMs=e.NewValue;};panel.Children.Add(compensation);
        Action("Nahrát novou stopu do projektu",StartStudioCaptureAsync);
        panel.Children.Add(Text("Nová stopa se po zastavení připojí do projektu. Korekce používá hlášené zpoždění ovladače a aparátu; skutečnou odezvu lze doladit ručně. Během nahrávání neměň pozici, zařízení ani zdroj.",true));
        studioReading=Text("Vytvoř nebo otevři projekt.",true);panel.Children.Add(studioReading);_ = ReloadStudioListsAsync();RefreshStudio();return panel;
    }
    private async Task InvalidateStudioPlaybackAsync(){if(audio is not null){await audio.StopAsync();await audio.ClearMediaAsync();}studioOwnsPlayback=false;studioTicket=0;mediaPath=null;mediaMidi=null;song=null;}
    private void SetStudioEditorEnabled(bool enabled){if(studioTrackEditor is not null)foreach(var control in studioTrackEditor.Children.OfType<Control>())control.IsEnabled=enabled;}
    private void RememberStudio(){if(studioProject is null)return;if(studioUndo.Count>=20){var keep=studioUndo.Take(19).Reverse().ToArray();studioUndo.Clear();foreach(var value in keep)studioUndo.Push(value);}studioUndo.Push(studioProject);}
    private async Task SaveStudioAsync()
    {
        if(studioProject is null || database is null)return;var original=studioProject;var owned=original with{UpdatedAt=DateTimeOffset.UtcNow};await new ProjectRepository(userFiles,database).SaveAsync(owned);if(ReferenceEquals(studioProject,original)){studioProject=owned;RemoveCloseGuard("studio");}
    }
    private async Task ReloadStudioListsAsync()
    {
        if(database is null)return;try{var snapshot=await database.SnapshotAsync();if(closed)return;if(studioProjects is not null)studioProjects.ItemsSource=snapshot.Documents.Where(d=>d.Kind=="project").Select(d=>UserData.Read<ProjectIndex>(d.Value)).OrderBy(p=>p.Name).ToArray();if(studioRecordings is not null && recordingLibrary is not null)studioRecordings.ItemsSource=await recordingLibrary.ListAsync();}catch(Exception error){if(!closed)ShowPersistenceError("Seznam Studia nelze načíst. "+error.Message);}
    }
    private async Task AttachStudioRecordingAsync(RecordingData recording)
    {
        if(studioProject is null || database is null)return;var repository=new ProjectRepository(userFiles,database);var attached=await repository.AttachMediaAsync(studioProject.Id,new(recording.RelativePath,recording.Sha256,recording.Bytes));if(studioOwnsPlayback)await InvalidateStudioPlaybackAsync();RememberStudio();studioProject=studioProject with{Tracks=studioProject.Tracks.Append(new ProjectTrack(Guid.NewGuid().ToString("N"),recording.Name,attached,recording.Sha256,recording.DurationSec,SourceSampleRate:recording.Capture?.SampleRate,SourceFrames:recording.Capture?.Frames)).ToArray(),Looper=null};await SaveStudioAsync();studioOwnsPlayback=false;studioTicket=0;await ReloadStudioListsAsync();RefreshStudio();
    }
    private async Task ImportStudioTrackAsync()
    {
        if(studioProject is null || recordingLibrary is null || audio is null)return;var picker=new FileOpenPicker();foreach(var extension in new[]{".wav",".mp3",".ogg",".webm",".m4a"})picker.FileTypeFilter.Add(extension);WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var file=await picker.PickSingleFileAsync();if(file is null)return;
        await using var input=File.OpenRead(file.Path);var media=await new OwnedMedia(userFiles).ImportAsync(input,Path.GetExtension(file.Path).ToLowerInvariant());await audio.PrepareFileAsync(OwnedPaths.Resolve(userFiles.Root,media.RelativePath));await WaitForSongAsync(1);studioOwnsPlayback=false;studioTicket=0;var duration=audio.GetTransport().DurationSeconds;var mime=Path.GetExtension(file.Path).ToLowerInvariant() switch{".wav"=>"audio/wav",".mp3"=>"audio/mpeg",".ogg"=>"audio/ogg",".webm"=>"audio/webm",_=>"audio/mp4"};var recording=new RecordingData(Guid.NewGuid().ToString(),file.DisplayName,mime,duration,DateTimeOffset.UtcNow,media.RelativePath,media.Bytes,media.Sha256,[]);await recordingLibrary.SaveAsync(recording);await AttachStudioRecordingAsync(recording);
    }
    private void RefreshStudio()
    {
        if(studioReading is not null)studioReading.Text=studioProject is null?"Vytvoř nebo otevři projekt.":$"{studioProject.Name} · {studioProject.Tracks.Count} stop · {studioProject.SampleRate} Hz · průběžné ukládání projektu";
        if(studioTrackList is not null && studioProject is not null){var selected=(studioTrackList.SelectedItem as ProjectTrack)?.Id;studioUpdating=true;studioTrackList.ItemsSource=studioProject.Tracks;studioTrackList.SelectedItem=studioProject.Tracks.FirstOrDefault(t=>t.Id==selected);studioUpdating=false;}DrawStudioTimeline();
    }
    private Task EditStudioTrackAsync(ProjectTrack value,bool timeline=false)
    {
        if(studioProject is null || studioOperation)return Task.CompletedTask;RememberStudio();studioProject=studioProject with{Tracks=studioProject.Tracks.Select(t=>t.Id==value.Id?value:t).ToArray(),Looper=timeline||value.Pan!=0||value.Solo?null:studioProject.Looper};studioPendingEdits[value.Id]=value;studioPendingTimeline|=timeline;RegisterCloseGuard("studio",new(false,true),async()=>{if(studioEditTask is not null)await studioEditTask;await SaveStudioAsync();return true;},()=>{});studioEditTask??=DrainStudioEditsAsync();DrawStudioTimeline();return studioEditTask;
    }
    private async Task DrainStudioEditsAsync()
    {
        try{await Task.Delay(120);while(studioPendingEdits.Count>0){var batch=studioPendingEdits.Values.ToArray();studioPendingEdits.Clear();bool timeline=studioPendingTimeline;studioPendingTimeline=false;await SaveStudioAsync();if(studioOwnsPlayback && studioTicket!=0 && audio is not null && studioProject is not null){if(timeline){await InvalidateStudioPlaybackAsync();MediaStatus.Text="Časová osa se změnila. Použij Připravit a přehrát projekt.";}else foreach(var track in batch){int index=studioProject.Tracks.ToList().FindIndex(t=>t.Id==track.Id);if(index>=0)await audio.SetProjectTrackAsync(studioTicket,(uint)index,track.Gain,track.Pan,track.Muted,track.Solo);}}}}
        catch(Exception error){RegisterCloseGuard("studio",new(false,true),async()=>{await SaveStudioAsync();return true;},()=>{});ShowPersistenceError("Editace zůstávají v paměti; ulož projekt znovu. "+error.Message);}finally{studioEditTask=null;}
    }
    private void BuildStudioTrackEditor()
    {
        if(studioUpdating || studioTrackEditor is null)return;studioTrackEditor.Children.Clear();if(studioTrackList?.SelectedItem is not ProjectTrack selected)return;
        ProjectTrack Current()=>studioProject?.Tracks.FirstOrDefault(t=>t.Id==selected.Id)??selected;
        var name=new Button{Content="Přejmenovat stopu…"};name.Click+=async(_,_)=>{var label=await AskRecordingNameAsync("Název stopy",Current().Name);if(label is not null){await EditStudioTrackAsync(Current() with{Name=label});RefreshStudio();}};studioTrackEditor.Children.Add(name);
        var gain=new ValueDial{Label="Hlasitost stopy",Minimum=0,Maximum=100,Value=selected.Gain*100};gain.ValueChanged+=async(_,_)=>await EditStudioTrackAsync(Current() with{Gain=gain.Value/100});studioTrackEditor.Children.Add(gain);
        var pan=new ValueDial{Label="Panorama · vlevo / vpravo",Minimum=-100,Maximum=100,Value=selected.Pan*100};pan.ValueChanged+=async(_,_)=>await EditStudioTrackAsync(Current() with{Pan=pan.Value/100});studioTrackEditor.Children.Add(pan);
        var mute=new CheckBox{Content="Mute · ztlumit stopu",IsChecked=selected.Muted};mute.Checked+=async(_,_)=>await EditStudioTrackAsync(Current() with{Muted=true});mute.Unchecked+=async(_,_)=>await EditStudioTrackAsync(Current() with{Muted=false});studioTrackEditor.Children.Add(mute);
        var solo=new CheckBox{Content="Solo · přehrávat jen zvolené stopy",IsChecked=selected.Solo};solo.Checked+=async(_,_)=>await EditStudioTrackAsync(Current() with{Solo=true});solo.Unchecked+=async(_,_)=>await EditStudioTrackAsync(Current() with{Solo=false});studioTrackEditor.Children.Add(solo);
        var offset=new NumberBox{Header="Začátek na časové ose (vzorky projektu)",Minimum=0,Maximum=uint.MaxValue,Value=selected.OffsetFrames,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};offset.ValueChanged+=async(_,e)=>{if(double.IsFinite(e.NewValue))await EditStudioTrackAsync(Current() with{OffsetFrames=(long)Math.Round(e.NewValue)},true);};studioTrackEditor.Children.Add(offset);
        var trim=new NumberBox{Header="Oříznout začátek zdroje (vzorky souboru)",Minimum=0,Maximum=uint.MaxValue,Value=selected.TrimStartFrames,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};trim.ValueChanged+=async(_,e)=>{if(double.IsFinite(e.NewValue))await EditStudioTrackAsync(Current() with{TrimStartFrames=(long)Math.Round(e.NewValue)},true);};studioTrackEditor.Children.Add(trim);
        var remove=new Button{Content="Odstranit stopu…"};remove.Click+=async(_,_)=>{if(studioProject is null || studioOperation)return;var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Odstranit stopu?",Content=Current().Name,PrimaryButtonText="Odstranit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;await InvalidateStudioPlaybackAsync();RememberStudio();studioProject=studioProject with{Tracks=studioProject.Tracks.Where(t=>t.Id!=selected.Id).ToArray(),Looper=null};await SaveStudioAsync();studioOwnsPlayback=false;studioTicket=0;RefreshStudio();};studioTrackEditor.Children.Add(remove);
    }
    private void DrawStudioTimeline()
    {
        if(studioTimeline is null)return;studioTimeline.Children.Clear();if(studioProject is null)return;double end=1;for(int i=0;i<studioProject.Tracks.Count;++i){var track=studioProject.Tracks[i];double start=track.OffsetFrames/(double)studioProject.SampleRate;double duration=track.SourceFrames is long sourceFrames && track.SourceSampleRate is int sourceRate?(sourceFrames-track.TrimStartFrames)/(double)sourceRate:track.DurationSec;end=Math.Max(end,start+duration);var block=new Border{Width=Math.Max(2,duration*studioZoom),Height=36,Child=Text(track.Name),Style=(Style)Application.Current.Resources["VintageCardStyle"]};Canvas.SetLeft(block,start*studioZoom);Canvas.SetTop(block,i*44);studioTimeline.Children.Add(block);}studioTimeline.Width=Math.Min(200000,end*studioZoom);studioTimeline.Height=Math.Max(100,studioProject.Tracks.Count*44);
    }
    private IReadOnlyList<ProjectAudioTrack> StudioAudioTracks(ProjectManifest project)
    {
        var repository=new ProjectRepository(userFiles,database!);return project.Tracks.Select(t=>new ProjectAudioTrack(OwnedPaths.Resolve(repository.DirectoryFor(project.Id),t.MediaPath),t.Gain,t.Pan,t.Muted,t.Solo,t.OffsetFrames,t.TrimStartFrames)).ToArray();
    }
    private async Task PrepareStudioAsync()
    {
        if(studioProject is null || audio is null || studioProject.Tracks.Count==0)return;var repository=new ProjectRepository(userFiles,database!);var checkedProject=await repository.LoadAsync(studioProject.Id);if(checkedProject.MissingMedia.Count>0)throw new InvalidDataException("Projekt má změněné nebo chybějící médium.");studioLoadCancelled=false;var project=studioProject;var preparedTicket=await audio.PrepareProjectAsync(StudioAudioTracks(project),(uint)project.SampleRate);if(studioLoadCancelled)return;await WaitForSongAsync(1);var resolved=new List<ProjectTrack>();for(uint i=0;i<project.Tracks.Count;++i){var info=await audio.GetProjectTrackInfoAsync(preparedTicket,i);resolved.Add(project.Tracks[(int)i] with{SourceSampleRate=checked((int)info.SampleRate),SourceFrames=checked((long)info.Frames)});}if(studioLoadCancelled || closed)return;studioTicket=preparedTicket;studioProject=project with{Tracks=resolved};await SaveStudioAsync();studioOwnsPlayback=true;currentSpeed=1;mediaPath=null;mediaMidi=null;song=null;songPeaks=[];await audio.SetLoopAsync(0,0,false);MediaStatus.Text="Studio · "+project.Name;
    }
    private async Task StartStudioCaptureAsync()
    {
        if(studioProject is null || captureDraft is not null || recordingOperation || audio is null)return;
        if(recordingBus==2)throw new InvalidOperationException("Pro novou stopu zvol suchou kytaru nebo kytaru za aparátem; výsledný mix by zdvojil podklad.");
        if(deviceSelectionUnavailable || appSettings.AudioDevice is not{InputChannel:>=0})throw new InvalidOperationException("Vyber vstup kytary v nastavení zvuku.");
        if(studioProject.Tracks.Count>0){await PrepareStudioAsync();if(studioLoadCancelled)return;await audio.SeekAsync(0);}
        else{await audio.StopAsync();await audio.StartLiveAsync();}
        var details=await audio.GetDeviceDetailsAsync();studioCaptureProject=studioProject.Id;studioCaptureBacking=studioProject.Tracks.Count>0;studioInputDelay=details.InputLatencyFrames;studioOutputDelay=details.OutputLatencyFrames;
        await StartCaptureAsync(studioCaptureBacking?studioTicket:0);if(captureDraft is null)studioCaptureProject=null;
    }
    private async Task AttachStudioCaptureAsync(RecordingData recording,RecordingAlignment alignment)
    {
        var projectId=studioCaptureProject;if(projectId is null || database is null)return;
        var repository=new ProjectRepository(userFiles,database);var loaded=await repository.LoadAsync(projectId);var project=loaded.Project;if(project.Tracks.Any(t=>t.Id==recording.Id)){studioCaptureProject=null;return;}
        TrackAlignment? provenance=null;long offset=0,trim=0;bool aligned=!studioCaptureBacking || alignment.Started && alignment.Continuous;
        if(studioCaptureBacking && alignment.Started){provenance=new(alignment.FirstProjectFrame,studioInputDelay>=0?studioInputDelay:null,studioOutputDelay>=0?studioOutputDelay:null,alignment.DspFrames,studioManualDelayMs,alignment.Continuous);var placement=StudioRules.Place(provenance,project.SampleRate,recording.Capture!.SampleRate,recording.Capture.Frames);offset=placement.OffsetFrames;trim=placement.TrimStartFrames;}

        if(recording.Capture is { } capture)trim=Math.Min(trim,Math.Max(0,capture.Frames-1));
        var media=await repository.AttachMediaAsync(project.Id,new(recording.RelativePath,recording.Sha256,recording.Bytes));
        var track=new ProjectTrack(recording.Id,recording.Name+(aligned?"":" · zarovnání ověř ručně"),media,recording.Sha256,recording.DurationSec,OffsetFrames:offset,TrimStartFrames:trim,SourceSampleRate:recording.Capture?.SampleRate,SourceFrames:recording.Capture?.Frames,Alignment:provenance);
        project=project with{Tracks=project.Tracks.Append(track).ToArray(),UpdatedAt=DateTimeOffset.UtcNow,Looper=null};await repository.SaveAsync(project);studioCaptureProject=null;if(studioProject?.Id==project.Id){RememberStudio();studioProject=project;studioOwnsPlayback=false;studioTicket=0;RefreshStudio();}await ReloadStudioListsAsync();
        if(!aligned || studioInputDelay<0 || studioOutputDelay<0){ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Warning;ShellNotice.Message="Stopa je zachovaná. Zarovnání a případnou ruční korekci zkontroluj při poslechu; zpoždění ovladače nebylo změřené.";}
    }
    private async Task ExportStudioAsync()
    {
        if(studioProject is null || studioProject.Tracks.Count==0)return;var check=await new ProjectRepository(userFiles,database!).LoadAsync(studioProject.Id);if(check.MissingMedia.Count>0)throw new InvalidDataException("Projekt má změněné nebo chybějící médium.");var picker=new FileSavePicker{SuggestedFileName=studioProject.Name};picker.FileTypeChoices.Add("Stereo WAV PCM24",new[]{".wav"});WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var file=await picker.PickSaveFileAsync();if(file is null)return;if(Path.GetFullPath(file.Path).StartsWith(userFiles.Root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Export zvol mimo datovou složku Rockeru.");exportOperation=true;exportCancellation=new();try{var progress=new Progress<double>(value=>{ShellNotice.IsOpen=true;ShellNotice.Message=$"Export projektu · {value:P0}.";});await ProjectExport.WavAsync(StudioAudioTracks(studioProject),(uint)studioProject.SampleRate,file.Path,studioExportStart,studioExportEnd,progress:progress,cancellation:exportCancellation.Token);ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Success;ShellNotice.Message="Projekt byl exportován: "+file.Name;}finally{exportOperation=false;exportCancellation.Dispose();exportCancellation=null;}
    }    private async Task VerifyStudioAsync()
    {
        if(database is null || recordingLibrary is null || audio is null)throw new InvalidOperationException("Studio dependencies did not initialize.");
        await audio.StopAsync();var recording=(await recordingLibrary.ListAsync()).First(r=>r.Capture?.Frames==480);var repository=new ProjectRepository(userFiles,database);var id=Guid.NewGuid().ToString();var media=await repository.AttachMediaAsync(id,new(recording.RelativePath,recording.Sha256,recording.Bytes));var now=DateTimeOffset.UtcNow;
        studioProject=new(1,id,"Kontrolní Studio",now,now,48000,[new("left","Kytara 1",media,recording.Sha256,.01,.5,false,-1,SourceSampleRate:48000,SourceFrames:480),new("right","Kytara 2",media,recording.Sha256,.01,.25,false,1,true,100,20,48000,480)]);await SaveStudioAsync();Navigate(new("15"));RefreshStudio();await PrepareStudioAsync();var transport=audio.GetTransport();var restored=await new ProjectRepository(userFiles,await DataRepository.OpenAsync(userFiles)).LoadAsync(id);
        bool passed=restored.Project.Tracks.Count==2 && restored.Project.Tracks[1].OffsetFrames==100 && restored.Project.Tracks[1].TrimStartFrames==20 && restored.Project.Tracks[1].Solo && restored.MissingMedia.Count==0 && studioTrackList?.Items.Count==2 && studioTimeline?.Children.Count==2 && audio.GetClipInfo().Frames==560 && transport.Playing==0;
        WriteShellDiagnostic("studio-view.json",new{result=passed?"passed":"failed",nativeTrackControls=studioTrackList?.Items.Count,timelineRegions=studioTimeline?.Children.Count,exactFrames=audio.GetClipInfo().Frames,persistenceReopened=true,physicalOverdubVerified=false,manualDialogsVerified=false});await InvalidateStudioPlaybackAsync();studioProject=null;RemoveCloseGuard("studio");if(!passed)throw new InvalidOperationException("Studio native form, source and project round-trip failed.");
    }

}
