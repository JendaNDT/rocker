// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using Rocker.Infrastructure;
using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
internal static class ReleaseUpdateChecks
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        { cancellation.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
    }
    public static async Task<int> RunAsync(string directory)
    {
        int checks = 0; void Check(bool condition, string name) { ++checks; if (!condition) throw new Exception(name); }
        byte[] installer = new byte[128]; installer[0] = (byte)'M'; installer[1] = (byte)'Z'; BinaryPrimitives.WriteInt32LittleEndian(installer.AsSpan(0x3c), 64); "PE\0\0"u8.CopyTo(installer.AsSpan(64)); BinaryPrimitives.WriteUInt16LittleEndian(installer.AsSpan(68), 0x014c);
        string name = "Rocker-Windows-Setup-0.2.0-x64.exe", digest = Convert.ToHexStringLower(SHA256.HashData(installer));
        byte[] proof = JsonSerializer.SerializeToUtf8Bytes(new { result = "passed", sourceCommit = new string('a', 40), windowsRun = "https://github.com/JendaNDT/rocker/actions/runs/12345", installer = name });
        object Asset(long id, string assetName, byte[] bytes) => new { id, name = assetName, size = bytes.Length, digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes)) };
        object Release(string tag, bool draft, string date) => new { tag_name = tag, name = "Test candidate", draft, prerelease = true, published_at = date, assets = new[] { Asset(1, name, installer), Asset(2, "candidate-validation.json", proof) } };
        byte[] list = JsonSerializer.SerializeToUtf8Bytes(new[] { Release("windows-0.2.0-rc5", true, "2026-10-14T00:00:00Z"), Release("windows-0.2.0-rc4", false, "2026-10-13T00:00:00Z"), Release("windows-0.2.0-rc3", false, "2026-10-12T00:00:00Z") });
        using var parsed = JsonDocument.Parse(list); var release = ReleaseUpdates.SelectRelease(parsed.RootElement)!;
        Check(release.Tag == "windows-0.2.0-rc4" && release.Installer.Sha256 == digest, "private prerelease selection excludes drafts and chooses the most recently published Windows package");
        var verified = ReleaseUpdates.ValidateCandidate(release, proof);
        Check(verified.RunId == 12345 && verified.Commit == new string('a', 40), "release proof is hashed and tied to a successful Windows run");
        byte[] tampered = proof.ToArray(); tampered[0] = (byte)'x'; bool rejected = false;
        try { ReleaseUpdates.ValidateCandidate(release, tampered); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "altered candidate proof is rejected before download");
        var requests = new List<(string Host, string? Authorization)>();
        using var handler = new Handler(request =>
        {
            requests.Add((request.RequestUri!.Host, request.Headers.Authorization?.Parameter));
            if (request.RequestUri.Host == "release-assets.githubusercontent.com") return new(HttpStatusCode.OK) { Content = new ByteArrayContent(installer) };
            if (request.RequestUri.AbsolutePath.EndsWith("/assets/1")) return new(HttpStatusCode.Found) { Headers = { Location = new("https://release-assets.githubusercontent.com/test-installer") } };
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(request.RequestUri.AbsolutePath.EndsWith("/assets/2") ? proof : list) };
        });
        using var http = new HttpClient(handler); var updater = new ReleaseUpdates(http);
        const string canary = "SYNTHETIC_GITHUB_ACCESS_CANARY";
        var checkedRelease = await updater.CheckAsync(canary);
        Check(checkedRelease?.Commit == verified.Commit, "HTTP metadata and asset proof produce the checked candidate");
        var files = new UserFiles(Path.Combine(directory, "release-update-profile"));
        string destination = await updater.DownloadAsync(verified, files, canary);
        Check((await File.ReadAllBytesAsync(destination)).SequenceEqual(installer), "only the complete digest-verified Windows executable is published into the update cache");
        Check(requests.Where(r => r.Host == "api.github.com").All(r => r.Authorization == canary) && requests.Where(r => r.Host != "api.github.com").All(r => r.Authorization is null), "private GitHub authorization never follows the asset redirect");
        installer[100] = 1; bool damaged = false;
        try { await updater.DownloadAsync(verified, files, canary); } catch (InvalidDataException) { damaged = true; }
        Check(damaged && (await File.ReadAllBytesAsync(destination))[100] == 0 && !Directory.EnumerateFiles(files.DirectoryFor("updates")).Any(p => p.EndsWith(".tmp")), "corrupt download removes partial output and retains the previous verified installer");
        using var unsafeHttp = new HttpClient(new Handler(_ => new(HttpStatusCode.Found) { Headers = { Location = new("https://example.com/installer") } }));
        bool unsafeRedirect = false; try { await new ReleaseUpdates(unsafeHttp).CheckAsync(canary); } catch (IOException) { unsafeRedirect = true; }
        Check(unsafeRedirect, "off-domain update redirect is rejected");
        using var deniedHttp = new HttpClient(new Handler(_ => new(HttpStatusCode.NotFound))); bool denied = false;
        try { await new ReleaseUpdates(deniedHttp).CheckAsync(null); } catch (UnauthorizedAccessException error) { denied = error.Message.Contains("prohlížeč", StringComparison.Ordinal); }
        Check(denied, "private repository denial gives an authenticated-browser fallback without guessing a download");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); bool cancelled = false;
        try { await updater.DownloadAsync(verified, files, canary, cancellation: cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && File.Exists(destination) && !Directory.EnumerateFiles(files.DirectoryFor("updates")).Any(p => p.EndsWith(".tmp")), "cancelled download keeps the verified previous executable and no partial file");
        await File.WriteAllBytesAsync(destination, installer); bool modified = false;
        try { await ReleaseUpdates.VerifyInstallerAsync(destination, verified.Release.Installer); } catch (InvalidDataException) { modified = true; }
        Check(modified, "a cache file modified after download cannot pass the pre-launch verification");
        var database = await DataRepository.OpenAsync(files);
        var amp = AmpRules.Sanitize(JsonSerializer.SerializeToElement(new { drive = .41 }));
        var metro = MetronomeRules.Sanitize(JsonSerializer.SerializeToElement(new { bpm = 91 }));
        var workspace = new PracticeWorkspace([new(Guid.NewGuid().ToString("D"), "Moje sestava", new("02"), amp, true, metro, "standard")], ["view:02"]);
        await database.SaveAsync([UserData.Document("practice-workspace", "default", workspace)]);
        var snapshot = await (await DataRepository.OpenAsync(files)).SnapshotAsync();
        Check(UserData.Read<PracticeWorkspace>(snapshot.Documents.Single().Value).Setups[0].Amp.Drive == .41, "practice setups and favorites survive an actual SQLite reopen");
        string backup = Path.Combine(directory, "practice-workspace.rockerbackup"); await new BackupService(files, database).ExportAsync(backup);
        var targetFiles = new UserFiles(Path.Combine(directory, "practice-workspace-restored")); var target = await DataRepository.OpenAsync(targetFiles);
        var restoreService=new BackupService(targetFiles,target);var preview=await restoreService.PreviewAsync(backup);await restoreService.RestoreAsync(backup,preview,RestoreStrategy.ReplaceIncluded);
        var restored = await target.SnapshotAsync(); Check(UserData.Read<PracticeWorkspace>(restored.Documents.Single().Value).Favorites.SequenceEqual(["view:02"]), "full backup restores practice workspace and excludes downloaded installers");
        Check(!Directory.Exists(Path.Combine(targetFiles.Root, "updates")), "update cache is not transferred as user data");
        Console.WriteLine($"PASS: {checks} release updates and practice workspace storage assertions (synthetic HTTP, no installer executed)."); return checks;
    }
}
