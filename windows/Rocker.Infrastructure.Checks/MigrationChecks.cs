// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using Rocker.Infrastructure;
using System.Text;
using System.Text.Json;
internal static class MigrationChecks
{
    public static async Task<int> RunAsync(string root)
    {
        int checks=0;void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        // Locate the checkout from the checks executable; fixtures remain read-only reference data.
        var checkout=new DirectoryInfo(AppContext.BaseDirectory);while(checkout is not null && !File.Exists(Path.Combine(checkout.FullName,"WINDOWS_ROADMAP.md")))checkout=checkout.Parent;
        if(checkout is null)throw new Exception("Reference checkout missing");
        string fixtures=Path.Combine(checkout.FullName,"docs/windows/baseline/fixtures");
        var catalog=ContentCatalog.Load(await File.ReadAllBytesAsync(Path.Combine(checkout.FullName,"windows/content/runtime-catalog.json")));
        var plan=LegacyMigration.Parse(await File.ReadAllBytesAsync(Path.Combine(fixtures,"settings-v2.json")),catalog);
        Check(plan.Documents.Count==12 && !plan.HasSecret,"all eleven PWA groups migrate into twelve typed documents");
        var settings=UserData.Read<AppSettings>(plan.Documents.Single(d=>d.Kind=="settings").Value);
        Check(settings.Theme=="dark" && settings.Handedness=="left" && settings.TuningId=="custom-w01" && settings.ZenMode && settings.Notation=="german","settings translation preserves practice preferences");
        Check(UserData.Read<AmpSettings>(plan.Documents.Single(d=>d.Kind=="amp").Value).PresetId=="user-w01","selected amp preset preserved");
        Check(UserData.Read<MetronomeConfig>(plan.Documents.Single(d=>d.Kind=="metronome").Value).VisualMode=="pendulum","metronome visual preference preserved");
        Check(plan.Documents.Single(d=>d.Kind=="metronome-presets").Value[0].GetProperty("config").GetProperty("subdivisionSound").GetString()=="hihat","custom metronome preset settings preserved");
        Check(UserData.Read<SavedChatMessage[]>(plan.Documents.Single(d=>d.Kind=="chat").Value).All(m=>m.CreatedAt is null),"legacy chat timestamps not invented");
        var v1=LegacyMigration.Parse(await File.ReadAllBytesAsync(Path.Combine(fixtures,"settings-v1.json")),catalog);Check(v1.Documents.Count>0,"v1 settings supported");
        var partial=LegacyMigration.Parse(await File.ReadAllBytesAsync(Path.Combine(fixtures,"settings-partial.json")),catalog);Check(partial.Documents.Count>0 && partial.Documents.Count<plan.Documents.Count,"missing legacy stores not silently invented");
        var corrupt=LegacyMigration.Parse(await File.ReadAllBytesAsync(Path.Combine(fixtures,"settings-corrupt-value.json")),catalog);Check(!corrupt.Documents.Any(d=>d.Kind=="progress") && corrupt.Warnings.Any(w=>w.Contains("rocker-progress",StringComparison.Ordinal)),"corrupt store isolated and reported");
        try{LegacyMigration.Parse(await File.ReadAllBytesAsync(Path.Combine(fixtures,"settings-invalid.json")),catalog);throw new Exception("invalid app accepted");}catch(InvalidDataException){Check(true,"foreign backup refused");}
        const string key="LEGACY_TEST_SECRET_CANARY";
        var secretBackup=new {app="rocker",kind="settings-backup",version=2,data=new Dictionary<string,string>{["rocker-ai"]=JsonSerializer.Serialize(new{state=new{apiKey=key,model="gemini-2.5-flash"},version=0}),["rocker-chat"]=JsonSerializer.Serialize(new{state=new{messages=new[]{new{role="user",text=key}}},version=0}),["rocker-progress"]=JsonSerializer.Serialize(new{state=new{bpmRecords=new Dictionary<string,int>{["smoke-on-the-water"]=90}},version=0})}};
        var secretPlan=LegacyMigration.Parse(JsonSerializer.SerializeToUtf8Bytes(secretBackup),catalog);
        Check(secretPlan.HasSecret && secretPlan.Secret==key && !secretPlan.ToString().Contains(key,StringComparison.Ordinal) && !JsonSerializer.Serialize(secretPlan,UserData.Json).Contains(key,StringComparison.Ordinal),"legacy secret held separately and redacted from ordinary documents and diagnostics");
        Check(UserData.Read<ProgressData>(secretPlan.Documents.Single(d=>d.Kind=="progress").Value).BpmRecords.ContainsKey("chromatic-pulse"),"legacy riff record ID translated to owned replacement");
        var files=new UserFiles(Path.Combine(root,"migration-profile"));var database=await DataRepository.OpenAsync(files);var importer=new LegacyImport(files,database,catalog);
        string archive=Path.Combine(checkout.FullName,"docs/windows/w12/fixtures/reference.rockermigration");
        await using(var migration=await importer.PrepareAsync(archive))
        {
            Check(migration.Preview.Recordings==5 && migration.Preview.Documents==17 && !migration.HasSecret,"actual PWA-generated archive includes all typed settings and five legacy audio formats");
            await importer.CommitAsync(migration,RestoreStrategy.MergeKeepCurrent);
        }
        var snapshot=await database.SnapshotAsync();
        var original=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(fixtures,"recordings.json")));
        using(original)foreach(var meta in original.RootElement.EnumerateArray())
        {
            var migrated=UserData.Read<RecordingData>(snapshot.Documents.Single(d=>d.Kind=="recording" && d.Id==meta.GetProperty("id").GetString()).Value);
            Check(migrated.Name==meta.GetProperty("name").GetString() && migrated.MimeType==meta.GetProperty("mimeType").GetString() && migrated.CreatedAt.ToUnixTimeMilliseconds()==meta.GetProperty("createdAt").GetInt64() && migrated.Markers[0].TimeSec==0.25,"recording identity, MIME, timestamp and markers preserved");
            Check(await new OwnedMedia(files).VerifyAsync(new(migrated.RelativePath,migrated.Sha256,migrated.Bytes)),"original audio bytes verify after migration");
        }
        int countBefore=Directory.EnumerateFiles(files.DirectoryFor("recordings")).Count();
        await using(var repeated=await importer.PrepareAsync(archive)){var result=await importer.CommitAsync(repeated,RestoreStrategy.MergeKeepCurrent);Check(result.Data.KeptCurrent==17 && Directory.EnumerateFiles(files.DirectoryFor("recordings")).Count()==countBefore,"repeated import deduplicates IDs and original media");}
        await using(var old=await importer.PrepareAsync(Path.Combine(fixtures,"settings-v1.json")))Check(old.Preview.Recordings==0 && old.Warnings.Any(w=>w.Contains("neobsahuje nahrávky",StringComparison.Ordinal)),"old JSON clearly reports recordings absent");
        string secretFile=Path.Combine(root,"legacy-secret.json");await File.WriteAllBytesAsync(secretFile,JsonSerializer.SerializeToUtf8Bytes(secretBackup));
        await using(var old=await importer.PrepareAsync(secretFile))
        {
            Check(old.HasSecret && !Encoding.UTF8.GetString(await File.ReadAllBytesAsync(old.Archive)).Contains(key,StringComparison.Ordinal),"converted desktop archive does not embed legacy key");
            var result=await importer.CommitAsync(old,RestoreStrategy.ReplaceIncluded,OperatingSystem.IsWindows());
            Check(!result.SecretFailed && result.SecretImported==OperatingSystem.IsWindows(),"explicit secret import uses DPAPI only on Windows");
            if(OperatingSystem.IsWindows())Check((await new SecretStore(files).ReadAsync()).Value==key,"legacy key safely available after DPAPI migration");
        }
        Check(!Directory.EnumerateDirectories(files.DirectoryFor("backups")).Any(p=>Path.GetFileName(p).StartsWith("migration-",StringComparison.Ordinal)),"prepared migration workspace cleaned after disposal");
        Console.WriteLine($"PASS: {checks} legacy settings, PWA archive, five audio formats, metadata, deduplication and secret migration assertions.");return checks;
    }
}
