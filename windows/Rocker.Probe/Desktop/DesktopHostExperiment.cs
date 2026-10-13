// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Rocker.Interop;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;

namespace Rocker.Probe.Desktop;

// D03 isolated runtime experiment. It owns views/windows, never an audio host.
// Production application services and document sessions are separate later steps.
internal sealed class DesktopHostExperiment : IDisposable
{
    private sealed class Host
    {
        public required Window Window { get; init; }
        public required Grid Root { get; init; }
        public required TabView Tabs { get; init; }
        public bool Closed { get; set; }
        public nint Handle => WinRT.Interop.WindowNative.GetWindowHandle(Window);
    }
    private readonly List<Host> hosts = [];
    private readonly Grid document = new() { RowSpacing = 8, Margin = new(12) };
    private readonly TextBox selection = new() { Header = "Výběr dokumentu", Text = "Kytarový region · neuložená ukázka" };
    private readonly NumberBox zoom = new() { Header = "Zvětšení", Minimum = 50, Maximum = 200, Value = 125 };
    private readonly WebView2 video = new() { Height = 100 };
    private readonly string documentId = Guid.NewGuid().ToString("N");
    private Host? documentHost, detachedHost;
    private Host? workspace;
    private bool transferring, disposed;
    private readonly List<object> checks = [];
    private readonly Dictionary<string, Host> tools = [];

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);

    public DesktopHostExperiment(ImageSource? scoreSource)
    {
        document.RowDefinitions.Add(new() { Height = GridLength.Auto });
        document.RowDefinitions.Add(new() { Height = GridLength.Auto });
        document.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        document.RowDefinitions.Add(new() { Height = GridLength.Auto });
        document.Children.Add(selection);
        Grid.SetRow(zoom, 1); document.Children.Add(zoom);
        var image = new Image { Source = scoreSource, Stretch = Stretch.Uniform, MaxHeight = 240 };
        Grid.SetRow(image, 2); document.Children.Add(image);
        Grid.SetRow(video, 3); document.Children.Add(video);
    }
    private void Check(string name, bool value)
    {
        checks.Add(new { name, passed = value });
        if (!value) throw new InvalidOperationException("Desktop host experiment failed: " + name);
    }
    private Host CreateHost(string title, int width, int height, nint owner = 0)
    {
        var window = new Window { Title = "Rocker — " + title };
        window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Rocker.ico"));
        window.AppWindow.Resize(new SizeInt32(width, height));
        var root = new Grid { RequestedTheme = ElementTheme.Dark };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var tabs = new TabView { IsAddTabButtonVisible = false, CanDragTabs = true, CanReorderTabs = true, AllowDrop = true };
        Grid.SetRow(tabs, 1); root.Children.Add(tabs);
        var label = new TextBlock { Text = "D03 · technický experiment · ukázkový dokument · zvuk se nespouští",
            Margin = new(12, 6, 12, 6), TextWrapping = TextWrapping.Wrap, FontSize = 11 };
        Grid.SetRow(label, 2); root.Children.Add(label);
        window.Content = root;
        var host = new Host { Window = window, Root = root, Tabs = tabs };
        hosts.Add(host);
        window.Closed += (_, _) => { host.Closed = true; tools.Remove(title); };
        if (owner != 0)
        {
            Marshal.SetLastPInvokeError(0);
            _ = SetWindowLongPtr(host.Handle, -8, owner);
            int error = Marshal.GetLastPInvokeError();
            if (error != 0) throw new System.ComponentModel.Win32Exception(error);
        }
        var menu = new MenuBar();
        var item = new MenuBarItem { Title = "Dokument" };
        var detach = new MenuFlyoutItem { Text = "Otevřít v samostatném okně" };
        detach.Click += (_, _) => Detach();
        var attach = new MenuFlyoutItem { Text = "Připojit do pracovního okna" };
        attach.Click += (_, _) => { if (workspace is not null) MoveDocument(workspace); };
        item.Items.Add(detach); item.Items.Add(attach); menu.Items.Add(item);
        root.Children.Add(menu);
        tabs.TabDragStarting += (_, args) =>
        {
            if (ReferenceEquals(host, documentHost))
            {
                args.Data.SetData("Rocker.D03.Document", documentId);
                args.Data.RequestedOperation = DataPackageOperation.Move;
            }
        };
        tabs.TabStripDragOver += (_, args) =>
        {
            if (args.DataView.Contains("Rocker.D03.Document")) args.AcceptedOperation = DataPackageOperation.Move;
        };
        tabs.TabStripDrop += async (_, args) =>
        {
            if (!args.DataView.Contains("Rocker.D03.Document")) return;
            var deferral = args.GetDeferral();
            try
            {
                var id = await args.DataView.GetDataAsync("Rocker.D03.Document");
                if (!disposed && !host.Closed && Equals(id, documentId)) MoveDocument(host);
            }
            finally { deferral.Complete(); }
        };
        root.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = VirtualKey.D, Modifiers = VirtualKeyModifiers.Control });
        root.KeyboardAccelerators[0].Invoked += (_, args) => { Detach(); args.Handled = true; };
        root.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = VirtualKey.R, Modifiers = VirtualKeyModifiers.Control });
        root.KeyboardAccelerators[1].Invoked += (_, args) => { if (workspace is not null) MoveDocument(workspace); args.Handled = true; };
        window.Activate();
        return host;
    }
    private void MoveDocument(Host target)
    {
        if (disposed || target.Closed || transferring || ReferenceEquals(target, documentHost)) return;
        transferring = true;
        try
        {
            if (documentHost is { } previous)
            {
                foreach (var item in previous.Tabs.TabItems.OfType<TabViewItem>().ToArray()) item.Content = null;
                previous.Tabs.TabItems.Clear();
            }
            var tab = new TabViewItem { Header = "Cvičná partitura *", Content = document };
            target.Tabs.TabItems.Add(tab); target.Tabs.SelectedItem = tab;
            documentHost = target;
            target.Window.Activate();
            selection.Focus(FocusState.Programmatic);
        }
        finally { transferring = false; }
    }
    private void Detach()
    {
        if (disposed || workspace is null) return;
        if (detachedHost is null || detachedHost.Closed) detachedHost = CreateHost("Odpojený dokument", 720, 660);
        MoveDocument(detachedHost);
    }
    private Host OpenTool()
    {
        const string title = "Plovoucí nástroj";
        if (tools.TryGetValue(title, out var existing) && !existing.Closed)
        {
            existing.Window.Activate(); return existing;
        }
        var host = CreateHost(title, 440, 400, workspace!.Handle);
        var content = new StackPanel { Spacing = 12, Margin = new(18) };
        content.Children.Add(new TextBlock { Text = "Metronom", FontSize = 24 });
        content.Children.Add(new NumberBox { Header = "Ukázkové tempo · bez zvuku", Value = 100, Minimum = 40, Maximum = 240 });
        content.Children.Add(new TextBlock { Text = "Nástroj používá stejný aplikační kontext. V tomto experimentu nic nepřehrává.", TextWrapping = TextWrapping.Wrap });
        host.Tabs.TabItems.Add(new TabViewItem { Header = "Nástroj", IsClosable = false, Content = content });
        tools.Add(title, host); return host;
    }
    public async Task<object> RunAsync(NativeAudio audio, Func<string, FrameworkElement, Task> capture)
    {
        var before = audio.GetSnapshot(); var clock = audio.GetClock();
        Check("experimentBeginsWithStoppedAudio", before.State == AudioState.Stopped);
        workspace = CreateHost("Desktopový pracovní host", 1000, 720);
        MoveDocument(workspace);
        await Task.Delay(200);
        Check("nativeWorkspaceLoaded", workspace.Root.XamlRoot is not null && workspace.Root.ActualWidth > 0 && IsWindow(workspace.Handle));
        var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null,
            Path.Combine(Environment.GetEnvironmentVariable("ROCKER_USER_DATA_DIR")!, "desktop-webview"), null);
        await video.EnsureCoreWebView2Async(environment);
        video.CoreWebView2.Settings.AreHostObjectsAllowed = false;
        video.CoreWebView2.Settings.AreDevToolsEnabled = false;
        var loaded = new TaskCompletionSource<bool>();
        video.CoreWebView2.NavigationCompleted += (_, args) => loaded.TrySetResult(args.IsSuccess);
        video.CoreWebView2.NavigateToString("<!doctype html><html lang='cs'><body><p>D03 · lokální video host bez zvuku</p><script>window.rockerMoveProbe=23;</script></body></html>");
        Check("localWebViewLoadsWithoutNetwork", await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15)));
        var core = video.CoreWebView2;
        Check("actualScoreSourceAvailable", document.Children.OfType<Image>().Single().Source is not null);
        selection.SelectionStart = 3; selection.SelectionLength = 6;
        var originalText = selection.Text;
        await capture("screens/desktop-experiment-workspace.png", workspace.Root);
        Detach();
        await Task.Delay(250);
        Check("detachedNativeWindowHasSeparateHwnd", detachedHost is not null && detachedHost.Handle != workspace.Handle && IsWindow(detachedHost.Handle));
        Check("documentHasExactlyOneHost", workspace.Tabs.TabItems.Count == 0 && detachedHost!.Tabs.TabItems.Count == 1);
        Check("movePreservesTextZoomAndSelection", selection.Text == originalText && zoom.Value == 125 && selection.SelectionStart == 3 && selection.SelectionLength == 6);
        Check("moveChangesActualXamlRoot", ReferenceEquals(document.XamlRoot, detachedHost!.Root.XamlRoot) && !ReferenceEquals(document.XamlRoot, workspace.Root.XamlRoot));
        Check("webViewSurvivesDetachWithoutRecreation", ReferenceEquals(core, video.CoreWebView2) && await core.ExecuteScriptAsync("window.rockerMoveProbe") == "23");
        Check("detachedWindowUsesOwnRasterizationScale", document.XamlRoot.RasterizationScale > 0);
        Check("documentWindowHasNoToolOwner", GetWindow(detachedHost.Handle, 4) == 0);
        await capture("screens/desktop-experiment-detached.png", detachedHost.Root);
        var tool = OpenTool();
        Check("toolSingletonActivatesExistingHost", ReferenceEquals(tool, OpenTool()));
        Check("toolHasExplicitNativeOwner", GetWindow(tool.Handle, 4) == workspace.Handle);
        await Task.Delay(150);
        await capture("screens/desktop-experiment-tool.png", tool.Root);
        var dialog = new ContentDialog { XamlRoot = detachedHost.Root.XamlRoot, Title = "D03 · vlastník dialogu",
            Content = "Dialog patří odpojenému dokumentu. Zavření nezahodí jeho stav.", CloseButtonText = "Zrušit" };
        var dialogTask = dialog.ShowAsync().AsTask();
        await Task.Delay(150);
        Check("dialogUsesDetachedDocumentRoot", ReferenceEquals(dialog.XamlRoot, document.XamlRoot) && !dialogTask.IsCompleted);
        dialog.Hide();
        Check("dialogCancelRetainsDocument", await dialogTask == ContentDialogResult.None && selection.Text == originalText);
        MoveDocument(workspace);
        await Task.Delay(250);
        Check("reattachMovesSameViewAndKeepsWebViewState", detachedHost.Tabs.TabItems.Count == 0 && workspace.Tabs.TabItems.Count == 1 &&
            ReferenceEquals(document.XamlRoot, workspace.Root.XamlRoot) && ReferenceEquals(core, video.CoreWebView2) && await core.ExecuteScriptAsync("window.rockerMoveProbe") == "23");
        tool.Window.Close();
        await Task.Delay(100);
        Check("closingToolKeepsDocumentAlive", tool.Closed && IsWindow(workspace.Handle) && !workspace.Closed && selection.Text == originalText);
        detachedHost.Window.Close();
        await Task.Delay(100);
        Check("closingEmptyDocumentHostKeepsWorkspaceAlive", detachedHost.Closed && IsWindow(workspace.Handle));
        var after = audio.GetSnapshot(); var afterClock = audio.GetClock();
        Check("windowOperationsNeverStartOrRestartAudio", after.State == AudioState.Stopped && after.RenderedFrames == before.RenderedFrames &&
            afterClock.Epoch == clock.Epoch && afterClock.Frame == clock.Frame && !audio.GetMeters().Monitoring);
        return new { result = "passed", checks, nativeWindowsCreated = hosts.Count, singleDocumentId = documentId,
            webViewRecreatedDuringMove = false, actualScoreSourceUsed = true, soundStarted = false,
            dragDropHandlersInstalled = true, actualPointerDragVerified = false, hardwareVerified = false,
            physicalMultiMonitorVerified = false, physicalAltTabVerified = false, productionShellReplaced = false };
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        video.Close();
        foreach (var host in hosts.Where(h => !h.Closed).Reverse()) host.Window.Close();
        tools.Clear();
        documentHost = detachedHost = workspace = null;
    }
}
