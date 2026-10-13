// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using Rocker.Domain;
namespace Rocker.Infrastructure;

public sealed class WorkspaceLayoutStore(UserFiles files)
{
    public string FilePath => Path.Combine(files.DirectoryFor("settings"),"desktop-layout.json");
    private sealed record Key(string ViewId,string ResourceId,DocumentIdentityScope Scope)
    {
        public static Key From(DesktopDocumentKey key)=>new(key.ViewId,key.ResourceId,key.Scope);
        public DesktopDocumentKey Read()=>Scope switch
        {
            DocumentIdentityScope.Object=>DesktopDocumentKey.ForObject(ViewId,ResourceId),
            DocumentIdentityScope.CanonicalFile=>DesktopDocumentKey.ForCanonicalFile(ViewId,ResourceId),
            _=>throw new InvalidDataException("Identita dokumentu není platná.")
        };
    }
    private sealed record Window(string Id,DesktopWindowRole Role,DesktopPixelBounds Bounds,Key[] Documents,string? ToolViewId,bool Maximized,bool AlwaysOnTop,bool Visible,int SelectedDocumentIndex);
    private sealed record Layout(int SchemaVersion,Window[] Windows,double LibraryWidthDip,double InspectorWidthDip,bool LibraryVisible,bool InspectorVisible,string[]? AttachedTools);
    public DesktopWorkspaceLayout? Load()
    {
        if(!File.Exists(FilePath))return null;
        if(new FileInfo(FilePath).Length>256*1024)throw new InvalidDataException("Soubor rozložení je příliš velký.");
        try
        {
            var value=JsonSerializer.Deserialize<Layout>(File.ReadAllText(FilePath))??throw new InvalidDataException("Soubor rozložení je prázdný.");
            if(value.Windows is null || value.Windows.Any(w=>w is null || w.Documents is null || w.Documents.Any(k=>k is null)))throw new InvalidDataException("Soubor rozložení není úplný.");
            return new DesktopWorkspaceLayout(value.SchemaVersion,value.Windows.Select(w=>new DesktopWindowLayout(w.Id,w.Role,w.Bounds,w.Documents.Select(k=>k.Read()).ToArray(),w.ToolViewId,w.Maximized,w.AlwaysOnTop,w.Visible,w.SelectedDocumentIndex)).ToArray(),value.LibraryWidthDip,value.InspectorWidthDip,value.LibraryVisible,value.InspectorVisible,value.AttachedTools).Validate();
        }
        catch(Exception error) when(error is JsonException or ArgumentException){throw new InvalidDataException("Uložené rozložení je poškozené. Hudební data zůstávají zachovaná.",error);}
    }
    public Task SaveAsync(DesktopWorkspaceLayout layout,CancellationToken cancellation=default)
    {
        var value=layout.Validate();
        var stored=new Layout(value.SchemaVersion,value.Windows.Select(w=>new Window(w.Id,w.Role,w.Bounds,w.Documents.Select(Key.From).ToArray(),w.ToolViewId,w.Maximized,w.AlwaysOnTop,w.Visible,w.SelectedDocumentIndex)).ToArray(),value.LibraryWidthDip,value.InspectorWidthDip,value.LibraryVisible,value.InspectorVisible,(value.AttachedTools??[]).ToArray());
        return UserFiles.WriteTextAsync(FilePath,JsonSerializer.Serialize(stored),cancellation);
    }
}
