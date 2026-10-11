// SPDX-License-Identifier: AGPL-3.0-only
using System.Text;
namespace Rocker.Infrastructure;
public sealed class UserFiles
{
    public string Root { get; }
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rocker");
    public UserFiles(string? root = null) => Root = Path.GetFullPath(root ?? DefaultRoot);
    public string DirectoryFor(string category)
    {
        if (category is not ("recordings" or "projects" or "backups" or "logs" or "WebView2" or "database")) throw new ArgumentException("Neznámá složka dat.", nameof(category));
        var path = Path.Combine(Root, category); Directory.CreateDirectory(path); return path;
    }
    public string RecordingPath(Guid id, string extension)
    {
        if (extension is not (".wav" or ".mp3" or ".ogg" or ".webm" or ".m4a")) throw new ArgumentException("Neplatná přípona nahrávky.", nameof(extension));
        return Path.Combine(DirectoryFor("recordings"), id.ToString("N") + extension);
    }
    public static async Task WriteTextAsync(string path, string text, CancellationToken cancellation = default)
    {
        var destination = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".rocker-write-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellation);
                await stream.FlushAsync(cancellation);
            }
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
