// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Rocker.Domain;
using Rocker.Infrastructure;
using Windows.Graphics;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private bool desktopRestoring;
    private static DesktopPixelBounds DesktopBounds(Window window)
    {
        var position=window.AppWindow.Position;var size=window.AppWindow.Size;return new(position.X,position.Y,size.Width,size.Height);
    }
    private void FitDesktopWindow(Window window,DesktopPixelBounds bounds,bool maximized=false,bool topmost=false)
    {
        var area=DisplayArea.GetFromRect(new(bounds.X,bounds.Y,bounds.Width,bounds.Height),DisplayAreaFallback.Nearest).WorkArea;
        var fitted=new WindowRectangle(bounds.X,bounds.Y,bounds.Width,bounds.Height).FitTo(new(area.X,area.Y,area.Width,area.Height));
        window.AppWindow.MoveAndResize(new(fitted.X,fitted.Y,fitted.Width,fitted.Height));
        if(window.AppWindow.Presenter is OverlappedPresenter presenter){presenter.IsAlwaysOnTop=topmost;if(maximized)presenter.Maximize();}
    }
    private async Task SaveDesktopLayoutAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(!desktopEnabled || desktopRestoring)return;
        var windows=new List<DesktopWindowLayout>();
        windows.Add(new("workspace",DesktopWindowRole.Workspace,new(normalBounds.X,normalBounds.Y,normalBounds.Width,normalBounds.Height),documentTabs.TabItems.OfType<TabViewItem>().Select(t=>t.Tag).OfType<Rocker.AppCore.DocumentSession>().Select(s=>s.Key!).Where(k=>k is not null).ToArray(),Maximized:AppWindow.Presenter is OverlappedPresenter{State:OverlappedPresenterState.Maximized},SelectedDocumentIndex:Math.Max(0,documentTabs.SelectedIndex)));
        foreach(var window in desktopWindows.Where(w=>floatingHosts.ContainsValue(w)))
        {
            var sessions=window.Tabs.TabItems.OfType<TabViewItem>().Select(t=>t.Tag).OfType<Rocker.AppCore.DocumentSession>().ToArray();if(sessions.Length==0)continue;
            var session=sessions[0];var role=desktopDocuments.ContainsKey(session)?DesktopWindowRole.Document:session.Target.ViewId=="02"?DesktopWindowRole.Live:DesktopRoutes.ToolViews.Contains(session.Target.ViewId)?DesktopWindowRole.Tool:DesktopWindowRole.Settings;
            windows.Add(new(window.Id,role,DesktopBounds(window),sessions.Where(s=>s.Key is not null).Select(s=>s.Key!).ToArray(),role==DesktopWindowRole.Tool?session.Target.ViewId:null,window.AppWindow.Presenter is OverlappedPresenter{State:OverlappedPresenterState.Maximized},window.AppWindow.Presenter is OverlappedPresenter{IsAlwaysOnTop:true},window.AppWindow.IsVisible,role==DesktopWindowRole.Document?Math.Max(0,window.Tabs.SelectedIndex):0));
        }
        var attached=desktopTools.Values.Where(t=>DesktopRoutes.ToolViews.Contains(t.Session.Target.ViewId) && toolTabs.TabItems.Contains(t.Tab)).Select(t=>t.Session.Target.ViewId).ToArray();
        var preferences=new List<DesktopViewPreferences>();
        foreach(var session in services.Documents.All.Where(s=>s.Key is not null))
        {
            using var scope=documentContext.Enter(session);
            if(session.Target.ViewId=="15")preferences.Add(new(session.Key!,studioZoom));
            if(session.Target.ViewId=="20" && notationDocument is not null)preferences.Add(new(session.Key!,notationOptions.Scale,notationOptions.Track,(int)NotationBar.Value,(int)NotationLoopFirst.Value,Math.Max((int)NotationLoopFirst.Value,(int)NotationLoopLast.Value),NotationAllTracks.IsChecked==true,NotationFollow.IsChecked==true));
        }
        var layout=new DesktopWorkspaceLayout(1,windows,desktopLibraryWidth,desktopInspectorWidth,desktopLibraryVisible,desktopInspectorVisible,attached,preferences);
        await new WorkspaceLayoutStore(userFiles).SaveAsync(layout);
    }
    private async Task RestoreDesktopLayoutAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(!desktopEnabled || desktopRestoring)return;desktopRestoring=true;
        try
        {
            var layout=new WorkspaceLayoutStore(userFiles).Load();if(layout is null)return;
            desktopLibraryWidth=layout.LibraryWidthDip;desktopInspectorWidth=layout.InspectorWidthDip;desktopLibraryVisible=layout.LibraryVisible;desktopInspectorVisible=layout.InspectorVisible;RefreshDesktopPaneWidths();
            foreach(var window in layout.Windows)
            {
                if(window.Role==DesktopWindowRole.Workspace)FitDesktopWindow(this,window.Bounds,window.Maximized);
                Desktop.DesktopWindow? documentHost=null;
                var restoredTabs=new List<TabViewItem>();
                foreach(var key in window.Documents)
                {
                    try
                    {
                        var session=OpenDesktop(new(key.ViewId,key.Scope==DocumentIdentityScope.Object && key.ResourceId!="default"?key.ResourceId:null),key);
                        using var scope=documentContext.Enter(session);
                        if(key.Scope==DocumentIdentityScope.CanonicalFile && File.Exists(key.ResourceId))
                        {
                            if(key.ViewId=="20")await LoadScoreAsync(key.ResourceId);
                            else if(key.ViewId=="07")await OpenDesktopSongAsync(key.ResourceId,Path.GetFileNameWithoutExtension(key.ResourceId),null);
                        }
                        else if(key.ViewId=="15" && key.ResourceId!="default"){var result=await services.Storage!.Projects.LoadAsync(key.ResourceId);studioProject=result.Project;RebuildDesktopDocument(session);}
                        if(layout.ViewPreferences?.FirstOrDefault(v=>v.Key.Equals(key)) is{ } preference)
                        {
                            if(key.ViewId=="15"){studioZoom=preference.Zoom;RebuildDesktopDocument(session);}
                            if(key.ViewId=="20" && notationDocument is{ } score)
                            {
                                await ChangeNotationViewAsync(notationOptions with{Scale=Math.Clamp(preference.Zoom,.5,2),Track=Math.Min(preference.Track,score.Tracks.Count-1),AllTracksAudio=preference.AllTracks},false);
                                notationUpdating=true;
                                try{NotationBar.Value=Math.Min(preference.Bar,score.BarCount);NotationLoopFirst.Value=Math.Min(preference.LoopFirst,score.BarCount);NotationLoopLast.Value=Math.Min(preference.LoopLast,score.BarCount);NotationFollow.IsChecked=preference.Follow;UpdateNotationOccurrences();MarkNotationBar((int)NotationBar.Value);}
                                finally{notationUpdating=false;}
                            }
                        }
                        if(TabFor(session) is{ } tab)
                        {
                            restoredTabs.Add(tab);
                            if(window.Role==DesktopWindowRole.Document)
                            {
                                if(documentHost is null){DetachDesktopTab(tab);documentHost=floatingHosts[tab];FitDesktopWindow(documentHost,window.Bounds,window.Maximized,window.AlwaysOnTop);}
                                else MoveDesktopTab(tab,documentHost.Tabs,documentHost);
                            }
                        }
                    }
                    catch(Exception error){ShowPersistenceError("Dokument z rozložení nelze obnovit: "+error.Message);}
                }
                if(documentHost is not null && restoredTabs.Count>0){documentHost.Tabs.SelectedItem=restoredTabs[Math.Min(window.SelectedDocumentIndex,restoredTabs.Count-1)];if(!window.Visible)documentHost.AppWindow.Hide();}
                if(window.Role is DesktopWindowRole.Tool or DesktopWindowRole.Live or DesktopWindowRole.Settings)
                {
                    string id=window.ToolViewId??(window.Role==DesktopWindowRole.Live?"02":"28");var session=OpenDesktop(new(id));var tab=TabFor(session)!;DetachDesktopTab(tab);var host=floatingHosts[tab];FitDesktopWindow(host,window.Bounds,window.Maximized,window.AlwaysOnTop);if(!window.Visible)host.AppWindow.Hide();
                }
            }
            foreach(string tool in layout.AttachedTools??[])OpenDesktop(new(tool));
            var main=layout.Windows.Single(w=>w.Role==DesktopWindowRole.Workspace);var selectedKey=main.Documents.Count>0?main.Documents[main.SelectedDocumentIndex]:null;
            var selected=desktopDocuments.FirstOrDefault(p=>p.Key.Key?.Equals(selectedKey)==true).Value??documentTabs.TabItems.OfType<TabViewItem>().FirstOrDefault();if(selected is not null)ActivateDesktopTab(selected);
        }
        catch(Exception error){ShowPersistenceError("Rozložení nelze obnovit; používá se výchozí pracovní plocha. Hudební data zůstala zachovaná. "+error.Message);}
        finally{desktopRestoring=false;}
    }
}
