// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private readonly HttpClient updateHttp = ReleaseUpdates.CreateClient();
    private CancellationTokenSource? updateCancellation;
    private VerifiedRelease? availableUpdate;
    private string? downloadedUpdate, pendingUpdateInstaller;
    private bool updateOperation, updateInstalling;
    private TextBlock? updateStatus;
    private ProgressBar? updateProgress;
    private Button? updateCheckButton, updateDownloadButton, updateInstallButton, updateCancelButton;
    private CheckBox? updateAtStartup;
    private string updateMessage = "Kontrola a stažení jsou na tvoje vyžádání. Instalace se nikdy nespustí sama.";
    private (string Commit, long Run) InstalledBuild()
    {
        try
        {
            using var info = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "build-info.json")));
            string commit = info.RootElement.GetProperty("commit").GetString() ?? "", url = info.RootElement.GetProperty("runUrl").GetString() ?? "";
            long.TryParse(url[(url.LastIndexOf('/') + 1)..], out long run);
            return (commit, run);
        }
        catch (Exception) { return ("", 0); }
    }
    private FrameworkElement BuildUpdates()
    {
        var panel = Stack(); panel.Children.Add(Text("Aktualizace Rockeru"));
        var build = InstalledBuild(); panel.Children.Add(Text(build.Commit.Length >= 12 ? "Nainstalované sestavení: " + build.Commit[..12] : "Vývojové sestavení bez údajů vydání.", true));
        updateStatus = Text(updateMessage, true); Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(updateStatus, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite); panel.Children.Add(updateStatus);
        updateProgress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Visibility = Visibility.Collapsed }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(updateProgress, "Stažení aktualizace"); panel.Children.Add(updateProgress);
        updateCheckButton = PrimaryAction("Zkontrolovat nové vydání"); updateCheckButton.Click += async (_, _) => await CheckUpdatesAsync(false); panel.Children.Add(updateCheckButton);
        updateDownloadButton = new Button { Content = "Stáhnout ověřený instalátor", MinHeight = 40, IsEnabled = false }; updateDownloadButton.Click += async (_, _) => await DownloadUpdateAsync(); panel.Children.Add(updateDownloadButton);
        updateInstallButton = new Button { Content = "Nainstalovat a zavřít Rocker…", MinHeight = 40, IsEnabled = false }; updateInstallButton.Click += async (_, _) => await InstallUpdateAsync(); panel.Children.Add(updateInstallButton);
        updateCancelButton = new Button { Content = "Zrušit kontrolu / stažení", Visibility = Visibility.Collapsed }; updateCancelButton.Click += (_, _) => updateCancellation?.Cancel(); panel.Children.Add(updateCancelButton);
        var browser = new Button { Content = "Otevřít vydání v přihlášeném prohlížeči" }; browser.Click += async (_, _) => await Windows.System.Launcher.LaunchUriAsync(availableUpdate?.Release.Page ?? ReleaseUpdates.ReleasesPage); panel.Children.Add(browser);
        updateAtStartup = new CheckBox { Content = "Při spuštění nabídnout novou verzi", IsChecked = appSettings.CheckUpdatesOnStartup, IsEnabled = persistenceReady };
        void Startup() { appSettings = appSettings with { CheckUpdatesOnStartup = updateAtStartup.IsChecked == true }; SaveSettings(); }
        updateAtStartup.Checked += (_, _) => Startup(); updateAtStartup.Unchecked += (_, _) => Startup(); panel.Children.Add(updateAtStartup);
        panel.Children.Add(Text("Repozitář je soukromý. Pro kontrolu a stažení přímo z aplikace můžeš uložit vlastní GitHub token s přístupem jen k JendaNDT/rocker a oprávněním Contents: read. Bez tokenu použij prohlížeč přihlášený na GitHub a stáhni instalátor z vydání.", true));
        var access = Stack(); var token = new PasswordBox { Header = "GitHub token pro soukromé aktualizace", MaxLength = 4096 }; access.Children.Add(token);
        var keyState = Text("Token se chrání pro tvůj účet Windows; nepatří do záloh ani diagnostiky.", true); access.Children.Add(keyState);
        var save = new Button { Content = "Uložit přístup k aktualizacím" };
        save.Click += async (_, _) =>
        {
            if (updateOperation || closingWindow || secretOperationRunning) return;
            try { secretOperationRunning = true; await new SecretStore(userFiles, "github-updates").SetAsync(token.Password); token.Password = ""; keyState.Text = "Přístup je uložený. Oprávnění ověří tlačítko Zkontrolovat nové vydání."; }
            catch (Exception) { keyState.Text = "Přístup se nepodařilo uložit. Zkontroluj token a zkus to znovu."; }
            finally { secretOperationRunning = false; }
        }; access.Children.Add(save);
        var remove = new Button { Content = "Odstranit uložený přístup" };
        remove.Click += async (_, _) =>
        {
            if (updateOperation || closingWindow || secretOperationRunning) return;
            try { secretOperationRunning = true; await new SecretStore(userFiles, "github-updates").RemoveAsync(); token.Password = ""; keyState.Text = "Uložený přístup byl odstraněn."; }
            catch (Exception) { keyState.Text = "Přístup se nepodařilo odstranit."; }
            finally { secretOperationRunning = false; }
        }; access.Children.Add(remove);
        panel.Children.Add(new Expander { Header = "Volitelný přístup ke GitHubu", Content = access, HorizontalAlignment = HorizontalAlignment.Stretch });
        RefreshUpdateControls(); return Card(panel);
    }
    private void RefreshUpdateControls()
    {
        if (updateStatus is not null) updateStatus.Text = updateMessage;
        if (updateCheckButton is not null) updateCheckButton.IsEnabled = !updateOperation && !updateInstalling;
        if (updateDownloadButton is not null) updateDownloadButton.IsEnabled = availableUpdate is not null && !updateOperation && !updateInstalling;
        if (updateInstallButton is not null) updateInstallButton.IsEnabled = downloadedUpdate is not null && availableUpdate is not null && !updateOperation && !updateInstalling;
        if (updateCancelButton is not null) updateCancelButton.Visibility = updateOperation ? Visibility.Visible : Visibility.Collapsed;
        if (updateProgress is not null) updateProgress.Visibility = updateOperation ? Visibility.Visible : Visibility.Collapsed;
    }
    private async void Updates_Click(object sender, RoutedEventArgs args)
    {
        Navigate(new("28")); await Task.Yield();
        updateCheckButton?.StartBringIntoView(); updateCheckButton?.Focus(FocusState.Programmatic);
    }
    private async Task CheckUpdatesAsync(bool startup)
    {
        if (updateOperation || updateInstalling || closingWindow || closed || !persistenceReady) return;
        updateOperation = true; availableUpdate = null; downloadedUpdate = null;
        updateCancellation = new(TimeSpan.FromSeconds(90)); updateMessage = "Kontroluji ověřená Windows vydání na GitHubu…"; RefreshUpdateControls();
        if (updateProgress is not null) updateProgress.IsIndeterminate = true;
        try
        {
            var secret = await new SecretStore(userFiles, "github-updates").ReadAsync(updateCancellation.Token);
            var candidate = await new ReleaseUpdates(updateHttp).CheckAsync(secret.State == SecretState.Ready ? secret.Value : null, updateCancellation.Token);
            var installed = InstalledBuild();
            if (candidate is null) updateMessage = "Žádné ověřené Windows vydání nebylo nalezené.";
            else if (candidate.Commit == installed.Commit || installed.Run > 0 && candidate.RunId <= installed.Run) updateMessage = "Používáš stejné nebo novější sestavení než poslední vydání.";
            else if (installed.Run == 0) updateMessage = "Vývojové sestavení nelze porovnat s vydáním. Instalátor je dostupný v prohlížeči.";
            else
            {
                availableUpdate = candidate; updateMessage = "Je dostupná " + candidate.Release.Title + ". Můžeš stáhnout ověřený instalátor.";
                if (startup && !closed) { ShellNotice.IsOpen = true; ShellNotice.Severity = InfoBarSeverity.Informational; ShellNotice.Message = updateMessage + " Otevři Nápověda → Aktualizace."; }
            }
        }
        catch (OperationCanceledException) { updateMessage = "Kontrola byla zrušená nebo vypršel čas spojení. Současná aplikace zůstává beze změny."; }
        catch (UnauthorizedAccessException error) { updateMessage = error.Message;if(startup && !closed){ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Informational;ShellNotice.Message="Kontrola soukromých aktualizací potřebuje přístup ke GitHubu. Otevři Nápověda → Aktualizace.";} }
        catch (Exception) { updateMessage = "Nové vydání nelze ověřit. Zkus to později nebo otevři vydání v prohlížeči."; }
        finally { updateOperation = false; updateCancellation.Dispose(); updateCancellation = null; if (!closed) RefreshUpdateControls(); }
    }
    private async Task DownloadUpdateAsync()
    {
        if (availableUpdate is not { } candidate || updateOperation || closingWindow || closed) return;
        updateOperation = true; downloadedUpdate = null; updateCancellation = new(TimeSpan.FromMinutes(10)); updateMessage = "Stahuji instalátor…";
        if (updateProgress is not null) { updateProgress.IsIndeterminate = false; updateProgress.Value = 0; } RefreshUpdateControls();
        try
        {
            var secret = await new SecretStore(userFiles, "github-updates").ReadAsync(updateCancellation.Token);
            var progress = new Progress<double>(fraction => { if (!closed && updateProgress is not null) updateProgress.Value = fraction * 100; });
            downloadedUpdate = await new ReleaseUpdates(updateHttp).DownloadAsync(candidate, userFiles, secret.State == SecretState.Ready ? secret.Value : null, progress, updateCancellation.Token);
            updateMessage = "Instalátor je stažený a jeho SHA-256 ověřený. Instalace začne až po tlačítku Nainstalovat a zavřít Rocker.";
        }
        catch (OperationCanceledException) { updateMessage = "Stažení bylo zrušené. Neúplný soubor byl odstraněný."; }
        catch (UnauthorizedAccessException error) { updateMessage = error.Message; }
        catch (Exception) { updateMessage = "Instalátor se nepodařilo stáhnout nebo ověřit. Nebude spuštěný; zkus stažení znovu."; }
        finally { updateOperation = false; updateCancellation.Dispose(); updateCancellation = null; if (!closed) RefreshUpdateControls(); }
    }
    private async Task InstallUpdateAsync()
    {
        if (downloadedUpdate is not { } path || availableUpdate is not { } candidate || updateOperation || updateInstalling || pendingUpdateInstaller is not null || closingWindow) return;
        if (busy || workspaceOperation || deviceOperationRunning || fileOperation || recordingOperation || studioOperation || studioEditTask is { IsCompleted: false } || looperOperation || exportOperation || backupOperationRunning || profileOperation || secretOperationRunning)
        { ShowPersistenceError("Nejdřív dokonči probíhající práci. Stažený instalátor zůstane připravený."); return; }
        try
        {
            var dialog = new ContentDialog { PrimaryButtonStyle=(Style)Application.Current.Resources["VintageRootButtonStyle"],RequestedTheme = ShellRoot.RequestedTheme, XamlRoot = ShellRoot.XamlRoot, Title = "Nainstalovat aktualizaci?", Content = candidate.Release.Title + "\nRocker uloží práci, zastaví zvuk a zavře se. Potom se otevře běžný instalátor; tvoje data zůstanou zachovaná.", PrimaryButtonText = "Uložit a nainstalovat", CloseButtonText = "Zrušit", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            updateInstalling = true; RefreshUpdateControls();
            await ReleaseUpdates.VerifyInstallerAsync(path, candidate.Release.Installer);
            updateInstalling = false; pendingUpdateInstaller = path; Close();
        }
        catch (Exception) { updateInstalling = false; pendingUpdateInstaller = null; updateMessage = "Instalátor nelze bezpečně otevřít. Stáhni jej znovu."; RefreshUpdateControls(); }
    }
    private void LaunchPendingInstaller()
    {
        if (pendingUpdateInstaller is not { } path) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); pendingUpdateInstaller = null;
    }
}
