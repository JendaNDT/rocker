// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml.Controls;
using Rocker.AppCore;
using Rocker.Domain;
using Rocker.Interop;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private async Task OpenDesktopSongAsync(string path,string name,SongSession? relocated)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        path=Rocker.Infrastructure.CanonicalFiles.ResolveExisting(path).Path;
        var key=DesktopDocumentKey.ForCanonicalFile("07",path);
        var session=OpenDesktop(new("07",relocated?.Id),key);
        using var scope=documentContext.Enter(session);
        if(song is not null && mediaPath==path)return;
        if(session.Get<Task?>("song-loading",()=>null) is{ } pending){await pending;return;}
        var loading=LoadDesktopSongCoreAsync(session,path,name,relocated);session.Set("song-loading",loading);
        try{await loading;}finally{session.Set<Task?>("song-loading",null);}
    }
    private async Task LoadDesktopSongCoreAsync(DocumentSession session,string path,string name,SongSession? relocated)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        using var scope=documentContext.Enter(session);
        using var operation=services.Operations.Begin(session.Id,"inspect-media","Čtení skladby a zvukové časové osy");
        try
        {
            var result=await MediaInspection.ReadAsync(path,operation.Cancellation);
            if(!session.IsAlive)return;
            var existing=relocated??songs.Values.FirstOrDefault(s=>string.Equals(s.SourcePath,path,StringComparison.OrdinalIgnoreCase));
            song=SongRules.Validate((existing??new(Guid.NewGuid().ToString("D"),name,path,result.DurationSeconds)) with{SourcePath=path,Duration=result.DurationSeconds,Sections=(existing?.Sections??[]).Where(s=>s.End<=result.DurationSeconds).ToArray()});
            mediaPath=path;mediaMidi=null;currentSpeed=song.Speed;songPeaks=result.Peaks;SaveSong();RememberFile(path);RebuildDesktopDocument(session);RefreshDesktopLibrary();
        }
        catch(OperationCanceledException){ShowPreparationCancelled();}
    }
    private async Task<bool> OpenDesktopRecordingAsync(RecordingData value)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        var session=OpenDesktop(new("14",value.Id));using var scope=documentContext.Enter(session);
        using var operation=services.Operations.Begin(session.Id,"inspect-recording","Čtení nahrávky");
        if(!await new Rocker.Infrastructure.OwnedMedia(userFiles).VerifyAsync(new(value.RelativePath,value.Sha256,value.Bytes),operation.Cancellation))throw new InvalidDataException("Nahrávka chybí nebo je změněná.");
        string path=Rocker.Infrastructure.OwnedPaths.Resolve(userFiles.Root,value.RelativePath);
        var result=await MediaInspection.ReadAsync(path,operation.Cancellation);if(!session.IsAlive)return false;
        playingRecording=value;mediaPath=path;mediaMidi=null;currentSpeed=1;recordingPeaks=result.Peaks;songPeaks=result.Peaks;
        RebuildDesktopDocument(session);DrawRecordingWaveform();return true;
    }
    private void RebuildDesktopDocument(DocumentSession session)
    {
        if(!session.IsAlive || !desktopDocuments.TryGetValue(session,out var tab))return;
        using var scope=documentContext.Enter(session);
        if(session.Target.ViewId is "08" or "20")return;
        var page=BuildWorkspace(session.Target);tab.Content=DesktopDocumentContent(page);
        tab.Header=session.Target.ViewId=="07"?song?.Name??"Skladba":session.Target.ViewId=="15"?studioProject?.Name??"Studio":WorkspaceViews.Get(session.Target.ViewId).Title;
    }
    private async Task ClaimDesktopSourceAsync(DocumentSession requested)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(!desktopEnabled || audio is null)return;
        if(services.Transport.Owner==requested && audio.GetTransport().Ready!=0)return;
        services.Transport.Capturing=captureDraft is not null;
        services.Transport.LooperActive=audio.GetLoop().State is 1 or 2 or 3 or 4 or 5;
        using var operation=services.Operations.Begin(requested.Id,"source","Příprava zdroje přehrávání");
        using var cancellation=operation.Cancellation.Register(()=>audio.CancelMediaPreparation());
        await services.Transport.TakeAsync(requested,async()=>
        {
            var previous=services.Transport.Owner;var oldTicket=await audio.GetMediaSourceTicketAsync();var oldState=audio.GetTransport();
            try{await PrepareDesktopSourceCoreAsync(requested);operation.Cancellation.ThrowIfCancellationRequested();}
            catch
            {
                audio.CancelMediaPreparation();
                if(await audio.GetMediaSourceTicketAsync()!=oldTicket)
                {
                    if(previous is{IsAlive:true})
                    {
                        using var restoreScope=documentContext.Enter(previous);
                        try{await PrepareDesktopSourceCoreAsync(previous);await audio.SeekAsync(Math.Min(oldState.PositionSeconds,audio.GetTransport().DurationSeconds));}
                        catch{services.Transport.Release(previous);await audio.ClearMediaAsync();}
                    }
                    else await audio.ClearMediaAsync();
                }
                throw;
            }
            if(previous is not null && previous!=requested){using var scope=documentContext.Enter(previous);studioOwnsPlayback=false;studioTicket=0;}
        },operation.Cancellation);
        RefreshDesktopInspector(documentContext.Active);
    }
    private async Task PrepareDesktopSourceCoreAsync(DocumentSession session)
    {
            using var scope=documentContext.Enter(session);
            switch(session.Target.ViewId)
            {
                case "07" or "14":
                    if(mediaPath is null)throw new InvalidOperationException("Nejdřív otevři zvukový soubor.");
                    var ticket=await audio!.PrepareFileWithIdentityAsync(mediaPath,currentSpeed);await WaitForDesktopSourceAsync(ticket);
                    await audio!.SeekAsync(Math.Min((song?.Position??0)/currentSpeed,audio.GetTransport().DurationSeconds));break;
                case "20":
                    if(preview is null)throw new InvalidOperationException("Nejdřív otevři partituru.");
                    await PrepareNotationCoreAsync(preview);break;
                case "15":await PrepareStudioCoreAsync();break;
                default:throw new InvalidOperationException("Tento dokument nemá připravený zvukový zdroj.");
            }
    }
    private async Task WaitForDesktopSourceAsync(ulong ticket,CancellationToken cancellation=default)
    {
        for(int wait=0;wait<200;++wait)
        {
            cancellation.ThrowIfCancellationRequested();if(services.Operations.Snapshot().Any(o=>o.Owner==documentContext.Current.Id && o.Kind=="source" && o.CancellationRequested))throw new OperationCanceledException();if(closed)throw new OperationCanceledException();
            if(audio is not null && await audio.GetMediaSourceTicketAsync()==ticket && audio.GetTransport().Ready!=0)return;
            await Task.Delay(10,cancellation);
        }
        throw new InvalidOperationException("Přehrávač nepřevzal požadovaný zdroj. Přehrávání se samo nespustí.");
    }
    private async Task ClaimDesktopGraderAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(!desktopEnabled)return;var next=documentContext.Current;
        if(services.Transport.Grader is{ } old && old!=next)
        {
            using var scope=documentContext.Enter(old);CancelPalm();surgicalListening=false;await StopRhythmUiAsync();
        }
        services.Transport.SetGrader(next);
    }
    private void RefreshDesktopSessions(TransportSnapshot transport,AnalysisSnapshot analysis)
    {
        foreach(var session in services.Documents.All)
        {
            using var scope=documentContext.Enter(session);
            if(services.Transport.Grader==session){RefreshLesson(analysis);RefreshSurgical(analysis);RefreshPalmMute(analysis);RefreshRhythm();}
            if(services.Transport.Owner==session){RefreshNotation(transport);RefreshSong(transport);RefreshRecordingWaveform();}
            if(desktopDocuments.TryGetValue(session,out var tab))
            {
                session.Dirty=closeGuards.Any(p=>p.Key.StartsWith(session.Id+"/",StringComparison.Ordinal) && p.Value.State.RequiresConfirmation);
                string title=session.Target.ViewId=="07"?song?.Name??"Skladba":session.Target.ViewId=="15"?studioProject?.Name??"Studio":WorkspaceViews.Get(session.Target.ViewId).Title;
                if(session.Target.ViewId=="20" && notationName.Length>0)title=notationName;
                tab.Header=title+(session.Dirty?" *":"");
            }
        }
        RefreshDesktopInspector(documentContext.Active);
    }
}
