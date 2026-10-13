// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Rocker.Infrastructure;
using Rocker.Domain;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private readonly Queue<string> activationRequests=[];
    [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool SetForegroundWindow(nint window);
    internal void QueueFileActivation(string request){if(closed)return;Activate();SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));if(request.Length>0 && activationRequests.Count<8)activationRequests.Enqueue(request);}
    private async void ProcessFileActivation(){
        using var invocationScope=documentContext.Enter(documentContext.Current);
if(!persistenceReady || workspaceOperation || fileOperation || busy || studioOperation || backupOperationRunning || profileOperation || activationRequests.Count==0 || captureDraft is not null)return;string request=activationRequests.Dequeue();try{string path=RecentFiles.ValidatePath(request);await OpenWorkspaceFileAsync(path);string? report=Environment.GetEnvironmentVariable("ROCKER_ACTIVATION_REPORT");if(!string.IsNullOrEmpty(report)){bool opened=recentFiles.Any(f=>string.Equals(f.Path,path,StringComparison.OrdinalIgnoreCase));await File.WriteAllTextAsync(report,System.Text.Json.JsonSerializer.Serialize(new{result=opened?"passed":"failed",kind=RecentFiles.Kind(path).ToString(),view=CurrentTarget.ViewId,canonicalPath=path,projectId=CurrentTarget.ViewId=="15"?studioProject?.Id:null,projectName=CurrentTarget.ViewId=="15"?studioProject?.Name:null}));}}catch(Exception){ShowPersistenceError("Soubor předaný z Průzkumníka nelze otevřít. Otevři jej znovu přes nabídku Soubor.");}}
    private readonly List<RecentFile> recentFiles=[];private Task recentSaveTask=Task.CompletedTask;private bool fileOperation;private CancellationTokenSource? fileCancellation;
    private void InitializeFileCommands()
    {
        recentFiles.AddRange(new RecentFiles(userFiles).Load());RefreshRecentMenu();ShellRoot.AllowDrop=true;
        ShellRoot.DragOver+=(_,args)=>{if(!busy && !fileOperation && args.DataView.Contains(StandardDataFormats.StorageItems)){args.AcceptedOperation=DataPackageOperation.Copy;args.DragUIOverride.Caption="Otevřít soubor v Rockeru";}};
        ShellRoot.Drop+=async(_,args)=>{args.Handled=true;try{if(args.DataView.Contains(StandardDataFormats.StorageItems)){var items=await args.DataView.GetStorageItemsAsync();if(items.Count!=1 || items[0] is not StorageFile file){ShowPersistenceError("Přetáhni jeden zvukový, notový nebo projektový soubor.");return;}await OpenWorkspaceFileAsync(file.Path);}}catch(Exception){ShowPersistenceError("Přetažený soubor nelze otevřít. Původní práce je zachovaná.");}};
        ShellRoot.KeyDown+=async(_,args)=>{if(desktopEnabled)return;if(args.Handled || TextOrInteractiveFocus(args.OriginalSource as DependencyObject))return;if(args.Key==VirtualKey.Z && Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)){if(CurrentTarget.ViewId=="15" && studioUndo.Count>0){args.Handled=true;UndoCurrent_Click(this,new());}return;}if(args.Key==VirtualKey.Space){args.Handled=true;await ToggleTransportAsync();}};
        var flyout=new MenuFlyout();var play=new MenuFlyoutItem{Text="Přehrát / pauza"};play.Click+=async(_,_)=>await ToggleTransportAsync();flyout.Items.Add(play);var stop=new MenuFlyoutItem{Text="Zastavit zvuk"};stop.Click+=Stop_Click;flyout.Items.Add(stop);var folder=new MenuFlyoutItem{Text="Zobrazit aktuální soubor ve složce"};folder.Click+=async(_,_)=>await RevealCurrentFileAsync();flyout.Items.Add(folder);var copy=new MenuFlyoutItem{Text="Kopírovat cestu k souboru"};copy.Click+=(_,_)=>{var path=CurrentFilePath();if(path is null)return;var data=new DataPackage();data.SetText(path);Clipboard.SetContent(data);};flyout.Items.Add(copy);MediaPanel.ContextFlyout=flyout;
    }
    private static bool TextOrInteractiveFocus(DependencyObject? source)
    {for(var item=source;item is not null;item=VisualTreeHelper.GetParent(item))if(item is TextBox or PasswordBox or RichEditBox or NumberBox or ComboBox or ButtonBase or Slider or ToggleSwitch or ListViewBase)return true;return false;}
    private async Task ToggleTransportAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(busy || deviceOperationRunning || fileOperation || closingWindow)return;try{if(CurrentTarget.ViewId=="08"){SendVideo(new{op=videoPractice.State==1?"pause":"play"});return;}if(audio is null)return;if(CurrentTarget.ViewId=="16"){var loop=audio.GetLoop();if(loop.State is 3 or 4 or 5)await audio.LoopCommandAsync(4);else if(loop.State==6 && loop.Frames>0)await audio.LoopCommandAsync(5);return;}var transport=audio.GetTransport();if(transport.Playing!=0)await audio.PauseAsync();else if(transport.Ready!=0)await audio.PlayAsync();RefreshState();}catch(Exception){ShowPersistenceError("Přehrávání se nepodařilo změnit. Zkontroluj zvukové zařízení.");}
    }
    private async void OpenFile_Click(object sender,RoutedEventArgs args)
    {
        if(desktopEnabled){await ExecuteDesktopCommandAsync("file.open");return;}
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(fileOperation || busy)return;try{var picker=new FileOpenPicker();foreach(var ext in new[]{".wav",".mp3",".ogg",".opus",".webm",".m4a",".mp4",".gp3",".gp4",".gp5",".gpx",".gp",".musicxml",".xml",".mxl",".rockerproj"})picker.FileTypeFilter.Add(ext);WinRT.Interop.InitializeWithWindow.Initialize(picker,DialogWindowHandle);var file=await picker.PickSingleFileAsync();if(file is not null)await OpenWorkspaceFileAsync(file.Path);}catch(Exception){ShowPersistenceError("Soubor se nepodařilo otevřít.");}
    }
    internal async Task OpenWorkspaceFileAsync(string path)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(!persistenceReady || fileOperation || busy || studioOperation || studioEditTask is{IsCompleted:false} || captureDraft is not null || backupOperationRunning || profileOperation){ShowPersistenceError("Počkej na dokončení práce nebo zastav nahrávání před otevřením souboru.");return;}path=RecentFiles.ValidatePath(path);if(!File.Exists(path))throw new FileNotFoundException("Soubor není dostupný. Vyber jeho nové umístění.");fileOperation=true;fileCancellation=new();CancelFileMenu.IsEnabled=true;
        try{switch(RecentFiles.Kind(path)){
            case WorkspaceFileKind.Audio:if(!desktopEnabled)Navigate(new("07"));await OpenSongFileAsync(path,Path.GetFileNameWithoutExtension(path));break;
            case WorkspaceFileKind.Score:if(desktopEnabled){var session=OpenDesktop(new("20"),DesktopDocumentKey.ForCanonicalFile("20",path));using var scope=documentContext.Enter(session);if(notationSourcePath!=path)await LoadScoreAsync(path);}else{Navigate(new("20"));await LoadScoreAsync(path);}break;
            case WorkspaceFileKind.Project:
                if(closeGuards.TryGetValue("studio",out var guard) && guard.State.RequiresConfirmation && !await guard.Save())return;
                var project=await new ProjectRepository(userFiles,database!).ImportAsync(path,fileCancellation.Token);fileCancellation.Token.ThrowIfCancellationRequested();await InvalidateStudioPlaybackAsync();studioProject=project;studioUndo.Clear();Navigate(new("15"));await ReloadStudioListsAsync();RefreshStudio();RememberFile(path);break;
        }}finally{fileOperation=false;fileCancellation.Dispose();fileCancellation=null;CancelFileMenu.IsEnabled=false;}
    }
    private void RememberFile(string path)
    {
        try{path=RecentFiles.ValidatePath(path);recentFiles.RemoveAll(v=>string.Equals(v.Path,path,StringComparison.OrdinalIgnoreCase));recentFiles.Insert(0,new(path,DateTimeOffset.UtcNow));if(recentFiles.Count>24)recentFiles.RemoveRange(24,recentFiles.Count-24);var owned=recentFiles.ToArray();recentSaveTask=SaveRecentAfterAsync(recentSaveTask,owned);RefreshRecentMenu();}catch(Exception){ShowPersistenceError("Seznam posledních souborů se nepodařilo uložit.");}
    }
    private async Task SaveRecentAfterAsync(Task previous,RecentFile[] values){
        using var invocationScope=documentContext.Enter(documentContext.Current);
try{await previous;await new RecentFiles(userFiles).SaveAsync(values);}catch(Exception){if(!closed)ShowPersistenceError("Poslední soubory se neuložily; samotná práce zůstává dostupná.");}}
    private void RefreshRecentMenu()
    {
        RecentFilesMenu.Items.Clear();if(recentFiles.Count==0){RecentFilesMenu.Items.Add(new MenuFlyoutItem{Text="Žádný soubor zatím nebyl otevřený",IsEnabled=false});return;}foreach(var file in recentFiles){var entry=new MenuFlyoutItem{Text=file.Label};ToolTipService.SetToolTip(entry,file.Path);entry.Click+=async(_,_)=>{try{await OpenWorkspaceFileAsync(file.Path);}catch(Exception){ShowPersistenceError("Poslední soubor není dostupný. Otevři jej znovu přes Soubor → Otevřít.");}};RecentFilesMenu.Items.Add(entry);}var clear=new MenuFlyoutItem{Text="Vymazat pouze seznam posledních souborů"};clear.Click+=(_,_)=>{recentFiles.Clear();recentSaveTask=SaveRecentAfterAsync(recentSaveTask,[]);RefreshRecentMenu();};RecentFilesMenu.Items.Add(new MenuFlyoutSeparator());RecentFilesMenu.Items.Add(clear);
    }
    private string? CurrentFilePath()=>CurrentTarget.ViewId=="20"?notationSourcePath:CurrentTarget.ViewId=="15" && studioProject is { } project?System.IO.Path.Combine(new ProjectRepository(userFiles,database!).DirectoryFor(project.Id),"project.rockerproj"):mediaPath;
    private async Task RevealCurrentFileAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        try{var path=CurrentFilePath();if(path is null || !File.Exists(path)){ShowPersistenceError("Aktuální pohled nemá dostupný soubor na disku.");return;}var folder=await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(path)!);var options=new FolderLauncherOptions();options.ItemsToSelect.Add(await StorageFile.GetFileFromPathAsync(path));await Launcher.LaunchFolderAsync(folder,options);}catch(Exception){ShowPersistenceError("Umístění souboru nelze otevřít.");}
    }
    private async void RevealFile_Click(object sender,RoutedEventArgs args)=>await RevealCurrentFileAsync();
    private void CancelFile_Click(object sender,RoutedEventArgs args){fileCancellation?.Cancel();songLoadCancelled=true;studioLoadCancelled=true;audio?.CancelMediaPreparation();}
    private async Task VerifyFileCommandsAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        var previous=recentFiles.ToArray();var path=System.IO.Path.Combine(AppContext.BaseDirectory,"fixtures","notation-phrases.gp");RememberFile(path);RememberFile(path);await recentSaveTask;var loaded=new RecentFiles(userFiles).Load();bool recent=loaded.Count(v=>string.Equals(v.Path,path,StringComparison.OrdinalIgnoreCase))==1 && RecentFilesMenu.Items.Count>0;bool context=MediaPanel.ContextFlyout is MenuFlyout {Items.Count:4};bool routes=RecentFiles.Kind(path)==WorkspaceFileKind.Score && RecentFiles.Kind("sample.rockerproj")==WorkspaceFileKind.Project;recentFiles.Clear();recentFiles.AddRange(previous);recentSaveTask=SaveRecentAfterAsync(recentSaveTask,previous);await recentSaveTask;RefreshRecentMenu();WriteShellDiagnostic("file-commands.json",new{result=recent && context && routes?"passed":"failed",recentFileDeduplicated=recent,nativeContextMenu=context,typedRoutes=routes,manualPickersVerified=false,explorerAssociationVerified=false});if(!recent || !context || !routes)throw new InvalidOperationException("Native file commands failed.");
    }
    private async void SaveCurrent_Click(object sender,RoutedEventArgs args)
    {
        if(desktopEnabled){await ExecuteDesktopCommandAsync("file.save");return;}
        using var invocationScope=documentContext.Enter(documentContext.Current);
try{if(CurrentTarget.ViewId=="15" && !studioOperation)await SaveStudioAsync();else if(CurrentTarget.ViewId=="16" && !looperOperation)await SaveLooperSessionAsync();else if(CurrentTarget.ViewId=="20")ExportMidi_Click(sender,args);else if(autosave is not null)await autosave.FlushAsync();}catch(Exception){ShowPersistenceError("Práci se nepodařilo uložit. Zkus jiné umístění nebo vytvoř zálohu.");}}
    private async void UndoCurrent_Click(object sender,RoutedEventArgs args)
    {
        if(desktopEnabled){await ExecuteDesktopCommandAsync("edit.undo");return;}
        using var invocationScope=documentContext.Enter(documentContext.Current);
if(CurrentTarget.ViewId!="15" || studioOperation || studioEditTask is{IsCompleted:false} || studioUndo.Count==0)return;try{await InvalidateStudioPlaybackAsync();studioProject=studioUndo.Pop();await SaveStudioAsync();RefreshStudio();}catch(Exception){ShowPersistenceError("Vrácení editace se nepodařilo uložit.");}}
}
