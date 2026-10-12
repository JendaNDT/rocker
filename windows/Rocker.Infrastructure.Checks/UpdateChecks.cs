// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.Data.Sqlite;
using Rocker.Domain;
using Rocker.Infrastructure;
using System.Security.Cryptography;
internal static class UpdateChecks
{
    public static async Task<int> RunAsync(string directory)
    {
        int checks=0;void Check(bool condition,string message){++checks;if(!condition)throw new Exception(message);}
        var files=new UserFiles(Path.Combine(directory,"update-profile"));var database=await DataRepository.OpenAsync(files);await database.SaveAsync([UserData.Document("settings","default",new AppSettings(Theme:"dark",Onboarded:true))]);
        var backup=await UpdatePreparation.CreateAsync(files,"0.2.0");Check(backup is not null && File.Exists(backup.DatabaseCopy),"pre-update raw SQLite backup created");await using(var input=File.OpenRead(backup!.Archive))Check(Convert.ToHexStringLower(await SHA256.HashDataAsync(input))==backup.ArchiveSha256,"update archive checksum recorded");
        string stableArchive=Path.Combine(directory,"pre-update-reference.rockerbackup");File.Copy(backup.Archive,stableArchive);
        var preview=await new BackupService(files,database).PreviewAsync(backup.Archive);Check(preview.Documents==1 && preview.IncludesProjects && preview.IncludesRecordings,"full-scope update backup remains compatible");
        string secret=Path.Combine(files.DirectoryFor("secrets"),"owned-encrypted-value.bin");await File.WriteAllBytesAsync(secret,[1,2,3,4]);string databasePath=database.DatabasePath;
        using(var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=databasePath,Pooling=false}.ToString())){connection.Open();using var command=connection.CreateCommand();command.CommandText="PRAGMA user_version=99";command.ExecuteNonQuery();}
        bool rejected=false;try{await UpdatePreparation.CreateAsync(files,"0.1.0");}catch(InvalidDataException){rejected=true;}Check(rejected,"newer schema is rejected before an update touches user data");
        var recovery=await ColdProfileRecovery.RestoreAsync(files,stableArchive);var restored=await (await DataRepository.OpenAsync(files)).SnapshotAsync();Check(UserData.Read<AppSettings>(restored.Documents.Single().Value).Theme=="dark","cold compatible backup opens after future schema refusal");Check(Directory.Exists(recovery.RetainedDirectory) && File.Exists(Path.Combine(recovery.RetainedDirectory,"database","rocker.db")),"incompatible original profile retained intact");Check((await File.ReadAllBytesAsync(secret)).SequenceEqual(new byte[]{1,2,3,4}),"protected secret bytes preserved outside exported backup");
        foreach(string phase in new[]{"prepared","retained","published"})
        {
            await (await DataRepository.OpenAsync(files)).SaveAsync([UserData.Document("settings","default",new AppSettings(Theme:"light"))]);
            bool failed=false,reached=false;try{await ColdProfileRecovery.RestoreAsync(files,stableArchive,checkpoint:value=>{if(value==phase){reached=true;throw new IOException("Injected recovery interruption");}});}catch(IOException){failed=true;}Check(failed && reached,"injected recovery interruption reached "+phase);await ColdProfileRecovery.RecoverInterruptedAsync(files);
            var state=await (await DataRepository.OpenAsync(files)).SnapshotAsync();Check(UserData.Read<AppSettings>(state.Documents.Single().Value).Theme==(phase=="prepared"?"light":"dark"),"recovery publishes only validated profile after "+phase);
        }
        string bad=Path.Combine(directory,"invalid-backup.rockerbackup");await File.WriteAllBytesAsync(bad,[1,2,3]);var before=await File.ReadAllBytesAsync(databasePath);bool corrupt=false;try{await ColdProfileRecovery.RestoreAsync(files,bad);}catch(InvalidDataException){corrupt=true;}Check(corrupt && (await File.ReadAllBytesAsync(databasePath)).SequenceEqual(before),"invalid archive leaves the exact previous database untouched");
        Console.WriteLine($"PASS: {checks} update backup, schema refusal and cold profile recovery assertions.");return checks;
    }
}
