// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Rocker.Domain;
using Rocker.Infrastructure;
using System.Text.Json;
using Windows.Graphics;

namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private readonly UserFiles userFiles = new(Environment.GetEnvironmentVariable("ROCKER_USER_DATA_DIR"));
    private readonly NavigationSession navigation = new();
    private readonly Dictionary<string, NavigationViewItem> navigationItems = [];
    private readonly Dictionary<string, NavigationViewItem> navigationParents = [];
    private readonly Dictionary<NavigationTarget, FrameworkElement> workspaceCache = [];
    private readonly Dictionary<string, LessonSession> lessonSessions = [];
    private readonly HashSet<string> completedLessons = [], visitedViews = [];
    private readonly Dictionary<string, CloseGuard> closeGuards = [];
    private ContentCatalog? catalog;
    private WindowPreferencesStore? windowStore;
    private WindowRectangle normalBounds = new(40,40,1280,940);
    private bool selectingNavigation, shellReady, closingWindow, closeApproved, videoInitializationStarted;
    private string selectedTheme = "system";
    private int audioHostsCreated;
    private sealed record ListEntry(string Id, string Title, string Detail);
    private sealed record CloseGuard(WorkState State, Func<Task<bool>> Save, Action Discard);
    private void InitializeShell()
    {
        foreach (var group in WorkspaceViews.All.GroupBy(view => view.Group))
        {
            string? mainId = group.Key switch { "Přehled" => "01", "Live" => "02", "Nastavení" => "28", _ => null };
            var parent = new NavigationViewItem { Content = group.Key, Tag = mainId, SelectsOnInvoked = mainId is not null, Icon = new SymbolIcon(group.Key switch { "Přehled" => Symbol.Home, "Live" => Symbol.Play, "Cvičení" => Symbol.Edit, "Zvuk" => Symbol.Volume, "Knihovna" => Symbol.Library, "AI tutor" => Symbol.Message, _ => Symbol.Setting }) };
            foreach (var view in group)
            {
                navigationParents.Add(view.Id, parent);
                if (view.Id == mainId) { navigationItems.Add(view.Id, parent); continue; }
                var item = new NavigationViewItem { Content = view.Title, Tag = view.Id };
                parent.MenuItems.Add(item); navigationItems.Add(view.Id, item);
            }
            Navigation.MenuItems.Add(parent);
        }
        try { catalog = ContentCatalog.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "content", "catalog.json"))); }
        catch (Exception error) { ShellNotice.IsOpen=true;ShellNotice.Severity = InfoBarSeverity.Error; ShellNotice.Message = "Výukový obsah nelze načíst. " + error.Message; }
        windowStore = new(userFiles);
        try
        {
            var saved = windowStore.Load();
            var bounds = saved?.Bounds ?? normalBounds;
            var rect = new RectInt32(bounds.X,bounds.Y,bounds.Width,bounds.Height);
            var area = DisplayArea.GetFromRect(rect, DisplayAreaFallback.Nearest).WorkArea;
            normalBounds = bounds.FitTo(new(area.X,area.Y,area.Width,area.Height));
            AppWindow.MoveAndResize(new(normalBounds.X,normalBounds.Y,normalBounds.Width,normalBounds.Height));
            ApplyTheme(saved?.Theme ?? "system");
            if (saved?.Maximized == true && AppWindow.Presenter is OverlappedPresenter presenter) presenter.Maximize();
        }
        catch (Exception error) { ShellNotice.IsOpen=true;ShellNotice.Message = "Polohu okna se nepodařilo obnovit. " + error.Message; }
        Navigation.DisplayModeChanged+=(_,args)=>{var inset=args.DisplayMode==NavigationViewDisplayMode.Minimal?new Thickness(48,0,0,0):new Thickness(0);WorkspaceTitle.Margin=WorkspaceTrail.Margin=inset;};
        AppWindow.Changed += (_,_) => TrackWindowBounds();
        AppWindow.Closing += Shell_Closing;
        shellReady = true; ShowWorkspace();
    }
    private void TrackWindowBounds()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
            normalBounds = new(AppWindow.Position.X,AppWindow.Position.Y,AppWindow.Size.Width,AppWindow.Size.Height);
    }
    private void Navigate(NavigationTarget target) { componentMode = false; navigation.Navigate(target);if(persistenceReady && PracticeViews.Contains(target.ViewId)){appSettings=appSettings with{ResumeTask=target};SaveSettings();}ShowWorkspace(); }
    private void ShowWorkspace()
    {
        if (!shellReady || closed) return;
        var target = navigation.Current;ApplyNavigationTask(target);var view = WorkspaceViews.Get(target.ViewId);
        foreach(var old in workspaceCache.Keys.Where(k=>k.ViewId==target.ViewId && k!=target).ToArray())workspaceCache.Remove(old);
        if(aiActive is { } active && target.ViewId!=active.View)aiCancellation?.Cancel();
        if(target.ViewId!=rhythmView && (rhythmRunning || rhythmOperation))_ = StopRhythmUiAsync();
        if(target.ViewId!="12")CancelPalm();
        if(target.ViewId!="22" && (earHeard || earOperation))_ = PauseEarPreviewAsync();
        if(target.ViewId!="09" && surgicalListening){surgicalListening=false;UpdateSurgical();}
        visitedViews.Add(view.Id); Title = "Rocker — " + view.Title;
        WorkspaceTitle.Text = view.Title; WorkspaceTrail.Text = WorkspaceDescription(target);
        selectingNavigation = true;
        foreach (var parent in navigationParents.Values.Distinct()) if (parent != navigationParents[view.Id]) parent.IsExpanded = false;
        if (navigationItems[view.Id] != navigationParents[view.Id]) navigationParents[view.Id].IsExpanded = true;
        Navigation.SelectedItem = navigationItems[view.Id]; selectingNavigation = false;
        Navigation.IsBackEnabled = BackMenu.IsEnabled = navigation.CanGoBack; ForwardMenu.IsEnabled = navigation.CanGoForward;
        NotationPanel.Visibility = view.Id == "20" ? Visibility.Visible : Visibility.Collapsed;
        if(view.Id!="08")SendVideo(new{op="pause"});
        VideoPanel.Visibility = view.Id == "08" ? Visibility.Visible : Visibility.Collapsed;
        DevicePanel.Visibility = view.Id == "29" ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceContent.Visibility = view.Id is "08" or "20" or "29" ? Visibility.Collapsed : Visibility.Visible;
        if (WorkspaceContent.Visibility == Visibility.Visible)
        {
            if (view.Id is "01" or "06" or "19" or "27") workspaceCache.Remove(target); // Refresh summaries after lesson completion.
            if (!workspaceCache.TryGetValue(target, out var page)) { page = BuildWorkspace(target); workspaceCache.Add(target,page); }
            WorkspaceContent.Content = page;
        }
        LabelNativeControls(ShellRoot);
        if(view.Id=="28")RefreshSettingsControls();
        RefreshPlanControls();
        RefreshWorkspaceChrome();
        if(aiPages.TryGetValue(view.Id,out var aiPage) && !aiBusy)RefreshAiPage(aiPage);
        if (view.Id == "08" && !videoInitializationStarted) _ = EnsureVideoAsync();
    }
    private async Task EnsureVideoAsync()
    {
        if (videoInitializationStarted || closed) return;
        videoInitializationStarted = true;
        await InitializeVideoAsync();
    }
    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!selectingNavigation && args.SelectedItem is NavigationViewItem { Tag: string id }) Navigate(new(id));
    }
    private void Navigation_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args) { GoBack(); }
    private void Back_Click(object sender, RoutedEventArgs args) { GoBack(); }
    private void GoBack() { if (componentMode) componentMode = false; else navigation.Back(); ShowWorkspace(); }
    private void Forward_Click(object sender, RoutedEventArgs args) { navigation.Forward(); ShowWorkspace(); }
    private void NavigateMenu_Click(object sender, RoutedEventArgs args) { if (sender is MenuFlyoutItem { Tag: string id }) Navigate(new(id)); }
    private void Theme_Click(object sender, RoutedEventArgs args) { if (sender is MenuFlyoutItem { Tag: string theme }) ApplyTheme(theme); }
    private void ApplyTheme(string theme)
    {
        selectedTheme = theme;
        appSettings = appSettings with { Theme=theme }; SaveSettings();
        ShellRoot.RequestedTheme = theme switch { "dark" => ElementTheme.Dark, "light" => ElementTheme.Light, _ => ElementTheme.Default };
        RefreshSettingsControls();
    }
    private void Shell_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (!shellReady) return;
        var orientation = args.NewSize.Width < 850 ? Orientation.Vertical : Orientation.Horizontal;
        ScoreButtons.Orientation = VideoButtons.Orientation = PlanButtons.Orientation = orientation;
        if(args.NewSize.Width<780){Grid.SetColumn(PracticeStatus,0);PracticeRow.RowDefinitions.Clear();PracticeRow.RowDefinitions.Add(new(){Height=GridLength.Auto});PracticeRow.RowDefinitions.Add(new(){Height=GridLength.Auto});Grid.SetRow(PracticeStatus,1);}else{Grid.SetRow(PracticeStatus,0);Grid.SetColumn(PracticeStatus,1);PracticeRow.RowDefinitions.Clear();}
        ShellStateText.Visibility = args.NewSize.Width < 640 ? Visibility.Collapsed : Visibility.Visible;
    }
    private static StackPanel Stack() => new() { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    private static TextBlock Text(string text, bool secondary = false) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Style = (Style)Application.Current.Resources[secondary ? "VintageSecondaryTextStyle" : "VintageBodyTextStyle"] };
    private static Border Card(UIElement child) => new() { Child = child, Style = (Style)Application.Current.Resources["VintageCardStyle"] };
    private FrameworkElement BuildWorkspace(NavigationTarget target)
    {
        if (catalog is null) return Text("Výukový obsah není dostupný. Přehrávání souborů zůstává dostupné.");
        if (target.ViewId == "31")return BuildFirstRun();
        if (target.ViewId == "26")return BuildInstrument();
        if (target.ViewId == "27")return BuildProgress();
        if (target.ViewId == "23")return BuildAiChat();
        if (target.ViewId == "24")return BuildAiPlan();
        if (target.ViewId == "25")return BuildAiFeedback();
        if (target.ViewId == "17") return BuildChords(target.ItemId);
        if (target.ViewId == "18") return BuildScales(target.ItemId);
        if (target.ViewId == "19") return BuildRiffs(target.ItemId);
        if (target.ViewId == "21") return BuildFretboardGame();
        if (target.ViewId == "22") return BuildEarGame();
        if (target.ViewId is "10" or "11") return BuildRhythmTrainer(target.ViewId,target.ItemId);
        if (target.ViewId == "12") return BuildPalmMute(target.ItemId);
        if (target.ViewId == "09") return BuildSurgical(target.ItemId);
        if (target.ViewId == "15") return BuildStudio();
        if (target.ViewId == "16") return BuildLooper();
        if (target.ViewId == "14") return BuildRecordings();
        if (target.ViewId == "32") return BuildRecordingExport();
        if (target.ViewId == "07") return BuildSong();
        if (target.ViewId == "03") return BuildTuner();
        if (target.ViewId == "04") return BuildMetronome();
        if (target.ViewId == "13") return BuildAmplifier();
        if (target.ViewId == "02") return BuildLive();
        if (target.ViewId == "01") return BuildOverview();
        if (target.ViewId == "05") return BuildLessonList();
        if (target.ViewId == "06") return BuildLesson(target.ItemId);
        if (target.ViewId == "28") return BuildSettings();
        if (target.ViewId == "30") return BuildBackups();
        throw new InvalidOperationException("Chybí nativní pohled: "+target.ViewId);
    }
    private async void Guide_Click(object sender, RoutedEventArgs args)
    {
        if(closingWindow)return;
        string path=Path.Combine(AppContext.BaseDirectory,"help","user-guide.txt");
        string text="Návod je součástí instalačního balíku. Nastavení lze projít také přes Nastavení → První spuštění.";
        try{if(File.Exists(path))text=await File.ReadAllTextAsync(path);}catch(IOException){text="Návod nelze načíst. Zkus opravit instalaci; nastavení je dostupné v průvodci První spuštění.";}
        if(closed)return;
        var content=new ScrollViewer{MaxHeight=480,Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true}};
        var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Návod k používání",Content=content,CloseButtonText="Zavřít"};
        try{await dialog.ShowAsync();}catch(InvalidOperationException){}
    }
    private async void About_Click(object sender, RoutedEventArgs args)
    {
        if (closingWindow) return;
        var dialog = new ContentDialog { XamlRoot = ShellRoot.XamlRoot, Title = "Rocker", Content = $"Nativní kytarový pomocník pro Windows · verze {typeof(App).Assembly.GetName().Version!.ToString(3)}.\nLekce, aparát, metronom, nahrávky, Looper, Studio a knihovna.\nLicence najdeš u aplikace; odpovídající zdroje lze stáhnout zvlášť u stejného vydání.", CloseButtonText = "Zavřít" };
        try { await dialog.ShowAsync(); } catch (InvalidOperationException) { }
    }
    // Each recording/project feature registers its actual state and actions.
    // No simulated recording or unsaved document is installed by the shell.
    private void RegisterCloseGuard(string id, WorkState state, Func<Task<bool>> save, Action discard) => closeGuards[id] = new(state,save,discard);
    private void RemoveCloseGuard(string id) => closeGuards.Remove(id);
    private async void Shell_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (closeApproved) return;
        args.Cancel = true;
        if (closingWindow) return;
        if (studioOperation || studioEditTask is{IsCompleted:false}) { ShowPersistenceError("Počkej na dokončení práce ve Studiu."); return; }
        if (looperOperation) { ShowPersistenceError("Počkej na dokončení práce s Looperem."); return; }
        if (exportOperation) { ShowPersistenceError("Počkej na dokončení exportu nebo jej zruš v pohledu Export."); return; }
        if (backupOperationRunning) { ShowPersistenceError("Počkej na dokončení zálohy nebo obnovy."); return; }
        if (deviceOperationRunning) { ShowPersistenceError("Počkej na dokončení změny zvukového zařízení."); return; }
        if(fileOperation){ShowPersistenceError("Počkej na otevření souboru nebo načítání zruš.");return;}
        if (profileOperation) { ShowPersistenceError("Počkej na dokončení uložení profilu kytary."); return; }
        if (secretOperationRunning) { ShowPersistenceError("Počkej na dokončení uložení nebo odstranění klíče."); return; }
        closingWindow = true;
        try
        {
            await powerStopTask;
            foreach (var guard in closeGuards.Values.Where(guard => guard.State.RequiresConfirmation).ToArray())
            {
                var dialog = new ContentDialog { XamlRoot = ShellRoot.XamlRoot, Title = "Zavřít Rocker?", Content = guard.State.Prompt, PrimaryButtonText = "Uložit a zavřít", SecondaryButtonText = guard.State.Recording ? "" : "Zahodit", CloseButtonText = "Zrušit", DefaultButton = ContentDialogButton.Primary };
                var choice = await dialog.ShowAsync();
                if (choice == ContentDialogResult.None) return;
                if (choice == ContentDialogResult.Primary && !await guard.Save()) return;
                if (choice == ContentDialogResult.Secondary) guard.Discard();
            }
            practiceSession.End();UpdateWakeLock();FlushPractice();
            await CancelAiAsync();await recentSaveTask;
            SendVideo(new{op="pause"});
            CancelPalm();await StopRhythmUiAsync();
            if (autosave is not null) await autosave.FlushAsync();
            TrackWindowBounds();
            try { await windowStore!.SaveAsync(new(1,normalBounds,AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized },selectedTheme)); }
            catch (Exception error) { WriteShellDiagnostic("window-state-error.json",new { error = error.Message }); }
            ampApplyTimer.Stop();metroApplyTimer.Stop();timer.Stop();
            if(audio is not null) { await audio.DisposeAsync();audio=null; }
            closeApproved = true; Close();
        }
        catch (Exception error) { ShellNotice.IsOpen = true; ShellNotice.Severity = InfoBarSeverity.Error; ShellNotice.Message = "Okno nelze zavřít. " + error.Message; }
        finally { closingWindow = false; }
    }
    private async Task VerifyShellAsync()
    {
        foreach (var view in WorkspaceViews.All) { Navigate(new(view.Id)); await Task.Yield(); }
        if (closed) return;
        var task = new NavigationTarget("06",catalog!.Lessons[0].Id);
        Navigate(task); Navigate(new("20")); navigation.Back(); ShowWorkspace();
        bool restoredTask = navigation.Current == task;
        Navigate(new("08")); await EnsureVideoAsync();
        Navigate(new("01"));
        await VerifyPersistenceAsync();
        await VerifyAmplifierAsync();
        await VerifyMetronomeAsync();
        await VerifyTunerAsync();
        await VerifyDiagnosticsAsync();
        await VerifySongsAsync();
        await VerifyRecordingsAsync();
        await VerifyLooperAsync();
        await VerifyStudioAsync();
        await VerifyLessonsAsync();
        VerifySurgical();
        await VerifyRhythmAndPalmAsync();
        VerifyLibraryGames();
        await VerifyNotationIntegratedAsync();
        await VerifyVideoPracticeAsync();
        await VerifyAiViewsAsync();
        await VerifyPracticeAndProfileAsync();
        await VerifyOverviewLiveAsync();
        await VerifyFirstRunAsync();
        await VerifyFileCommandsAsync();
        await VerifyPowerRecoveryAsync();
        await VerifyWorkspaceUxAsync();
        await VerifyAllDesignViewsAsync();
        await CaptureVisualFinishAsync();
        await VerifyAccessibilityAsync();
        await VerifyDevicesAsync();
        VerifyComponents();
        if (Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTIC_VIEW") == "components") ShowComponents();
        await CaptureNativeViewAsync();
        WriteShellDiagnostic("shell-navigation.json",new { result = visitedViews.Count == 32 && restoredTask && audioHostsCreated == 1 ? "passed" : "failed", viewsVisited = visitedViews.Count, views = visitedViews.Order().ToArray(), backRestoredTask = restoredTask, audioHostsCreated, cachedNativePages = workspaceCache.Count, allNativeWorkspacesImplemented = true, manualInteractionsVerified = false, completeFeaturesVerified = false });
    }
    private static void WriteShellDiagnostic(string name, object value)
    {
        try
        {
            var directory = Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory,name),JsonSerializer.Serialize(value));
        }
        catch (Exception) { }
    }
}
