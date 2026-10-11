// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using Rocker.Infrastructure;
using System.IO.Compression;
using System.Text.Json;
internal static class BackupChecks
{
    public static async Task<int> RunAsync(string root)
    {
        int checks=0;void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        var sourceFiles=new UserFiles(Path.Combine(root,"backup-source"));var sourceDb=await DataRepository.OpenAsync(sourceFiles);
        var media=new OwnedMedia(sourceFiles);using var sound=new MemoryStream([1,2,3,4,5]);var stored=await media.ImportAsync(sound,".wav");
        string id=Guid.NewGuid().ToString("D"),recordingId=Guid.NewGuid().ToString("D");
        var projects=new ProjectRepository(sourceFiles,sourceDb);string attached=await projects.AttachMediaAsync(id,stored);
        await projects.SaveAsync(new(1,id,"Testovací projekt",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,48000,[new("track","Kytara",attached,stored.Sha256,1)]));
        await sourceDb.SaveAsync([UserData.Document("settings","default",new AppSettings(Theme:"dark")),UserData.Document("lessons","default",new LessonProgress(["intro"],new Dictionary<string,int>{["intro"]=2})),UserData.Document("recording",recordingId,new RecordingData(recordingId,"Test","audio/wav",1,DateTimeOffset.UtcNow,stored.RelativePath,stored.Bytes,stored.Sha256,[]))]);
        Directory.CreateDirectory(sourceFiles.DirectoryFor("secrets"));await File.WriteAllTextAsync(Path.Combine(sourceFiles.Root,"secrets","do-not-export"),"BACKUP_SECRET_CANARY");
        string archive=Path.Combine(root,"full.rockerbackup");var exporter=new BackupService(sourceFiles,sourceDb);await exporter.ExportAsync(archive);
        using(var zip=ZipFile.OpenRead(archive))Check(!zip.Entries.Any(e=>e.FullName.Contains("secret",StringComparison.OrdinalIgnoreCase)),"secret directory excluded by backup whitelist");
        var destinationFiles=new UserFiles(Path.Combine(root,"backup-destination"));var destinationDb=await DataRepository.OpenAsync(destinationFiles);var importer=new BackupService(destinationFiles,destinationDb);
        var preview=await importer.PreviewAsync(archive);Check(preview.Documents==4 && preview.Recordings==1 && preview.Projects==1 && preview.Conflicts==0,"preview shows data, media and conflicts");
        var result=await importer.RestoreAsync(archive,preview,RestoreStrategy.ReplaceIncluded);
        var restored=await destinationDb.SnapshotAsync();Check(restored.Documents.Count==4 && result.Imported==4 && Directory.Exists(result.RollbackDirectory),"full restore commits documents and keeps rollback directory");
        Check(await new OwnedMedia(destinationFiles).VerifyAsync(stored),"restored recording has same bytes and hash");
        Check((await new ProjectRepository(destinationFiles,destinationDb).LoadAsync(id)).MissingMedia.Count==0,"restored project references resolve");
        await destinationDb.SaveAsync([UserData.Document("settings","default",new AppSettings(Theme:"light"))]);
        preview=await importer.PreviewAsync(archive);Check(preview.Conflicts==4,"existing conflicts detected before import");
        result=await importer.RestoreAsync(archive,preview,RestoreStrategy.MergeKeepCurrent);
        Check(result.KeptCurrent==4 && UserData.Read<AppSettings>((await destinationDb.SnapshotAsync()).Documents.Single(d=>d.Kind=="settings").Value).Theme=="light","merge preserves existing conflicted settings");
        await importer.RestoreAsync(archive,await importer.PreviewAsync(archive),RestoreStrategy.ReplaceIncluded);
        Check(UserData.Read<AppSettings>((await destinationDb.SnapshotAsync()).Documents.Single(d=>d.Kind=="settings").Value).Theme=="dark","replacement changes conflicting settings before undo");
        await importer.UndoLastRestoreAsync();
        Check(UserData.Read<AppSettings>((await destinationDb.SnapshotAsync()).Documents.Single(d=>d.Kind=="settings").Value).Theme=="light","undo restores pre-import data through validated rollback copy");
        string settingsArchive=Path.Combine(root,"settings.rockerbackup");await exporter.ExportAsync(settingsArchive,false,false);
        var settingsPreview=await importer.PreviewAsync(settingsArchive);Check(settingsPreview.Documents==2 && !settingsPreview.IncludesRecordings && !settingsPreview.IncludesProjects,"settings-only scope is explicit");
        await importer.RestoreAsync(settingsArchive,settingsPreview,RestoreStrategy.ReplaceIncluded);
        Check((await destinationDb.SnapshotAsync()).Documents.Count==4 && await new OwnedMedia(destinationFiles).VerifyAsync(stored),"partial replacement retains excluded recordings and projects");
        using(var cancellation=new CancellationTokenSource())
        { cancellation.Cancel();try{await exporter.ExportAsync(archive,cancellation:cancellation.Token);throw new Exception("cancelled backup accepted");}catch(OperationCanceledException){Check((await importer.PreviewAsync(archive)).Documents==4,"cancelled export preserves existing archive");} }
        foreach(string kind in new[]{"traversal","checksum","future","duplicate","unknown"})
        {
            string bad=Path.Combine(root,kind+".rockerbackup");File.Copy(archive,bad);
            using(var zip=ZipFile.Open(bad,ZipArchiveMode.Update))
            {
                if(kind=="duplicate"){zip.CreateEntry("documents.json");}
                else if(kind=="unknown"){zip.CreateEntry("secrets/forbidden");}
                else
                {
                    var entry=zip.GetEntry("manifest.json")!;BackupManifest manifest;
                    using(var input=entry.Open())manifest=JsonSerializer.Deserialize<BackupManifest>(input,UserData.Json)!;
                    if(kind=="future")manifest=manifest with{SchemaVersion=99};
                    else if(kind=="traversal")manifest=manifest with{Files=manifest.Files.Select((f,i)=>i==0 ? f with{Path="../escape"}:f).ToArray()};
                    else manifest=manifest with{Files=manifest.Files.Select((f,i)=>i==0 ? f with{Sha256=new string('0',64)}:f).ToArray()};
                    entry.Delete();using var output=zip.CreateEntry("manifest.json").Open();JsonSerializer.Serialize(output,manifest,UserData.Json);
                }
            }
            try{await importer.PreviewAsync(bad);throw new Exception("invalid backup accepted: "+kind);}catch(InvalidDataException){Check((await destinationDb.SnapshotAsync()).Documents.Count==4,"invalid archive rejected without changing profile: "+kind);}
        }
        // Simulate a process dying after the old database directory moved and a new one became visible.
        string rollback="backups/restore-"+Guid.NewGuid().ToString("N"),stage="backups/stage-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(OwnedPaths.Resolve(destinationFiles.Root,rollback));Directory.CreateDirectory(OwnedPaths.Resolve(destinationFiles.Root,stage));
        Directory.Move(destinationFiles.DirectoryFor("database"),OwnedPaths.Resolve(destinationFiles.Root,rollback+"/database"));Directory.CreateDirectory(destinationFiles.DirectoryFor("database"));await File.WriteAllTextAsync(Path.Combine(destinationFiles.Root,"database","incomplete"),"bad");
        await UserFiles.WriteTextAsync(Path.Combine(destinationFiles.DirectoryFor("settings"),"restore-journal.json"),JsonSerializer.Serialize(new{version=1,stage,rollback,existing=new[]{"database","recordings","projects"},committed=false},UserData.Json));
        await BackupService.RecoverInterruptedAsync(destinationFiles);
        Check((await DataRepository.OpenAsync(destinationFiles)).DatabasePath==destinationDb.DatabasePath && (await destinationDb.SnapshotAsync()).Documents.Count==4 && !File.Exists(Path.Combine(destinationFiles.Root,"database","incomplete")),"startup journal restores previous database after interrupted swap");
        await BackupService.RecoverInterruptedAsync(destinationFiles);Check(!File.Exists(Path.Combine(destinationFiles.Root,"settings","restore-journal.json")),"recovery is repeatable and clears completed journal");
        Check(!Directory.EnumerateDirectories(destinationFiles.DirectoryFor("backups")).Any(dir=>Path.GetFileName(dir).StartsWith("stage-",StringComparison.Ordinal) && Directory.EnumerateFiles(dir,"*",SearchOption.AllDirectories).Any()),"preview and failed import leave no extracted data");
        Console.WriteLine($"PASS: {checks} backup round-trip, scope, conflicts, malicious archives and interrupted restore contracts.");return checks;
    }
}
