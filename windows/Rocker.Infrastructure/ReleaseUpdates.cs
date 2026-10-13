// SPDX-License-Identifier: AGPL-3.0-only
using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Rocker.Domain;
namespace Rocker.Infrastructure;

public sealed record ReleaseAsset(long Id, string Name, long Bytes, string Sha256);
public sealed record WindowsRelease(string Tag, string Title, DateTimeOffset PublishedAt, ReleaseAsset Installer, ReleaseAsset Validation)
{
    public Uri Page => new("https://github.com/JendaNDT/rocker/releases/tag/" + Tag);
}
public sealed record VerifiedRelease(WindowsRelease Release, string Commit, long RunId);
public sealed class ReleaseUpdates(HttpClient client)
{
    public static Uri ReleasesPage => new("https://github.com/JendaNDT/rocker/releases");
    private const string Api = "https://api.github.com/repos/JendaNDT/rocker/";
    public static HttpClient CreateClient() => new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(10) };
    private static ReleaseAsset ReadAsset(JsonElement asset)
    {
        long id = asset.GetProperty("id").GetInt64(), size = asset.GetProperty("size").GetInt64();
        string name = asset.GetProperty("name").GetString()!, digest = asset.GetProperty("digest").GetString() ?? "";
        if (id <= 0 || size is <= 0 or > 512L * 1024 * 1024 || !digest.StartsWith("sha256:", StringComparison.Ordinal) || !UserData.ValidHash(digest[7..]))
            throw new InvalidDataException("Vydání nemá ověřitelný kontrolní součet souboru.");
        return new(id, name, size, digest[7..]);
    }
    public static bool IsInstallerName(string? name)
    {
        const string prefix="Rocker-Windows-Setup-",suffix="-x64.exe";
        return name is {Length:<100} && name.StartsWith(prefix,StringComparison.Ordinal) && name.EndsWith(suffix,StringComparison.Ordinal)
            && name[prefix.Length..^suffix.Length].Count(c=>c=='.')==2 && Version.TryParse(name[prefix.Length..^suffix.Length],out _);
    }
    public static WindowsRelease? SelectRelease(JsonElement list)
    {
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > 100) throw new InvalidDataException("Neplatný seznam vydání.");
        var releases = new List<WindowsRelease>();
        foreach (var release in list.EnumerateArray())
        {
            string tag = release.GetProperty("tag_name").GetString() ?? "";
            if (release.GetProperty("draft").GetBoolean() || !tag.StartsWith("windows-", StringComparison.Ordinal) || tag.Length > 128 || tag.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '.')) continue;
            var assets = release.GetProperty("assets").EnumerateArray().ToArray();
            var installers = assets.Where(a => IsInstallerName(a.GetProperty("name").GetString())).ToArray();
            var validations = assets.Where(a => a.GetProperty("name").GetString() == "candidate-validation.json").ToArray();
            if (installers.Length != 1 || validations.Length != 1) continue;
            var installer = ReadAsset(installers[0]); var validation = ReadAsset(validations[0]);
            if (validation.Bytes > 64 * 1024) throw new InvalidDataException("Ověření vydání je příliš velké.");
            releases.Add(new(tag, release.GetProperty("name").GetString() ?? tag, release.GetProperty("published_at").GetDateTimeOffset(), installer, validation));
        }
        return releases.OrderByDescending(r => r.PublishedAt).FirstOrDefault();
    }
    public static VerifiedRelease ValidateCandidate(WindowsRelease release, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != release.Validation.Bytes || Convert.ToHexStringLower(SHA256.HashData(bytes)) != release.Validation.Sha256)
            throw new InvalidDataException("Ověření vydání nesouhlasí s kontrolním součtem GitHubu.");
        using var document = JsonDocument.Parse(bytes.ToArray()); var value = document.RootElement;
        string commit = value.GetProperty("sourceCommit").GetString() ?? "", url = value.GetProperty("windowsRun").GetString() ?? "";
        const string prefix = "https://github.com/JendaNDT/rocker/actions/runs/";
        if (value.GetProperty("result").GetString() != "passed" || commit.Length != 40 || commit.Any(c => !char.IsAsciiHexDigit(c))
            || !url.StartsWith(prefix, StringComparison.Ordinal) || !long.TryParse(url[prefix.Length..], out long run) || run <= 0
            || value.GetProperty("installer").GetString() != release.Installer.Name
            || value.GetProperty("nativeWorkspaces").GetInt32()<32 || value.GetProperty("nativeSuites").GetInt32()<22)
            throw new InvalidDataException("Vydání neobsahuje úspěšné ověření Windows instalátoru.");
        return new(release, commit, run);
    }
    private async Task<HttpResponseMessage> GetAsync(string url, string? token, bool binary, CancellationToken cancellation)
    {
        Uri current = new(url);
        for (int redirects = 0; redirects < 5; ++redirects)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("Rocker-Windows/0.2");
            if (current.Host == "api.github.com")
            {
                request.Headers.Accept.Add(new(binary ? "application/octet-stream" : "application/vnd.github+json"));
                request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var location = response.Headers.Location; response.Dispose();
                if (location is null) throw new IOException("GitHub vrátil neplatné přesměrování.");
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                if (current.Scheme != "https" || current.UserInfo.Length != 0 || !current.IsDefaultPort || current.Host is not ("api.github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                    throw new IOException("Stažení bylo přesměrováno mimo podporované servery GitHubu.");
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode; response.Dispose();
                if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                    throw new UnauthorizedAccessException("Soukromé vydání vyžaduje přístup k repozitáři. Použij přihlášený prohlížeč nebo volitelný GitHub token s oprávněním Contents: read pro JendaNDT/rocker.");
                throw new IOException("Kontrola GitHubu nebyla dostupná. Zkus ji později.");
            }
            return response;
        }
        throw new IOException("GitHub vrátil příliš mnoho přesměrování.");
    }
    private async Task<byte[]> ReadBoundedAsync(string url, string? token, bool binary, int maximum, CancellationToken cancellation)
    {
        using var response = await GetAsync(url, token, binary, cancellation);
        if (response.Content.Headers.ContentLength > maximum) throw new InvalidDataException("Odpověď GitHubu je příliš velká.");
        await using var source = await response.Content.ReadAsStreamAsync(cancellation); using var buffer = new MemoryStream();
        var block = new byte[8192]; int count;
        while ((count = await source.ReadAsync(block, cancellation)) > 0)
        { if (buffer.Length + count > maximum) throw new InvalidDataException("Odpověď GitHubu je příliš velká."); buffer.Write(block, 0, count); }
        return buffer.ToArray();
    }
    public async Task<VerifiedRelease?> CheckAsync(string? token, CancellationToken cancellation = default)
    {
        var bytes = await ReadBoundedAsync(Api + "releases?per_page=30", token, false, 2 * 1024 * 1024, cancellation);
        using var list = JsonDocument.Parse(bytes); var release = SelectRelease(list.RootElement);
        if (release is null) return null;
        var proof = await ReadBoundedAsync(Api + "releases/assets/" + release.Validation.Id, token, true, 64 * 1024, cancellation);
        return ValidateCandidate(release, proof);
    }
    public async Task<string> DownloadAsync(VerifiedRelease release, UserFiles files, string? token, IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        var asset = release.Release.Installer;
        string directory = files.DirectoryFor("updates"), destination = Path.Combine(directory, "Rocker-Setup-" + asset.Sha256 + ".exe"), temporary = Path.Combine(directory, ".download-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using var response = await GetAsync(Api + "releases/assets/" + asset.Id, token, true, cancellation);
            if (response.Content.Headers.ContentLength is long length && length != asset.Bytes) throw new InvalidDataException("Velikost instalátoru nesouhlasí s vydáním.");
            await using (var source = await response.Content.ReadAsStreamAsync(cancellation))
            await using (var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                var block = new byte[65536]; long total = 0; int count;
                while ((count = await source.ReadAsync(block, cancellation)) > 0)
                {
                    total += count; if (total > asset.Bytes) throw new InvalidDataException("Instalátor překročil deklarovanou velikost.");
                    await target.WriteAsync(block.AsMemory(0, count), cancellation); progress?.Report(total / (double)asset.Bytes);
                }
                if (total != asset.Bytes) throw new IOException("Stažení instalátoru nebylo úplné.");
                await target.FlushAsync(cancellation);
            }
            await VerifyInstallerAsync(temporary, asset, cancellation);
            cancellation.ThrowIfCancellationRequested(); File.Move(temporary, destination, true); return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static async Task VerifyInstallerAsync(string path, ReleaseAsset asset, CancellationToken cancellation = default)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (file.Length != asset.Bytes || Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellation)) != asset.Sha256)
            throw new InvalidDataException("Instalátor nesouhlasí s kontrolním součtem vydání.");
        file.Position = 0; byte[] header = new byte[64]; await file.ReadExactlyAsync(header, cancellation);
        int offset = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0x3c));
        if (header[0] != 'M' || header[1] != 'Z' || offset < 64 || offset > file.Length - 6) throw new InvalidDataException("Stažený soubor není Windows instalátor.");
        file.Position = offset; byte[] pe = new byte[6]; await file.ReadExactlyAsync(pe, cancellation);
        // Inno Setup's bootstrap is x86; it contains and installs the x64 app.
        if (!pe.AsSpan(0, 4).SequenceEqual("PE\0\0"u8) || BinaryPrimitives.ReadUInt16LittleEndian(pe.AsSpan(4)) is not (0x014c or 0x8664))
            throw new InvalidDataException("Stažený soubor nemá podporovaný Windows formát.");
    }
}
