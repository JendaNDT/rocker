// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Infrastructure;
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
    Console.WriteLine($"PASS: {checks} file ownership/replacement assertions; SQLite and data migration remain W09–W12.");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
