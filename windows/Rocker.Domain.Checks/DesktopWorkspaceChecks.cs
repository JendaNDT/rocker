// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;

internal static class DesktopWorkspaceChecks
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { ++checks; if (!condition) throw new Exception(message); }
        void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (Exception e) when (e is InvalidDataException or ArgumentException) { rejected = true; }
            Check(rejected, message);
        }
        foreach (var view in WorkspaceViews.All)
            Check(DesktopRoutes.Resolve(new(view.Id)).Target.ViewId == view.Id, "All legacy views keep a route: " + view.Id);
        Check(DesktopRoutes.Resolve(new("14")).Destination == DesktopDestination.Library &&
            DesktopRoutes.Resolve(new("14", "take-id")).Destination == DesktopDestination.Document, "Library selection differs from recording document identity");
        Check(DesktopRoutes.Resolve(new("31")).Destination == DesktopDestination.Dialog && DesktopRoutes.Resolve(new("32")).Destination == DesktopDestination.Dialog, "Wizard/export are owned dialogs");
        foreach (var id in new[] { "03", "04", "13", "16" }) Check(DesktopRoutes.Resolve(new(id)).Destination == DesktopDestination.Tool, "Stable tool identity");
        Reject(() => DesktopRoutes.Resolve(new("unknown")), "Unknown legacy target cannot open a fallback document");
        var first = DesktopDocumentKey.ForObject("15", "project-A");
        var same = DesktopDocumentKey.ForObject("15", "project-A");
        var other = DesktopDocumentKey.ForObject("15", "project-B");
        Check(first.Equals(same) && first.GetHashCode() == same.GetHashCode() && !first.Equals(other), "Same project activates existing session; a different project is distinct");
        Check(!first.Equals(DesktopDocumentKey.ForObject("15", "PROJECT-A")), "Catalog/object identity preserves case");
        var file = DesktopDocumentKey.ForCanonicalFile("20", @"C:\Music\tone.gp");
        var sameFile = DesktopDocumentKey.ForCanonicalFile("20", @"c:\music\TONE.GP");
        Check(file.Equals(sameFile) && file.GetHashCode() == sameFile.GetHashCode(), "Canonical Windows paths deduplicate regardless of case");
        Check(!file.Equals(DesktopDocumentKey.ForObject("20", @"C:\Music\tone.gp")), "File and object identity namespaces stay distinct");
        Reject(() => DesktopDocumentKey.ForObject("03", "tuner"), "Global tool is not a document");
        Reject(() => DesktopDocumentKey.ForObject("15", "\n"), "Control characters cannot become session identity");
        Reject(() => DesktopDocumentKey.ForCanonicalFile("06", @"C:\Music\tone.gp"), "Lesson is not a file document");
        Reject(() => DesktopDocumentKey.ForCanonicalFile("20", "path\n"), "Invalid file identity is refused before registration");
        Reject(() => DesktopDocumentKey.ForCanonicalFile("20", "tone.gp"), "Relative path cannot bypass canonical identity requirement");
        Reject(() => DesktopDocumentKey.ForCanonicalFile("20", @"C:\Music\..\tone.gp"), "Unnormalized path cannot create an alias document session");
        var state = new DesktopDocumentState(first, "main", true, true);
        var moved = state.MoveTo("detached");
        Check(ReferenceEquals(moved.Key, state.Key) && moved.HasUnsavedChanges && moved.OwnsAudioSource && moved.WindowId == "detached" && state.WindowId == "main", "Moving host preserves session and does not mutate audio ownership");
        Reject(() => state.MoveTo(""), "Window identity must be valid");
        Check(!state.CanClose(CloseChoice.Cancel, true, true), "Cancel retains dirty document");
        Check(!state.CanClose(CloseChoice.Save, false, true), "Failed save retains document");
        Check(!state.CanClose(CloseChoice.Save, true, false), "Save alone cannot close active audio owner");
        Check(state.CanClose(CloseChoice.Save, true, true), "Confirmed save and source release permit closing");
        Check(state.CanClose(CloseChoice.Discard, false, true), "Explicit discard of ordinary edits is possible after source release");
        var capture = state with { HasActiveCapture = true };
        Check(!capture.CanClose(CloseChoice.Discard, false, true) && capture.CanClose(CloseChoice.Save, true, true), "An active take requires successful save, not implicit discard");
        var transport = new DesktopTransportOwnership(first, Capturing: true);
        Check(transport.DecideSwitch(same) == SourceSwitchDecision.KeepCurrent, "Activating the same source does not request replacement");
        Check(transport.DecideSwitch(other) == SourceSwitchDecision.FinishCaptureFirst, "Another document cannot silently replace an active capture source");
        Check((transport with { Capturing = false, LooperActive = true }).DecideSwitch(other) == SourceSwitchDecision.ReleaseLooperFirst, "Looper conflict is explicit");
        Check((transport with { Capturing = false }).DecideSwitch(other) == SourceSwitchDecision.Ready && transport.Owner == first, "Switch decision never performs audio mutation");
        var main = new DesktopWindowLayout("main", DesktopWindowRole.Workspace, new(-1000, 40, 1280, 720), [first]);
        var tool = new DesktopWindowLayout("metro", DesktopWindowRole.Tool, new(20, 20, 400, 320), [], "04");
        var layout = new DesktopWorkspaceLayout(1, [main, tool]);
        var validated = layout.Validate();
        Check(validated.Windows.Count == 2 && validated.Windows[0].Bounds.X == -1000, "Negative coordinates remain valid for a left monitor; fitting belongs to window layer");
        Reject(() => (layout with { SchemaVersion = 99 }).Validate(), "Future layout schema is not silently rewritten");
        Reject(() => (layout with { LibraryWidthDip = double.NaN }).Validate(), "Invalid panel size cannot reach UI");
        Reject(() => (layout with { Windows = [main, main] }).Validate(), "Duplicate window identities are refused");
        Reject(() => (layout with { Windows = [main, tool, tool with { Id = "other-tool" }] }).Validate(), "Same metronome cannot restore twice");
        Reject(() => (layout with { Windows = [main, new("other", DesktopWindowRole.Document, new(0, 0, 640, 480), [same])] }).Validate(), "Same document cannot restore into two hosts");
        Reject(() => (layout with { Windows = [main, tool with { Documents = [other] }] }).Validate(), "Tool window cannot host a project");
        Reject(() => (layout with { Windows = [main with { Bounds = new(0, 0, -1, 720) }] }).Validate(), "Invalid dimensions are refused");
        Reject(() => (layout with { Windows = [tool] }).Validate(), "Workspace must have one main window");
        Reject(() => (layout with { Windows = [main with { Role = (DesktopWindowRole)99 }] }).Validate(), "Unknown role cannot become an arbitrary restored window");
        var mutable = new List<DesktopDocumentKey> { first };
        var frozen = (layout with { Windows = [main with { Documents = mutable }] }).Validate();
        mutable.Add(other);
        Check(frozen.Windows[0].Documents.Count == 1, "Validated layout copies collections before external mutation");
        Console.WriteLine($"PASS: {checks} desktop route, identity, source ownership, close and layout contracts (no window or audio device opened).");
        return checks;
    }
}
