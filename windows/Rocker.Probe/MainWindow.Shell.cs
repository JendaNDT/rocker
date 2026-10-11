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
            var parent = new NavigationViewItem { Content = group.Key, SelectsOnInvoked = false, Icon = new SymbolIcon(group.Key switch { "Přehled" => Symbol.Home, "Live" => Symbol.Play, "Cvičení" => Symbol.Edit, "Zvuk" => Symbol.Volume, "Knihovna" => Symbol.Library, "AI tutor" => Symbol.Message, _ => Symbol.Setting }) };
            foreach (var view in group)
            {
                var item = new NavigationViewItem { Content = view.Title, Tag = view.Id };
                parent.MenuItems.Add(item); navigationItems.Add(view.Id, item);
            }
            Navigation.MenuItems.Add(parent);
            if (group.Key == "Přehled") parent.IsExpanded = true;
        }
        try { catalog = ContentCatalog.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "content", "catalog.json"))); }
        catch (Exception error) { ShellNotice.Severity = InfoBarSeverity.Error; ShellNotice.Message = "Výukový obsah nelze načíst. " + error.Message; }
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
        catch (Exception error) { ShellNotice.Message = "Polohu okna se nepodařilo obnovit. " + error.Message; }
        AppWindow.Changed += (_,_) => TrackWindowBounds();
        AppWindow.Closing += Shell_Closing;
        shellReady = true; ShowWorkspace();
    }
    private void TrackWindowBounds()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
            normalBounds = new(AppWindow.Position.X,AppWindow.Position.Y,AppWindow.Size.Width,AppWindow.Size.Height);
    }
    private void Navigate(NavigationTarget target) { componentMode = false; navigation.Navigate(target); ShowWorkspace(); }
    private void ShowWorkspace()
    {
        if (!shellReady || closed) return;
        var target = navigation.Current; var view = WorkspaceViews.Get(target.ViewId);
        visitedViews.Add(view.Id); Title = "Rocker — " + view.Title;
        WorkspaceTitle.Text = view.Title; WorkspaceTrail.Text = "Rocker / " + view.Group + (target.ItemId is null ? "" : " / " + target.ItemId);
        selectingNavigation = true; Navigation.SelectedItem = navigationItems[view.Id]; selectingNavigation = false;
        Navigation.IsBackEnabled = BackMenu.IsEnabled = navigation.CanGoBack; ForwardMenu.IsEnabled = navigation.CanGoForward;
        NotationPanel.Visibility = view.Id == "20" ? Visibility.Visible : Visibility.Collapsed;
        VideoPanel.Visibility = view.Id == "08" ? Visibility.Visible : Visibility.Collapsed;
        DevicePanel.Visibility = view.Id == "29" ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceContent.Visibility = view.Id is "08" or "20" or "29" ? Visibility.Collapsed : Visibility.Visible;
        if (WorkspaceContent.Visibility == Visibility.Visible)
        {
            if (view.Id is "01" or "06" or "27") workspaceCache.Remove(target); // Refresh summaries after lesson completion.
            if (!workspaceCache.TryGetValue(target, out var page)) { page = BuildWorkspace(target); workspaceCache.Add(target,page); }
            WorkspaceContent.Content = page;
        }
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
    }
    private void Shell_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (!shellReady) return;
        var orientation = args.NewSize.Width < 850 ? Orientation.Vertical : Orientation.Horizontal;
        ScoreButtons.Orientation = VideoButtons.Orientation = orientation;
        ShellStateText.Visibility = args.NewSize.Width < 640 ? Visibility.Collapsed : Visibility.Visible;
    }
    private static StackPanel Stack() => new() { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    private static TextBlock Text(string text, bool secondary = false) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Style = (Style)Application.Current.Resources[secondary ? "VintageSecondaryTextStyle" : "VintageBodyTextStyle"] };
    private static Border Card(UIElement child) => new() { Child = child, Style = (Style)Application.Current.Resources["VintageCardStyle"] };
    private FrameworkElement BuildWorkspace(NavigationTarget target)
    {
        if (catalog is null) return Text("Výukový obsah není dostupný. Přehrávání souborů zůstává dostupné.");
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
        if (target.ViewId == "27") return Card(Text($"Dokončené lekce: {completedLessons.Count} / {catalog.Lessons.Count}. Pokrok a rozpracované kroky se průběžně ukládají."));
        var group = target.ViewId switch { "03" => "tunings", "04" => "metronomePresets", "09" => "surgicalExercises", "10" => "powerChordExercises", "11" => "gallopExercises", "12" => "palmMuteDrills", "13" => "ampPresets", "17" => "chords", "18" => "scales", "19" => "riffs", _ => null };
        if (group is not null) return BuildCatalog(group, target.ItemId);
        var panel = Stack();
        panel.Children.Add(Text(target.ViewId switch {
            "02" => "Živý vstup z kytary ještě není dostupný. Přehrávání nahrávky nebo not můžeš ovládat ve spodním panelu.",

            "14" => "Nahrávky zatím nejsou uložené v knihovně. Zvukový soubor můžeš otevřít a přehrát.",
            "15" => "Studio čeká na připojení stop a ukládání projektu.", "16" => "Looper čeká na živý vstup a nahrávání vrstev.",
            "21" => "Hra na hmatníku čeká na dokončení pracovního rozhraní.", "22" => "Sluchové cvičení čeká na ovládání otázek a společné přehrávání tónů.",
            "23" or "24" or "25" => "AI tutor zatím není připojený. Klíč ani žádné dotazy se nyní neposílají.",
            "26" => "Profil kytary čeká na propojení s laděním a trvalým ukládáním.",
            "30" => "Záloha a převod dat budou dostupné po dokončení ukládání. Původní webová data zůstávají zachovaná.",
            "31" => "Průvodce zvukovým vstupem ještě není připravený. Vzhled okna můžeš změnit v Nastavení.",
            "32" => "Export nahrávky bude dostupný po dokončení nahrávání. Noty už lze uložit jako MIDI.", _ => "Tento pracovní pohled se připravuje." }));
        if (target.ViewId is "07" or "14") { var open = new Button { Content = "Otevřít nahrávku…" }; open.Click += OpenMedia_Click; panel.Children.Add(open); }
        if (target.ViewId == "32") { var notes = new Button { Content = "Otevřít noty a export MIDI" }; notes.Click += (_,_) => Navigate(new("20")); panel.Children.Add(notes); }
        return Card(panel);
    }
    private FrameworkElement BuildOverview()
    {
        var panel = Stack(); panel.Children.Add(Text("Pojďme si zahrát."));
        var plan = Practice.GenerateDailyPlan(catalog!,completedLessons,DateOnly.FromDateTime(DateTime.Now));
        panel.Children.Add(Text($"Dnešní plán · {plan.TotalMinutes} minut", true));
        foreach (var item in plan.Items)
        {
            var details = Stack(); details.Children.Add(Text($"{item.Title} · {item.Minutes} min")); details.Children.Add(Text(item.Detail,true));
            var button = new Button { Content = details, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new(16,12,16,12) };
            button.Click += (_,_) => Navigate(item.LessonId is not null ? new("06",item.LessonId) : item.TrainerId == "surgical" ? new("09","spider") : item.TrainerId == "riff" ? new("19") : new(item.Href switch { "/tuner" => "03", "/metronome" => "04", "/youtube" => "08", _ => "05" }));
            panel.Children.Add(button);
        }
        return Card(panel);
    }
    private FrameworkElement BuildLessonList()
    {
        var panel = Stack(); panel.Children.Add(Text("Vyber lekci. Rozpracovaný krok se při přepnutí pohledu zachová."));
        var list = new ListView { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single, DisplayMemberPath = "Title", ItemsSource = catalog!.Lessons.Select(lesson => new ListEntry(lesson.Id,lesson.Title,$"{lesson.EstimatedMinutes} min")).ToArray() };
        list.ItemClick += (_,args) => { if (args.ClickedItem is ListEntry entry) Navigate(new("06",entry.Id)); };
        panel.Children.Add(list); return Card(panel);
    }
    private FrameworkElement BuildLesson(string? id)
    {
        var lesson = catalog!.Lessons.FirstOrDefault(lesson => lesson.Id == id) ?? LessonRules.NextIncomplete(catalog.Lessons,completedLessons) ?? catalog.Lessons[0];
        if (!lessonSessions.ContainsKey(lesson.Id)) lessonSessions.Add(lesson.Id,LessonSession.Start(lesson,savedLessonPositions.GetValueOrDefault(lesson.Id)) with { Completed=completedLessons.Contains(lesson.Id) });
        var panel = Stack();
        void Render()
        {
            panel.Children.Clear(); var session = lessonSessions[lesson.Id]; var step = session.CurrentStep;
            panel.Children.Add(Text(lesson.Title)); panel.Children.Add(Text($"Krok {session.StepIndex + 1} / {lesson.Steps.Count}",true));
            panel.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = session.ProgressPercent });
            panel.Children.Add(Text(step.GetProperty("title").GetString()!)); panel.Children.Add(Text(step.GetProperty("body").GetString()!));
            if (step.GetProperty("kind").GetString() == "note") panel.Children.Add(Text("Cílový tón: " + step.GetProperty("targetLabel").GetString() + ". Živý poslech ještě není dostupný.",true));
            if (step.GetProperty("kind").GetString() == "chord") panel.Children.Add(Text("Akord: " + step.GetProperty("chordId").GetString(),true));
            if (session.Completed) panel.Children.Add(Text("Lekce je dokončená."));
            var previous = new Button { Content = "Zpět", IsEnabled = !session.IsFirst && !session.Completed }; previous.Click += (_,_) => { lessonSessions[lesson.Id] = session.Previous(); SaveLessonProgress(); Render(); };
            var next = new Button { Content = session.IsLast ? "Dokončit lekci" : "Další", IsEnabled = !session.Completed };
            next.Click += (_,_) => { lessonSessions[lesson.Id] = session.IsLast ? session.Finish() : session.Next(); if (session.IsLast) completedLessons.Add(lesson.Id); SaveLessonProgress(); Render(); };
            panel.Children.Add(previous); panel.Children.Add(next);
        }
        Render(); return Card(panel);
    }
    private FrameworkElement BuildCatalog(string group, string? selected)
    {
        var panel = Stack(); panel.Children.Add(Text("Obsah je přenesený. Příslušný trenažér nebo hudební nástroj ještě dokončujeme.",true));
        var entries = catalog!.Items(group).Select(item => new ListEntry(item.GetProperty("id").GetString()!,
            item.TryGetProperty("name", out var name) ? name.GetString()! : item.TryGetProperty("title", out var title) ? title.GetString()! : item.GetProperty("label").GetString()!,
            item.TryGetProperty("description", out var description) ? description.GetString()! : item.TryGetProperty("focus", out var focus) ? focus.GetString()! : item.GetProperty("id").GetString()!)).ToArray();
        var list = new ListView { DisplayMemberPath = "Title", SelectionMode = ListViewSelectionMode.Single, ItemsSource = entries };
        var detail = Text("Vyber položku.",true);
        list.SelectionChanged += (_,_) => { if (list.SelectedItem is ListEntry entry) detail.Text = entry.Detail; };
        if (selected is not null) list.SelectedItem = entries.FirstOrDefault(entry => entry.Id == selected);
        var search = new TextBox { Header = "Hledat", PlaceholderText = "Název nebo popis", MaxLength = 128 };
        search.TextChanged += (_,_) => list.ItemsSource = entries.Where(entry => entry.Title.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase) || entry.Detail.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)).ToArray();
        panel.Children.Add(search); panel.Children.Add(list); panel.Children.Add(detail); return Card(panel);
    }
    private FrameworkElement BuildSettings()
    {
        var panel = Stack(); var theme = new ComboBox { Header = "Vzhled", ItemsSource = new[] { "Podle Windows", "Tmavý Vintage", "Světlý Vintage" }, SelectedIndex = selectedTheme == "dark" ? 1 : selectedTheme == "light" ? 2 : 0, MinWidth = 200 };
        theme.SelectionChanged += (_,_) => ApplyTheme(theme.SelectedIndex == 1 ? "dark" : theme.SelectedIndex == 2 ? "light" : "system");
        theme.IsEnabled=persistenceReady;
        panel.Children.Add(theme); panel.Children.Add(Text("Vzhled a rozpracované lekce se ukládají průběžně. Poloha a velikost okna se uloží při zavření. Vysoký kontrast se řídí nastavením Windows.",true));
        panel.Children.Add(Text("Složka dat: " + userFiles.Root));
        var open = new Button { Content = "Otevřít složku dat" }; open.Click += async (_,_) =>
        {
            try { Directory.CreateDirectory(userFiles.Root); await Windows.System.Launcher.LaunchFolderAsync(await Windows.Storage.StorageFolder.GetFolderFromPathAsync(userFiles.Root)); }
            catch (Exception error) { ShellNotice.IsOpen = true; ShellNotice.Message = "Složku nelze otevřít. " + error.Message; }
        };
        panel.Children.Add(open);
        panel.Children.Add(BuildAudioDiagnostics());panel.Children.Add(BuildSecretSettings()); return Card(panel);
    }
    private async void About_Click(object sender, RoutedEventArgs args)
    {
        if (closingWindow) return;
        var dialog = new ContentDialog { XamlRoot = ShellRoot.XamlRoot, Title = "Rocker", Content = "Nativní kytarový pomocník pro Windows. Rozpracovaná verze 0.1.0.\nWinUI 3 a společné C++ zvukové jádro.\nLicence a samostatné zdroje jsou součástí podkladů k předání.", CloseButtonText = "Zavřít" };
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
        if (looperOperation) { ShowPersistenceError("Počkej na dokončení práce s Looperem."); return; }
        if (exportOperation) { ShowPersistenceError("Počkej na dokončení exportu nebo jej zruš v pohledu Export."); return; }
        if (backupOperationRunning) { ShowPersistenceError("Počkej na dokončení zálohy nebo obnovy."); return; }
        if (deviceOperationRunning) { ShowPersistenceError("Počkej na dokončení změny zvukového zařízení."); return; }
        if (secretOperationRunning) { ShowPersistenceError("Počkej na dokončení uložení nebo odstranění klíče."); return; }
        closingWindow = true;
        try
        {
            foreach (var guard in closeGuards.Values.Where(guard => guard.State.RequiresConfirmation).ToArray())
            {
                var dialog = new ContentDialog { XamlRoot = ShellRoot.XamlRoot, Title = "Zavřít Rocker?", Content = guard.State.Prompt, PrimaryButtonText = "Uložit a zavřít", SecondaryButtonText = guard.State.Recording ? "" : "Zahodit", CloseButtonText = "Zrušit", DefaultButton = ContentDialogButton.Primary };
                var choice = await dialog.ShowAsync();
                if (choice == ContentDialogResult.None) return;
                if (choice == ContentDialogResult.Primary && !await guard.Save()) return;
                if (choice == ContentDialogResult.Secondary) guard.Discard();
            }
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
        await VerifyDevicesAsync();
        VerifyComponents();
        if (Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTIC_VIEW") == "components") ShowComponents();
        await CaptureNativeViewAsync();
        WriteShellDiagnostic("shell-navigation.json",new { result = visitedViews.Count == 32 && restoredTask && audioHostsCreated == 1 ? "passed" : "failed", viewsVisited = visitedViews.Count, views = visitedViews.Order().ToArray(), backRestoredTask = restoredTask, audioHostsCreated, cachedNativePages = workspaceCache.Count, manualInteractionsVerified = false, completeFeaturesVerified = false });
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
