// SPDX-License-Identifier: AGPL-3.0-only
using System.Security.Cryptography;
using System.Text.Json;
namespace Rocker.Infrastructure;
public sealed record UpdateBackup(string ProductVersion,int DatabaseSchema,string Archive,string ArchiveSha256,string DatabaseCopy,DateTimeOffset CreatedAt);
public static class UpdatePreparation
{
    public static async Task<UpdateBackup?> CreateAsync(UserFiles files,string version,CancellationToken cancellation=default)
    {
        await ColdProfileRecovery.RecoverInterruptedAsync(files);
        if(!File.Exists(Path.Combine(files.Root,"database","rocker.db")))return null;
        await BackupService.RecoverInterruptedAsync(files);var database=await DataRepository.OpenAsync(files,cancellation);
        string directory=Path.Combine(files.DirectoryFor("backups"),"update-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        string copy=Path.Combine(directory,"before-update.db"),archive=Path.Combine(directory,"before-update.rockerbackup");
        await database.BackupDatabaseAsync(copy,cancellation);await new BackupService(files,database).ExportAsync(archive,cancellation:cancellation);
        await using var input=File.OpenRead(archive);var backup=new UpdateBackup(version,DataRepository.SchemaVersion,archive,Convert.ToHexStringLower(await SHA256.HashDataAsync(input,cancellation)),copy,DateTimeOffset.UtcNow);
        await UserFiles.WriteTextAsync(Path.Combine(directory,"update-backup.json"),JsonSerializer.Serialize(backup),cancellation);return backup;
    }
}
