// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;
using Rocker.AppCore;
using Rocker.Infrastructure;
using Windows.System;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private void InitializeDesktopCommands()
    {
        void Command(string id,string label,Func<Task> execute,Func<bool>? enabled=null)
        {
            services.Commands.Register(new(id,label,c=>{using var scope=documentContext.Enter(c.Session);return !closingWindow && !closed && (enabled?.Invoke()??true); },async c=>{using var scope=documentContext.Enter(c.Session);await execute();}));
        }
        Command("file.open","Otevřít…",OpenDesktopFilePickerAsync,()=>!fileOperation && !busy);
        Command("file.save","Uložit",async()=>{if(CurrentTarget.ViewId=="15")await SaveStudioAsync();else if(CurrentTarget.ViewId=="16")await SaveLooperSessionAsync();else if(CurrentTarget.ViewId=="20")await ExportDesktopMidiAsync();else await services.FlushAsync();},()=>!fileOperation && !studioOperation);
        Command("edit.undo","Zpět",()=>UndoDesktopStudioAsync(false),()=>CurrentTarget.ViewId=="15" && studioUndo.Count>0 && !studioOperation);
        Command("edit.redo","Znovu",()=>UndoDesktopStudioAsync(true),()=>CurrentTarget.ViewId=="15" && studioRedo.Count>0 && !studioOperation);
        Command("document.close","Zavřít dokument",async()=>{if(TabFor(documentContext.Current) is{ } tab)await CloseDesktopTabAsync(tab);});
        Command("document.detach","Samostatné okno",()=>{DetachDesktopTab(TabFor(documentContext.Current));return Task.CompletedTask;});
        Command("document.attach","Připojit do pracovní plochy",()=>{AttachDesktopTab(TabFor(documentContext.Current));return Task.CompletedTask;});
        Command("transport.toggle","Přehrát / pauza",ToggleDesktopTransportAsync,()=>!busy && !fileOperation && !deviceOperationRunning);
        Command("transport.stop","Zastavit vše",StopDesktopAudioAsync);
        Command("window.focus","Jen hraju · F11",()=>{SetFocusMode(!focusMode);return Task.CompletedTask;});
        Command("window.search","Hledat…",()=>{Search_Click(this,new());return Task.CompletedTask;});
    }
    private async Task ExecuteDesktopCommandAsync(string id,DocumentSession? session=null)
    {
        var owner=session??documentContext.Active;using var scope=documentContext.Enter(owner);
        try{await services.Commands.ExecuteAsync(id,new(owner,HostWindow(owner) is Desktop.DesktopWindow host?host.Id:"workspace",services.Transport.Owner));}
        catch(OperationCanceledException){ShowPreparationCancelled();}
        catch(Exception error){ShowPersistenceError(error.Message);}
    }
    private void BindDesktopCommands(FrameworkElement root)
    {
        root.KeyDown+=async(_,args)=>
        {
            if(args.Handled || !desktopEnabled)return;
            bool control=Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            bool shift=Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            string? command=control?args.Key switch{VirtualKey.O=>"file.open",VirtualKey.S=>"file.save",VirtualKey.W=>"document.close",VirtualKey.K=>"window.search",VirtualKey.Y=>"edit.redo",VirtualKey.Z=>shift?"edit.redo":"edit.undo",_=>null}:args.Key switch{VirtualKey.F11=>"window.focus",VirtualKey.Space=>"transport.toggle",_=>null};
            if(command is "edit.undo" or "edit.redo" && TextOrInteractiveFocus(args.OriginalSource as DependencyObject))return;
            if(command=="transport.toggle" && TextOrInteractiveFocus(args.OriginalSource as DependencyObject))return;
            if(args.Key==VirtualKey.Escape && focusMode){SetFocusMode(false);args.Handled=true;return;}
            if(control && args.Key==VirtualKey.Tab)
            {
                var tabs=HostWindow(documentContext.Active) is Desktop.DesktopWindow window?window.Tabs:documentTabs;
                if(tabs.TabItems.Count>0){tabs.SelectedIndex=(tabs.SelectedIndex+(shift?-1:1)+tabs.TabItems.Count)%tabs.TabItems.Count;args.Handled=true;}return;
            }
            if(command is null)return;args.Handled=true;await ExecuteDesktopCommandAsync(command);
        };
    }
    private void InitializeDesktopWindowMenu(Desktop.DesktopWindow window,TabViewItem item)
    {
        var file=new MenuBarItem{Title="Soubor"};var edit=new MenuBarItem{Title="Úpravy"};var view=new MenuBarItem{Title="Okno"};var playback=new MenuBarItem{Title="Přehrávání"};
        void Add(MenuBarItem menu,string label,string command){var entry=new MenuFlyoutItem{Text=label};entry.Click+=async(_,_)=>{if(window.Tabs.SelectedItem is TabViewItem{Tag:DocumentSession session})await ExecuteDesktopCommandAsync(command,session);};menu.Items.Add(entry);}
        Add(file,"Otevřít… · Ctrl+O","file.open");Add(file,"Uložit · Ctrl+S","file.save");Add(file,"Zavřít · Ctrl+W","document.close");
        Add(edit,"Zpět · Ctrl+Z","edit.undo");Add(edit,"Znovu · Ctrl+Y","edit.redo");
        Add(view,"Připojit do pracovní plochy","document.attach");Add(view,"Jen hraju · F11","window.focus");
        var topmost=new ToggleMenuFlyoutItem{Text="Vždy navrchu"};topmost.Click+=(_,_)=>{if(window.AppWindow.Presenter is OverlappedPresenter presenter)presenter.IsAlwaysOnTop=topmost.IsChecked;};view.Items.Add(topmost);
        Add(playback,"Přehrát / pauza · mezerník","transport.toggle");Add(playback,"Zastavit vše","transport.stop");
        window.Menu.Items.Add(file);window.Menu.Items.Add(edit);window.Menu.Items.Add(playback);window.Menu.Items.Add(view);
        window.Root.AddHandler(UIElement.PointerPressedEvent,new PointerEventHandler((_,args)=>TrackPracticeInteraction(args.OriginalSource as DependencyObject)),true);
        window.Root.AddHandler(UIElement.KeyDownEvent,new KeyEventHandler((_,args)=>TrackPracticeInteraction(args.OriginalSource as DependencyObject)),true);
        if(item.Tag is DocumentSession session && Rocker.Domain.DesktopRoutes.ToolViews.Contains(session.Target.ViewId))window.AppWindow.Resize(new(540,700));
    }
    private async Task OpenDesktopFilePickerAsync()
    {
        using var scope=documentContext.Enter(documentContext.Current);
        using var operation=services.Operations.Begin(documentContext.Current.Id,"picker","Výběr souboru",cancellable:false);
        var picker=new Windows.Storage.Pickers.FileOpenPicker();foreach(var ext in new[]{".wav",".mp3",".ogg",".opus",".webm",".m4a",".mp4",".gp3",".gp4",".gp5",".gpx",".gp",".musicxml",".xml",".mxl",".rockerproj"})picker.FileTypeFilter.Add(ext);
        WinRT.Interop.InitializeWithWindow.Initialize(picker,DialogWindowHandle);var file=await picker.PickSingleFileAsync();if(file is not null)await OpenWorkspaceFileAsync(file.Path);
    }
    private async Task ExportDesktopMidiAsync()
    {
        var score=preview;if(score is null)return;var owner=documentContext.Current;
        using var operation=services.Operations.Begin(owner.Id,"picker","Uložení MIDI",cancellable:false);
        var picker=new Windows.Storage.Pickers.FileSavePicker{SuggestedFileName=Path.GetFileNameWithoutExtension(notationName)};picker.FileTypeChoices.Add("MIDI",new[]{".mid"});WinRT.Interop.InitializeWithWindow.Initialize(picker,DialogWindowHandle);
        var file=await picker.PickSaveFileAsync();if(file is not null && owner.IsAlive){await Windows.Storage.FileIO.WriteBytesAsync(file,score.Midi);NotationStatus.Text="MIDI soubor byl uložen: "+file.Name;}
    }
    private async Task UndoDesktopStudioAsync(bool redo)
    {
        using var scope=documentContext.Enter(documentContext.Current);
        if(CurrentTarget.ViewId!="15" || studioProject is null || studioOperation || studioEditTask is{IsCompleted:false})return;
        if(captureDraft is not null && (studioCaptureSession==documentContext.Current || services.Transport.Owner==documentContext.Current))throw new InvalidOperationException("Před změnou projektu zastav a ulož nahrávanou stopu.");
        var source=redo?studioRedo:studioUndo;if(source.Count==0)return;
        using var operation=services.Operations.Begin(documentContext.Current.Id,"project-save",redo?"Opakování editace":"Vrácení editace",cancellable:false);
        var current=studioProject;await InvalidateStudioPlaybackAsync();var restored=source.Peek();
        try{studioProject=restored;await SaveStudioAsync();source.Pop();(redo?studioUndo:studioRedo).Push(current);RefreshStudio();}
        catch{studioProject=current;throw;}
    }
    private async Task ClaimDesktopVideoAsync()
    {
        if(CurrentTarget.ViewId!="08" || !videoReady)return;var selected=documentContext.Current;
        if(videoPractice.State==1){SendVideo(new{op="pause"});return;}
        foreach(var other in services.Documents.All.Where(s=>s.Target.ViewId=="08" && s!=selected))
        {using var scope=documentContext.Enter(other);SendVideo(new{op="pause"});}
        if(audio is not null && audio.GetTransport().Playing!=0)await audio.PauseAsync();
        SendVideo(new{op="play"});
    }
    private async Task ToggleDesktopTransportAsync()
    {
        if(CurrentTarget.ViewId=="08"){await ClaimDesktopVideoAsync();return;}
        if(CurrentTarget.ViewId=="04"){if(metroEnabled)MetroStop_Click(this,new());else MetroStart_Click(this,new());return;}
        if(CurrentTarget.ViewId=="16"){await ToggleTransportAsync();return;}
        if(audio is null)return;
        if(CurrentTarget.ViewId is "07" or "14" or "15" or "20")await ClaimDesktopSourceAsync(documentContext.Current);
        if(audio.GetTransport().Playing!=0)await audio.PauseAsync();else await audio.PlayAsync();
    }
    private async Task StopDesktopAudioAsync()
    {
        await StopDesktopSessionsAsync();++playbackIntent;metroEnabled=false;++metroRevision;metroApplyTimer.Stop();
        if(audio is not null){await audio.StopAsync();await StopCaptureAsync();await ApplyMetronomeAsync();}
        RefreshState();
    }
}
