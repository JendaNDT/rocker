// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
using Rocker.Interop;
using Rocker.Probe.Controls;
using Path=System.IO.Path;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private bool looperOperation,looperAligned=true,looperUseDriverDelay=true;
    private uint looperBus,looperCountIn=1,looperManualDelay;
    private TextBlock? looperReading;private StackPanel? looperLayers;
    private uint looperShownLayers=uint.MaxValue;
    private string? looperSavedFingerprint,looperProjectId;private DateTimeOffset looperCreated;
    private ComboBox? looperSessions;
    private static string LoopFingerprint(LoopSnapshot value)=>$"{value.Frames}:{value.Layers}:{value.SampleRate}:"+string.Join(",",Enumerable.Range(0,(int)value.Layers).Select(value.Gain));
    private FrameworkElement BuildLooper()
    {
        var panel=Stack();panel.Children.Add(Text("Looper · společné zvukové hodiny"));
        var bus=new ComboBox{Header="Zdroj nové vrstvy",ItemsSource=new[]{"Suchá kytara","Kytara za aparátem"},SelectedIndex=(int)looperBus};bus.SelectionChanged+=(_,_)=>{if(bus.SelectedIndex>=0)looperBus=(uint)bus.SelectedIndex;};panel.Children.Add(bus);
        var aligned=new CheckBox{Content="První smyčku zarovnat na celý takt",IsChecked=looperAligned};aligned.Checked+=(_,_)=>looperAligned=true;aligned.Unchecked+=(_,_)=>looperAligned=false;panel.Children.Add(aligned);
        var count=new ComboBox{Header="Předtakt první smyčky",ItemsSource=new[]{"Bez předtaktu","1 takt","2 takty"},SelectedIndex=(int)looperCountIn};count.SelectionChanged+=(_,_)=>{if(count.SelectedIndex>=0)looperCountIn=(uint)count.SelectedIndex;};panel.Children.Add(count);
        panel.Children.Add(Text("Tempo a takt se přebírají z metronomu. Délku určí první záznam; další vrstvy mají přesně stejný počet vzorků. Předčasné pozastavení nebo výpadek během nové vrstvy ji zruší; předchozí vrstvy zůstanou. Uložení počká na dokončení probíhající vrstvy. Nejvýše 8 mono vrstev v celkovém limitu 64 MiB. Limit délky závisí na vzorkovací frekvenci.",true));
        var estimate=new CheckBox{Content="Kompenzovat novou vrstvu podle údajů ovladače",IsChecked=looperUseDriverDelay};estimate.Checked+=(_,_)=>looperUseDriverDelay=true;estimate.Unchecked+=(_,_)=>looperUseDriverDelay=false;panel.Children.Add(estimate);
        var delay=new NumberBox{Header="Ruční kompenzace (vzorky; při vypnutém odhadu)",Minimum=0,Maximum=192000,Value=looperManualDelay,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};delay.ValueChanged+=(_,e)=>{if(double.IsFinite(e.NewValue))looperManualDelay=(uint)Math.Clamp(Math.Round(e.NewValue),0,192000);};panel.Children.Add(delay);
        panel.Children.Add(Text("Údaje ovladače jsou odhad. K nim se pro záznam za aparátem přičte jeho známá DSP prodleva; kompenzace se aplikuje jednou na novou vrstvu. Celková odezva kytary zde není změřená.",true));
        void Button(string label,Func<Task> action){var button=new Button{Content=label};button.Click+=async(_,_)=>{if(powerTransition || fileOperation || backupOperationRunning || deviceOperationRunning || looperOperation || audio is null || busy)return;looperOperation=true;try{await action();}catch(Exception error){ShowPersistenceError("Looper: "+error.Message);}finally{looperOperation=false;RefreshLooper();}};panel.Children.Add(button);}
        Button("Nahrát první smyčku",async()=>{if(deviceSelectionUnavailable || deviceOperationRunning || appSettings.AudioDevice is not{InputChannel:>=0})throw new InvalidOperationException("Vyber vstup kytary v nastavení zvuku.");looperSavedFingerprint=null;looperProjectId=null;looperCreated=DateTimeOffset.UtcNow;await audio!.LoopCommandAsync(1,looperBus,looperCountIn,(uint)metroConfig.BeatsPerBar,looperAligned);});
        Button("Ukončit první smyčku a opakovat",()=>audio!.LoopCommandAsync(2));
        Button("Přidat další vrstvu na začátku smyčky",async()=>{uint compensation=looperManualDelay;if(looperUseDriverDelay){var device=await audio!.GetDeviceDetailsAsync();if(device.InputLatencyFrames<0||device.OutputLatencyFrames<0)throw new InvalidOperationException("Ovladač nenahlásil potřebné prodlevy. Vypni odhad a nastav kompenzaci ručně.");compensation=checked((uint)(device.InputLatencyFrames+device.OutputLatencyFrames)+(looperBus==1?audio.GetClock().DspLatencyFrames:0));}await audio!.LoopCommandAsync(3,looperBus,delayFrames:compensation);});
        Button("Vrátit poslední vrstvu",()=>audio!.LoopCommandAsync(6));Button("Pozastavit Looper",()=>audio!.LoopCommandAsync(4));Button("Pokračovat v opakování",()=>audio!.LoopCommandAsync(5));
        Button("Vymazat smyčku…",async()=>{var dialog=new ContentDialog{XamlRoot=DialogRoot,Title="Vymazat Looper?",Content="Neuložené vrstvy zmizí. Uložené projekty zůstanou zachované.",PrimaryButtonText="Vymazat",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await dialog.ShowAsync()==ContentDialogResult.Primary){await audio!.LoopCommandAsync(7);looperSavedFingerprint=null;looperProjectId=null;RemoveCloseGuard("looper");}});
        // Save uses its own operation guard because it is also called by the close guard.
        var save=new Button{Content="Uložit celou session pro Looper a Studio…"};save.Click+=async(_,_)=>await SaveLooperSessionAsync();panel.Children.Add(save);
        Button("Uložit mix do knihovny nahrávek…",ExportLoopRecordingAsync);
        looperSessions=new ComboBox{Header="Uložené Looper sessions",DisplayMemberPath="Name",HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(looperSessions);
        Button("Otevřít vybranou session",async()=>{if(looperSessions.SelectedItem is ProjectIndex selected)await OpenLooperSessionAsync(selected.Id);});
        looperReading=Text("Looper je prázdný.",true);panel.Children.Add(looperReading);looperLayers=Stack();looperShownLayers=uint.MaxValue;panel.Children.Add(Card(looperLayers));_ = ReloadLooperSessionsAsync();RefreshLooper();return panel;
    }
    private void RefreshLooper()
    {
        if(audio is null)return;try{
            var state=audio.GetLoop();if(looperReading is not null)looperReading.Text=$"{state.State switch{0=>"Prázdný",1=>"Předtakt / čekání na takt",2=>"Nahrávám první smyčku",3=>"Opakuji smyčku",4=>"Vrstva začne na dalším začátku",5=>"Nahrávám vrstvu",6=>"Pozastaveno",_=>"Přerušeno"}} · {state.Layers} vrstev · {(state.State==2?state.Position:state.Frames)} vzorků · {(state.SampleRate>0?(state.State==2?state.Position:state.Frames)/(double)state.SampleRate:0):F3} s · {state.Cycles} opakování"+(state.Failure>0?$" · {state.Failure switch{1=>"Dosažen paměťový limit; zachovanou část lze uložit",2=>"Rozpracovaný záznam byl přerušen",3=>"Chybí vstup kytary",4=>"Vstup obsahoval neplatné vzorky",5=>"Zařízení nebo jeho formát se změnily; zkontroluj původní frekvenci session",_=>"Záznam byl přerušen"}}":"")+((state.Flags&1)!=0?" · neúplný první záznam":"");
            if(looperLayers is not null && state.Layers!=looperShownLayers){looperLayers.Children.Clear();looperShownLayers=state.Layers;for(uint i=0;i<state.Layers;++i){var index=i;var volume=new ValueDial{Label=$"Vrstva {i+1} · hlasitost",Minimum=0,Maximum=100,Value=state.Gain((int)i)*100};volume.ValueChanged+=async(_,_)=>{if(audio is null || looperOperation)return;try{await audio.LoopCommandAsync(8,layer:index,gain:volume.Value/100);}catch(Exception error){ShowPersistenceError(error.Message);}};looperLayers.Children.Add(volume);}}
            bool capturing=state.State is 1 or 2 or 4 or 5;bool dirty=state.Frames>0 && LoopFingerprint(state)!=looperSavedFingerprint;
            if(capturing||dirty)RegisterCloseGuard("looper",new(capturing,dirty),SaveLooperSessionAsync,()=>{looperSavedFingerprint=LoopFingerprint(state);});else RemoveCloseGuard("looper");
        }catch(Exception error){if(!closed)ShowPersistenceError("Stav Looperu není dostupný. "+error.Message);}
    }
    private void ResetLooperWorkspace(){looperReading=null;looperLayers=null;looperSessions=null;looperShownLayers=uint.MaxValue;looperProjectId=null;looperSavedFingerprint=null;}
    private async Task PauseLooperForSaveAsync()
    {
        if(audio is null)return;var state=audio.GetLoop();
        if(state.State==2){await audio.LoopCommandAsync(2);var deadline=DateTime.UtcNow.AddSeconds(30);while(audio.GetLoop().State==2 && DateTime.UtcNow<deadline)await Task.Delay(20);if(audio.GetLoop().State==2)throw new IOException("První smyčka se nedokončila. Zastav zvuk a ulož zachovanou část.");}
        state=audio.GetLoop();if(state.State==4)await audio.LoopCommandAsync(6);else if(state.State==5){var finish=DateTime.UtcNow.AddSeconds(Math.Min(60,state.Duration+2));while(audio.GetLoop().State==5 && DateTime.UtcNow<finish)await Task.Delay(20);if(audio.GetLoop().State==5)throw new IOException("Vrstva se nedokončila. Zastav zvuk pro zachování předchozích vrstev a zkus uložení znovu.");}
        var ack=audio.GetLoop().Acknowledged;await audio.LoopCommandAsync(4);var until=DateTime.UtcNow.AddSeconds(3);while((audio.GetLoop().Active || audio.GetLoop().Acknowledged<=ack)&&DateTime.UtcNow<until)await Task.Delay(10);if(audio.GetLoop().Active || audio.GetLoop().Acknowledged<=ack)throw new IOException("Looper nepotvrdil zastavení zápisu do vrstev.");
    }
    private async Task<bool> SaveLooperSessionAsync()
    {
        if(looperOperation || database is null || audio is null)return false;looperOperation=true;
        string temporary=Path.Combine(userFiles.DirectoryFor("projects"),".rocker-loop-export-"+Guid.NewGuid().ToString("N"));
        try{
            await PauseLooperForSaveAsync();var state=audio.GetLoop();if(state.Layers==0)return true;var id=looperProjectId??Guid.NewGuid().ToString();var repository=new ProjectRepository(userFiles,database);var previous=looperProjectId is null?null:(await repository.LoadAsync(id)).Project;var name=await AskRecordingNameAsync("Uložit Looper session",previous?.Name??"Looper session");if(name is null)return false;
            Directory.CreateDirectory(temporary);var tracks=new List<ProjectTrack>();
            for(uint layer=0;layer<state.Layers;++layer){string path=Path.Combine(temporary,$"layer-{layer}.wav");await audio.ExportLoopAsync(path,(int)layer);await using var input=File.OpenRead(path);var stored=await new OwnedMedia(userFiles).ImportAsync(input,".wav");var attached=await repository.AttachMediaAsync(id,stored);var prior=previous?.Tracks.ElementAtOrDefault((int)layer);tracks.Add(new(prior?.Id??Guid.NewGuid().ToString("N"),prior?.Name??$"Vrstva {layer+1}",attached,stored.Sha256,state.Duration,state.Gain((int)layer)));}
            var created=looperProjectId is null?DateTimeOffset.UtcNow:looperCreated;if((state.Flags&1)!=0 && !name.EndsWith(" · neúplná session",StringComparison.Ordinal))name+=" · neúplná session";var project=new ProjectManifest(1,id,name,created,DateTimeOffset.UtcNow,(int)state.SampleRate,tracks,new((int)looperBus,checked((long)state.Frames),state.DelayFrames,(state.Flags&4)!=0,(state.Flags&1)!=0));await repository.SaveAsync(project);looperProjectId=id;looperCreated=created;looperSavedFingerprint=LoopFingerprint(state);RemoveCloseGuard("looper");await ReloadLooperSessionsAsync();ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Success;ShellNotice.Message="Looper session je uložená v projektu: "+name;return true;
        }catch(Exception error){ShowPersistenceError("Looper session nelze uložit. Vrstvy zůstávají v paměti. "+error.Message);return false;}
        finally{if(Directory.Exists(temporary))Directory.Delete(temporary,true);looperOperation=false;RefreshLooper();}
    }
    private async Task ReloadLooperSessionsAsync()
    {
        if(database is null || looperSessions is null)return;try{var snapshot=await database.SnapshotAsync();var selected=new List<ProjectIndex>();var repository=new ProjectRepository(userFiles,database);foreach(var item in snapshot.Documents.Where(d=>d.Kind=="project")){var index=UserData.Read<ProjectIndex>(item.Value);if((await repository.LoadAsync(index.Id)).Project.Looper is not null)selected.Add(index);}if(!closed && looperSessions is not null)looperSessions.ItemsSource=selected;}catch(Exception error){if(!closed)ShowPersistenceError("Seznam sessions nelze načíst. "+error.Message);}
    }
    private async Task OpenLooperSessionAsync(string id)
    {
        if(database is null || audio is null)return;var repository=new ProjectRepository(userFiles,database);var loaded=await repository.LoadAsync(id);if(loaded.Project.Looper is null || loaded.MissingMedia.Count>0)throw new InvalidDataException("Session nemá dostupné zvukové vrstvy.");
        if(audio.GetLoop().Frames>0){var dialog=new ContentDialog{XamlRoot=DialogRoot,Title="Otevřít jinou session?",Content="Aktuální neuložené vrstvy nahradí zvolená session.",PrimaryButtonText="Otevřít",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;}
        await PauseLooperForSaveAsync();await audio.StopAsync();await audio.LoopCommandAsync(7);
        for(uint layer=0;layer<loaded.Project.Tracks.Count;++layer){var track=loaded.Project.Tracks[(int)layer];await audio.LoadLoopLayerAsync(OwnedPaths.Resolve(repository.DirectoryFor(id),track.MediaPath),layer,track.Muted?0:track.Gain);}
        await audio.SetLoopMetadataAsync((uint)loaded.Project.Looper.Bus,loaded.Project.Looper.Partial,loaded.Project.Looper.DelayFrames,loaded.Project.Looper.DelayMeasured);
        looperBus=(uint)loaded.Project.Looper.Bus;looperProjectId=id;looperCreated=loaded.Project.CreatedAt;looperSavedFingerprint=LoopFingerprint(audio.GetLoop());looperShownLayers=uint.MaxValue;
        ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Informational;ShellNotice.Message=$"Session je načtená a pozastavená. Pro přehrávání použij na kartě {loaded.Project.SampleRate} Hz.";
    }
    private async Task ExportLoopRecordingAsync()
    {
        if(database is null || audio is null)return;await PauseLooperForSaveAsync();var state=audio.GetLoop();if(state.Frames==0)return;var name=await AskRecordingNameAsync("Mix Looperu do nahrávek","Looper · mix");if(name is null)return;
        var path=Path.Combine(userFiles.DirectoryFor("recordings"),".rocker-loop-export-"+Guid.NewGuid().ToString("N")+".wav");try{await audio.ExportLoopAsync(path);await using var input=File.OpenRead(path);var media=await new OwnedMedia(userFiles).ImportAsync(input,".wav");var value=new RecordingData(Guid.NewGuid().ToString(),name+((state.Flags&1)!=0?" · neúplný Looper":""),"audio/wav",state.Duration,DateTimeOffset.UtcNow,media.RelativePath,media.Bytes,media.Sha256,[]);await recordingLibrary!.SaveAsync(value);await ReloadRecordingsAsync();ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Success;ShellNotice.Message="Mix Looperu je v knihovně nahrávek.";}finally{if(File.Exists(path))File.Delete(path);}
    }
    private async Task VerifyLooperAsync()
    {
        if(audio is null || database is null || recordingLibrary is null)throw new InvalidOperationException("Looper dependencies did not initialize.");
        var recording=(await recordingLibrary.ListAsync()).First(r=>r.Capture?.Frames==480);await audio.StopAsync();await audio.LoopCommandAsync(7);var source=OwnedPaths.Resolve(userFiles.Root,recording.RelativePath);await audio.LoadLoopLayerAsync(source,0,.4);await audio.LoadLoopLayerAsync(source,1,.5);Navigate(new("16"));RefreshLooper();var state=audio.GetLoop();
        var id=Guid.NewGuid().ToString();var repository=new ProjectRepository(userFiles,database);var media=await repository.AttachMediaAsync(id,new(recording.RelativePath,recording.Sha256,recording.Bytes));var now=DateTimeOffset.UtcNow;var project=new ProjectManifest(1,id,"Kontrolní Looper session",now,now,48000,[new("layer-one","Vrstva 1",media,recording.Sha256,.01,.4),new("layer-two","Vrstva 2",media,recording.Sha256,.01,.5)],new(0,480,0,false,false));await repository.SaveAsync(project);var reopened=await new ProjectRepository(userFiles,await DataRepository.OpenAsync(userFiles)).LoadAsync(id);await ReloadLooperSessionsAsync();
        bool passed=state.Layers==2 && state.Frames==480 && state.State==6 && Math.Abs(state.Gain1-.5)<.00001 && reopened.Project.Looper?.Frames==480 && reopened.MissingMedia.Count==0 && looperLayers?.Children.Count==2;
        WriteShellDiagnostic("looper-view.json",new{result=passed?"passed":"failed",nativeLayersLoaded=state.Layers,exactFrames=state.Frames,pausedWithoutAudioDevice=state.State==6,persistenceReopened=reopened.Project.Looper?.Frames==480,nativeLayerControls=looperLayers?.Children.Count,physicalOverdubVerified=false,manualDialogsVerified=false});await audio.LoopCommandAsync(7);RemoveCloseGuard("looper");if(!passed)throw new InvalidOperationException("Looper form/session round-trip failed.");
    }

}
