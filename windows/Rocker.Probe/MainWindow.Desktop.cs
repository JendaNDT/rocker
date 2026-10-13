// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;
using Rocker.AppCore;
using Rocker.Domain;
using Rocker.Infrastructure;
using Rocker.Probe.Desktop;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private readonly Grid desktopRoot = new() { ColumnSpacing=0 };
    private readonly TabView documentTabs = new() { IsAddTabButtonVisible=false, CanDragTabs=true, CanReorderTabs=true };
    private readonly TabView toolTabs = new() { IsAddTabButtonVisible=false, CanDragTabs=true, CanReorderTabs=true };
    private readonly TreeView desktopLibrary = new() { SelectionMode=TreeViewSelectionMode.Single };
    private readonly TextBox librarySearch = new() { PlaceholderText="Hledat v knihovně",MaxLength=128 };
    private readonly TextBlock inspectorTitle = new() { Text="Dokument",FontSize=16 };
    private readonly TextBlock inspectorDetails = new() { TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true };
    private readonly TextBlock desktopSource = new() { Text="Zdroj: žádný",TextTrimming=TextTrimming.CharacterEllipsis };
    private readonly ListView desktopOperations = new(){DisplayMemberPath="Title",MaxHeight=100,SelectionMode=ListViewSelectionMode.Single};
    private readonly Button cancelDesktopOperation = new(){Content="Zrušit vybranou operaci",MinHeight=30};
    private readonly Button desktopCompletePlan = new(){Content="Potvrdit blok plánu",MinHeight=30,Visibility=Visibility.Collapsed};
    private double desktopLibraryWidth=230,desktopInspectorWidth=280;
    private bool desktopLibraryVisible=true,desktopInspectorVisible=true;
    private readonly TextBlock operationStatus = new() { TextWrapping=TextWrapping.Wrap };
    private readonly Dictionary<DocumentSession,TabViewItem> desktopDocuments = [];
    private readonly Dictionary<string,DesktopDocumentKey> desktopFileAliases = [];
    private readonly Dictionary<string,string> desktopProjectAliases = [];
    private readonly Dictionary<string,(DocumentSession Session,TabViewItem Tab)> desktopTools = [];
    private readonly Dictionary<TabViewItem,DesktopWindow> floatingHosts = [];
    
    private readonly List<DesktopWindow> desktopWindows = [];
    private DocumentSession? draggingDocument;
    private TabViewItem? draggingTab;
    private sealed record LibraryEntry(string Title,NavigationTarget Target) { public override string ToString()=>Title; }
    private DialogService? dialogService;
    private Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog) => (dialogService??=new(services.Operations)).ShowAsync(dialog,new(HostWindow(documentContext.Current),HostRoot(documentContext.Current),documentContext.Current));
    private XamlRoot DialogRoot => desktopEnabled ? HostRoot(documentContext.Current).XamlRoot ?? ShellRoot.XamlRoot : ShellRoot.XamlRoot;
    private nint DialogWindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(desktopEnabled ? HostWindow(documentContext.Current) : this);
    private Window HostWindow(DocumentSession session)
    {
        var tab=TabFor(session);
        return tab is not null && floatingHosts.TryGetValue(tab,out var host) ? host : this;
    }
    private FrameworkElement HostRoot(DocumentSession session) => HostWindow(session) is DesktopWindow host ? host.Root : ShellRoot;
    private TabViewItem? TabFor(DocumentSession session) => desktopDocuments.GetValueOrDefault(session) ?? desktopTools.Values.FirstOrDefault(t=>t.Session==session).Tab;
    private void InitializeDesktop()
    {
        if(desktopEnabled)return;
        desktopEnabled=true;Navigation.Visibility=Visibility.Collapsed;
        desktopRoot.ColumnDefinitions.Add(new(){Width=new(230)});
        desktopRoot.ColumnDefinitions.Add(new(){Width=new(5)});
        desktopRoot.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        desktopRoot.ColumnDefinitions.Add(new(){Width=new(5)});
        desktopRoot.ColumnDefinitions.Add(new(){Width=new(280)});
        var library=new Grid{Padding=new Thickness(12),RowSpacing=8};library.RowDefinitions.Add(new(){Height=GridLength.Auto});library.RowDefinitions.Add(new(){Height=GridLength.Auto});library.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        library.Children.Add(new TextBlock{Text="KNIHOVNA",FontSize=12,CharacterSpacing=100});Grid.SetRow(librarySearch,1);library.Children.Add(librarySearch);Grid.SetRow(desktopLibrary,2);library.Children.Add(desktopLibrary);
        librarySearch.TextChanged+=(_,_)=>RefreshDesktopLibrary();desktopLibrary.ItemInvoked+=(_,e)=>{if(e.InvokedItem is TreeViewNode{Content:LibraryEntry entry})Navigate(entry.Target);else if(e.InvokedItem is LibraryEntry item)Navigate(item.Target);};
        desktopRoot.Children.Add(library);Grid.SetColumn(documentTabs,2);desktopRoot.Children.Add(documentTabs);
        var right=new Grid{Padding=new Thickness(12),RowSpacing=10};right.RowDefinitions.Add(new(){Height=GridLength.Auto});right.RowDefinitions.Add(new(){Height=GridLength.Auto});right.RowDefinitions.Add(new(){Height=GridLength.Auto});right.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        right.Children.Add(inspectorTitle);Grid.SetRow(inspectorDetails,1);right.Children.Add(inspectorDetails);var operationPanel=Stack();operationPanel.Spacing=4;operationPanel.Children.Add(desktopCompletePlan);desktopCompletePlan.Click+=CompletePlan_Click;operationPanel.Children.Add(operationStatus);operationPanel.Children.Add(desktopOperations);operationPanel.Children.Add(cancelDesktopOperation);Grid.SetRow(operationPanel,2);right.Children.Add(operationPanel);
        cancelDesktopOperation.Click+=(_,_)=>{if(desktopOperations.SelectedItem is OperationStatus operation)services.Operations.Cancel(operation.Id);};desktopOperations.SelectionChanged+=(_,_)=>cancelDesktopOperation.IsEnabled=desktopOperations.SelectedItem is OperationStatus{CanCancel:true};Grid.SetRow(toolTabs,3);right.Children.Add(toolTabs);Grid.SetColumn(right,4);desktopRoot.Children.Add(right);
        AddDesktopSplitter(1,0);AddDesktopSplitter(3,4);
        Grid.SetRow(desktopRoot,1);ShellRoot.Children.Add(desktopRoot);
        BindDesktopTabs(documentTabs);BindDesktopTabs(toolTabs);
        var windowsMenu=new MenuBarItem{Title="Okno"};
        void Menu(string text,Action action){var item=new MenuFlyoutItem{Text=text};item.Click+=(_,_)=>action();windowsMenu.Items.Add(item);}
        Menu("Odpojit aktivní dokument",()=>DetachDesktopTab(TabFor(documentContext.Active)));
        Menu("Připojit do pracovní plochy",()=>AttachDesktopTab(TabFor(documentContext.Active)));
        Menu("Zavřít aktivní dokument",()=>{if(TabFor(documentContext.Active) is{ } tab)_=CloseDesktopTabAsync(tab);});
        windowsMenu.Items.Add(new MenuFlyoutSeparator());
        foreach(string id in new[]{"02","03","04","13","16"}){var view=WorkspaceViews.Get(id);Menu(view.Title,()=>Navigate(new(id)));}
        windowsMenu.Items.Add(new MenuFlyoutSeparator());
        Menu("Skrýt / zobrazit knihovnu",()=>{desktopLibraryVisible=!desktopLibraryVisible;RefreshDesktopPaneWidths();});
        Menu("Skrýt / zobrazit inspektor a nástroje",()=>{desktopInspectorVisible=!desktopInspectorVisible;RefreshDesktopPaneWidths();});
        Menu("Obnovit výchozí rozložení",()=>{desktopLibraryWidth=230;desktopInspectorWidth=280;desktopLibraryVisible=desktopInspectorVisible=true;RefreshDesktopPaneWidths();foreach(var tab in floatingHosts.Keys.ToArray())AttachDesktopTab(tab);});
        MainMenu.Items.Add(windowsMenu);
        var sourceRow=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};sourceRow.Children.Add(desktopSource);var stopAll=new Button{Content="Zastavit vše",MinHeight=30};stopAll.Click+=Stop_Click;sourceRow.Children.Add(stopAll);MediaTitle.Visibility=Visibility.Collapsed;
        Grid.SetColumn(sourceRow,0);MediaTransportRow.Children.Add(sourceRow);
        services.Operations.Changed+=DesktopOperationsChanged;
        InitializeDesktopCommands();BindDesktopCommands(ShellRoot);RefreshDesktopLibrary();_=RefreshDesktopLibraryAssetsAsync();OpenDesktop(new("01"));
        if(!appSettings.Onboarded)_=ShowDesktopDialogAsync(new("31"));
    }
    private void AddDesktopSplitter(int column,int resized)
    {
        var thumb=new Thumb{Width=5,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch,IsTabStop=true};
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(thumb,resized==0?"Šířka knihovny":"Šířka inspektoru");
        thumb.DragDelta+=(_,e)=>ResizeDesktopPane(resized,e.HorizontalChange*(resized==0?1:-1));
        thumb.KeyDown+=(_,e)=>{if(e.Key is VirtualKey.Left or VirtualKey.Right){ResizeDesktopPane(resized,(e.Key==VirtualKey.Left?-16:16)*(resized==0?1:-1));e.Handled=true;}};
        Grid.SetColumn(thumb,column);desktopRoot.Children.Add(thumb);
    }
    private void ResizeDesktopPane(int index,double delta)
    {
        double value=Math.Clamp((index==0?desktopLibraryWidth:desktopInspectorWidth)+delta,140,Math.Max(140,ShellRoot.ActualWidth*.35));
        if(index==0)desktopLibraryWidth=value;else desktopInspectorWidth=value;RefreshDesktopPaneWidths();
    }
    private void RefreshDesktopPaneWidths()
    {
        if(!desktopEnabled || desktopRoot.ColumnDefinitions.Count!=5)return;
        double width=ShellRoot.ActualWidth;
        desktopRoot.ColumnDefinitions[0].Width=new(desktopLibraryVisible && width>=760?desktopLibraryWidth:0);
        desktopRoot.ColumnDefinitions[4].Width=new(desktopInspectorVisible && width>=1060?desktopInspectorWidth:0);
    }
    private DocumentSession OpenDesktop(NavigationTarget target,DesktopDocumentKey? key=null)
    {
        var route=DesktopRoutes.Resolve(target);
        if(route.Destination==DesktopDestination.Dialog){_=ShowDesktopDialogAsync(target);return documentContext.Active;}
        if(route.Destination==DesktopDestination.Library){using var scope=documentContext.Enter(documentContext.Default);var pane=target.ViewId=="14"?BuildRecordings():BuildLessonList();desktopLibrary.Visibility=Visibility.Collapsed;var container=new ScrollViewer{Content=pane};Grid.SetRow(container,2);var parent=(Grid)desktopLibrary.Parent;foreach(var old in parent.Children.OfType<ScrollViewer>().ToArray())parent.Children.Remove(old);parent.Children.Add(container);return documentContext.Active;}
        if(route.Destination is DesktopDestination.Tool or DesktopDestination.Live or DesktopDestination.Settings)
        {
            string identity=route.Destination==DesktopDestination.Settings?"settings":target.ViewId;
            if(desktopTools.TryGetValue(identity,out var existing)){
                if(!floatingHosts.ContainsKey(existing.Tab) && !toolTabs.TabItems.Contains(existing.Tab))toolTabs.TabItems.Add(existing.Tab);
                if(route.Destination==DesktopDestination.Settings && existing.Session.Target!=target){existing.Session.Target=target;using var settingsScope=documentContext.Enter(existing.Session);existing.Tab.Content=BuildDesktopSettingsContent(existing.Session,target);existing.Tab.Header=WorkspaceViews.Get(target.ViewId).Title;}
                ActivateDesktopTab(existing.Tab);return existing.Session;}
            var session=new DocumentSession(target);using var scope=documentContext.Enter(route.Destination==DesktopDestination.Tool || route.Destination==DesktopDestination.Live ? documentContext.Default : session);
            FrameworkElement page=route.Destination==DesktopDestination.Settings?BuildDesktopSettingsContent(session,target):BuildWorkspace(target);
            var tab=CreateDesktopTab(session,page,false);desktopTools.Add(identity,(session,tab));
            if(route.Destination==DesktopDestination.Tool){toolTabs.TabItems.Add(tab);toolTabs.SelectedItem=tab;}
            else{toolTabs.TabItems.Add(tab);DetachDesktopTab(tab);}
            ActivateDesktopTab(tab);return session;
        }
        if(key is null && target.ViewId=="07" && target.ItemId is{ } songId && songs.TryGetValue(songId,out var savedSong))key=DesktopDocumentKey.ForCanonicalFile("07",RecentFiles.ValidatePath(savedSong.SourcePath));
        key??=DesktopDocumentKey.ForObject(target.ViewId,target.ItemId??"default");
        if(key.Scope==DocumentIdentityScope.CanonicalFile && File.Exists(key.ResourceId))
        {
            var file=CanonicalFiles.ResolveExisting(key.ResourceId);string alias=key.ViewId+":"+file.Identity;
            if(desktopFileAliases.TryGetValue(alias,out var existingKey))key=existingKey;
            else{key=DesktopDocumentKey.ForCanonicalFile(key.ViewId,file.Path);desktopFileAliases[alias]=key;}
        }
        var document=services.Documents.Open(key,target);
        if(!desktopDocuments.TryGetValue(document,out var item))
        {
            using var scope=documentContext.Enter(document);
            ApplyNavigationTask(target);FrameworkElement page=target.ViewId switch{"20"=>scoreDocumentView,"08"=>videoDocumentView,_=>BuildDesktopDocumentPage(target)};
            item=CreateDesktopTab(document,page,true);desktopDocuments.Add(document,item);documentTabs.TabItems.Add(item);
            if(target.ViewId=="08"){if(target.ItemId is{ } videoId && Rocker.Video.VideoProtocol.ExtractId(videoId)==videoId)queuedDesktopVideo=(videoId,null);RefreshSavedVideoLoops();_=EnsureVideoAsync();}
            if(target.ViewId=="14" && target.ItemId is{ } recordingId)_=LoadDesktopRecordingAsync(document,recordingId);
            if(target.ViewId=="15" && target.ItemId is{ } projectId)_=LoadDesktopProjectAsync(document,projectId);
            if(target.ViewId=="07" && key.Scope==DocumentIdentityScope.CanonicalFile)_=OpenDesktopSongAsync(key.ResourceId,songs.Values.FirstOrDefault(s=>string.Equals(s.SourcePath,key.ResourceId,StringComparison.OrdinalIgnoreCase))?.Name??Path.GetFileNameWithoutExtension(key.ResourceId),null);
        }
        ActivateDesktopTab(item);return document;
    }
    private FrameworkElement TakeDevicePanel()
    {
        if(DevicePanel.Parent is Panel parent)parent.Children.Remove(DevicePanel);
        else if(DevicePanel.Parent is ScrollViewer scroll)scroll.Content=null;
        else if(DevicePanel.Parent is Border border)border.Child=null;
        DevicePanel.Visibility=Visibility.Visible;return DevicePanel;
    }
    private TabViewItem CreateDesktopTab(DocumentSession session,FrameworkElement page,bool document)
    {
        var view=WorkspaceViews.Get(session.Target.ViewId);
        var scroll=DesktopDocumentContent(page);
        var item=new TabViewItem{Header=view.Title,Tag=session,Content=scroll,IsClosable=true};
        var menu=new MenuFlyout();var detach=new MenuFlyoutItem{Text="Otevřít v samostatném okně"};detach.Click+=(_,_)=>DetachDesktopTab(item);menu.Items.Add(detach);var attach=new MenuFlyoutItem{Text="Připojit do pracovní plochy"};attach.Click+=(_,_)=>AttachDesktopTab(item);menu.Items.Add(attach);item.ContextFlyout=menu;
        LabelNativeControls(page);return item;
    }
    private void BindDesktopTabs(TabView tabs,DesktopWindow? targetHost=null)
    {
        tabs.SelectionChanged+=(_,_)=>{if(tabs.SelectedItem is TabViewItem{Tag:DocumentSession session} tab){documentContext.Select(session);RefreshDesktopInspector(session);}};
        tabs.TabCloseRequested+=(sender,e)=>{_=CloseDesktopTabAsync(e.Tab);};
        tabs.TabDragStarting+=(_,e)=>{draggingTab=e.Tab;draggingDocument=e.Tab.Tag as DocumentSession;e.Data.SetText("rocker-desktop:"+draggingDocument?.Id);};
        tabs.TabDragCompleted+=(_,_)=>{draggingTab=null;draggingDocument=null;};
        tabs.AllowDrop=true;tabs.TabStripDragOver+=(_,e)=>{if(draggingTab is not null)e.AcceptedOperation=DataPackageOperation.Move;};
        tabs.TabStripDrop+=(_,e)=>
        {
            if(draggingTab is not{Tag:DocumentSession session} tab)return;
            bool document=desktopDocuments.ContainsKey(session);
            if(tabs==toolTabs && document || tabs==documentTabs && !document)return;
            if(targetHost is not null && tabs.TabItems.OfType<TabViewItem>().FirstOrDefault()?.Tag is DocumentSession target && desktopDocuments.ContainsKey(target)!=document)return;
            if(!document && !DesktopRoutes.ToolViews.Contains(session.Target.ViewId))return;
            MoveDesktopTab(tab,tabs,targetHost);
            e.Handled=true;
        };
    }
    private void MoveDesktopTab(TabViewItem tab,TabView tabs,DesktopWindow? targetHost=null)
    {
        floatingHosts.TryGetValue(tab,out var oldHost);RemoveDesktopTabParent(tab);floatingHosts.Remove(tab);
        tabs.TabItems.Add(tab);tabs.SelectedItem=tab;if(targetHost is not null)floatingHosts[tab]=targetHost;
        if(oldHost is not null && oldHost!=targetHost && oldHost.Tabs.TabItems.Count==0){dialogService?.Forget(oldHost.Root.XamlRoot);oldHost.ExitApproved=true;oldHost.Close();desktopWindows.Remove(oldHost);}
    }
    private void RemoveDesktopTabParent(TabViewItem item)
    {
        documentTabs.TabItems.Remove(item);toolTabs.TabItems.Remove(item);
        foreach(var host in desktopWindows)host.Tabs.TabItems.Remove(item);
    }
    private void ActivateDesktopTab(TabViewItem item)
    {
        var session=(DocumentSession)item.Tag;documentContext.Select(session);
        if(floatingHosts.TryGetValue(item,out var window)){window.Tabs.SelectedItem=item;window.AppWindow.Show();window.Activate();}
        else if(documentTabs.TabItems.Contains(item))documentTabs.SelectedItem=item;
        else toolTabs.SelectedItem=item;
        RefreshDesktopInspector(session);
    }
    private void DetachDesktopTab(TabViewItem? item)
    {
        if(item is null)return;if(floatingHosts.TryGetValue(item,out var current)){current.AppWindow.Show();current.Activate();return;}
        RemoveDesktopTabParent(item);var window=new DesktopWindow("Rocker — "+item.Header);desktopWindows.Add(window);floatingHosts[item]=window;window.Tabs.TabItems.Add(item);window.Tabs.SelectedItem=item;BindDesktopTabs(window.Tabs,window);window.Root.RequestedTheme=ShellRoot.RequestedTheme;InitializeDesktopWindowMenu(window,item);BindDesktopCommands(window.Root);
        window.Activated+=(_,_)=>{if(window.Tabs.SelectedItem is TabViewItem{Tag:DocumentSession session})documentContext.Select(session);};
        window.AppWindow.Closing+=(sender,e)=>{if(window.ExitApproved)return;e.Cancel=true;if(window.Tabs.TabItems.OfType<TabViewItem>().Any(t=>t.Tag is DocumentSession session && desktopDocuments.ContainsKey(session)))_=CloseDesktopHostAsync(window);else window.AppWindow.Hide();};
        window.Closed+=(_,_)=>
        {
            dialogService?.Forget(window.Root.XamlRoot);
            foreach(var hosted in window.Tabs.TabItems.OfType<TabViewItem>().ToArray())
            {
                window.Tabs.TabItems.Remove(hosted);floatingHosts.Remove(hosted);
                if(!window.ExitApproved && hosted.Tag is DocumentSession live && desktopDocuments.ContainsKey(live))documentTabs.TabItems.Add(hosted);
            }
            desktopWindows.Remove(window);
        };
        window.Activate();
    }
    private void AttachDesktopTab(TabViewItem? item)
    {
        if(item is null)return;if(item.Tag is DocumentSession moving && moving.Target.ViewId is "02" or "26" or "28" or "29" or "30")return;RemoveDesktopTabParent(item);
        if(floatingHosts.Remove(item,out var host) && host.Tabs.TabItems.Count==0){dialogService?.Forget(host.Root.XamlRoot);host.ExitApproved=true;host.Close();desktopWindows.Remove(host);}
        var session=(DocumentSession)item.Tag;var target=desktopDocuments.ContainsKey(session)?documentTabs:toolTabs;target.TabItems.Add(item);target.SelectedItem=item;Activate();
    }
    private async Task CloseDesktopHostAsync(DesktopWindow window)
    {
        foreach(var tab in window.Tabs.TabItems.OfType<TabViewItem>().ToArray())
        {await CloseDesktopTabAsync(tab);if(tab.Tag is DocumentSession session && session.IsAlive && desktopDocuments.ContainsKey(session))return;}
    }
    private async Task CloseDesktopTabAsync(TabViewItem item)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        var session=(DocumentSession)item.Tag;
        if(!desktopDocuments.ContainsKey(session)){if(floatingHosts.TryGetValue(item,out var tool))tool.AppWindow.Hide();else toolTabs.TabItems.Remove(item);return;}
        if(services.Operations.Snapshot().Any(o=>o.Owner==session.Id && o.Kind!="ai-request")){ShowPersistenceError("Počkej na dokončení operace dokumentu nebo ji zruš.");return;}
        using var scope=documentContext.Enter(session);
        if(studioCaptureSession==session && captureDraft is not null){ShowPersistenceError("Nejdřív zastav a ulož stopu nahrávanou do tohoto projektu.");return;}
        if(studioEditTask is{IsCompleted:false})await studioEditTask;
        if(aiBusy){var dialog=new ContentDialog{XamlRoot=DialogRoot,Title="Dotaz AI ještě běží",Content="Zavření dokumentu zruší probíhající dotaz.",PrimaryButtonText="Zrušit dotaz a zavřít",CloseButtonText="Zůstat"};if(await ShowDialogAsync(dialog)!=ContentDialogResult.Primary)return;await CancelAiAsync();}
        if(!await services.Exit.ResolveAsync(async guard=>
        {
            var dialog=new ContentDialog{XamlRoot=DialogRoot,Title="Zavřít dokument?",Content=guard.State.Prompt,PrimaryButtonText="Uložit",SecondaryButtonText=guard.State.Recording?"":"Zahodit",CloseButtonText="Zrušit"};
            return await ShowDialogAsync(dialog) switch{ContentDialogResult.Primary=>CloseChoice.Save,ContentDialogResult.Secondary=>CloseChoice.Discard,_=>CloseChoice.Cancel};
        },session.Id))return;
        if(services.Transport.Owner==session){if(captureDraft is not null){ShowPersistenceError("Nejdřív zastav a ulož nahrávání.");return;}if(audio is not null)await audio.PauseAsync();services.Transport.Release(session);}
        if(session.Target.ViewId=="08"){SendVideo(new{op="stop"});VideoView.Close();}
        RemoveDesktopTabParent(item);desktopDocuments.Remove(session);services.Documents.Close(session);services.Transport.Release(session);
        if(floatingHosts.Remove(item,out var host) && host.Tabs.TabItems.Count==0){dialogService?.Forget(host.Root.XamlRoot);host.ExitApproved=true;host.Close();desktopWindows.Remove(host);}
        documentContext.Select(documentTabs.SelectedItem is TabViewItem{Tag:DocumentSession selected}?selected:documentContext.Default);RefreshDesktopInspector(documentContext.Active);
    }
    private async Task ShowDesktopDialogAsync(NavigationTarget target)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        try{var dialog=new ContentDialog{XamlRoot=DialogRoot,Title=WorkspaceViews.Get(target.ViewId).Title,Content=new ScrollViewer{Content=BuildWorkspace(target),MaxHeight=520},CloseButtonText="Zavřít"};await ShowDialogAsync(dialog);}catch(InvalidOperationException){ShowPersistenceError("Dokonči nejdřív otevřený dialog.");}
    }
    private void RefreshDesktopInspector(DocumentSession session)
    {
        using var scope=documentContext.Enter(session);
        string title=TabFor(session)?.Header as string??WorkspaceViews.Get(session.Target.ViewId).Title;
        string details=session.Target.ViewId switch
        {
            "15" when studioProject is{ } project=>$"{project.Name}\n{project.Tracks.Count} stop · {project.SampleRate:N0} Hz",
            "07" when song is{ } track=>$"{track.Name}\n{FormatTime(track.Duration)} · {track.Speed:0.##}×\n{track.SourcePath}",
            "14" when playingRecording is{ } recording=>$"{recording.Name}\n{FormatTime(recording.DurationSec)} · {recording.Markers.Count} značek",
            "20" when notationDocument is{ } score=>$"{notationName}\n{score.Tracks.Count} stop · {score.BarCount} taktů",
            _=>WorkspaceDescription(session.Target)
        };
        if(services.Transport.Owner==session)details+="\nZdroj společného přehrávače";
        RefreshPlanControls();
        if(inspectorTitle.Text!=title)inspectorTitle.Text=title;if(inspectorDetails.Text!=details)inspectorDetails.Text=details;
        string source=services.Transport.Owner is{ } owner?TabFor(owner)?.Header as string??WorkspaceViews.Get(owner.Target.ViewId).Title:"žádný";
        if(desktopSource.Text!="Zdroj: "+source)desktopSource.Text="Zdroj: "+source;
        desktopSource.MaxWidth=Math.Max(100,ShellRoot.ActualWidth-580);
        if(HostWindow(session) is DesktopWindow window){window.Status.Text=source=="žádný"?"Přehrávání není připravené":"Zdroj: "+source;window.Title="Rocker — "+title;}
    }
    private void DesktopOperationsChanged(object? sender,EventArgs args)=>DispatcherQueue.TryEnqueue(()=>
    {
        if(closed)return;var operations=services.Operations.Snapshot();var selected=(desktopOperations.SelectedItem as OperationStatus)?.Id;
        desktopOperations.ItemsSource=operations;desktopOperations.SelectedItem=operations.FirstOrDefault(o=>o.Id==selected)??operations.FirstOrDefault();
        desktopOperations.Visibility=cancelDesktopOperation.Visibility=operations.Count>0?Visibility.Visible:Visibility.Collapsed;
        operationStatus.Text=operations.Count==0?"":$"Probíhá {operations.Count} operací";
        cancelDesktopOperation.IsEnabled=desktopOperations.SelectedItem is OperationStatus{CanCancel:true};
    });
}
