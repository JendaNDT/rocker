// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Frozen;
namespace Rocker.Domain;

public enum DesktopDestination { Document, Library, Tool, Live, Settings, Dialog }
public enum DesktopWindowRole { Workspace, Document, Tool, Live, Settings }
public enum DocumentIdentityScope { Object, CanonicalFile }
public enum SourceSwitchDecision { KeepCurrent, Ready, FinishCaptureFirst, ReleaseLooperFirst }

public sealed record DesktopRoute(DesktopDestination Destination, NavigationTarget Target);

public static class DesktopRoutes
{
    public static IReadOnlySet<string> ToolViews { get; } = new[] { "03", "04", "13", "16" }.ToFrozenSet(StringComparer.Ordinal);
    public static DesktopRoute Resolve(NavigationTarget target)
    {
        WorkspaceViews.Validate(target);
        var destination = target.ViewId switch
        {
            "02" => DesktopDestination.Live,
            "03" or "04" or "13" or "16" => DesktopDestination.Tool,
            "05" => DesktopDestination.Library,
            "14" when target.ItemId is null => DesktopDestination.Library,
            "26" or "28" or "29" or "30" => DesktopDestination.Settings,
            "31" or "32" => DesktopDestination.Dialog,
            _ => DesktopDestination.Document
        };
        return new(destination, target);
    }
}

// File paths must be canonicalized/validated by Infrastructure before this factory.
// Windows file identity is case-insensitive; content/catalog object IDs are not.
public sealed class DesktopDocumentKey : IEquatable<DesktopDocumentKey>
{
    public string ViewId { get; }
    public string ResourceId { get; }
    public DocumentIdentityScope Scope { get; }
    private DesktopDocumentKey(string viewId, string resourceId, DocumentIdentityScope scope)
    {
        if (DesktopRoutes.Resolve(new(viewId, "identity")).Destination != DesktopDestination.Document)
            throw new ArgumentException("Pohled není dokument.", nameof(viewId));
        ViewId = viewId; ResourceId = resourceId; Scope = scope;
    }
    public static DesktopDocumentKey ForObject(string viewId, string objectId)
    {
        if (!UserData.ValidId(objectId)) throw new ArgumentException("Identita objektu není platná.", nameof(objectId));
        return new(viewId, objectId, DocumentIdentityScope.Object);
    }
    public static DesktopDocumentKey ForCanonicalFile(string viewId, string canonicalPath)
    {
        if (viewId is not ("07" or "14" or "15" or "20") ||
            string.IsNullOrWhiteSpace(canonicalPath) || canonicalPath.Length > 32767 || canonicalPath.Any(char.IsControl))
            throw new ArgumentException("Identita souborového dokumentu není platná.");
        bool absolute = canonicalPath.StartsWith('/') || canonicalPath.StartsWith(@"\\", StringComparison.Ordinal) ||
            canonicalPath.Length >= 3 && char.IsAsciiLetter(canonicalPath[0]) && canonicalPath[1] == ':' && canonicalPath[2] is '/' or '\\';
        if (!absolute || canonicalPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            throw new ArgumentException("Cesta musí být absolutní a kanonická.", nameof(canonicalPath));
        return new(viewId, canonicalPath, DocumentIdentityScope.CanonicalFile);
    }
    private StringComparer ResourceComparer => Scope == DocumentIdentityScope.CanonicalFile ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public bool Equals(DesktopDocumentKey? other) => other is not null && ViewId == other.ViewId && Scope == other.Scope && ResourceComparer.Equals(ResourceId, other.ResourceId);
    public override bool Equals(object? other) => other is DesktopDocumentKey key && Equals(key);
    public override int GetHashCode() => HashCode.Combine(ViewId, Scope, ResourceComparer.GetHashCode(ResourceId));
}

public sealed record DesktopDocumentState(DesktopDocumentKey Key, string WindowId, bool HasUnsavedChanges = false,
    bool OwnsAudioSource = false, bool HasActiveCapture = false)
{
    public DesktopDocumentState MoveTo(string windowId)
    {
        if (!UserData.ValidId(windowId)) throw new ArgumentException("Identita okna není platná.", nameof(windowId));
        return this with { WindowId = windowId };
    }
    // The coordinator performs saving/source release; this model never does I/O.
    public bool CanClose(CloseChoice choice, bool saveSucceeded, bool sourceReleased)
    {
        if (!Enum.IsDefined(choice) || choice == CloseChoice.Cancel) return false;
        if ((OwnsAudioSource || HasActiveCapture) && !sourceReleased) return false;
        if (HasActiveCapture && (choice != CloseChoice.Save || !saveSucceeded)) return false;
        return !HasUnsavedChanges || choice == CloseChoice.Discard || saveSucceeded;
    }
}

public sealed record DesktopTransportOwnership(DesktopDocumentKey? Owner, bool Capturing = false, bool LooperActive = false)
{
    public SourceSwitchDecision DecideSwitch(DesktopDocumentKey requested)
    {
        ArgumentNullException.ThrowIfNull(requested);
        if (requested.Equals(Owner)) return SourceSwitchDecision.KeepCurrent;
        if (Capturing) return SourceSwitchDecision.FinishCaptureFirst;
        if (LooperActive) return SourceSwitchDecision.ReleaseLooperFirst;
        return SourceSwitchDecision.Ready;
    }
}

// Bounds are physical screen pixels; panel dimensions are DIPs. No HWND/UI types.
public sealed record DesktopPixelBounds(int X, int Y, int Width, int Height);
public sealed record DesktopWindowLayout(string Id, DesktopWindowRole Role, DesktopPixelBounds Bounds,
    IReadOnlyList<DesktopDocumentKey> Documents, string? ToolViewId = null, bool Maximized = false, bool AlwaysOnTop = false, bool Visible = true, int SelectedDocumentIndex = 0);
public sealed record DesktopViewPreferences(DesktopDocumentKey Key,double Zoom=1,int Track=0,int Bar=1,int LoopFirst=1,int LoopLast=1,bool AllTracks=false,bool Follow=true);
public sealed record DesktopWorkspaceLayout(int SchemaVersion, IReadOnlyList<DesktopWindowLayout> Windows,
    double LibraryWidthDip = 220, double InspectorWidthDip = 240, bool LibraryVisible = true, bool InspectorVisible = true, IReadOnlyList<string>? AttachedTools = null,IReadOnlyList<DesktopViewPreferences>? ViewPreferences=null,int InterfaceRevision=0)
{
    public const int CurrentSchema = 1;
    public DesktopWorkspaceLayout Validate()
    {
        if (InterfaceRevision is <0 or >2 || SchemaVersion != CurrentSchema || Windows is null || Windows.Count is < 1 or > 12 ||
            !double.IsFinite(LibraryWidthDip) || LibraryWidthDip is < 120 or > 800 ||
            !double.IsFinite(InspectorWidthDip) || InspectorWidthDip is < 120 or > 800)
            throw new InvalidDataException("Rozložení pracovního prostředí není platné.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var tools = new HashSet<string>(StringComparer.Ordinal);
        var documents = new HashSet<DesktopDocumentKey>();
        var windows = new List<DesktopWindowLayout>();
        foreach (var window in Windows)
        {
            if (window is null || !UserData.ValidId(window.Id) || !ids.Add(window.Id) || !Enum.IsDefined(window.Role) ||
                window.Bounds is null || window.Bounds.Width is < 120 or > 16384 || window.Bounds.Height is < 100 or > 16384 ||
                window.Documents is null || window.Documents.Count > 64 || window.SelectedDocumentIndex<0 || (window.Documents.Count>0?window.SelectedDocumentIndex>=window.Documents.Count:window.SelectedDocumentIndex!=0))
                throw new InvalidDataException("Uložené okno není platné.");
            if (window.Role == DesktopWindowRole.Tool)
            {
                if (window.ToolViewId is null || !DesktopRoutes.ToolViews.Contains(window.ToolViewId) ||
                    !tools.Add(window.ToolViewId) || window.Documents.Count != 0)
                    throw new InvalidDataException("Nástroj musí mít jedinou instanci a nesmí hostovat dokument.");
            }
            else if (window.ToolViewId is not null || window.Role is DesktopWindowRole.Live or DesktopWindowRole.Settings && window.Documents.Count != 0)
                throw new InvalidDataException("Role okna neodpovídá obsahu.");
            foreach (var key in window.Documents)
                if (key is null || !documents.Add(key) || documents.Count > 64)
                    throw new InvalidDataException("Dokument smí mít právě jednoho hostitele.");
            windows.Add(window with { Documents = Array.AsReadOnly(window.Documents.ToArray()) });
        }
        if (windows.Count(w => w.Role == DesktopWindowRole.Workspace) != 1 ||
            windows.Count(w => w.Role == DesktopWindowRole.Live) > 1 || windows.Count(w => w.Role == DesktopWindowRole.Settings) > 1)
            throw new InvalidDataException("Hlavní, Live a nastavovací okno musí být jednoznačné.");
        foreach(var attached in AttachedTools??[])
            if(!DesktopRoutes.ToolViews.Contains(attached) || !tools.Add(attached))throw new InvalidDataException("Připojený nástroj není jednoznačný.");
        var preferences=new HashSet<DesktopDocumentKey>();
        if(ViewPreferences?.Count>64)throw new InvalidDataException("Příliš mnoho nastavení dokumentů.");
        foreach(var view in ViewPreferences??[])
            if(view is null || view.Key is null || !documents.Contains(view.Key) || !preferences.Add(view.Key) || !double.IsFinite(view.Zoom) || view.Zoom is <.1 or >200 || view.Track is <0 or >10000 || view.Bar is <1 or >100000 || view.LoopFirst is <1 or >100000 || view.LoopLast<view.LoopFirst || view.LoopLast>100000)throw new InvalidDataException("Nastavení dokumentu není platné.");
        return this with { Windows = windows.AsReadOnly(), AttachedTools=Array.AsReadOnly((AttachedTools??[]).ToArray()),ViewPreferences=Array.AsReadOnly((ViewPreferences??[]).ToArray()) };
    }
}
