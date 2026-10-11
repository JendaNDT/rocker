// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace Rocker.Infrastructure;
public enum WorkspaceFileKind{Audio,Score,Project}
public sealed record RecentFile(string Path,DateTimeOffset OpenedAt)
{
    public string Label=>System.IO.Path.GetFileName(Path);
}
public sealed class RecentFiles(UserFiles files)
{
    private string StorePath=>Path.Combine(files.DirectoryFor("settings"),"recent-files.json");
    public static WorkspaceFileKind Kind(string path)=>Path.GetExtension(path).ToLowerInvariant() switch{
        ".wav" or ".mp3" or ".ogg" or ".opus" or ".webm" or ".m4a" or ".mp4"=>WorkspaceFileKind.Audio,
        ".gp3" or ".gp4" or ".gp5" or ".gpx" or ".gp" or ".musicxml" or ".xml" or ".mxl"=>WorkspaceFileKind.Score,
        ".rockerproj"=>WorkspaceFileKind.Project,_=>throw new InvalidDataException("Tento typ souboru Rocker neotevře.")};
    public static string ValidatePath(string path){if(string.IsNullOrWhiteSpace(path) || path.Length>32767 || path.Any(char.IsControl) || !Path.IsPathFullyQualified(path))throw new InvalidDataException("Cesta souboru není platná.");_ =Kind(path);return Path.GetFullPath(path);}
    public IReadOnlyList<RecentFile> Load()
    {
        try{if(!File.Exists(StorePath))return [];if(new FileInfo(StorePath).Length>1024*1024)return [];return (JsonSerializer.Deserialize<RecentFile[]>(File.ReadAllBytes(StorePath))??[]).Where(v=>{try{_ =ValidatePath(v.Path);return true;}catch(Exception){return false;}}).DistinctBy(v=>v.Path,StringComparer.OrdinalIgnoreCase).Take(24).ToArray();}catch(Exception){return [];}
    }
    public async Task SaveAsync(IEnumerable<RecentFile> values,CancellationToken cancellation=default)
    {var valid=values.Select(v=>v with{Path=ValidatePath(v.Path)}).DistinctBy(v=>v.Path,StringComparer.OrdinalIgnoreCase).Take(24).ToArray();await UserFiles.WriteTextAsync(StorePath,JsonSerializer.Serialize(valid),cancellation);}
}
