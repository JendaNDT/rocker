// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private readonly Dictionary<string,List<LibraryEntry>> desktopLibraryAssets=[];
    private void RefreshDesktopLibrary()
    {
        if(!desktopEnabled)return;
        if(desktopLibraryPage is not null)return;
        var expanded=desktopLibrary.RootNodes.Where(n=>n.IsExpanded).Select(n=>n.Content as string).ToHashSet();
        desktopLibrary.Visibility=Visibility.Visible;
        if(desktopLibrary.Parent is Grid parent)foreach(var old in parent.Children.OfType<FrameworkElement>().Where(c=>c.Tag as string=="desktop-library").ToArray())parent.Children.Remove(old);
        desktopLibrary.RootNodes.Clear();string query=PracticeWorkspaceRules.SearchText(librarySearch.Text.Trim());
        var groups=WorkspaceViews.All.Where(v=>v.Id is not("31" or "32")).GroupBy(v=>v.Group).ToDictionary(g=>g.Key,g=>g.Select(v=>new LibraryEntry(v.Title,new(v.Id))).ToList());
        if(catalog is not null)
        {
            groups["Lekce"]=catalog.Lessons.Select(l=>new LibraryEntry(l.Title,new("06",l.Id))).ToList();
            foreach(var (name,view,title) in new[]{("surgicalExercises","09","Přesný trénink"),("powerChordExercises","10","Power chordy"),("gallopExercises","11","Gallop"),("palmMuteDrills","12","Palm mute"),("chords","17","Akordy"),("scales","18","Stupnice"),("riffs","19","Riffy")})
                groups[title]=catalog.Items(name).Select(item=>{string id=item.GetProperty("id").GetString()!;var target=new NavigationTarget(view,id);return new LibraryEntry(TaskTitle(target),target);}).ToList();
        }
        groups["Skladby"]=songs.Values.Select(song=>new LibraryEntry(song.Name,new("07",song.Id))).ToList();
        foreach(var group in desktopLibraryAssets)groups[group.Key]=group.Value;
        var visibleViews = desktopLibrarySection.SelectedIndex switch
        {
            1 => new[]{"05","06","09","10","11","12","21","22"},
            2 => new[]{"07","08","14","15","20"},
            3 => new[]{"17","18","19","20","23","24","25"},
            _ => new[]{"01","02","03","04","13","16","27","28","29","30"}
        };
        foreach(var group in groups)
        {
            var matches=group.Value.Where(e=>query.Length>0 ? PracticeWorkspaceRules.SearchText(group.Key).Contains(query,StringComparison.Ordinal) || PracticeWorkspaceRules.SearchText(e.Title).Contains(query,StringComparison.Ordinal) : visibleViews.Contains(e.Target.ViewId)).ToArray();
            if(matches.Length==0)continue;var node=new TreeViewNode{Content=group.Key,IsExpanded=query.Length>0 || expanded.Contains(group.Key) || group.Value.Count<6};
            foreach(var entry in matches)node.Children.Add(new TreeViewNode{Content=entry});desktopLibrary.RootNodes.Add(node);
        }
    }
    private async Task RefreshDesktopLibraryAssetsAsync()
    {
        if(!desktopEnabled || !persistenceReady || closed || database is null)return;
        using var operation=services.Operations.Begin("application","library-load","Načítání knihovny",cancellable:false);
        try
        {
            var snapshot=await database.SnapshotAsync();var recordings=recordingLibrary is null?[]:await recordingLibrary.ListAsync();if(closed)return;
            desktopLibraryAssets["Projekty"]=snapshot.Documents.Where(d=>d.Kind=="project").Select(d=>UserData.Read<ProjectIndex>(d.Value)).OrderBy(p=>p.Name).Select(p=>new LibraryEntry(p.Name,new("15",p.Id))).ToList();
            desktopLibraryAssets["Moje nahrávky"]=recordings.Select(r=>new LibraryEntry(r.LibraryLabel,new("14",r.Id))).ToList();RefreshDesktopLibrary();foreach(var overview in services.Documents.All.Where(s=>s.Target.ViewId=="01").ToArray())RebuildDesktopDocument(overview);
        }
        catch(Exception error){if(!closed)ShowPersistenceError("Knihovnu nelze obnovit: "+error.Message);}
    }
}
