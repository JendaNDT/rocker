// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.Data.Sqlite;
using Rocker.Domain;
using Rocker.Infrastructure;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
internal static class StorageChecks
{
    public static async Task<int> RunAsync(UserFiles files)
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        var database = await DataRepository.OpenAsync(files);
        var initial = new AppSettings(Theme:"dark");
        var docs = new List<DataDocument> {
            UserData.Document("settings","default",initial), UserData.Document("instrument","default",InstrumentProfile.Default),
            UserData.Document("progress","default",new {}), UserData.Document("lessons","default",new LessonProgress(["intro"],new Dictionary<string,int> { ["intro"] = 2 })),
            UserData.Document("amp","default",new {}),UserData.Document("amp-presets","default",Array.Empty<AmpPreset>()),
            UserData.Document("metronome","default",new {}),UserData.Document("metronome-presets","default",Array.Empty<object>()),
            UserData.Document("custom-tunings","default",Array.Empty<CustomTuning>()),UserData.Document("yt-loops","default",new[] { new SavedVideoLoop("loop","abcdefghijk","Test",1,4,0.75) }),
            UserData.Document("ai-settings","default",new AiSettings()),UserData.Document("chat","default",new[] { new SavedChatMessage("user","Test") })
        };
        docs.Add(UserData.Document("practice-workspace","default",PracticeWorkspaceRules.Empty));
        var mediaStore = new OwnedMedia(files);
        using var source = new MemoryStream([82,73,70,70,0,1,2,3]);
        var media = await mediaStore.ImportAsync(source,".wav");
        string recordingId = Guid.NewGuid().ToString("D");
        docs.Add(UserData.Document("recording",recordingId,new RecordingData(recordingId,"Test","audio/wav",1,DateTimeOffset.UtcNow,media.RelativePath,media.Bytes,media.Sha256,[])));
        string id = Guid.NewGuid().ToString("D");
        var projects = new ProjectRepository(files,database);
        string trackPath = await projects.AttachMediaAsync(id,media);
        var project = new ProjectManifest(1,id,"První",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,48000,[new("track","Kytara",trackPath,media.Sha256,1)]);
        await projects.SaveAsync(project);
        docs.Add(UserData.Document("project",id,new ProjectIndex(id,"První","projects/"+Guid.Parse(id).ToString("N")+"/project.rockerproj")));
        string songId=Guid.NewGuid().ToString("D");
        docs.Add(UserData.Document("song",songId,new SongSession(songId,"Reference song",Path.Combine(files.Root,"external.wav"),60,12,.75,[new("intro","Úvod",5,15)],1)));
        await database.SaveAsync(docs,replaceAll:true);
        database = await DataRepository.OpenAsync(files);
        var snapshot = await database.SnapshotAsync();
        Check(snapshot.Documents.Count == UserData.Kinds.Count && snapshot.Warnings.Count == 0,"all supported data kinds survive reopening");
        foreach (var doc in docs) Check(JsonElement.DeepEquals(snapshot.Documents.Single(x=>x.Kind==doc.Kind && x.Id==doc.Id).Value,doc.Value),"round trip " + doc.Kind);
        var invalid = new DataDocument("settings","secret",1,JsonSerializer.SerializeToElement(new { apiKey="SECRET_CANARY_DO_NOT_STORE" }));
        try { await database.SaveAsync([UserData.Document("settings","default",new AppSettings()),invalid],replaceAll:true); throw new Exception("Invalid batch accepted"); }
        catch (InvalidDataException) { Check(UserData.Read<AppSettings>((await database.SnapshotAsync()).Documents.Single(x=>x.Kind=="settings").Value).Theme=="dark","invalid batch leaves entire old transaction"); }
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            try { await database.SaveAsync([],true,cancellation.Token); throw new Exception("Cancelled save accepted"); }
            catch (OperationCanceledException) { Check((await database.SnapshotAsync()).Documents.Count==UserData.Kinds.Count,"cancelled replacement keeps state"); }
        }
        var autosave = new Autosave(database);
        autosave.Queue(UserData.Document("settings","default",new AppSettings(Theme:"light")));
        autosave.Queue(UserData.Document("settings","default",initial)); await autosave.FlushAsync();
        Check(UserData.Read<AppSettings>((await database.SnapshotAsync()).Documents.Single(x=>x.Kind=="settings").Value)==initial,"latest autosave wins");
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exclusive=database.ExclusiveAsync(async _=>{entered.SetResult();await release.Task;return true;});await entered.Task;
        var reader=database.SnapshotAsync();Check(!reader.IsCompleted,"exclusive profile operation also excludes concurrent database readers");release.SetResult();await exclusive;await reader;
        string signal = Path.Combine(files.Root,"child-ready");
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach (var arg in new[] { "--crash-writer",database.DatabasePath,signal }) start.ArgumentList.Add(arg);
        using (var child = Process.Start(start) ?? throw new Exception("Cannot start owned crash writer"))
        {
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(signal) && !child.HasExited && deadline.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(25);
            if (!File.Exists(signal)) { if (!child.HasExited) child.Kill(); throw new Exception("Crash writer did not reach uncommitted state"); }
            child.Kill(); await child.WaitForExitAsync();
        }
        database = await DataRepository.OpenAsync(files);
        Check(UserData.Read<AppSettings>((await database.SnapshotAsync()).Documents.Single(x=>x.Kind=="settings").Value)==initial,"forced termination rolls back uncommitted SQLite write");
        // Windows may still have a SQLite/inspection handle after the crash check.
        // Inspect the same bytes using read sharing compatible with SQLite.
        await using(var raw=new FileStream(database.DatabasePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
        using(var bytes=new MemoryStream())
        {await raw.CopyToAsync(bytes);Check(!System.Text.Encoding.UTF8.GetString(bytes.ToArray()).Contains("SECRET_CANARY_DO_NOT_STORE",StringComparison.Ordinal),"rejected structured secret absent from database");}
        using var duplicate = new MemoryStream(source.ToArray());
        Check(await mediaStore.ImportAsync(duplicate,".wav")==media && await mediaStore.VerifyAsync(media),"immutable media deduplicates and verifies");
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel(); using var cancelled = new MemoryStream([1,2,3]);
            try { await mediaStore.ImportAsync(cancelled,".wav",cancellation.Token); throw new Exception("Cancelled media accepted"); }
            catch (OperationCanceledException) { Check(!Directory.EnumerateFiles(files.DirectoryFor("recordings")).Any(p=>p.EndsWith(".tmp",StringComparison.Ordinal)),"cancelled media cleans owned temporary file"); }
        }
        foreach (var path in new[] { "../escape", "media/.. /escape", "C:/escape", "media/./escape", "media/a\\b", "media/name.", "media//name" })
        { try { _ = OwnedPaths.Resolve(files.Root,path); throw new Exception("Unsafe path accepted: "+path); } catch (InvalidDataException) { Check(true,"unsafe path refused"); } }
        await projects.SaveAsync(project with { Name="Druhý" });
        var loaded = await projects.LoadAsync(id);
        Check(loaded.Project.Name=="Druhý" && loaded.MissingMedia.Count==0 && !loaded.RecoveredPrevious,"project reopens with verified relative media");
        string projectDir = projects.DirectoryFor(id), manifest = Path.Combine(projectDir,"project.rockerproj");
        await File.WriteAllTextAsync(manifest,"{broken"); loaded = await projects.LoadAsync(id);
        Check(loaded.Project.Name=="První" && loaded.RecoveredPrevious,"corrupt project recovers previous committed manifest");
        await projects.SaveAsync(project);
        File.Delete(OwnedPaths.Resolve(projectDir,trackPath)); loaded=await projects.LoadAsync(id);
        Check(loaded.MissingMedia.SequenceEqual(["track"]),"missing media reported without deleting project");
        await projects.AttachMediaAsync(id,media);
        Check(await projects.RebuildIndexAsync()==1,"project index rebuilds from manifests");
        var relocatedFiles = new UserFiles(Path.Combine(files.Root,"relocated"));
        Directory.CreateDirectory(Path.Combine(relocatedFiles.Root,"projects"));
        CopyTree(projectDir,Path.Combine(relocatedFiles.Root,"projects",Guid.Parse(id).ToString("N")));
        var relocated = new ProjectRepository(relocatedFiles,await DataRepository.OpenAsync(relocatedFiles));
        Check((await relocated.LoadAsync(id)).MissingMedia.Count==0,"project remains portable after copying to new profile");
        string future = JsonSerializer.Serialize(project with { SchemaVersion=99 },UserData.Json); await File.WriteAllTextAsync(manifest,future);
        try { await projects.SaveAsync(project); throw new Exception("Future project overwritten"); }
        catch (NotSupportedException) { Check(await File.ReadAllTextAsync(manifest)==future,"future project preserved"); }
        var futureFiles = new UserFiles(Path.Combine(files.Root,"future")); string futureDb=Path.Combine(futureFiles.DirectoryFor("database"),"rocker.db");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=futureDb,Pooling=false }.ToString())) { connection.Open(); using var cmd=connection.CreateCommand();cmd.CommandText="PRAGMA user_version=99;CREATE TABLE future(data TEXT);INSERT INTO future VALUES('keep')";cmd.ExecuteNonQuery(); }
        byte[] before=await File.ReadAllBytesAsync(futureDb);
        try { await DataRepository.OpenAsync(futureFiles); throw new Exception("Future database accepted"); }
        catch (InvalidDataException) { byte[] after = await File.ReadAllBytesAsync(futureDb); Check(before.SequenceEqual(after),"future SQLite schema refused without changing bytes"); }
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=database.DatabasePath,Pooling=false }.ToString())) { connection.Open();using var cmd=connection.CreateCommand();cmd.CommandText="INSERT INTO documents VALUES('settings','broken',1,'null','test')";cmd.ExecuteNonQuery(); }
        snapshot = await database.SnapshotAsync();
        Check(snapshot.Documents.Count==UserData.Kinds.Count && snapshot.Warnings.Count==1,"invalid row isolated and valid rows retained");
        Console.WriteLine($"PASS: {checks} SQLite, crash recovery, media, project and autosave assertions.");
        return checks;
    }
    private static void CopyTree(string source,string target)
    { Directory.CreateDirectory(target); foreach (string file in Directory.EnumerateFiles(source)) File.Copy(file,Path.Combine(target,Path.GetFileName(file))); foreach (string dir in Directory.EnumerateDirectories(source)) CopyTree(dir,Path.Combine(target,Path.GetFileName(dir))); }
    public static async Task CrashWriterAsync(string database,string signal)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=database,Pooling=false }.ToString());connection.Open();
        using var transaction=connection.BeginTransaction();using var cmd=connection.CreateCommand();cmd.Transaction=transaction;
        cmd.CommandText="UPDATE documents SET payload='{}' WHERE kind='settings'";cmd.ExecuteNonQuery();
        await File.WriteAllTextAsync(signal,"ready"); await Task.Delay(Timeout.Infinite);
    }
}
