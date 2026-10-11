// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using System.Security.Cryptography;
namespace Rocker.Infrastructure;
public static class OwnedPaths
{
    public static string Resolve(string root, string relative)
    {
        if (!UserData.ValidRelativePath(relative)) throw new InvalidDataException("Cesta souboru není platná.");
        var fullRoot = Path.GetFullPath(root); var destination = Path.GetFullPath(Path.Combine(fullRoot,relative.Replace('/',Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(fullRoot.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new InvalidDataException("Soubor leží mimo projekt.");
        if (Directory.Exists(fullRoot) && (File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Datová složka je odkazem na jiný prostor.");
        var current = fullRoot;
        foreach (var part in relative.Split('/'))
        {
            current = Path.Combine(current,part);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Odkazované soubory v datové složce nejsou podporované.");
        }
        return destination;
    }
}
public sealed record StoredMedia(string RelativePath, string Sha256, long Bytes);
public sealed class OwnedMedia(UserFiles files)
{
    public const long MaximumBytes = 1024L*1024*1024;
    public async Task<StoredMedia> ImportAsync(Stream source, string extension, CancellationToken cancellation = default)
    {
        if (extension is not (".wav" or ".mp3" or ".ogg" or ".webm" or ".m4a")) throw new InvalidDataException("Formát souboru není podporovaný.");
        var directory = files.DirectoryFor("recordings"); var temporary = Path.Combine(directory,".rocker-media-"+Guid.NewGuid().ToString("N")+".tmp");
        long total = 0; using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            await using (var output = new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,128*1024,FileOptions.Asynchronous|FileOptions.WriteThrough))
            {
                var buffer = new byte[128*1024];
                while (true)
                {
                    int count = await source.ReadAsync(buffer,cancellation); if (count == 0) break;
                    total += count; if (total > MaximumBytes) throw new InvalidDataException("Zvukový soubor přesahuje limit 1 GB.");
                    hash.AppendData(buffer,0,count); await output.WriteAsync(buffer.AsMemory(0,count),cancellation);
                }
                if (total == 0) throw new InvalidDataException("Zvukový soubor je prázdný.");
                await output.FlushAsync(cancellation);
            }
            string digest = Convert.ToHexStringLower(hash.GetHashAndReset()); string relative = "recordings/"+digest+extension;
            string destination = OwnedPaths.Resolve(files.Root,relative);
            cancellation.ThrowIfCancellationRequested();
            if (File.Exists(destination))
            {
                await using var existing = File.OpenRead(destination);
                if (existing.Length != total || Convert.ToHexStringLower(await SHA256.HashDataAsync(existing,cancellation)) != digest) throw new InvalidDataException("Existující zvukový soubor má jiný obsah.");
            }
            else File.Move(temporary,destination,false);
            return new(relative,digest,total);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public async Task<bool> VerifyAsync(StoredMedia media, CancellationToken cancellation = default)
    {
        string path = OwnedPaths.Resolve(files.Root,media.RelativePath);
        if (!File.Exists(path)) return false;
        await using var stream = File.OpenRead(path);
        return stream.Length == media.Bytes && Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,cancellation)) == media.Sha256;
    }
}
