// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Infrastructure;
if (args.Length == 3 && args[0] == "--crash-writer") { await StorageChecks.CrashWriterAsync(args[1],args[2]); return; }
var directory = Path.Combine(Path.GetTempPath(), "rocker-file-check-" + Guid.NewGuid().ToString("N"));
int checks = 0;
void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
try
{
    var files = new UserFiles(directory);
    var recording = files.RecordingPath(Guid.Parse("11111111-1111-1111-1111-111111111111"), ".wav");
    Check(Path.GetFileName(recording) == "11111111111111111111111111111111.wav" && Path.GetDirectoryName(recording) == Path.Combine(directory, "recordings"), "stable media ID path");
    try { files.DirectoryFor("../escape"); throw new Exception("Path traversal accepted"); }
    catch (ArgumentException) { Check(true, "unknown category rejected"); }
    try { files.RecordingPath(Guid.NewGuid(), "../escape"); throw new Exception("Invalid extension accepted"); }
    catch (ArgumentException) { Check(true, "invalid extension rejected"); }
    var path = Path.Combine(files.DirectoryFor("projects"), "český projekt.json");
    await UserFiles.WriteTextAsync(path, "původní");
    Check(await File.ReadAllTextAsync(path) == "původní", "Unicode file contents");
    await UserFiles.WriteTextAsync(path, "nový");
    Check(await File.ReadAllTextAsync(path) == "nový", "replace existing file");
    using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    try { await UserFiles.WriteTextAsync(path, "cancelled", cancellation.Token); throw new Exception("Cancelled write completed"); }
    catch (OperationCanceledException) { Check(await File.ReadAllTextAsync(path) == "nový", "cancelled write retains original"); }
    Check(!Directory.EnumerateFiles(Path.GetDirectoryName(path)!).Any(file => file.EndsWith(".tmp", StringComparison.Ordinal)), "temporary files cleaned up");
    var outside = new WindowRectangle(3000,-600,2000,1500).FitTo(new(0,0,1920,1040));
    Check(outside == new WindowRectangle(0,0,1920,1040), "removed monitor and oversized window restored into remaining work area");
    var tiny = new WindowRectangle(-500,500,100,100).FitTo(new(-1280,0,1280,720));
    Check(tiny.X == -640 && tiny.Y == 240 && tiny.Width == 640 && tiny.Height == 480, "negative monitor coordinates and minimum visible size");
    var windowStore = new WindowPreferencesStore(files);
    var preferences = new WindowPreferences(1, new(10,20,1000,800), true, "dark");
    await windowStore.SaveAsync(preferences);
    Check(windowStore.Load() == preferences, "window geometry and theme survive reload");
    await UserFiles.WriteTextAsync(Path.Combine(files.DirectoryFor("settings"),"window.json"), "{\"SchemaVersion\":99,\"Bounds\":null}");
    try { windowStore.Load(); throw new Exception("Corrupt window state accepted"); }
    catch (InvalidDataException) { Check(true, "future or incomplete geometry rejected"); }
    Console.WriteLine($"PASS: {checks} file ownership/replacement assertions.");
    int storageChecks = await StorageChecks.RunAsync(files);
    int secretChecks = await SecretChecks.RunAsync(files);
    int backupChecks = await BackupChecks.RunAsync(directory);
    int migrationChecks = await MigrationChecks.RunAsync(directory);
    if (args.Length == 1) await File.WriteAllTextAsync(args[0], System.Text.Json.JsonSerializer.Serialize(new { result="passed", fileChecks=checks, storageChecks, secretChecks, backupChecks, migrationChecks, dpapiVerified=OperatingSystem.IsWindows(), forcedTerminationVerified=true, manualHardwareVerified=false }));
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
