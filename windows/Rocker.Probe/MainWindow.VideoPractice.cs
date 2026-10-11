// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private bool videoSeeking,changingVideoLoop;
    private void InitializeVideoPractice(IReadOnlyList<DataDocument> documents)
    {
        savedVideoLoops.Clear();var record=documents.FirstOrDefault(d=>d.Kind=="yt-loops" && d.Id=="default");if(record is not null)savedVideoLoops.AddRange(UserData.Read<SavedVideoLoop[]>(record.Value));pendingVideoLoop=deletedVideoLoop=null;RefreshSavedVideoLoops();
    }
    private void RefreshSavedVideoLoops()=>VideoSavedLoops.ItemsSource=savedVideoLoops.Select(l=>new ListEntry(l.Id,$"{l.Label} · {FormatTime(l.Start)}–{FormatTime(l.End)} · {l.Rate:0.##}× · {l.VideoId}",l.VideoId)).ToArray();
    private void PersistVideoLoops(){if(persistenceReady)autosave!.Queue(UserData.Document("yt-loops","default",savedVideoLoops.ToArray()));RefreshSavedVideoLoops();}
    private SavedVideoLoop? SelectedVideoLoop()=>VideoSavedLoops.SelectedItem is ListEntry item?savedVideoLoops.FirstOrDefault(l=>l.Id==item.Id):null;
    private void VideoMark_Click(object sender,RoutedEventArgs args){if(!videoReady)return;if(sender is Button{Tag:"a"})VideoLoopA.Value=videoPosition;else VideoLoopB.Value=videoPosition;}
    private void ApplyVideoLoop()
    {
        if(!videoReady)throw new InvalidOperationException("Nejdřív načti dostupné video.");videoPractice.SetLoop(VideoLoopA.Value,VideoLoopB.Value);SendVideo(new{op="loop",enabled=true,start=videoPractice.Loop!.Start,end=videoPractice.Loop.End});changingVideoLoop=true;VideoLoopEnabled.IsChecked=true;changingVideoLoop=false;VideoLoopStatus.Text=$"Opakuje se {FormatTime(videoPractice.Loop.Start)}–{FormatTime(videoPractice.Loop.End)}. Časování YouTube je přibližné.";
    }
    private void VideoApplyLoop_Click(object sender,RoutedEventArgs args){try{ApplyVideoLoop();}catch(Exception error){VideoLoopStatus.Text=error.Message;}}
    private void VideoLoop_Changed(object sender,RoutedEventArgs args)
    {
        if(changingVideoLoop)return;
        if(VideoLoopEnabled.IsChecked==true){try{ApplyVideoLoop();}catch(Exception error){VideoLoopEnabled.IsChecked=false;VideoLoopStatus.Text=error.Message;}}else{videoPractice.ClearLoop();SendVideo(new{op="loop",enabled=false});}
    }
    private void TryRestoreVideoLoop()
    {
        if(pendingVideoLoop is not { } loop || !videoReady || videoPractice.Rates.Count==0 || videoPractice.Duration<=0)return;pendingVideoLoop=null;
        try{ApplyVideoLoop();if(videoPractice.Rates.Contains(loop.Rate))SendVideo(new{op="rate",rate=loop.Rate});else VideoLoopStatus.Text+=" Uložená rychlost není dostupná; zůstává aktuální rychlost videa.";SendVideo(new{op="seek",seconds=loop.Start});}catch(Exception error){VideoLoopStatus.Text="Uložený úsek nelze použít. "+error.Message;}
    }
    private void VideoSaveLoop_Click(object sender,RoutedEventArgs args)
    {
        try{ApplyVideoLoop();if(savedVideoLoops.Count>=10000)throw new InvalidOperationException("Uloženo už je 10 000 úseků.");var loop=videoPractice.Loop!;savedVideoLoops.Add(new(Guid.NewGuid().ToString("N"),videoPractice.Id!,VideoLoopLabel.Text.Trim() is {Length:>0} label?label:"Úsek",loop.Start,loop.End,VideoRates.SelectedItem is double rate?rate:videoPractice.Rate));PersistVideoLoops();VideoLoopStatus.Text="Úsek je uložený.";}catch(Exception error){VideoLoopStatus.Text=error.Message;}
    }
    private void VideoOpenLoop_Click(object sender,RoutedEventArgs args){if(!videoBridgeReady || SelectedVideoLoop() is not { } loop)return;LoadVideo(loop.VideoId,loop);}
    private void VideoRenameLoop_Click(object sender,RoutedEventArgs args){if(SelectedVideoLoop() is not { } loop)return;string label=VideoLoopLabel.Text.Trim();if(label.Length==0){VideoLoopStatus.Text="Nejdřív napiš nový název úseku.";return;}savedVideoLoops[savedVideoLoops.IndexOf(loop)]=loop with{Label=label};PersistVideoLoops();}
    private void VideoDeleteLoop_Click(object sender,RoutedEventArgs args){if(SelectedVideoLoop() is not { } loop)return;deletedVideoLoop=loop;savedVideoLoops.Remove(loop);PersistVideoLoops();VideoLoopStatus.Text="Úsek smazán; smazání lze vrátit.";}
    private void VideoUndoLoop_Click(object sender,RoutedEventArgs args){if(deletedVideoLoop is null || savedVideoLoops.Count>=10000)return;savedVideoLoops.Add(deletedVideoLoop);deletedVideoLoop=null;PersistVideoLoops();}
    private void SeekVideo(){if(videoReady)SendVideo(new{op="seek",seconds=VideoSeek.Value});}
    private void VideoSeek_Released(object sender,PointerRoutedEventArgs args){if(videoSeeking){videoSeeking=false;SeekVideo();}}
    private void VideoSeek_KeyUp(object sender,KeyRoutedEventArgs args){if(args.Key is Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Home or Windows.System.VirtualKey.End)SeekVideo();}
    private async Task VerifyVideoPracticeAsync()
    {
        var previous=savedVideoLoops.ToArray();savedVideoLoops.Clear();videoPractice.Begin("M7lc1UVf-VE");videoPractice.Observe(new("ready",Token:videoPractice.Token));videoPractice.Observe(new("progress",Position:12,Duration:120,Rate:1,Token:videoPractice.Token));videoReady=true;VideoRates.ItemsSource=new double[]{.5,1,2};VideoRates.SelectedItem=1.0;VideoLoopA.Value=4;VideoLoopB.Value=10;VideoLoopLabel.Text="Kontrolní úsek";VideoSaveLoop_Click(this,new());
        bool saved=savedVideoLoops.Count==1 && savedVideoLoops[0].Start==4 && savedVideoLoops[0].End==10;VideoSavedLoops.SelectedIndex=0;VideoLoopLabel.Text="Přejmenovaný úsek";VideoRenameLoop_Click(this,new());bool renamed=savedVideoLoops[0].Label=="Přejmenovaný úsek";VideoSavedLoops.SelectedIndex=0;VideoDeleteLoop_Click(this,new());bool deleted=savedVideoLoops.Count==0;VideoUndoLoop_Click(this,new());bool undone=savedVideoLoops.Count==1;
        await autosave!.FlushAsync();var snapshot=await (await DataRepository.OpenAsync(userFiles)).SnapshotAsync();bool persisted=UserData.Read<SavedVideoLoop[]>(snapshot.Documents.Single(d=>d.Kind=="yt-loops").Value).Single()==savedVideoLoops.Single();
        savedVideoLoops.Clear();savedVideoLoops.AddRange(previous);PersistVideoLoops();await autosave.FlushAsync();videoReady=false;SetVideoControls(false);VideoLoopEnabled.IsChecked=false;bool pass=saved && renamed && deleted && undone && persisted;
        WriteShellDiagnostic("video-practice.json",new{result=pass?"passed":"failed",nativeSavedLoopList=true,saveRenameDeleteUndoVerified=saved && renamed && deleted && undone,persistenceReopened=persisted,onlineVideoVerified=false,audioCoexistenceVerified=false});if(!pass)throw new InvalidOperationException("Native video practice persistence check failed.");
    }
    private async void VideoSharedAudio_Click(object sender,RoutedEventArgs args)
    {
        Navigate(new("29"));await DeviceOperationAsync(async()=>{await RefreshDeviceListAsync();if(closed)return;if(deviceCatalog?.Backends.Any(b=>b.Name=="Windows Audio")==true){DeviceBackendBox.SelectedItem="Windows Audio";DeviceSelectionStatus.Text="Sdílené WASAPI je vybrané. Zvol interfejs pro vstup a výstup a potvrď Ověřit a použít. Ve Windows nastav stejné zařízení pro přehrávání videa.";}else DeviceSelectionStatus.Text="Sdílené Windows Audio není dostupné. Zkontroluj ovladač zvukového zařízení.";});
    }
}
