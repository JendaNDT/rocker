// SPDX-License-Identifier: AGPL-3.0-only
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Rocker.Domain;
namespace Rocker.Infrastructure;
public sealed record CaptureDraft(string Id,string Name,DateTimeOffset CreatedAt,int Rate,int Channels,int Bus,CaptureMetadata? Finished=null);
public sealed class RecordingLibrary(UserFiles files,DataRepository database)
{
    private readonly SemaphoreSlim gate=new(1,1);
    private string Sidecar(string id)=>OwnedPaths.Resolve(files.Root,"recordings/"+Guid.Parse(id).ToString("N")+".capture.json");
    public string PendingPath(string id)=>OwnedPaths.Resolve(files.Root,"recordings/"+Guid.Parse(id).ToString("N")+".wav.pending");
    private string FinalPath(string id)=>files.RecordingPath(Guid.Parse(id),".wav");
    public async Task<CaptureDraft> BeginAsync(string name,int rate,int channels,int bus)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Length>256||rate is <8000 or >192000||channels is <1 or >2||bus is <0 or >2)throw new ArgumentException("Neplatné nastavení nahrávky.");
        var draft=new CaptureDraft(Guid.NewGuid().ToString(),name.Trim(),DateTimeOffset.UtcNow,rate,channels,bus);
        await UserFiles.WriteTextAsync(Sidecar(draft.Id),JsonSerializer.Serialize(draft,UserData.Json));return draft;
    }
    public async Task<IReadOnlyList<RecordingData>> ListAsync()=> (await database.SnapshotAsync()).Documents.Where(d=>d.Kind=="recording").Select(d=>UserData.Read<RecordingData>(d.Value)).OrderByDescending(d=>d.CreatedAt).ToArray();
    public async Task<RecordingData?> FinishAsync(CaptureDraft draft,CaptureMetadata? result=null,bool recovered=false)
    {
        await gate.WaitAsync();try
        {
            if(result is not null){draft=draft with{Finished=result};await UserFiles.WriteTextAsync(Sidecar(draft.Id),JsonSerializer.Serialize(draft,UserData.Json));}
            var source=File.Exists(PendingPath(draft.Id))?PendingPath(draft.Id):FinalPath(draft.Id);
            if(!File.Exists(source))return null; // Keep the descriptor for inspection; never invent a take.
            long frames=await Task.Run(()=>RepairPcm(source,draft));
            if(frames==0)return null;
            var capture=draft.Finished ?? new(draft.Rate,draft.Channels,draft.Bus,frames,null,null,0,false,true,"Obnoveno po přerušení aplikace; začátek zvukových hodin není známý.");
            capture=capture with{Frames=frames,Partial=capture.Partial||recovered};
            if(source!=FinalPath(draft.Id))File.Move(source,FinalPath(draft.Id),false);
            await using var input=File.OpenRead(FinalPath(draft.Id));var digest=Convert.ToHexStringLower(await SHA256.HashDataAsync(input));
            var recording=new RecordingData(draft.Id,draft.Name,"audio/wav",frames/(double)draft.Rate,draft.CreatedAt,"recordings/"+Guid.Parse(draft.Id).ToString("N")+".wav",input.Length,digest,[],capture);
            // Rename first and retain the descriptor until SQLite commits: recovery covers either filename.
            await database.SaveAsync([UserData.Document("recording",recording.Id,recording)]);File.Delete(Sidecar(draft.Id));return recording;
        }finally{gate.Release();}
    }
    public async Task<IReadOnlyList<string>> RecoverAsync()
    {
        var warnings=new List<string>();
        foreach(var sidecar in Directory.EnumerateFiles(files.DirectoryFor("recordings"),"*.capture.json").Take(10001))
        {
            try
            {
                OwnedPaths.Resolve(files.Root,"recordings/"+Path.GetFileName(sidecar));
                if(new FileInfo(sidecar).Length>16384)throw new InvalidDataException("Popis obnovy je příliš velký.");
                var draft=JsonSerializer.Deserialize<CaptureDraft>(await File.ReadAllTextAsync(sidecar),UserData.Json)??throw new InvalidDataException("Popis nahrávky chybí.");
                if(Path.GetFullPath(sidecar)!=Sidecar(draft.Id)||draft.Rate is <8000 or >192000||draft.Channels is <1 or >2||draft.Bus is <0 or >2||string.IsNullOrWhiteSpace(draft.Name))throw new InvalidDataException("Popis nahrávky není platný.");
                // A committed row means only cleanup was interrupted: preserve its markers/name.
                if((await ListAsync()).Any(r=>r.Id==draft.Id)){File.Delete(sidecar);continue;}
                if(await FinishAsync(draft,recovered:true) is { } recording)warnings.Add("Obnovena rozpracovaná nahrávka: "+recording.Name);
                else warnings.Add("Rozpracovaná nahrávka nemá zvuková data; původní soubory jsou zachované.");
            }catch(Exception error)when(error is IOException or JsonException or ArgumentException or InvalidDataException or UnauthorizedAccessException){warnings.Add("Nahrávku nelze obnovit; soubory zůstávají zachované. "+error.Message);}
        }
        return warnings;
    }
    private static long RepairPcm(string path,CaptureDraft draft)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);if(stream.Length<44||stream.Length>OwnedMedia.MaximumBytes)throw new InvalidDataException("Velikost rozpracované nahrávky není platná.");
        byte[] header=new byte[44];stream.ReadExactly(header);
        if(!header.AsSpan(0,4).SequenceEqual("RIFF"u8)||!header.AsSpan(8,8).SequenceEqual("WAVEfmt "u8)||!header.AsSpan(36,4).SequenceEqual("data"u8)||BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16))!=16||BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(20))!=1||BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(22))!=draft.Channels||BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24))!=draft.Rate||BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(34))!=24)throw new InvalidDataException("Zvukový soubor neodpovídá popisu obnovy.");
        int alignment=draft.Channels*3;long frames=(stream.Length-44)/alignment;
        if(draft.Finished is { } done){if(done.SampleRate!=draft.Rate||done.Channels!=draft.Channels||done.Bus!=draft.Bus||done.Frames<0||done.Frames>frames)throw new InvalidDataException("Dokončená nahrávka má neúplná data.");if(!done.Partial)frames=done.Frames;}
        uint bytes=checked((uint)(frames*alignment));int pad=(int)(bytes&1);stream.SetLength(44L+bytes+pad);if(pad!=0){stream.Position=44L+bytes;stream.WriteByte(0);}
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4),36+bytes+(uint)pad);BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(40),bytes);stream.Position=0;stream.Write(header);stream.Flush(true);return frames;
    }
    public Task SaveAsync(RecordingData value)=>database.SaveAsync([UserData.Document("recording",value.Id,value)]);
    public Task RemoveAsync(RecordingData value)=>RemoveManyAsync([value]);
    public async Task RemoveManyAsync(IEnumerable<RecordingData> recordings)
    {
        var owned=recordings.ToArray();if(owned.Length==0)return;if(owned.Length>10000)throw new InvalidDataException("Příliš mnoho nahrávek.");foreach(var item in owned)UserData.Document("recording",item.Id,item);
        // Keep owned media so projects and shared imported blobs remain valid. W41 performs explicit cleanup.
        await UserFiles.WriteTextAsync(Path.Combine(files.DirectoryFor("settings"),"recording-undo.json"),JsonSerializer.Serialize(owned,UserData.Json));await database.RemoveManyAsync(owned.Select(r=>("recording",r.Id)));
    }
    public async Task UndoAsync()
    {
        var path=Path.Combine(files.DirectoryFor("settings"),"recording-undo.json");if(!File.Exists(path))return;if(new FileInfo(path).Length>32*1024*1024)throw new InvalidDataException("Vratná kopie je příliš velká.");
        var values=JsonSerializer.Deserialize<RecordingData[]>(await File.ReadAllTextAsync(path),UserData.Json)??throw new InvalidDataException("Vratná kopie není platná.");var current=await ListAsync();
        foreach(var value in values){UserData.Document("recording",value.Id,value);if(current.Any(r=>r.Id==value.Id))throw new InvalidOperationException("Nahrávka se stejným ID už existuje.");if(!await new OwnedMedia(files).VerifyAsync(new(value.RelativePath,value.Sha256,value.Bytes)))throw new InvalidDataException("Zvukový soubor vratné kopie chybí nebo je změněný.");}
        await database.SaveAsync(values.Select(v=>UserData.Document("recording",v.Id,v)));File.Delete(path);
    }
    public async Task ExportOriginalAsync(RecordingData value,string destination,CancellationToken cancellation=default)
    {
        if(!await new OwnedMedia(files).VerifyAsync(new(value.RelativePath,value.Sha256,value.Bytes),cancellation))throw new InvalidDataException("Nahrávka chybí nebo je změněná.");
        string source=OwnedPaths.Resolve(files.Root,value.RelativePath),target=Path.GetFullPath(destination);
        if(target.StartsWith(files.Root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new InvalidDataException("Export zvol mimo datovou složku Rockeru.");
        string temporary=Path.Combine(Path.GetDirectoryName(target)!,".rocker-export-"+Guid.NewGuid().ToString("N")+".tmp");
        try{await using(var input=File.OpenRead(source))await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,131072,FileOptions.Asynchronous|FileOptions.WriteThrough)){await input.CopyToAsync(output,cancellation);await output.FlushAsync(cancellation);output.Flush(true);}cancellation.ThrowIfCancellationRequested();File.Move(temporary,target,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
