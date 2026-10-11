// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Rocker.Infrastructure;
public sealed record BackupFile(string Path,long Bytes,string Sha256);
public sealed record BackupManifest(int SchemaVersion,DateTimeOffset CreatedAt,bool IncludesRecordings,bool IncludesProjects,IReadOnlyList<BackupFile> Files);
public sealed record BackupPreview(string Sha256,int Documents,int Recordings,int Projects,long Bytes,int Conflicts,bool IncludesRecordings,bool IncludesProjects);
public enum RestoreStrategy { MergeKeepCurrent, ReplaceIncluded }
public sealed record RestoreResult(int Imported,int KeptCurrent,string RollbackDirectory);
public sealed class BackupService(UserFiles files,DataRepository database)
{
    public const long MaximumArchiveBytes=2L*1024*1024*1024;
    private static readonly string[] Categories=["database","recordings","projects"];
    private sealed record RestoreJournal(int Version,string Stage,string Rollback,IReadOnlyList<string> Existing,bool Committed);
    private sealed record ArchiveData(string Stage,BackupManifest Manifest,DataDocument[] Documents,string Hash);
    private string JournalPath => Path.Combine(files.DirectoryFor("settings"),"restore-journal.json");
    public async Task ExportAsync(string destination,bool includeRecordings=true,bool includeProjects=true,CancellationToken cancellation=default)
    {
        await database.ExclusiveAsync(async ()=>
        {
            var snapshot=await database.SnapshotAsync(cancellation);
            if(snapshot.Warnings.Count>0)throw new InvalidDataException("Záloha se nevytvořila: nejprve vyřeš varování uložených dat.");
            var documents=snapshot.Documents.Where(d=>(includeRecordings || d.Kind!="recording") && (includeProjects || d.Kind!="project")).ToArray();
            // Only typed documents and explicitly referenced media enter the archive.
            var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var doc in documents)
            {
                if(doc.Kind=="recording")paths.Add(UserData.Read<RecordingData>(doc.Value).RelativePath);
                if(doc.Kind=="project")
                {
                    var index=UserData.Read<ProjectIndex>(doc.Value);paths.Add(index.RelativeManifest);
                    string root=Path.GetDirectoryName(OwnedPaths.Resolve(files.Root,index.RelativeManifest))!;
                    var project=ReadProject(OwnedPaths.Resolve(files.Root,index.RelativeManifest));
                    if(project.Id!=index.Id)throw new InvalidDataException("Projektový index neodpovídá manifestu.");
                    foreach(var track in project.Tracks)paths.Add(Path.GetRelativePath(files.Root,OwnedPaths.Resolve(root,track.MediaPath)).Replace('\\','/'));
                }
            }
            byte[] documentBytes=JsonSerializer.SerializeToUtf8Bytes(documents,UserData.Json);
            var manifestFiles=new List<BackupFile> { new("documents.json",documentBytes.Length,Convert.ToHexStringLower(SHA256.HashData(documentBytes))) };
            foreach(string path in paths.Order(StringComparer.Ordinal))
            {
                await using var stream=File.OpenRead(OwnedPaths.Resolve(files.Root,path));
                if(stream.Length<=0 || stream.Length>OwnedMedia.MaximumBytes)throw new InvalidDataException("Soubor zálohy je prázdný nebo příliš velký.");
                manifestFiles.Add(new(path,stream.Length,Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,cancellation))));
            }
            if(manifestFiles.Count>20000 || manifestFiles.Sum(f=>f.Bytes)>MaximumArchiveBytes)throw new InvalidDataException("Záloha přesahuje povolenou velikost nebo počet souborů.");
            string output=Path.GetFullPath(destination),temporary=output+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                await using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,128*1024,FileOptions.Asynchronous|FileOptions.WriteThrough))
                {
                    using(var archive=new ZipArchive(stream,ZipArchiveMode.Create,true))
                    {
                        await WriteEntry(archive,"manifest.json",JsonSerializer.SerializeToUtf8Bytes(new BackupManifest(1,DateTimeOffset.UtcNow,includeRecordings,includeProjects,manifestFiles),UserData.Json),cancellation);
                        await WriteEntry(archive,"documents.json",documentBytes,cancellation);
                        foreach(var file in manifestFiles.Skip(1))
                        {
                            await using var input=File.OpenRead(OwnedPaths.Resolve(files.Root,file.Path));
                            await using var target=archive.CreateEntry(file.Path,CompressionLevel.NoCompression).Open();
                            await CopyCheckedAsync(input,target,file,cancellation);
                        }
                    }
                    await stream.FlushAsync(cancellation);
                }
                cancellation.ThrowIfCancellationRequested();File.Move(temporary,output,true);return true;
            }
            finally { if(File.Exists(temporary))File.Delete(temporary); }
        },cancellation);
    }
    private static async Task WriteEntry(ZipArchive archive,string path,byte[] bytes,CancellationToken cancellation)
    { await using var entry=archive.CreateEntry(path,CompressionLevel.Optimal).Open();await entry.WriteAsync(bytes,cancellation); }
    private static async Task CopyCheckedAsync(Stream source,Stream target,BackupFile expected,CancellationToken cancellation)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[128*1024];long size=0;
        while(true)
        {
            int count=await source.ReadAsync(buffer,cancellation);if(count==0)break;size+=count;
            if(size>expected.Bytes)throw new InvalidDataException("Soubor zálohy má jinou velikost.");
            hash.AppendData(buffer,0,count);await target.WriteAsync(buffer.AsMemory(0,count),cancellation);
        }
        if(size!=expected.Bytes || Convert.ToHexStringLower(hash.GetHashAndReset())!=expected.Sha256)throw new InvalidDataException("Kontrolní součet zálohy neodpovídá souboru.");
    }
    private static ProjectManifest ReadProject(string path)
    {
        if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Manifest projektu je příliš velký.");
        using var json=JsonDocument.Parse(File.ReadAllText(path));var project=UserData.Read<ProjectManifest>(json.RootElement);UserData.ValidateProject(project);return project;
    }
    private async Task<ArchiveData> ReadArchiveAsync(string path,CancellationToken cancellation)
    {
        string stage="backups/stage-"+Guid.NewGuid().ToString("N");string root=OwnedPaths.Resolve(files.Root,stage);Directory.CreateDirectory(root);
        try
        {
            await using var input=File.OpenRead(path);
            if(input.Length>MaximumArchiveBytes)throw new InvalidDataException("Archiv je příliš velký.");
            string digest=Convert.ToHexStringLower(await SHA256.HashDataAsync(input,cancellation));input.Position=0;
            using var archive=new ZipArchive(input,ZipArchiveMode.Read,true);
            if(archive.Entries.Count is <2 or >20001 || archive.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=archive.Entries.Count)throw new InvalidDataException("Archiv má neplatné nebo duplicitní soubory.");
            var manifestEntry=archive.GetEntry("manifest.json") ?? throw new InvalidDataException("Manifest zálohy chybí.");
            if(manifestEntry.Length>4*1024*1024)throw new InvalidDataException("Manifest zálohy je příliš velký.");
            BackupManifest manifest;
            using(var reader=manifestEntry.Open())
            {
                using var bounded=new MemoryStream();byte[] buffer=new byte[16384];
                while(true){int count=await reader.ReadAsync(buffer,cancellation);if(count==0)break;if(bounded.Length+count>4*1024*1024)throw new InvalidDataException("Manifest zálohy je příliš velký.");await bounded.WriteAsync(buffer.AsMemory(0,count),cancellation);}
                using var json=JsonDocument.Parse(bounded.ToArray(),new JsonDocumentOptions {MaxDepth=32});manifest=UserData.Read<BackupManifest>(json.RootElement);
            }
            if(manifest.SchemaVersion!=1 || manifest.Files is null || manifest.Files.Count>20000 || manifest.Files.Count+1!=archive.Entries.Count)throw new InvalidDataException("Verze nebo obsah zálohy není podporovaný.");
            long required=manifest.Files.Sum(f=>f.Bytes);EnsureSpace(root,required+16*1024*1024);
            long total=0;var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var file in manifest.Files)
            {
                if(file is null || !UserData.ValidRelativePath(file.Path) || !names.Add(file.Path) || !UserData.ValidHash(file.Sha256) || file.Bytes<=0 || file.Bytes>OwnedMedia.MaximumBytes || (file.Path!="documents.json" && !file.Path.StartsWith("recordings/",StringComparison.Ordinal) && !file.Path.StartsWith("projects/",StringComparison.Ordinal)))throw new InvalidDataException("Neplatná cesta nebo metadata zálohy.");
                if((!manifest.IncludesRecordings && file.Path.StartsWith("recordings/",StringComparison.Ordinal)) || (!manifest.IncludesProjects && file.Path.StartsWith("projects/",StringComparison.Ordinal)))throw new InvalidDataException("Záloha obsahuje soubory mimo deklarovaný rozsah.");
                total=checked(total+file.Bytes);if(total>MaximumArchiveBytes)throw new InvalidDataException("Rozbalená záloha je příliš velká.");
                var entry=archive.GetEntry(file.Path) ?? throw new InvalidDataException("Soubor zálohy chybí.");
                if(entry.Length!=file.Bytes)throw new InvalidDataException("Velikost souboru zálohy neodpovídá manifestu.");
                string target=OwnedPaths.Resolve(root,file.Path);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var source=entry.Open();await using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None,128*1024,FileOptions.Asynchronous);
                await CopyCheckedAsync(source,output,file,cancellation);
            }
            if(!names.Contains("documents.json"))throw new InvalidDataException("Dokumenty zálohy chybí.");
            string documentPath=Path.Combine(root,"documents.json");if(new FileInfo(documentPath).Length>32*1024*1024)throw new InvalidDataException("Dokumenty zálohy jsou příliš velké.");
            using var docsJson=JsonDocument.Parse(await File.ReadAllBytesAsync(documentPath,cancellation));
            var documents=UserData.Read<DataDocument[]>(docsJson.RootElement).Select(UserData.Normalize).ToArray();
            if(documents.Length>10000 || documents.Select(d=>(d.Kind,d.Id)).Distinct().Count()!=documents.Length)throw new InvalidDataException("Duplicitní nebo příliš rozsáhlá záloha dokumentů.");
            var referenced=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {"documents.json"};
            foreach(var document in documents)
            {
                if(document.Kind=="recording")
                {
                    if(!manifest.IncludesRecordings)throw new InvalidDataException("Nahrávka je mimo rozsah zálohy.");
                    var recording=UserData.Read<RecordingData>(document.Value);referenced.Add(recording.RelativePath);
                    var metadata=manifest.Files.SingleOrDefault(f=>f.Path==recording.RelativePath);
                    if(metadata is null || metadata.Bytes!=recording.Bytes || metadata.Sha256!=recording.Sha256)throw new InvalidDataException("Nahrávka neodpovídá zvukovému souboru.");
                }
                if(document.Kind=="project")
                {
                    if(!manifest.IncludesProjects)throw new InvalidDataException("Projekt je mimo rozsah zálohy.");
                    var index=UserData.Read<ProjectIndex>(document.Value);referenced.Add(index.RelativeManifest);
                    var project=ReadProject(OwnedPaths.Resolve(root,index.RelativeManifest));if(project.Id!=index.Id)throw new InvalidDataException("Projekt má odlišné ID.");
                    string projectRoot=Path.GetDirectoryName(OwnedPaths.Resolve(root,index.RelativeManifest))!;
                    foreach(var track in project.Tracks)
                    {
                        string relative=Path.GetRelativePath(root,OwnedPaths.Resolve(projectRoot,track.MediaPath)).Replace('\\','/');referenced.Add(relative);
                        if(manifest.Files.SingleOrDefault(f=>f.Path==relative)?.Sha256!=track.Sha256)throw new InvalidDataException("Projektová stopa chybí nebo má jiný obsah.");
                    }
                }
            }
            if(!referenced.SetEquals(names))throw new InvalidDataException("Archiv obsahuje nepoužité nebo chybějící soubory.");
            return new(stage,manifest,documents,digest);
        }
        catch { Directory.Delete(root,true);throw; }
    }
    public async Task<BackupPreview> PreviewAsync(string path,CancellationToken cancellation=default)
    {
        var data=await ReadArchiveAsync(path,cancellation);
        try
        {
            var existing=(await database.SnapshotAsync(cancellation)).Documents.Select(d=>(d.Kind,d.Id)).ToHashSet();
            return new(data.Hash,data.Documents.Length,data.Documents.Count(d=>d.Kind=="recording"),data.Documents.Count(d=>d.Kind=="project"),data.Manifest.Files.Sum(f=>f.Bytes),data.Documents.Count(d=>existing.Contains((d.Kind,d.Id))),data.Manifest.IncludesRecordings,data.Manifest.IncludesProjects);
        }
        finally { Directory.Delete(OwnedPaths.Resolve(files.Root,data.Stage),true); }
    }
    public async Task<RestoreResult> RestoreAsync(string path,BackupPreview approved,RestoreStrategy strategy,CancellationToken cancellation=default)
    {
        if(!Enum.IsDefined(strategy))throw new ArgumentException("Neplatná strategie obnovy.");
        return await database.ExclusiveAsync(async ()=>
        {
            await RecoverInterruptedAsync(files,cancellation);
            var data=await ReadArchiveAsync(path,cancellation);string stageRoot=OwnedPaths.Resolve(files.Root,data.Stage);
            bool journalCreated=false;
            try
            {
                if(data.Hash!=approved.Sha256)throw new InvalidDataException("Archiv se od náhledu změnil. Otevři náhled znovu.");
                var current=await database.SnapshotAsync(cancellation);if(current.Warnings.Count>0)throw new InvalidDataException("Nejprve vyřeš varování uložených dat.");
                var combined=current.Documents.ToDictionary(d=>(d.Kind,d.Id));int imported=0,kept=0;
                if(strategy==RestoreStrategy.ReplaceIncluded)
                    foreach(var key in combined.Keys.Where(key=>(key.Kind!="recording" || data.Manifest.IncludesRecordings) && (key.Kind!="project" || data.Manifest.IncludesProjects)).ToArray())combined.Remove(key);
                // Copy current owned data first into a new isolated profile. Incoming selected records then replace their files.
                foreach(string category in Categories.Skip(1).Where(category=>strategy==RestoreStrategy.MergeKeepCurrent || (category=="recordings" ? !data.Manifest.IncludesRecordings : !data.Manifest.IncludesProjects)))CopyTreeIfPresent(Path.Combine(files.Root,category),Path.Combine(stageRoot,"current-"+category),cancellation);
                foreach(string category in Categories.Skip(1))
                {
                    string incoming=Path.Combine(stageRoot,category),saved=Path.Combine(stageRoot,"current-"+category);
                    if(Directory.Exists(saved))
                    {
                        if(Directory.Exists(incoming))Directory.Move(incoming,Path.Combine(stageRoot,"incoming-"+category));
                        Directory.Move(saved,incoming);
                    }
                    else Directory.CreateDirectory(incoming);
                }
                foreach(var document in data.Documents)
                {
                    var key=(document.Kind,document.Id);
                    if(strategy==RestoreStrategy.MergeKeepCurrent && combined.ContainsKey(key)){kept++;continue;}
                    combined[key]=document;imported++;
                    string? relative=document.Kind=="recording" ? UserData.Read<RecordingData>(document.Value).RelativePath : document.Kind=="project" ? UserData.Read<ProjectIndex>(document.Value).RelativeManifest : null;
                    if(relative is not null)
                    {
                        string category=relative.Split('/')[0];string original=Path.Combine(stageRoot,"incoming-"+category,relative[(category.Length+1)..].Replace('/',Path.DirectorySeparatorChar));
                        if(File.Exists(original))
                        {
                            string target=OwnedPaths.Resolve(stageRoot,relative);
                            if(document.Kind=="recording"){ Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(original,target,true); }
                            else { string targetDirectory=Path.GetDirectoryName(target)!;if(Directory.Exists(targetDirectory))Directory.Delete(targetDirectory,true);CopyTreeIfPresent(Path.GetDirectoryName(original)!,targetDirectory,cancellation); }
                        }
                    }
                }
                var stagedFiles=new UserFiles(stageRoot);var stagedDatabase=await DataRepository.OpenAsync(stagedFiles,cancellation);await stagedDatabase.SaveAsync(combined.Values,true,cancellation);
                // No pooled connection survives this point. The journal recovers any interrupted directory swap on next startup.
                string rollback="backups/restore-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(OwnedPaths.Resolve(files.Root,rollback));
                var journal=new RestoreJournal(1,data.Stage,rollback,Categories.Where(c=>Directory.Exists(Path.Combine(files.Root,c))).ToArray(),false);
                await UserFiles.WriteTextAsync(JournalPath,JsonSerializer.Serialize(journal,UserData.Json),cancellation);journalCreated=true;
                cancellation.ThrowIfCancellationRequested();
                foreach(string category in Categories)
                {
                    string target=Path.Combine(files.Root,category);if(Directory.Exists(target))Directory.Move(target,OwnedPaths.Resolve(files.Root,rollback+"/"+category));
                    Directory.Move(Path.Combine(stageRoot,category),target);
                }
                await UserFiles.WriteTextAsync(JournalPath,JsonSerializer.Serialize(journal with {Committed=true},UserData.Json));
                File.Delete(JournalPath);journalCreated=false;
                try { await UserFiles.WriteTextAsync(Path.Combine(files.DirectoryFor("settings"),"last-restore.json"),JsonSerializer.Serialize(rollback)); } catch(IOException) { } catch(UnauthorizedAccessException) { }
                return new RestoreResult(imported,kept,OwnedPaths.Resolve(files.Root,rollback));
            }
            catch { if(journalCreated)await RecoverInterruptedAsync(files);throw; }
            finally { if(Directory.Exists(stageRoot))Directory.Delete(stageRoot,true); }
        },cancellation);
    }
    public string? LastRollback()
    {
        string path=Path.Combine(files.DirectoryFor("settings"),"last-restore.json");
        if(!File.Exists(path) || new FileInfo(path).Length>1024)return null;
        try { string? relative=JsonSerializer.Deserialize<string>(File.ReadAllText(path));return ValidOperationPath(relative,"restore-") && Directory.Exists(OwnedPaths.Resolve(files.Root,relative!)) ? relative:null; }
        catch(Exception error) when(error is JsonException or IOException or InvalidDataException){return null;}
    }
    public async Task<RestoreResult> UndoLastRestoreAsync(CancellationToken cancellation=default)
    {
        string relative=LastRollback() ?? throw new InvalidDataException("Vratná kopie není dostupná.");
        var oldFiles=new UserFiles(OwnedPaths.Resolve(files.Root,relative));var oldDb=await DataRepository.OpenAsync(oldFiles,cancellation);
        string archive=Path.Combine(files.DirectoryFor("backups"),"undo-"+Guid.NewGuid().ToString("N")+".rockerbackup");
        try
        {
            await new BackupService(oldFiles,oldDb).ExportAsync(archive,cancellation:cancellation);
            var preview=await PreviewAsync(archive,cancellation);return await RestoreAsync(archive,preview,RestoreStrategy.ReplaceIncluded,cancellation);
        }
        finally{if(File.Exists(archive))File.Delete(archive);}
    }
    private static void CopyTreeIfPresent(string source,string target,CancellationToken cancellation)
    {
        if(!Directory.Exists(source))return;
        if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Odkazované datové složky nelze obnovovat.");
        Directory.CreateDirectory(target);
        foreach(string file in Directory.EnumerateFiles(source))
        { cancellation.ThrowIfCancellationRequested();if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Odkazované soubory nelze obnovovat.");File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true); }
        foreach(string directory in Directory.EnumerateDirectories(source))CopyTreeIfPresent(directory,Path.Combine(target,Path.GetFileName(directory)),cancellation);
    }
    public static async Task RecoverInterruptedAsync(UserFiles files,CancellationToken cancellation=default)
    {
        string path=Path.Combine(files.DirectoryFor("settings"),"restore-journal.json");if(!File.Exists(path))return;
        if(new FileInfo(path).Length>8192)throw new InvalidDataException("Záznam obnovy je poškozený.");
        var journal=JsonSerializer.Deserialize<RestoreJournal>(await File.ReadAllTextAsync(path,cancellation),UserData.Json) ?? throw new InvalidDataException("Záznam obnovy chybí.");
        if(journal.Version!=1 || !ValidOperationPath(journal.Stage,"stage-") || !ValidOperationPath(journal.Rollback,"restore-") || journal.Existing is null || journal.Existing.Any(c=>!Categories.Contains(c)))throw new InvalidDataException("Neplatný záznam obnovy.");
        if(!journal.Committed)
        {
            foreach(string category in Categories)
            {
                string backup=OwnedPaths.Resolve(files.Root,journal.Rollback+"/"+category),target=Path.Combine(files.Root,category);
                if(Directory.Exists(backup)){if(Directory.Exists(target))Directory.Delete(target,true);Directory.Move(backup,target);}
                else if(!journal.Existing.Contains(category) && Directory.Exists(target))Directory.Delete(target,true);
            }
        }
        File.Delete(path);
        string stage=OwnedPaths.Resolve(files.Root,journal.Stage);if(Directory.Exists(stage))Directory.Delete(stage,true);
    }
    private static void EnsureSpace(string path,long required)
    {
        var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
        if(drive.IsReady && drive.AvailableFreeSpace<required)throw new IOException("Pro zálohu nebo obnovu není dostatek volného místa.");
    }
    private static bool ValidOperationPath(string? path,string prefix)=>path is not null && path.StartsWith("backups/"+prefix,StringComparison.Ordinal) && Guid.TryParseExact(path[(8+prefix.Length)..],"N",out _);
}
