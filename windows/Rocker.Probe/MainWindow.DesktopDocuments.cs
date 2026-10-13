// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.AppCore;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
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
    private FrameworkElement BuildDesktopRecordingView()
    {
        var session=documentContext.Current;var panel=Stack();
        panel.Children.Add(Text(playingRecording?.Name??"Nahrávka"));
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
        void Action(string title,Func<Task> work){var button=new Button{Content=title};button.Click+=async(_,_)=>{using var scope=documentContext.Enter(session);try{await work();}catch(Exception error){ShowPersistenceError(error.Message);}};buttons.Children.Add(button);}
        Action("Přehrát",async()=>{await ClaimDesktopSourceAsync(session);if(audio is not null)await audio.PlayAsync();});
        Action("Pozastavit",async()=>{if(services.Transport.Owner==session && audio is not null)await audio.PauseAsync();});
        Action("Značky a poznámky…",async()=>{if(playingRecording is{ } recording)await EditRecordingMarkersAsync(recording);});
        Action("Exportovat…",async()=>{if(playingRecording is{ } recording)await ExportRecordingAsync(recording);});
        panel.Children.Add(new ScrollViewer{Content=buttons,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollMode=ScrollMode.Enabled,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});
        panel.Children.Add(BuildRecordingWaveform());
        if(playingRecording is{ } value)panel.Children.Add(Text($"{FormatTime(value.DurationSec)} · {value.Markers.Count} značek\n"+value.RelativePath,true));
        return panel;
    }
}
