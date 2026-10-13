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
        if(!desktopEnabled || desktopRestoring)return;
        var windows=new List<DesktopWindowLayout>();
        windows.Add(new("workspace",DesktopWindowRole.Workspace,new(normalBounds.X,normalBounds.Y,normalBounds.Width,normalBounds.Height),documentTabs.TabItems.OfType<TabViewItem>().Select(t=>t.Tag).OfType<Rocker.AppCore.DocumentSession>().Select(s=>s.Key!).Where(k=>k is not null).ToArray(),Maximized:AppWindow.Presenter is OverlappedPresenter{State:OverlappedPresenterState.Maximized}));
        foreach(var pair in floatingHosts)
        {
            var session=(Rocker.AppCore.DocumentSession)pair.Key.Tag;var window=pair.Value;
            var role=desktopDocuments.ContainsKey(session)?DesktopWindowRole.Document:session.Target.ViewId=="02"?DesktopWindowRole.Live:DesktopRoutes.ToolViews.Contains(session.Target.ViewId)?DesktopWindowRole.Tool:DesktopWindowRole.Settings;
            windows.Add(new(window.Id,role,DesktopBounds(window),session.Key is null?[]:[session.Key],role==DesktopWindowRole.Tool?session.Target.ViewId:null,window.AppWindow.Presenter is OverlappedPresenter{State:OverlappedPresenterState.Maximized},window.AppWindow.Presenter is OverlappedPresenter{IsAlwaysOnTop:true},window.AppWindow.IsVisible));
        }
        var attached=desktopTools.Values.Where(t=>DesktopRoutes.ToolViews.Contains(t.Session.Target.ViewId) && toolTabs.TabItems.Contains(t.Tab)).Select(t=>t.Session.Target.ViewId).ToArray();
        var layout=new DesktopWorkspaceLayout(1,windows,Math.Max(140,desktopRoot.ColumnDefinitions[0].Width.Value),Math.Max(140,desktopRoot.ColumnDefinitions[4].Width.Value),desktopRoot.ColumnDefinitions[0].Width.Value!=0,desktopRoot.ColumnDefinitions[4].Width.Value!=0,attached);
        await new WorkspaceLayoutStore(userFiles).SaveAsync(layout);
    }
    private async Task RestoreDesktopLayoutAsync()
    {
        if(!desktopEnabled || desktopRestoring)return;desktopRestoring=true;
        try
        {
            var layout=new WorkspaceLayoutStore(userFiles).Load();if(layout is null)return;
            desktopRoot.ColumnDefinitions[0].Width=new(layout.LibraryVisible?layout.LibraryWidthDip:0);desktopRoot.ColumnDefinitions[4].Width=new(layout.InspectorVisible?layout.InspectorWidthDip:0);
            foreach(var window in layout.Windows)
            {
                if(window.Role==DesktopWindowRole.Workspace)FitDesktopWindow(this,window.Bounds,window.Maximized);
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
                        if(window.Role==DesktopWindowRole.Document && TabFor(session) is{ } tab){DetachDesktopTab(tab);FitDesktopWindow(floatingHosts[tab],window.Bounds,window.Maximized,window.AlwaysOnTop);}
                    }
                    catch(Exception error){ShowPersistenceError("Dokument z rozložení nelze obnovit: "+error.Message);}
                }
                if(window.Role is DesktopWindowRole.Tool or DesktopWindowRole.Live or DesktopWindowRole.Settings)
                {
                    string id=window.ToolViewId??(window.Role==DesktopWindowRole.Live?"02":"28");var session=OpenDesktop(new(id));var tab=TabFor(session)!;DetachDesktopTab(tab);var host=floatingHosts[tab];FitDesktopWindow(host,window.Bounds,window.Maximized,window.AlwaysOnTop);if(!window.Visible)host.AppWindow.Hide();
                }
            }
            foreach(string tool in layout.AttachedTools??[])OpenDesktop(new(tool));
            if(documentTabs.TabItems.OfType<TabViewItem>().FirstOrDefault() is{ } first)ActivateDesktopTab(first);
        }
        catch(Exception error){ShowPersistenceError("Rozložení nelze obnovit; používá se výchozí pracovní plocha. Hudební data zůstala zachovaná. "+error.Message);}
        finally{desktopRestoring=false;}
    }
}
