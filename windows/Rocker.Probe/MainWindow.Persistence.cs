// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private DataRepository? database => services.Storage?.Database;
    private Autosave? autosave => services.Storage?.Autosave;
    private AppSettings appSettings { get => services.Settings; set => services.Settings=value; }
    private bool autosaveObserverAttached;
    private readonly Dictionary<string,int> savedLessonPositions = [];
    private bool persistenceReady;
    private async Task InitializePersistenceAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        using var initialization=services.Operations.Begin("application","profile-load","Načítání nastavení a pracovních relací",cancellable:false);
        try
        {
            await services.InitializeStorageAsync();
            await InitializeRecordingsAsync();
            ResetLooperWorkspace();
            ResetStudioWorkspace();
            var snapshot = await database.SnapshotAsync();
            if (closed) return;
            var settings = snapshot.Documents.FirstOrDefault(d=>d.Kind=="settings" && d.Id=="default");
            if (settings is not null) appSettings = UserData.Read<AppSettings>(settings.Value);
            else appSettings = appSettings with { Theme=selectedTheme };
            var lessons = snapshot.Documents.FirstOrDefault(d=>d.Kind=="lessons" && d.Id=="default");
            if (lessons is not null)
            {
                var value = UserData.Read<LessonProgress>(lessons.Value);
                completedLessons.UnionWith(value.CompletedIds.Where(id=>catalog?.Lessons.Any(l=>l.Id==id)==true));
                foreach (var entry in value.Positions) savedLessonPositions[entry.Key]=entry.Value;
            }
            if(!autosaveObserverAttached){autosaveObserverAttached=true;autosave!.Failed += (_,_) => DispatcherQueue.TryEnqueue(()=>{if(!closed)ShowPersistenceError("Automatické uložení se nezdařilo. Změny zůstávají v paměti; zkus aplikaci zavřít pro opakování zápisu.");});}
            ApplyTheme(appSettings.Theme);
            persistenceReady = true;
            await InitializeAmplifierAsync(snapshot.Documents);
            await InitializeMetronomeAsync(snapshot.Documents);
            InitializeTuner(snapshot.Documents);
            InitializeTraining(snapshot.Documents);
            InitializeSongs(snapshot.Documents);
            InitializePracticeWorkspace(snapshot.Documents);
            InitializeVideoPractice(snapshot.Documents);
            InitializeAi(snapshot.Documents);if(practiceSession.Phase is PracticePhase.Running or PracticePhase.Paused)practiceSession.End();RefreshPracticeControls();
            await RestoreDevicePreferenceAsync();
            workspaceCache.Clear();if(!appSettings.Onboarded && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR")))Navigate(new("31"));else ShowWorkspace();
            if (snapshot.Warnings.Count > 0) ShowPersistenceError($"Při obnovení dat bylo upraveno nebo vynecháno {snapshot.Warnings.Count} neplatných záznamů. Původní databáze je zachovaná.");
        }
        catch (Exception)
        { ShowColdRecovery();ShowPersistenceError("Uložená data nelze otevřít. Původní soubory zůstávají zachované. Změny se nyní neukládají; obnov data ze zálohy nebo zkontroluj složku dat."); }
    }
    private void ShowPersistenceError(string message)
    { ShellNotice.IsOpen=true; ShellNotice.Severity=InfoBarSeverity.Error; ShellNotice.Message=message;if(desktopEnabled && HostWindow(documentContext.Current) is Desktop.DesktopWindow window)window.Status.Text=message; }
    private void SaveSettings()
    { if (persistenceReady) autosave!.Queue(UserData.Document("settings","default",appSettings)); }
    private void SaveLessonProgress()
    {
        if (!persistenceReady) return;
        foreach (var session in lessonSessions.Values) savedLessonPositions[session.Lesson.Id]=session.StepIndex;
        autosave!.Queue(UserData.Document("lessons","default",new LessonProgress(completedLessons.Order().ToArray(),new Dictionary<string,int>(savedLessonPositions))));
    }
    private async Task VerifyPersistenceAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if (!persistenceReady) throw new InvalidOperationException("Persistence did not initialize.");
        // The smoke script supplies a separate disposable profile, never the user's files.
        appSettings=appSettings with { Theme="light" }; ApplyTheme("light"); SaveSettings();
        var lesson=catalog!.Lessons[0];
        lessonSessions[lesson.Id]=LessonSession.Start(lesson).Next(); SaveLessonProgress();
        await autosave!.FlushAsync();
        var reopened=await DataRepository.OpenAsync(userFiles); var snapshot=await reopened.SnapshotAsync();
        var restored=UserData.Read<LessonProgress>(snapshot.Documents.Single(d=>d.Kind=="lessons").Value);
        WriteShellDiagnostic("persistence-view.json",new { result=restored.Positions[lesson.Id]==lessonSessions[lesson.Id].StepIndex ? "passed":"failed", savedLessonPosition=restored.Positions[lesson.Id], theme=UserData.Read<AppSettings>(snapshot.Documents.Single(d=>d.Kind=="settings").Value).Theme, reopenedDatabase=true, userProfileModified=false });
    }
}
