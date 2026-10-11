// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using System.Security.Cryptography;
using System.Text.Json;
namespace Rocker.Infrastructure;
public sealed record ProjectLoadResult(ProjectManifest Project, IReadOnlyList<string> MissingMedia, bool RecoveredPrevious);
public sealed class ProjectRepository(UserFiles files, DataRepository database)
{
    private readonly SemaphoreSlim writer = database.ProjectFilesWriter;
    public string DirectoryFor(string id)
    {
        if (!Guid.TryParse(id,out var guid)) throw new InvalidDataException("Neplatné ID projektu.");
        return OwnedPaths.Resolve(files.Root,"projects/"+guid.ToString("N"));
    }
    public async Task<string> AttachMediaAsync(string projectId, StoredMedia media, CancellationToken cancellation = default)
    {
        if (!await new OwnedMedia(files).VerifyAsync(media,cancellation)) throw new InvalidDataException("Zvukový soubor chybí nebo má jiný obsah.");
        string relative = "media/"+media.Sha256+Path.GetExtension(media.RelativePath);
        string destination = OwnedPaths.Resolve(DirectoryFor(projectId),relative); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!,".rocker-project-"+Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            await using (var input = File.OpenRead(OwnedPaths.Resolve(files.Root,media.RelativePath)))
            await using (var output = new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,128*1024,FileOptions.Asynchronous|FileOptions.WriteThrough))
            { await input.CopyToAsync(output,cancellation); await output.FlushAsync(cancellation); }
            await using (var input = File.OpenRead(temporary))
                if (Convert.ToHexStringLower(await SHA256.HashDataAsync(input,cancellation)) != media.Sha256) throw new InvalidDataException("Zvuk při kopírování změnil obsah.");
            cancellation.ThrowIfCancellationRequested();
            if (File.Exists(destination))
            {
                await using var input = File.OpenRead(destination);
                if (input.Length != media.Bytes || Convert.ToHexStringLower(await SHA256.HashDataAsync(input,cancellation)) != media.Sha256) throw new InvalidDataException("Existující stopa má jiný obsah.");
            }
            else File.Move(temporary,destination,false);
            return relative;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public async Task SaveAsync(ProjectManifest project, CancellationToken cancellation = default)
    {
        UserData.ValidateProject(project); await writer.WaitAsync(cancellation);
        try
        {
            string directory = DirectoryFor(project.Id); Directory.CreateDirectory(directory);
            foreach (var track in project.Tracks)
                if (!File.Exists(OwnedPaths.Resolve(directory,track.MediaPath))) throw new InvalidDataException("Projekt odkazuje na chybějící stopu: " + track.Name);
            string manifest = OwnedPaths.Resolve(directory,"project.rockerproj");
            string previous = OwnedPaths.Resolve(directory,"previous.rockerproj");
            if (File.Exists(manifest))
            {
                try { _ = ReadManifest(manifest); await UserFiles.WriteTextAsync(previous,await File.ReadAllTextAsync(manifest,cancellation),cancellation); }
                catch (Exception error) when (error is JsonException or InvalidDataException)
                { /* Preserve the known-good previous file when the primary is corrupt. */ }
            }
            await UserFiles.WriteTextAsync(manifest,JsonSerializer.Serialize(project,UserData.Json),cancellation);
            await database.SaveAsync([UserData.Document("project",project.Id,new ProjectIndex(project.Id,project.Name,"projects/"+Guid.Parse(project.Id).ToString("N")+"/project.rockerproj"))],cancellation:cancellation);
        }
        finally { writer.Release(); }
    }
    private static ProjectManifest ReadManifest(string path)
    {
        if (new FileInfo(path).Length > 4*1024*1024) throw new InvalidDataException("Projektový manifest je příliš velký.");
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var project = UserData.Read<ProjectManifest>(json.RootElement);
        if (project.SchemaVersion > 1) throw new NotSupportedException("Projekt pochází z novější verze Rockeru a nebude přepsán.");
        UserData.ValidateProject(project); return project;
    }
    public Task<ProjectLoadResult> LoadAsync(string id, CancellationToken cancellation = default) => Task.Run(() =>
    {
        string directory = DirectoryFor(id), primary = OwnedPaths.Resolve(directory,"project.rockerproj"); bool recovered = false;
        ProjectManifest project;
        try { project = ReadManifest(primary); }
        catch (Exception error) when (error is JsonException or FileNotFoundException) { project = ReadManifest(OwnedPaths.Resolve(directory,"previous.rockerproj")); recovered = true; }
        if (Guid.Parse(project.Id) != Guid.Parse(id)) throw new InvalidDataException("Projekt má jiné ID než jeho složka.");
        var missing = new List<string>();
        foreach (var track in project.Tracks)
        {
            cancellation.ThrowIfCancellationRequested(); string path = OwnedPaths.Resolve(directory,track.MediaPath);
            if (!File.Exists(path)) { missing.Add(track.Id); continue; }
            using var input = File.OpenRead(path);
            if (Convert.ToHexStringLower(SHA256.HashData(input)) != track.Sha256) missing.Add(track.Id);
        }
        return new ProjectLoadResult(project,missing,recovered);
    },cancellation);
    public async Task<ProjectManifest> ImportAsync(string manifestPath,CancellationToken cancellation=default)
    {
        var original=await Task.Run(()=>ReadManifest(Path.GetFullPath(manifestPath)),cancellation);var sourceDirectory=Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var project=original with{Id=Guid.NewGuid().ToString(),UpdatedAt=DateTimeOffset.UtcNow};var tracks=new List<ProjectTrack>();
        foreach(var track in original.Tracks)
        {
            cancellation.ThrowIfCancellationRequested();await using var input=File.OpenRead(OwnedPaths.Resolve(sourceDirectory,track.MediaPath));if(Convert.ToHexStringLower(await SHA256.HashDataAsync(input,cancellation))!=track.Sha256)throw new InvalidDataException("Přenášená stopa má jiný obsah: "+track.Name);input.Position=0;
            var owned=await new OwnedMedia(files).ImportAsync(input,Path.GetExtension(track.MediaPath),cancellation);var relative=await AttachMediaAsync(project.Id,owned,cancellation);tracks.Add(track with{MediaPath=relative});
        }
        project=project with{Tracks=tracks};await SaveAsync(project,cancellation);return project;
    }
    public async Task<string> ExportDirectoryAsync(string projectId,string parentDirectory,CancellationToken cancellation=default)
    {
        var loaded=await LoadAsync(projectId,cancellation);if(loaded.MissingMedia.Count>0)throw new InvalidDataException("Projekt má chybějící nebo změněné médium.");
        string parent=Path.GetFullPath(parentDirectory);Directory.CreateDirectory(parent);string name="Rocker-project-"+Guid.NewGuid().ToString("N"),temporary=Path.Combine(parent,"."+name+".tmp"),destination=Path.Combine(parent,name);Directory.CreateDirectory(temporary);
        try
        {
            foreach(var track in loaded.Project.Tracks.DistinctBy(t=>t.MediaPath))
            {
                string target=OwnedPaths.Resolve(temporary,track.MediaPath);Directory.CreateDirectory(Path.GetDirectoryName(target)!);await using var input=File.OpenRead(OwnedPaths.Resolve(DirectoryFor(projectId),track.MediaPath));await using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None,128*1024,FileOptions.Asynchronous|FileOptions.WriteThrough)){await input.CopyToAsync(output,cancellation);await output.FlushAsync(cancellation);}
                await using var copied=File.OpenRead(target);if(Convert.ToHexStringLower(await SHA256.HashDataAsync(copied,cancellation))!=track.Sha256)throw new InvalidDataException("Stopa při exportu změnila obsah.");
            }
            await UserFiles.WriteTextAsync(Path.Combine(temporary,"project.rockerproj"),JsonSerializer.Serialize(loaded.Project,UserData.Json),cancellation);cancellation.ThrowIfCancellationRequested();Directory.Move(temporary,destination);return destination;
        }
        finally{if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
    }
    public async Task<int> RebuildIndexAsync(CancellationToken cancellation = default)
    {
        var indices = new List<DataDocument>();
        foreach (var directory in Directory.EnumerateDirectories(files.DirectoryFor("projects")))
        {
            string name = Path.GetFileName(directory);
            if (!Guid.TryParseExact(name,"N",out var id)) continue;
            try
            {
                var result = await LoadAsync(id.ToString("D"),cancellation);
                indices.Add(UserData.Document("project",result.Project.Id,new ProjectIndex(result.Project.Id,result.Project.Name,"projects/"+name+"/project.rockerproj")));
            }
            catch (Exception error) when (error is InvalidDataException or JsonException or IOException) { /* Invalid projects remain on disk for recovery. */ }
        }
        if (indices.Count > 0) await database.SaveAsync(indices,cancellation:cancellation);
        return indices.Count;
    }
}
