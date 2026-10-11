// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace Rocker.Infrastructure;
public sealed class PreparedMigration(string directory,string archive,BackupPreview preview,IReadOnlyList<string> warnings,string? secret) : IAsyncDisposable
{
    public string Archive {get;}=archive;
    public BackupPreview Preview {get;}=preview;
    public IReadOnlyList<string> Warnings {get;}=warnings;
    [System.Text.Json.Serialization.JsonIgnore] internal string? Secret {get;}=secret;
    public bool HasSecret=>!string.IsNullOrEmpty(Secret);
    public override string ToString()=>$"{Preview.Documents} documents, {Preview.Recordings} recordings, {Warnings.Count} warnings";
    public ValueTask DisposeAsync(){if(Directory.Exists(directory))Directory.Delete(directory,true);return ValueTask.CompletedTask;}
}
public sealed record LegacyRecording(string Id,string Name,long CreatedAt,double DurationSec,string MimeType,IReadOnlyList<Marker> Markers,string File);
public sealed record LegacyCommitResult(RestoreResult Data,bool SecretImported,bool SecretFailed);
public sealed record MigrationManifest(string App,string Kind,int Version,string ExportedAt,IReadOnlyList<LegacyRecording> Recordings,IReadOnlyList<BackupFile> Files);
public sealed class LegacyImport(UserFiles files,DataRepository database,ContentCatalog catalog)
{
    public Task<PreparedMigration> PrepareAsync(string path,CancellationToken cancellation=default)=>Task.Run(async ()=>
    {
        string directory=OwnedPaths.Resolve(files.Root,"backups/migration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            LegacyMigrationPlan plan;var documents=new List<DataDocument>();var warnings=new List<string>();
            var stagedFiles=new UserFiles(Path.Combine(directory,"profile"));var media=new OwnedMedia(stagedFiles);
            if(Path.GetExtension(path).Equals(".json",StringComparison.OrdinalIgnoreCase))
            {
                if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Stará záloha je příliš velká.");
                plan=LegacyMigration.Parse(await File.ReadAllBytesAsync(path,cancellation),catalog);
                warnings.Add("Stará záloha JSON neobsahuje nahrávky. Pro jejich přenos použij export pro Windows z původního webového profilu.");
            }
            else
            {
                await using var input=File.OpenRead(path);if(input.Length>BackupService.MaximumArchiveBytes)throw new InvalidDataException("Migrační archiv je příliš velký.");
                using var archive=new ZipArchive(input,ZipArchiveMode.Read,true);
                if(archive.Entries.Count is <2 or >10002 || archive.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=archive.Entries.Count)throw new InvalidDataException("Neplatné nebo duplicitní soubory migrace.");
                var manifestEntry=archive.GetEntry("manifest.json") ?? throw new InvalidDataException("Manifest migrace chybí.");
                using var manifestJson=JsonDocument.Parse(await ReadBoundedAsync(manifestEntry,4*1024*1024,cancellation));var manifest=UserData.Read<MigrationManifest>(manifestJson.RootElement);
                if(manifest.App!="rocker" || manifest.Kind!="windows-migration" || manifest.Version!=1 || manifest.Files is null || manifest.Recordings is null || manifest.Recordings.Count>10000 || manifest.Files.Count+1!=archive.Entries.Count)throw new InvalidDataException("Nepodporovaná verze migrace.");
                var metadata=new Dictionary<string,BackupFile>(StringComparer.OrdinalIgnoreCase);long total=0;
                foreach(var file in manifest.Files)
                {
                    if(file is null || !UserData.ValidRelativePath(file.Path) || (file.Path!="settings.json" && !file.Path.StartsWith("audio/",StringComparison.Ordinal)) || !UserData.ValidHash(file.Sha256) || file.Bytes<=0 || file.Bytes>OwnedMedia.MaximumBytes || !metadata.TryAdd(file.Path,file))throw new InvalidDataException("Neplatné soubory migrace.");
                    total=checked(total+file.Bytes);if(total>BackupService.MaximumArchiveBytes)throw new InvalidDataException("Rozbalená migrace je příliš velká.");
                    var entry=archive.GetEntry(file.Path) ?? throw new InvalidDataException("Soubor migrace chybí.");if(entry.Length!=file.Bytes)throw new InvalidDataException("Velikost souboru migrace neodpovídá.");
                }
                if(!metadata.TryGetValue("settings.json",out var settingsMetadata))throw new InvalidDataException("Nastavení migrace chybí.");
                byte[] settings=await ReadBoundedAsync(archive.GetEntry("settings.json")!,4*1024*1024,cancellation);
                if(settings.LongLength!=settingsMetadata.Bytes || Convert.ToHexStringLower(SHA256.HashData(settings))!=settingsMetadata.Sha256)throw new InvalidDataException("Kontrolní součet nastavení neodpovídá.");
                plan=LegacyMigration.Parse(settings,catalog);
                var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"settings.json"};var ids=new HashSet<string>(StringComparer.Ordinal);
                foreach(var recording in manifest.Recordings)
                {
                    if(recording is null || !UserData.ValidRelativePath(recording.File) || !recording.File.StartsWith("audio/",StringComparison.Ordinal) || !metadata.TryGetValue(recording.File,out var file) || !used.Add(recording.File))throw new InvalidDataException("Neplatná reference nahrávky migrace.");
                    if(!UserData.ValidId(recording.Id) || !ids.Add(recording.Id)) {warnings.Add("Neplatné nebo duplicitní ID nahrávky bylo vynecháno.");continue;}
                    var entry=archive.GetEntry(recording.File)!;await using var sound=entry.Open();var stored=await media.ImportAsync(sound,Path.GetExtension(recording.File),cancellation);
                    if(stored.Bytes!=file.Bytes || stored.Sha256!=file.Sha256)throw new InvalidDataException("Kontrolní součet původní nahrávky neodpovídá.");
                    try
                    {
                        string name=recording.Name;
                        if(plan.Secret is {Length:>0} key)name=name.Replace(key,"[klíč odstraněn]",StringComparison.Ordinal);
                        var markers=recording.Markers?.Select(m=>m with{Label=plan.Secret is {Length:>0} secret ? m.Label.Replace(secret,"[klíč odstraněn]",StringComparison.Ordinal):m.Label}).ToArray() ?? [];
                        documents.Add(UserData.Document("recording",recording.Id,new RecordingData(recording.Id,name,recording.MimeType,recording.DurationSec,DateTimeOffset.FromUnixTimeMilliseconds(recording.CreatedAt),stored.RelativePath,stored.Bytes,stored.Sha256,markers)));
                    }
                    catch(Exception error)when(error is InvalidDataException or ArgumentException or NullReferenceException){warnings.Add("Nahrávka má neplatná metadata a byla vynechána.");}
                }
                if(!used.SetEquals(metadata.Keys))throw new InvalidDataException("Migrace obsahuje neodkazované soubory.");
            }
            documents.AddRange(plan.Documents);warnings.AddRange(plan.Warnings);
            if(documents.Count==0)throw new InvalidDataException("Migrační soubor nemá žádná použitelná data.");
            var stagedDatabase=await DataRepository.OpenAsync(stagedFiles,cancellation);await stagedDatabase.SaveAsync(documents,true,cancellation);
            string converted=Path.Combine(directory,"converted.rockerbackup");await new BackupService(stagedFiles,stagedDatabase).ExportAsync(converted,true,true,cancellation);
            var preview=await new BackupService(files,database).PreviewAsync(converted,cancellation);
            return new PreparedMigration(directory,converted,preview,warnings,plan.Secret);
        }
        catch{Directory.Delete(directory,true);throw;}
    },cancellation);
    public async Task<LegacyCommitResult> CommitAsync(PreparedMigration migration,RestoreStrategy strategy,bool importSecret=false,CancellationToken cancellation=default)
    {
        if(importSecret && migration.HasSecret && !OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Klíč nelze bezpečně převzít mimo Windows.");
        // Data restore is atomic/recoverable on its own. The separate key operation is never part of the ordinary ZIP/SQLite transaction.
        var result=await new BackupService(files,database).RestoreAsync(migration.Archive,migration.Preview,strategy,cancellation);
        bool imported=false,failed=false;
        if(importSecret && migration.Secret is {Length:>0} key)
        {
            try { await new SecretStore(files).SetAsync(key,cancellation);imported=true; }
            catch(Exception error) when(error is CryptographicException or IOException or UnauthorizedAccessException or OperationCanceledException or ArgumentException){failed=true;}
        }
        return new(result,imported,failed);
    }
    private static async Task<byte[]> ReadBoundedAsync(ZipArchiveEntry entry,int maximum,CancellationToken cancellation)
    {
        if(entry.Length>maximum)throw new InvalidDataException("JSON migrace je příliš velký.");
        using var output=new MemoryStream();await using var input=entry.Open();byte[] buffer=new byte[16384];
        while(true){int count=await input.ReadAsync(buffer,cancellation);if(count==0)break;if(output.Length+count>maximum)throw new InvalidDataException("JSON migrace je příliš velký.");await output.WriteAsync(buffer.AsMemory(0,count),cancellation);}
        return output.ToArray();
    }
}
