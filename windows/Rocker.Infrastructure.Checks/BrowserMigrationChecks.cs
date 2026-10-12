// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using Rocker.Infrastructure;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
internal static class BrowserMigrationChecks
{
    public static async Task RunAsync(string path,string reportPath)
    {
        int checks=0;void Check(bool value,string message){++checks;if(!value)throw new Exception(message);}
        var checkout=new DirectoryInfo(AppContext.BaseDirectory);while(checkout is not null && !File.Exists(Path.Combine(checkout.FullName,"WINDOWS_ROADMAP.md")))checkout=checkout.Parent;if(checkout is null)throw new Exception("Checkout missing.");
        var catalog=ContentCatalog.Load(await File.ReadAllBytesAsync(Path.Combine(checkout.FullName,"windows/content/runtime-catalog.json")));string temporary=Path.Combine(Path.GetTempPath(),"rocker-browser-import-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);
        try{
            var files=new UserFiles(temporary);var database=await DataRepository.OpenAsync(files);var service=new LegacyImport(files,database,catalog);await using var migration=await service.PrepareAsync(path);Check(migration.Preview.Documents==17 && migration.Preview.Recordings==5 && !migration.HasSecret,"actual browser archive contains eleven source groups and five recordings without key");var result=await service.CommitAsync(migration,RestoreStrategy.MergeKeepCurrent);Check(result.Data.Imported==17 && result.Data.KeptCurrent==0,"clean native profile accepts all browser documents");var snapshot=await (await DataRepository.OpenAsync(files)).SnapshotAsync();Check(snapshot.Documents.Count==17 && snapshot.Warnings.Count==0,"reopened native profile is complete");
            using var archive=new ZipArchive(File.OpenRead(path),ZipArchiveMode.Read);using var manifestStream=archive.GetEntry("manifest.json")!.Open();var manifest=JsonSerializer.Deserialize<MigrationManifest>(manifestStream,UserData.Json)!;
            foreach(var recording in manifest.Recordings){var saved=UserData.Read<RecordingData>(snapshot.Documents.Single(d=>d.Kind=="recording" && d.Id==recording.Id).Value);using var source=archive.GetEntry(recording.File)!.Open();string expected=Convert.ToHexStringLower(SHA256.HashData(source));using var imported=File.OpenRead(OwnedPaths.Resolve(files.Root,saved.RelativePath));Check(expected==saved.Sha256 && expected==Convert.ToHexStringLower(SHA256.HashData(imported)) && saved.Markers.SequenceEqual(recording.Markers) && saved.Name==recording.Name && saved.DurationSec==recording.DurationSec && saved.CreatedAt.ToUnixTimeMilliseconds()==recording.CreatedAt,"browser recording bytes/metadata/markers/timestamp preserved");}
            var settings=UserData.Read<AppSettings>(snapshot.Documents.Single(d=>d.Kind=="settings").Value);Check(settings.Handedness=="left" && settings.TuningId=="custom-w01" && settings.ZenMode,"browser preferences retained");Check(UserData.Read<CustomTuning[]>(snapshot.Documents.Single(d=>d.Kind=="custom-tunings").Value).Length==1 && UserData.Read<SavedVideoLoop[]>(snapshot.Documents.Single(d=>d.Kind=="yt-loops").Value).Length==1,"custom tuning and video loop retained");Check(UserData.Read<SavedChatMessage[]>(snapshot.Documents.Single(d=>d.Kind=="chat").Value).Length==2,"AI history retained without a key");
            await using var duplicate=await service.PrepareAsync(path);var again=await service.CommitAsync(duplicate,RestoreStrategy.MergeKeepCurrent);Check(again.Data.Imported==0 && again.Data.KeptCurrent==17,"repeat browser import is idempotent");
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool refused=false;try{await service.PrepareAsync(path,cancelled.Token);}catch(OperationCanceledException){refused=true;}Check(refused && (await database.SnapshotAsync()).Documents.Count==17,"cancelled browser import retains all native data");
            await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(new{result="passed",checks,sourceGroups=11,recordings=5,recordingBytesAndMetadataPreserved=true,duplicateImportVerified=true,cancelVerified=true,originalBrowserDataDeleted=false,realPersonalProfileUsed=false,physicalPlaybackVerified=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS: {checks} actual browser-to-native migration assertions.");
        }finally{Directory.Delete(temporary,true);}
    }
}
