// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Rocker.Notation;
using Rocker.Interop;
using Windows.Storage.Streams;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private ScoreDocument? notationDocument { get => documentContext.Current.Get<ScoreDocument?>("notationDocument", () => default); set => documentContext.Current.Set("notationDocument",value); }private NotationOptions notationOptions { get => documentContext.Current.Get<NotationOptions>("notationOptions", () => new()); set => documentContext.Current.Set("notationOptions",value); }
    private string? notationSourcePath { get => documentContext.Current.Get<string?>("notationSourcePath", () => default); set => documentContext.Current.Set("notationSourcePath",value); }private string notationName { get => documentContext.Current.Get<string>("notationName", () => ""); set => documentContext.Current.Set("notationName",value); }private byte[]? notationPlayingMidi { get => documentContext.Current.Get<byte[]?>("notationPlayingMidi", () => default); set => documentContext.Current.Set("notationPlayingMidi",value); }private bool notationUpdating { get => documentContext.Current.Get<bool>("notationUpdating", () => default); set => documentContext.Current.Set("notationUpdating",value); }private int notationFollowBar { get => documentContext.Current.Get<int>("notationFollowBar", () => default); set => documentContext.Current.Set("notationFollowBar",value); }
    private (double Start,double End)? notationLoop { get => documentContext.Current.Get<(double Start,double End)?>("notationLoop", () => default); set => documentContext.Current.Set("notationLoop",value); }private DateTime nextNotationFollowRender { get => documentContext.Current.Get<DateTime>("nextNotationFollowRender", () => default); set => documentContext.Current.Set("nextNotationFollowRender",value); }
    private async Task SetNotationImageAsync(ScorePreview result)
    {
        using var stream=new InMemoryRandomAccessStream();using(var writer=new DataWriter(stream.GetOutputStreamAt(0))){writer.WriteBytes(result.Png);await writer.StoreAsync();await writer.FlushAsync();writer.DetachStream();}stream.Seek(0);var bitmap=new BitmapImage();await bitmap.SetSourceAsync(stream);if(closed)return;
        preview=result;ScoreImage.Source=bitmap;ScoreImage.Width=ScoreCanvas.Width=result.Width;ScoreImage.Height=ScoreCanvas.Height=result.Height;NotationPlayhead.Visibility=Visibility.Collapsed;SelectionBox.Visibility=Visibility.Collapsed;
    }
    private void SetNotationDocument(ScoreDocument document,string name)
    {
        notationDocument=document;notationPlayingMidi=null;notationName=name;notationOptions=preview!.Options;notationLoop=null;notationFollowBar=0;notationUpdating=true;
        try{NotationTrackBox.ItemsSource=document.Tracks;NotationTrackBox.SelectedItem=document.Tracks[notationOptions.Track];NotationBar.Maximum=NotationLoopFirst.Maximum=NotationLoopLast.Maximum=document.BarCount;NotationBar.Value=NotationLoopFirst.Value=NotationLoopLast.Value=1;NotationAllTracks.IsChecked=notationOptions.AllTracksAudio;NotationZoom.SelectedIndex=2;UpdateNotationOccurrences();}finally{notationUpdating=false;}
    }
    private void UpdateNotationOccurrences()
    {
        if(notationDocument is null || !double.IsFinite(NotationLoopFirst.Value))return;int first=(int)NotationLoopFirst.Value;NotationOccurrence.ItemsSource=notationDocument.Playback.Where(b=>b.Number==first).Select((b,i)=>$"Průchod {i+1} · {FormatTime(b.StartSeconds)}").ToArray();NotationOccurrence.SelectedIndex=0;
    }
    private void MarkNotationBar(int number,bool playhead=false)
    {
        var bar=preview?.Bar(number);var box=playhead?NotationPlayhead:SelectionBox;if(bar is null){box.Visibility=Visibility.Collapsed;return;}Canvas.SetLeft(box,bar.X);Canvas.SetTop(box,bar.Y);box.Width=bar.Width;box.Height=bar.Height;box.Visibility=Visibility.Visible;
        if(!playhead){NotationAccessible.Text=notationDocument?.DescribeBar(notationOptions.Track,number)??"";SelectionStatus.Text=$"Takt {bar.Number} · první průchod {FormatTime(bar.StartSeconds)} až {FormatTime(bar.EndSeconds)}. Opakované průchody vybírej u smyčky.";}
    }
    private async Task ChangeNotationViewAsync(NotationOptions options,bool prepareAudio)
    {
        if(busy || notationDocument is null || closed)return;SetBusy(true);var document=notationDocument;var previousOptions=notationOptions;
        try{var result=await Task.Run(()=>document.Render(options));if(closed)return;await SetNotationImageAsync(result);notationOptions=options;MarkNotationBar(double.IsFinite(NotationBar.Value)?(int)NotationBar.Value:options.FirstBar);NotationStatus.Text=$"{notationName} · {document.Tracks[options.Track].Name} · takty {options.FirstBar}–{Math.Min(document.BarCount,options.FirstBar+options.PageBars-1)} · {options.Scale:P0}";
            if(prepareAudio && audio is not null && (!desktopEnabled || services.Transport.Owner==documentContext.Current)){if(!await StopCaptureAsync())return;var old=audio.GetTransport();bool wasNotation=ReferenceEquals(mediaMidi,notationPlayingMidi) && mediaMidi is not null;double position=wasNotation?old.PositionSeconds*currentSpeed:0;await PrepareNotationAsync(result);await audio.SeekAsync(Math.Min(position/currentSpeed,audio.GetTransport().DurationSeconds));if(wasNotation && old.Playing!=0)await audio.PlayAsync();}
        }catch(Exception error){if(preview?.Options!=options)notationOptions=previousOptions;NotationStatus.Text="Notový pohled nelze změnit. "+error.Message;}
        finally{if(!closed){notationUpdating=true;try{NotationTrackBox.SelectedItem=notationDocument.Tracks[notationOptions.Track];NotationAllTracks.IsChecked=notationOptions.AllTracksAudio;NotationZoom.SelectedIndex=Array.IndexOf(new[]{.5,.75,1.0,1.25,1.5,2.0},notationOptions.Scale);}finally{notationUpdating=false;}SetBusy(false);}}
    }
    private async Task PrepareNotationAsync(ScorePreview result)
    {
        if(desktopEnabled && services.Transport.Owner!=documentContext.Current)await ClaimDesktopSourceAsync(documentContext.Current);
        else await PrepareNotationCoreAsync(result);
    }
    private async Task PrepareNotationCoreAsync(ScorePreview score)
    {
        if(audio is null)return;songLoadCancelled=false;await StopRhythmUiAsync();await audio.PrepareMidiAsync(score.Midi,currentSpeed);await WaitForSongAsync(currentSpeed);if(closed)return;
        studioOwnsPlayback=false;studioTicket=0;mediaMidi=score.Midi;notationPlayingMidi=score.Midi;mediaPath=null;song=null;songPeaks=[];workspaceCache.Remove(new("07"));MediaTitle.Text="Noty · "+notationName;await audio.SetLoopAsync(0,0,false);if(notationLoop is { } loop)await audio.SetLoopAsync(loop.Start/currentSpeed,loop.End/currentSpeed,true);
    }
    private async void NotationTrack_Changed(object sender,SelectionChangedEventArgs args){if(notationUpdating || NotationTrackBox.SelectedItem is not NotationTrack track)return;await ChangeNotationViewAsync(notationOptions with{Track=track.Index},!notationOptions.AllTracksAudio);}
    private async void NotationAudio_Changed(object sender,RoutedEventArgs args){if(notationUpdating || notationDocument is null)return;await ChangeNotationViewAsync(notationOptions with{AllTracksAudio=NotationAllTracks.IsChecked==true},true);}
    private async void NotationZoom_Changed(object sender,SelectionChangedEventArgs args){if(notationUpdating || notationDocument is null || NotationZoom.SelectedItem is not ComboBoxItem item)return;await ChangeNotationViewAsync(notationOptions with{Scale=double.Parse((string)item.Tag,System.Globalization.CultureInfo.InvariantCulture)},false);}
    private async void NotationBar_Changed(NumberBox sender,NumberBoxValueChangedEventArgs args)
    {
        if(notationUpdating || notationDocument is null || !double.IsFinite(args.NewValue))return;int number=(int)Math.Clamp(args.NewValue,1,notationDocument.BarCount);int first=(number-1)/12*12+1;if(first!=notationOptions.FirstBar)await ChangeNotationViewAsync(notationOptions with{FirstBar=first},false);MarkNotationBar(number);
    }
    private void NotationLoopFirst_Changed(NumberBox sender,NumberBoxValueChangedEventArgs args){if(!notationUpdating)UpdateNotationOccurrences();}
    private async void NotationLoop_Click(object sender,RoutedEventArgs args)
    {
        if(audio is null || notationDocument is null || preview is null || busy || !double.IsFinite(NotationLoopFirst.Value) || !double.IsFinite(NotationLoopLast.Value))return;
        try{var loop=notationDocument.LoopSeconds((int)NotationLoopFirst.Value,(int)NotationLoopLast.Value,Math.Max(0,NotationOccurrence.SelectedIndex));SetBusy(true);if(!await StopCaptureAsync())return;if(!ReferenceEquals(mediaMidi,notationPlayingMidi) || mediaMidi is null)await PrepareNotationAsync(preview);await audio.SetLoopAsync(loop.Start/currentSpeed,loop.End/currentSpeed,true);notationLoop=loop;SelectionStatus.Text=$"Smyčka taktů {(int)NotationLoopFirst.Value}–{(int)NotationLoopLast.Value} · {FormatTime(loop.Start)} až {FormatTime(loop.End)}.";}
        catch(Exception error){NotationStatus.Text="Smyčku nelze použít. "+error.Message;}finally{if(!closed)SetBusy(false);}
    }
    private async void NotationLoopOff_Click(object sender,RoutedEventArgs args){notationLoop=null;try{if(audio is not null && mediaMidi is not null && ReferenceEquals(mediaMidi,notationPlayingMidi))await audio.SetLoopAsync(0,0,false);}catch(Exception error){ShowError(error);}}
    private async void NotationSeek_Click(object sender,RoutedEventArgs args)
    {
        if(audio is null || notationDocument is null || preview is null || busy || !double.IsFinite(NotationBar.Value))return;
        try{SetBusy(true);if(!await StopCaptureAsync())return;if(!ReferenceEquals(mediaMidi,notationPlayingMidi) || mediaMidi is null)await PrepareNotationAsync(preview);var bar=notationDocument.Playback.First(b=>b.Number==(int)NotationBar.Value);await audio.SeekAsync(Math.Min(bar.StartSeconds/currentSpeed,audio.GetTransport().DurationSeconds));}
        catch(Exception error){ShowError(error);}finally{if(!closed)SetBusy(false);}
    }
    private async Task VerifyNotationIntegratedAsync()
    {
        Navigate(new("20"));await LoadScoreAsync(Path.Combine(AppContext.BaseDirectory,"fixtures","notation-multitrack.musicxml"));
        var document=notationDocument!;bool structure=document.Tracks.Count==3 && NotationTrackBox.Items.Count==3;
        await ChangeNotationViewAsync(new(Track:2,FirstBar:3,PageBars:2,Scale:1.5),false);
        MarkNotationBar(3);bool rendered=preview?.Bar(3) is not null && preview.Bar(1) is null && preview.NoteOnCount==24 && SelectionBox.Visibility==Visibility.Visible && NotationAccessible.Text.Contains("bicí",StringComparison.Ordinal);
        var loop=document.LoopSeconds(1,2,1);bool repeated=Math.Abs(loop.Start-document.Playback[2].StartSeconds)<1e-6 && loop.End>loop.Start;
        var previous=preview;await LoadScoreAsync(Path.Combine(AppContext.BaseDirectory,"fixtures","missing-score.gp"));bool retained=ReferenceEquals(previous,preview) && ReferenceEquals(document,notationDocument);
        bool pass=structure && rendered && repeated && retained;
        WriteShellDiagnostic("notation-workspace.json",new{result=pass?"passed":"failed",nativeTrackCount=document.Tracks.Count,selectedTrack=notationOptions.Track,zoom=notationOptions.Scale,repeatedLoopVerified=repeated,lastGoodFilePreserved=retained,nativeBitmapDecoded=rendered,physicalAudioVerified=false});
        await LoadScoreAsync(Path.Combine(AppContext.BaseDirectory,"fixtures","notation-phrases.gp"));Navigate(new("01"));if(!pass)throw new InvalidOperationException("Native notation workspace failed.");
    }
    private void RefreshNotation(TransportSnapshot transport)
    {
        if(CurrentTarget.ViewId!="20" || notationDocument is null || mediaMidi is null || !ReferenceEquals(mediaMidi,notationPlayingMidi))return;var bar=notationDocument.AtSeconds(transport.PositionSeconds*currentSpeed);if(bar is null){NotationPlayhead.Visibility=Visibility.Collapsed;return;}MarkNotationBar(bar.Number,true);if(bar.Number==notationFollowBar || NotationFollow.IsChecked!=true || busy)return;notationFollowBar=bar.Number;int first=(bar.Number-1)/12*12+1;if(first!=notationOptions.FirstBar && DateTime.UtcNow>=nextNotationFollowRender){notationFollowBar=0;nextNotationFollowRender=DateTime.UtcNow.AddSeconds(1);_=ChangeNotationViewAsync(notationOptions with{FirstBar=first},false);}
    }
}
