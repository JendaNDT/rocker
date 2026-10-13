// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private async Task ApplyDesktopPracticeSetupAsync(PracticeSetup setup)
    {
        if(audio is null || DesktopHasCriticalOperation())throw new InvalidOperationException("Počkej na dokončení probíhající operace.");
        if(audio.GetLoop().State is 1 or 2 or 4 or 5)throw new InvalidOperationException("Nejdřív dokonči nahrávání vrstvy v Looperu.");
        if(!AllTunings().Any(t=>t.Id==setup.TuningId))throw new InvalidOperationException("Ladění sestavy chybí.");
        var source=setup.SongId is{ } id?songs.GetValueOrDefault(id)??throw new FileNotFoundException("Skladba sestavy chybí."):null;
        RecordingData? recording=null;
        if(setup.RecordingId is{ } recordingId)
        {
            recording=(await recordingLibrary!.ListAsync()).FirstOrDefault(r=>r.Id==recordingId)??throw new FileNotFoundException("Nahrávka sestavy chybí.");
            if(!await new OwnedMedia(userFiles).VerifyAsync(new(recording.RelativePath,recording.Sha256,recording.Bytes)))throw new InvalidDataException("Nahrávka sestavy je změněná nebo chybí.");
            source=source! with{SourcePath=OwnedPaths.Resolve(userFiles.Root,recording.RelativePath)};
        }
        if(source is not null && (!File.Exists(source.SourcePath) || setup.LoopEnabled && setup.LoopEnd>source.Duration))throw new InvalidDataException("Soubor nebo úsek sestavy není dostupný. Otevři skladbu znovu a uprav sestavu.");
        await StopDesktopSessionsAsync();workspaceOperation=true;BeginPreparation("Připravuji sestavu cvičení…");
        try
        {
            audio.SetMonitoring(false,liveGain);await audio.StopAsync();await audio.ClearMediaAsync();metroEnabled=false;++metroRevision;metroApplyTimer.Stop();await ApplyMetronomeAsync();
            if(source is not null)
            {
                Rocker.AppCore.DocumentSession owner;
                if(recording is not null){if(!await OpenDesktopRecordingAsync(recording))throw new OperationCanceledException();owner=OpenDesktop(new("14",recording.Id));}
                else{await OpenDesktopSongAsync(source.SourcePath,source.Name,source);owner=OpenDesktop(new("07",source.Id),DesktopDocumentKey.ForCanonicalFile("07",CanonicalFiles.ResolveExisting(source.SourcePath).Path));}
                using var scope=documentContext.Enter(owner);PreparationToken.ThrowIfCancellationRequested();
                if(song is null || mediaPath is null)throw new InvalidOperationException("Skladba se nepodařila připravit.");
                currentSpeed=setup.Speed;song=song with{Speed=setup.Speed,Position=Math.Min(setup.Position,song.Duration),Ramp=false};SaveSong();
                await ClaimDesktopSourceAsync(owner);PreparationToken.ThrowIfCancellationRequested();
                await audio.SeekAsync(song.Position/currentSpeed);await audio.SetLoopAsync(setup.LoopStart/currentSpeed,setup.LoopEnd/currentSpeed,setup.LoopEnabled);
                appliedSongLoopId=song.Id;appliedSongLoopStart=setup.LoopStart;appliedSongLoopEnd=setup.LoopEnd;
                if(recording is not null)SetRecordingRange(setup.LoopEnabled?setup.LoopStart:0,setup.LoopEnabled?setup.LoopEnd:recording.DurationSec);
            }
            else{if(services.Transport.Owner is{ } owner)services.Transport.Release(owner);await audio.ClearMediaAsync();}
            PreparationToken.ThrowIfCancellationRequested();ampSettings=setup.Amp;metroConfig=setup.Metronome;metroEnabled=false;
            appSettings=appSettings with{AmpEnabled=setup.AmpEnabled,TuningId=setup.TuningId};SaveSettings();QueueAmplifier();QueueMetronome();await ApplyAmplifierAsync();await ApplyMetronomeAsync();RefreshTuningSelector();RebuildTunerStrings();RebuildMetroBeats();await autosave!.FlushAsync();
            appliedNavigationTask=setup.Target;Navigate(setup.Target);RefreshState();ShellNotice.IsOpen=true;ShellNotice.Message="Připravená sestava: "+setup.Name+". Přehrávání a poslech kytary spustíš sám.";
        }
        finally{workspaceOperation=false;EndPreparation();RefreshFavoriteButton();}
    }
}
