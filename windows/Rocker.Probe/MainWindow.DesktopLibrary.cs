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
        var expanded=desktopLibrary.RootNodes.Where(n=>n.IsExpanded).Select(n=>n.Content as string).ToHashSet();
        desktopLibrary.Visibility=Visibility.Visible;
        if(desktopLibrary.Parent is Grid parent)foreach(var old in parent.Children.OfType<ScrollViewer>().ToArray())parent.Children.Remove(old);
        desktopLibrary.RootNodes.Clear();string query=librarySearch.Text.Trim();
        var groups=WorkspaceViews.All.Where(v=>v.Id is not("31" or "32")).GroupBy(v=>v.Group).ToDictionary(g=>g.Key,g=>g.Select(v=>new LibraryEntry(v.Title,new(v.Id))).ToList());
        if(catalog is not null)
        {
            groups["Lekce"]=catalog.Lessons.Select(l=>new LibraryEntry(l.Title,new("06",l.Id))).ToList();
            foreach(var (name,view,title) in new[]{("surgicalExercises","09","Přesný trénink"),("powerChordExercises","10","Power chordy"),("gallopExercises","11","Gallop"),("palmMuteDrills","12","Palm mute"),("chords","17","Akordy"),("scales","18","Stupnice"),("riffs","19","Riffy")})
                groups[title]=catalog.Items(name).Select(item=>{string id=item.GetProperty("id").GetString()!;var target=new NavigationTarget(view,id);return new LibraryEntry(TaskTitle(target),target);}).ToList();
        }
        groups["Skladby"]=songs.Values.Select(song=>new LibraryEntry(song.Name,new("07",song.Id))).ToList();
        foreach(var group in desktopLibraryAssets)groups[group.Key]=group.Value;
        foreach(var group in groups)
        {
            var matches=group.Value.Where(e=>query.Length==0 || group.Key.Contains(query,StringComparison.CurrentCultureIgnoreCase) || e.Title.Contains(query,StringComparison.CurrentCultureIgnoreCase)).ToArray();
            if(matches.Length==0)continue;var node=new TreeViewNode{Content=group.Key,IsExpanded=query.Length>0 || expanded.Contains(group.Key)};
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
            desktopLibraryAssets["Moje nahrávky"]=recordings.Select(r=>new LibraryEntry(r.LibraryLabel,new("14",r.Id))).ToList();RefreshDesktopLibrary();
        }
        catch(Exception error){if(!closed)ShowPersistenceError("Knihovnu nelze obnovit: "+error.Message);}
    }
}
