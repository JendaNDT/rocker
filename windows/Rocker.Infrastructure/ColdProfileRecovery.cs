// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace Rocker.Infrastructure;
public sealed record ColdRecoveryResult(string RetainedDirectory);
// Cold-start recovery only: no open database, audio capture or project writers.
// A complete validated replacement is prepared before touching the old profile.
public static class ColdProfileRecovery
{
    private sealed record Journal(int Version,string Stage,string Retained);
    private static string JournalPath(UserFiles files)=>files.Root.TrimEnd(Path.DirectorySeparatorChar)+".profile-recovery.json";
    private static string Sibling(UserFiles files,string kind,string id){if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("Neplatný záznam obnovy profilu.");return files.Root.TrimEnd(Path.DirectorySeparatorChar)+".recovery-"+kind+"-"+id;}
    private static void NoLink(string path){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Obnova odmítla odkaz místo vlastních dat.");}
    public static async Task RecoverInterruptedAsync(UserFiles files)
    {
        string path=JournalPath(files);if(!File.Exists(path))return;NoLink(path);if(new FileInfo(path).Length>8192)throw new InvalidDataException("Záznam obnovy je příliš velký.");var entry=JsonSerializer.Deserialize<Journal>(await File.ReadAllTextAsync(path))??throw new InvalidDataException("Záznam obnovy chybí.");if(entry.Version!=1)throw new InvalidDataException("Verze obnovy není podporovaná.");
        string stage=Sibling(files,"stage",entry.Stage),retained=Sibling(files,"retained",entry.Retained),marker=Path.Combine(files.Root,".recovered-profile");
        if(Directory.Exists(files.Root))NoLink(files.Root);if(Directory.Exists(stage))NoLink(stage);if(Directory.Exists(retained))NoLink(retained);
        if(!Directory.Exists(retained)){if(!Directory.Exists(files.Root))throw new IOException("Původní profil chybí.");if(Directory.Exists(stage))Directory.Delete(stage,true);File.Delete(path);return;}
        if(!Directory.Exists(files.Root)){if(Directory.Exists(stage))Directory.Move(stage,files.Root);else Directory.Move(retained,files.Root);}
        else if(!File.Exists(marker))throw new IOException("Profil se během obnovy změnil. Zachované složky se nepřepisují.");
        if(File.Exists(marker))File.Delete(marker);File.Delete(path);
    }
    public static async Task<ColdRecoveryResult> RestoreAsync(UserFiles files,string archive,CancellationToken cancellation=default,Action<string>? checkpoint=null)
    {
        await RecoverInterruptedAsync(files);NoLink(files.Root);string token=Guid.NewGuid().ToString("N");string stage=Sibling(files,"stage",token),retained=Sibling(files,"retained",token);Directory.CreateDirectory(stage);
        bool journalWritten=false;
        try
        {
            var replacement=new UserFiles(stage);var database=await DataRepository.OpenAsync(replacement,cancellation);var service=new BackupService(replacement,database);var preview=await service.PreviewAsync(archive,cancellation);await service.RestoreAsync(archive,preview,RestoreStrategy.ReplaceIncluded,cancellation);
            string secrets=Path.Combine(files.Root,"secrets");if(Directory.Exists(secrets)){NoLink(secrets);var source=Directory.GetFiles(secrets);if(source.Length>16)throw new InvalidDataException("Neplatná složka tajných hodnot.");foreach(string file in source){NoLink(file);if(new FileInfo(file).Length>65536)throw new InvalidDataException("Tajná hodnota je příliš velká.");File.Copy(file,Path.Combine(replacement.DirectoryFor("secrets"),Path.GetFileName(file)),false);}}
            await File.WriteAllTextAsync(Path.Combine(stage,".recovered-profile"),token,cancellation);cancellation.ThrowIfCancellationRequested();checkpoint?.Invoke("prepared");
            await UserFiles.WriteTextAsync(JournalPath(files),JsonSerializer.Serialize(new Journal(1,token,token)),cancellation);journalWritten=true;
            Directory.Move(files.Root,retained);checkpoint?.Invoke("retained");Directory.Move(stage,files.Root);checkpoint?.Invoke("published");
            await RecoverInterruptedAsync(files);return new(retained);
        }
        finally{if(!journalWritten && Directory.Exists(stage))Directory.Delete(stage,true);}
    }
}
