// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
using System.Text.Json;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private PracticeWorkspace practiceWorkspace = PracticeWorkspaceRules.Empty;
    private bool workspaceOperation;
    private void InitializePracticeWorkspace(IReadOnlyList<DataDocument> documents)
    {
        practiceWorkspace = documents.FirstOrDefault(d => d.Kind == "practice-workspace" && d.Id == "default") is { } item
            ? UserData.Read<PracticeWorkspace>(item.Value) : PracticeWorkspaceRules.Empty;
    }
    private async Task SavePracticeWorkspaceAsync(PracticeWorkspace value)
    {
        value = PracticeWorkspaceRules.Validate(value);
        autosave!.Queue(UserData.Document("practice-workspace", "default", value));
        practiceWorkspace = value;
        await autosave.FlushAsync();
        workspaceCache.Remove(new("01"));
        RefreshFavoriteButton();
    }
    private SearchEntry CurrentSearchEntry()
    {
        var target = navigation.Current;
        if (target.ViewId == "07" && song is { } s) return new("song:" + s.Id, s.Name, "Skladba", target, s.Id);
        return new("view:" + target.ViewId + (target.ItemId is null ? "" : ":" + target.ItemId), TaskTitle(target), WorkspaceViews.Get(target.ViewId).Group, target);
    }
    private void RefreshFavoriteButton()
    {
        if (!shellReady || catalog is null) return;
        bool favorite = practiceWorkspace.Favorites.Contains(CurrentSearchEntry().Key);
        FavoriteViewButton.Content = new FontIcon { Glyph = favorite ? "\uE735" : "\uE734" };
        string label = favorite ? "Odebrat pohled z oblíbených" : "Přidat pohled do oblíbených";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(FavoriteViewButton, label); ToolTipService.SetToolTip(FavoriteViewButton, label);
        FavoriteViewButton.IsEnabled = SearchButton.IsEnabled = persistenceReady && !workspaceOperation;
    }
    private async Task ToggleFavoriteAsync(string key)
    {
        if (!persistenceReady || workspaceOperation) return;
        var keys = practiceWorkspace.Favorites.ToList();
        if (!keys.Remove(key))
        {
            if (keys.Count >= 500) throw new InvalidOperationException("Nejdřív odeber některou z 500 oblíbených položek.");
            keys.Add(key);
        }
        await SavePracticeWorkspaceAsync(practiceWorkspace with { Favorites = keys });
    }
    private async void FavoriteView_Click(object sender, RoutedEventArgs args)
    {
        try { await ToggleFavoriteAsync(CurrentSearchEntry().Key); }
        catch (Exception) { ShowPersistenceError("Oblíbenou položku se nepodařilo uložit."); }
    }
    private async Task<SearchEntry[]> BuildSearchIndexAsync()
    {
        var index = WorkspaceViews.All.Select(v => new SearchEntry("view:" + v.Id, v.Title, v.Group, new(v.Id))).ToList();
        if (catalog is not null)
        {
            index.AddRange(catalog.Lessons.Select(l => new SearchEntry("view:06:" + l.Id, l.Title, "Lekce", new("06", l.Id))));
            foreach (var (group, view, category) in new[] { ("chords", "17", "Akord"), ("scales", "18", "Stupnice"), ("riffs", "19", "Riff"),
                ("surgicalExercises", "09", "Přesný trénink"), ("powerChordExercises", "10", "Power chordy"), ("gallopExercises", "11", "Gallop"), ("palmMuteDrills", "12", "Palm mute") })
                foreach (var item in catalog.Items(group))
                {
                    string id = item.GetProperty("id").GetString()!;
                    var target = new NavigationTarget(view, id);
                    index.Add(new("view:" + view + ":" + id, TaskTitle(target), category, target));
                }
        }
        index.AddRange(songs.Values.Select(s => new SearchEntry("song:" + s.Id, s.Name, "Skladba", new("07"), s.Id)));
        index.AddRange(practiceWorkspace.Setups.Select(s => new SearchEntry("setup:" + s.Id, s.Name, "Sestava cvičení", s.Target, s.Id)));
        if (recordingLibrary is not null) index.AddRange((await recordingLibrary.ListAsync()).Select(r => new SearchEntry("recording:" + r.Id, r.Name, "Nahrávka", new("14"), r.Id)));
        // References remain stable when a favorite's title changes. Missing files are
        // resolved on opening rather than executing an arbitrary stored command.
        return index.DistinctBy(e => e.Key).ToArray();
    }
    private async Task OpenSearchEntryAsync(SearchEntry entry)
    {
        if (workspaceOperation || busy || fileOperation || closingWindow || captureDraft is not null || backupOperationRunning || profileOperation)
            throw new InvalidOperationException("Nejdřív dokonči práci nebo zastav nahrávání.");
        if (entry.Key.StartsWith("setup:", StringComparison.Ordinal))
        {
            var setup = practiceWorkspace.Setups.FirstOrDefault(s => s.Id == entry.ResourceId) ?? throw new InvalidOperationException("Sestava už není dostupná.");
            await ApplyPracticeSetupAsync(setup);
        }
        else if (entry.Key.StartsWith("song:", StringComparison.Ordinal))
        {
            var selected = songs.GetValueOrDefault(entry.ResourceId!) ?? throw new InvalidOperationException("Skladba už není dostupná.");
            Navigate(new("07"));
            if (File.Exists(selected.SourcePath)) await OpenSongFileAsync(selected.SourcePath, selected.Name);
            else await RelocateSongAsync(selected);
        }
        else if (entry.Key.StartsWith("recording:", StringComparison.Ordinal))
        {
            var selected = (await recordingLibrary!.ListAsync()).FirstOrDefault(r => r.Id == entry.ResourceId) ?? throw new InvalidOperationException("Nahrávka už není dostupná.");
            Navigate(new("14"));
            await PreviewRecordingAsync(selected);
        }
        else Navigate(entry.Target);
    }
    private async void Search_Click(object sender, RoutedEventArgs args) => await ShowSearchAsync(false);
    private async void Favorites_Click(object sender, RoutedEventArgs args) => await ShowSearchAsync(true);
    private async Task ShowSearchAsync(bool favoritesOnly)
    {
        if (!persistenceReady || closingWindow || workspaceOperation) return;
        try
        {
            var index = await BuildSearchIndexAsync();
            var panel = Stack();
            var query = new TextBox { Header = "Najít v celém Rockeru", PlaceholderText = "Lekce, akord, skladba, nahrávka, sestava…", MaxLength = 256 };
            var only = new CheckBox { Content = "Pouze oblíbené", IsChecked = favoritesOnly };
            var list = new ListView { Header = "Výsledky", DisplayMemberPath = "Label", MaxHeight = 320, SelectionMode = ListViewSelectionMode.Single };
            var status = Text("", true);
            var favorite = new Button { Content = "Přidat do oblíbených", IsEnabled = false };
            panel.Children.Add(query); panel.Children.Add(only); panel.Children.Add(list); panel.Children.Add(status); panel.Children.Add(favorite);
            var dialog = new ContentDialog { RequestedTheme = ShellRoot.RequestedTheme, XamlRoot = ShellRoot.XamlRoot, Title = "Hledání a oblíbené", Content = panel, PrimaryButtonText = "Otevřít", CloseButtonText = "Zavřít", IsPrimaryButtonEnabled = false };
            void Refresh()
            {
                string? selected = (list.SelectedItem as SearchEntry)?.Key;
                var values = PracticeWorkspaceRules.Search(index, query.Text, practiceWorkspace.Favorites.ToHashSet(StringComparer.Ordinal), only.IsChecked == true);
                list.ItemsSource = values;
                list.SelectedItem = values.FirstOrDefault(e => e.Key == selected) ?? values.FirstOrDefault();
                status.Text = values.Length == 0 ? "Žádná odpovídající položka. Zkus kratší název nebo vypni filtr oblíbených." : $"{values.Length} výsledků" + (values.Length == 200 ? " · zobrazujeme prvních 200, zpřesni hledání" : "");
            }
            list.SelectionChanged += (_, _) =>
            {
                dialog.IsPrimaryButtonEnabled = favorite.IsEnabled = list.SelectedItem is SearchEntry;
                favorite.Content = list.SelectedItem is SearchEntry e && practiceWorkspace.Favorites.Contains(e.Key) ? "Odebrat z oblíbených" : "Přidat do oblíbených";
            };
            query.TextChanged += (_, _) => Refresh(); only.Checked += (_, _) => Refresh(); only.Unchecked += (_, _) => Refresh();
            favorite.Click += async (_, _) =>
            {
                if (list.SelectedItem is not SearchEntry selected) return;
                try { await ToggleFavoriteAsync(selected.Key); Refresh(); }
                catch (Exception) { status.Text = "Oblíbenou položku se nepodařilo uložit."; }
            };
            dialog.Opened += (_, _) => query.Focus(FocusState.Programmatic);
            Refresh(); LabelNativeControls(panel);activeWorkspaceDialog=dialog;
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && list.SelectedItem is SearchEntry result) await OpenSearchEntryAsync(result);
        }
        catch (Exception error) { ShowPersistenceError("Položku nelze otevřít. " + error.Message); }
        finally { activeWorkspaceDialog=null; }
    }
    private PracticeSetup CapturePracticeSetup(string name)
    {
        var transport = audio?.GetTransport();
        bool hasSong = song is not null && mediaPath == song.SourcePath && transport is { Ready: not 0 } && !studioOwnsPlayback;
        bool loop = hasSong && appliedSongLoopId == song!.Id && transport is { } t && (t.Reserved & 1) != 0;
        return PracticeWorkspaceRules.Validate(new PracticeSetup(Guid.NewGuid().ToString("D"), name, navigation.Current, ampSettings,
            appSettings.AmpEnabled, metroConfig, appSettings.TuningId, hasSong ? song!.Id : null,
            hasSong ? currentSpeed : 1, hasSong ? Math.Clamp(transport!.Value.PositionSeconds * currentSpeed, 0, song!.Duration) : 0,
            loop ? appliedSongLoopStart : 0, loop ? appliedSongLoopEnd : 0, loop, hasSong && playingRecording is { } recording && mediaPath==OwnedPaths.Resolve(userFiles.Root,recording.RelativePath)?recording.Id:null));
    }
    private async void SaveSetup_Click(object sender, RoutedEventArgs args)
    {
        if (!persistenceReady || closingWindow || workspaceOperation) return;
        try
        {
            if (practiceWorkspace.Setups.Count >= 100) throw new InvalidOperationException("Nejdřív odstraň některou ze 100 sestav.");
            var name = new TextBox { Header = "Název sestavy", Text = TaskTitle(navigation.Current), MaxLength = 120 };
            var panel = Stack(); panel.Children.Add(name); panel.Children.Add(Text("Uloží se tento pohled, ladění, nastavení aparátu a metronomu; u připravené skladby také rychlost, pozice a smyčka. Monitoring ani přehrávání se při otevření samy nezapnou.", true));
            var dialog = new ContentDialog { RequestedTheme = ShellRoot.RequestedTheme, XamlRoot = ShellRoot.XamlRoot, Title = "Uložit sestavu cvičení", Content = panel, PrimaryButtonText = "Uložit", CloseButtonText = "Zrušit", DefaultButton = ContentDialogButton.Primary };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(name.Text)) return;
            var setup = CapturePracticeSetup(name.Text);
            await SavePracticeWorkspaceAsync(practiceWorkspace with { Setups = [.. practiceWorkspace.Setups, setup] });
            ShellNotice.Severity = InfoBarSeverity.Success; ShellNotice.Message = "Sestava je uložená: " + setup.Name; ShellNotice.IsOpen = true;
        }
        catch (Exception error) { ShowPersistenceError("Sestavu nelze uložit. " + error.Message); }
    }
    private async void Setups_Click(object sender, RoutedEventArgs args)
    {
        if (!persistenceReady || closingWindow || workspaceOperation) return;
        try
        {
            var panel = Stack();
            var list = new ListView { Header = "Moje sestavy", DisplayMemberPath = "Name", MaxHeight = 300 };
            var name = new TextBox { Header = "Nový název vybrané sestavy", MaxLength = 120 };
            var status = Text("Vyber sestavu. Otevření připraví zvuk a nechá přehrávání i monitoring vypnutý.", true);
            var rename = new Button { Content = "Uložit nový název", IsEnabled = false };
            var remove = new Button { Content = "Odstranit vybranou sestavu…", IsEnabled = false };
            panel.Children.Add(list); panel.Children.Add(name); panel.Children.Add(rename); panel.Children.Add(remove); panel.Children.Add(status);
            var dialog = new ContentDialog { RequestedTheme = ShellRoot.RequestedTheme, XamlRoot = ShellRoot.XamlRoot, Title = "Sestavy cvičení", Content = panel, PrimaryButtonText = "Otevřít sestavu", CloseButtonText = "Zavřít", IsPrimaryButtonEnabled = false };
            void Refresh(string? id = null)
            {
                list.ItemsSource = practiceWorkspace.Setups.OrderBy(s => s.Name).ToArray();
                list.SelectedItem = practiceWorkspace.Setups.FirstOrDefault(s => s.Id == id);
                if (practiceWorkspace.Setups.Count == 0) status.Text = "Zatím žádná sestava. Připrav si cvičení a použij Uložit sestavu v nabídce Nástroje.";
            }
            list.SelectionChanged += (_, _) => { bool selected = list.SelectedItem is PracticeSetup; rename.IsEnabled = remove.IsEnabled = dialog.IsPrimaryButtonEnabled = selected; name.Text = (list.SelectedItem as PracticeSetup)?.Name ?? ""; };
            rename.Click += async (_, _) =>
            {
                if (list.SelectedItem is not PracticeSetup selected || string.IsNullOrWhiteSpace(name.Text)) return;
                try { await SavePracticeWorkspaceAsync(practiceWorkspace with { Setups = practiceWorkspace.Setups.Select(s => s.Id == selected.Id ? s with { Name = name.Text.Trim() } : s).ToArray() }); Refresh(selected.Id); }
                catch (Exception) { status.Text = "Název se nepodařilo uložit."; }
            };
            // A second click confirms deletion inside the same native dialog.
            string? deleting = null;
            remove.Click += async (_, _) =>
            {
                if (list.SelectedItem is not PracticeSetup selected) return;
                if (deleting != selected.Id) { deleting = selected.Id; status.Text = "Odstranit sestavu „" + selected.Name + "“? Dalším kliknutím potvrď. Skladba a presety zůstanou zachované."; return; }
                try { await SavePracticeWorkspaceAsync(practiceWorkspace with { Setups = practiceWorkspace.Setups.Where(s => s.Id != selected.Id).ToArray(), Favorites = practiceWorkspace.Favorites.Where(k => k != "setup:" + selected.Id).ToArray() }); deleting = null; Refresh(); status.Text = "Sestava byla odstraněna."; }
                catch (Exception) { status.Text = "Sestavu se nepodařilo odstranit."; }
            };
            Refresh(); LabelNativeControls(panel);activeWorkspaceDialog=dialog;
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && list.SelectedItem is PracticeSetup result) await ApplyPracticeSetupAsync(result);
        }
        catch (Exception error) { ShowPersistenceError("Sestavu nelze otevřít. " + error.Message); }
        finally { activeWorkspaceDialog=null; }
    }
    private async Task ApplyPracticeSetupAsync(PracticeSetup selected)
    {
        selected = PracticeWorkspaceRules.Validate(selected);
        if (audio is null || !persistenceReady || busy || workspaceOperation || powerTransition || deviceOperationRunning || fileOperation
            || captureDraft is not null || recordingOperation || studioOperation || studioEditTask is { IsCompleted: false } || looperOperation || backupOperationRunning || closingWindow)
            throw new InvalidOperationException("Nejdřív dokonči práci nebo zastav nahrávání.");
        workspaceOperation = true;ShellInteractionRoot.IsEnabled=false;
        try
        {
        if(audio.GetLoop().State is 1 or 2 or 4 or 5)throw new InvalidOperationException("Nejdřív dokonči nahrávání vrstvy v Looperu.");
        if (!AllTunings().Any(t => t.Id == selected.TuningId)) throw new InvalidOperationException("Ladění sestavy chybí. Obnov jej nebo sestavu ulož znovu.");
        SongSession? source = null;
        if (selected.SongId is not null)
        {
            source = songs.GetValueOrDefault(selected.SongId) ?? throw new InvalidOperationException("Skladba sestavy chybí. Otevři ji a sestavu ulož znovu.");
            if (selected.RecordingId is { } recordingId)
            {
                var owned=(await recordingLibrary!.ListAsync()).FirstOrDefault(r=>r.Id==recordingId)??throw new FileNotFoundException("Nahrávka sestavy chybí. Obnov ji ze zálohy nebo sestavu ulož znovu.");
                if(!await new OwnedMedia(userFiles).VerifyAsync(new(owned.RelativePath,owned.Sha256,owned.Bytes)))throw new InvalidDataException("Nahrávka sestavy chybí nebo je změněná.");
                source=source with{SourcePath=OwnedPaths.Resolve(userFiles.Root,owned.RelativePath)};
            }
            if (!File.Exists(source.SourcePath)) throw new FileNotFoundException("Soubor skladby chybí. Otevři skladbu ve Vlastní skladbě, vyber její nové umístění a zkus sestavu znovu.");
            if (selected.LoopEnabled && selected.LoopEnd > source.Duration) throw new InvalidDataException("Úsek sestavy už přesahuje délku skladby.");
        }
            if(powerTransition)throw new OperationCanceledException();
            audio.SetMonitoring(false, liveGain); await audio.StopAsync();
            metroEnabled = false; ++metroRevision; metroApplyTimer.Stop(); await ApplyMetronomeAsync();
            SendVideo(new { op = "pause" }); await StopRhythmUiAsync(); CancelPalm();
            if (source is not null)
            {
                await OpenSongFileAsync(source.SourcePath, source.Name,source);
                if (mediaPath != source.SourcePath || songLoadCancelled) throw new OperationCanceledException();
                await ChangeSongSpeedAsync(selected.Speed);
                if (songLoadCancelled) throw new OperationCanceledException();
                await audio.SeekAsync(Math.Min(selected.Position, song!.Duration) / currentSpeed);
                await audio.SetLoopAsync(selected.LoopStart / currentSpeed, selected.LoopEnd / currentSpeed, selected.LoopEnabled);
                appliedSongLoopId = song.Id; appliedSongLoopStart = selected.LoopStart; appliedSongLoopEnd = selected.LoopEnd;
                song = song with { Ramp = false }; SaveSong();
            }
            else { await CancelSongRampAsync(); await audio.ClearMediaAsync(); mediaPath = null; mediaMidi = null; song = null; songPeaks = []; MediaTitle.Text = "Připrav nahrávku nebo noty"; }
            ampSettings = selected.Amp; metroConfig = selected.Metronome; metroEnabled = false;
            appSettings = appSettings with { AmpEnabled = selected.AmpEnabled, TuningId = selected.TuningId }; SaveSettings();
            QueueAmplifier(); QueueMetronome(); await ApplyAmplifierAsync(); await ApplyMetronomeAsync();
            RefreshTuningSelector(); RebuildTunerStrings(); RebuildMetroBeats();
            await autosave!.FlushAsync();
            appliedNavigationTask=selected.Target;Navigate(selected.Target); RefreshState();
            ShellNotice.IsOpen = true; ShellNotice.Severity = InfoBarSeverity.Success; ShellNotice.Message = "Připravená sestava: " + selected.Name + ". Přehrávání a monitoring spustíš sám.";
        }
        finally { workspaceOperation = false;if(!closed)ShellInteractionRoot.IsEnabled=true;RefreshFavoriteButton(); }
    }
    private FrameworkElement BuildPracticeShortcuts()
    {
        var panel = Stack(); var title = Text("TVOJE CVIČENÍ"); title.Style = (Style)Application.Current.Resources["VintageSectionTextStyle"]; panel.Children.Add(title);
        var actions = ResponsiveColumns(Shortcut("Jen hraju · F11", FocusMode_Click), Shortcut("Sestavy cvičení", Setups_Click), Shortcut("Oblíbené a hledání · Ctrl+K", Favorites_Click)); panel.Children.Add(actions);
        foreach (var setup in practiceWorkspace.Setups.Take(3))
        {
            var button = new Button { Content = setup.Name + " · " + setup.Metronome.Bpm + " BPM", MinHeight = 40 };
            button.Click += async (_, _) => { try { await ApplyPracticeSetupAsync(setup); } catch (Exception error) { ShowPersistenceError(error.Message); } }; panel.Children.Add(button);
        }
        return Card(panel);
    }
    private static Button Shortcut(string label, RoutedEventHandler action)
    { var button = PrimaryAction(label); button.Click += action; return button; }
}
