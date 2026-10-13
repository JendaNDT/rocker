// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.AppCore;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private async Task OpenDesktopProjectFileAsync(string path,CancellationToken cancellation)
    {
        var source=Rocker.Infrastructure.CanonicalFiles.ResolveExisting(path);
        string? id=desktopProjectAliases.GetValueOrDefault(source.Identity);
        var repository=services.Storage!.Projects;
        if(id is null && source.Path.StartsWith(userFiles.DirectoryFor("projects")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) && Guid.TryParse(Path.GetFileName(Path.GetDirectoryName(source.Path)),out var ownedId))id=ownedId.ToString();
        if(id is not null){OpenDesktop(new("15",id));return;}
        using var operation=services.Operations.Begin(documentContext.Current.Id,"project-import","Import projektu",cancellable:false);
        var project=await repository.ImportAsync(source.Path,cancellation);cancellation.ThrowIfCancellationRequested();desktopProjectAliases[source.Identity]=project.Id;
        var session=OpenDesktop(new("15",project.Id));using var scope=documentContext.Enter(session);studioProject=project;studioUndo.Clear();RebuildDesktopDocument(session);await RefreshDesktopLibraryAssetsAsync();
    }
    private async Task LoadDesktopProjectAsync(DocumentSession session,string id)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        using var scope=documentContext.Enter(session);
        using var operation=services.Operations.Begin(session.Id,"project-load","Načítání projektu",cancellable:false);
        try
        {
            var result=await services.Storage!.Projects.LoadAsync(id);
            if(!session.IsAlive || studioProject is not null)return;
            studioProject=result.Project;studioUndo.Clear();studioOwnsPlayback=false;studioTicket=0;
            if(result.MissingMedia.Count>0)ShowPersistenceError("Projekt obsahuje chybějící nebo změněná média. Manifest zůstává zachovaný; doplň původní soubory před přehráváním.");
            RebuildDesktopDocument(session);
        }
        catch(Exception error){if(session.IsAlive)ShowPersistenceError("Projekt nelze otevřít: "+error.Message);}
    }
    private async Task LoadDesktopRecordingAsync(DocumentSession session,string id)
    {
        using var scope=documentContext.Enter(session);
        try
        {
            if(recordingLibrary is null)return;var value=(await recordingLibrary.ListAsync()).FirstOrDefault(r=>r.Id==id);
            if(value is null)throw new FileNotFoundException("Nahrávka už není v knihovně.");
            if(!session.IsAlive || playingRecording is not null)return;
            await OpenDesktopRecordingAsync(value);
        }
        catch(Exception error){if(session.IsAlive)ShowPersistenceError("Nahrávku nelze otevřít: "+error.Message);}
    }
    private FrameworkElement BuildDesktopRecordingView()
    {
        var session=documentContext.Current;
        var surface=(Desktop.DocumentSurface)BuildRecordingWaveform();
        void Action(string title,Func<Task> work)
        {
            var button=new Button{Content=title,MinHeight=32};
            button.Click+=async(_,_)=>{using var scope=documentContext.Enter(session);if(!session.IsAlive)return;try{await work();}catch(Exception error){ShowPersistenceError(error.Message);}};
            surface.Toolbar.Children.Insert(0,button);
        }
        Action("Exportovat…",async()=>{if(playingRecording is{ } recording)await ExportRecordingAsync(recording);});
        Action("Značky a poznámky…",async()=>{if(playingRecording is{ } recording)await EditRecordingMarkersAsync(recording);});
        Action("Pauza",async()=>{if(services.Transport.Owner==session && audio is not null)await audio.PauseAsync();});
        Action("Přehrát",async()=>{await ClaimDesktopSourceAsync(session);if(audio is not null)await audio.PlayAsync();});
        return surface;
    }
}
