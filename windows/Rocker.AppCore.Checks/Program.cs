// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.AppCore;
using Rocker.Domain;
using Rocker.Infrastructure;
using Rocker.Interop;
using System.Runtime.InteropServices;
using System.Text.Json;

int checks = 0;
void Check(bool condition, string message) { ++checks; if (!condition) throw new Exception(message); }
void Reject(Action action, string message)
{
    bool rejected = false; try { action(); } catch (Exception e) when (e is ArgumentException or InvalidOperationException or ObjectDisposedException) { rejected = true; }
    Check(rejected, message);
}
using (var registry = new OperationRegistry())
{
    using var first = registry.Begin("doc-A", "export", "Export nahrávky");
    using var other = registry.Begin("doc-B", "import", "Import skladby");
    first.Report(.5);
    Check(registry.Snapshot().Count == 2 && registry.Snapshot().Single(s => s.Owner == "doc-A").Progress == .5, "Progress and ownership remain distinct across windows");
    Reject(() => first.Report(double.NaN), "Non-finite progress is refused");
    Reject(() => registry.Begin("app", "restore", "Obnova", exclusive: true), "Restore cannot begin over pending document operations");
    Check(registry.Cancel(first.Status.Id) && first.Cancellation.IsCancellationRequested && registry.Snapshot().Any(s => s.CancellationRequested), "Cancel requests cooperative stop but retains operation until completion");
    first.Dispose(); first.Dispose();
    Check(registry.Snapshot().Count == 1, "Lease disposal removes only its own operation and is idempotent");
    other.Dispose();
    using var barrier = registry.Begin("application", "restore", "Obnova profilu", cancellable: false, exclusive: true);
    Reject(() => registry.Begin("doc-A", "export", "Export"), "Global barrier blocks new operations");
    Check(!registry.Cancel(barrier.Status.Id), "A non-cancellable critical transition is explicit");
}
var closedRegistry = new OperationRegistry(); closedRegistry.Dispose();
Reject(() => closedRegistry.Begin("app", "load", "Load"), "Disposed registry refuses new work");
int creations = 0;
await using (var failingAudio = new AudioSession(() => { ++creations; throw new InvalidOperationException("Synthetic unavailable backend."); }))
{
    Check(failingAudio.Native is null && creations == 0, "Viewing services never creates or starts an audio host");
    Reject(() => failingAudio.EnsureCreated(), "Missing audio backend surfaces an error");
    Check(failingAudio.StartupError is not null && failingAudio.HostsCreated == 0, "Failed creation is not a fabricated successful host");
}
var documents = new DocumentRegistry();
var keyA=DesktopDocumentKey.ForObject("15","project-A");
var docA=documents.Open(keyA,new("15","project-A"));
var docB=documents.Open(DesktopDocumentKey.ForObject("15","project-B"),new("15","project-B"));
Check(ReferenceEquals(docA,documents.Open(keyA,new("15","project-A"))),"Duplicate opening activates the existing document");
docA.Set("undo",3);docB.Set("undo",7);
Check(docA.Get("undo",()=>0)==3 && docB.Get("undo",()=>0)==7,"Each project retains its own history");
var context=new DocumentContext();context.Active=docA;
using(context.Enter(docB)){await Task.Yield();Check(context.Current==docB,"Await retains the originating document");}
Check(context.Current==docA,"Leaving the scope restores the active document");
var transport=new TransportCoordinator();
await transport.TakeAsync(docA,()=>Task.CompletedTask);
context.Active=docB;
Check(transport.Owner==docA,"Keyboard focus never changes the audio owner");
transport.Capturing=true;
try{await transport.TakeAsync(docB,()=>Task.CompletedTask);throw new Exception("Capture conflict was accepted");}catch(InvalidOperationException){Check(transport.Owner==docA,"Source conflict preserves the original owner");}
transport.Capturing=false;await transport.TakeAsync(docB,()=>Task.CompletedTask);
transport.SetGrader(docA);documents.Close(docA);transport.Release(docA);
Check(transport.Grader is null && transport.Owner==docB && documents.All.Count==1,"Closing a grader releases only its own authority");
var exit=new ExitCoordinator();int saved=0;
exit.Guards["doc-a/studio"]=new(new(false,true),()=>Task.FromResult(false),()=>{});
Check(!await exit.ResolveAsync(_=>Task.FromResult(CloseChoice.Save)) && exit.Guards.Count==1,"Failed save preserves the document and blocks shutdown");
Check(!await exit.ResolveAsync(_=>Task.FromResult(CloseChoice.Cancel)) && exit.Guards.Count==1,"Canceled exit retains the pending work");
exit.Guards["doc-a/studio"]=new(new(false,true),()=>{++saved;return Task.FromResult(true);},()=>{});
Check(await exit.ResolveAsync(_=>Task.FromResult(CloseChoice.Save),"doc-a") && saved==1 && exit.Guards.Count==0,"Explicit save resolves only the requested document");
int discarded=0,decisions=0;
exit.Guards["a/work"]=new(new(false,true),()=>Task.FromResult(true),()=>++discarded);
exit.Guards["b/work"]=new(new(false,true),()=>Task.FromResult(false),()=>++discarded);
Check(!await exit.ResolveAsync(_=>Task.FromResult(++decisions==1?CloseChoice.Discard:CloseChoice.Cancel)) && discarded==0 && exit.Guards.Count==2,"Canceling a later window retains earlier proposed discards");
decisions=0;Check(!await exit.ResolveAsync(_=>Task.FromResult(++decisions==1?CloseChoice.Discard:CloseChoice.Save)) && discarded==0 && exit.Guards.Count==2,"Failed save prevents every proposed discard and partial shutdown");
Check(await exit.ResolveAsync(_=>Task.FromResult(CloseChoice.Discard)) && discarded==2 && exit.Guards.Count==0,"Discards apply together after all decisions succeed");
var router=new CommandRouter();var commandContext=new CommandContext(docB,"window-b",docB);DocumentSession? commandOwner=null;
router.Register(new("save","Uložit",_=>true,async c=>{await Task.Yield();commandOwner=c.Session;}));
Check(await router.ExecuteAsync("save",commandContext) && commandOwner==docB,"Command retains the initiating document across an await");
Reject(()=>router.Register(new("save","Duplicate",_=>true,_=>Task.CompletedTask)),"Command IDs remain unique");
Check(!router.CanExecute("unknown",commandContext),"Unregistered commands do not execute arbitrary stored actions");
string directory = Path.Combine(Path.GetTempPath(), "rocker-app-services-" + Guid.NewGuid().ToString("N"));
try
{
    var services = new ApplicationServices(new UserFiles(directory));
    var a = services.InitializeStorageAsync(); var b = services.InitializeStorageAsync();
    var storage = await a;
    Check(ReferenceEquals(storage, await b) && ReferenceEquals(services.Storage, storage), "Concurrent initialization shares one database/autosave/recording session");
    storage.Autosave.Queue(UserData.Document("settings", "default", new AppSettings(Theme: "dark")));
    await services.FlushAsync();
    var snapshot = await services.ReadSnapshotAsync();
    Check(UserData.Read<AppSettings>(snapshot.Documents.Single(d => d.Kind == "settings").Value).Theme == "dark", "Shared autosave flush persists real data");
    Check(services.Operations.Snapshot().Count == 0 && services.Audio.Native is null, "Storage completion releases lease without opening audio");
    await services.DisposeAsync(); await services.DisposeAsync();
    Check(services.IsClosing, "Successful shutdown closes services once");
    Reject(() => services.InitializeStorageAsync(), "Closed services cannot reinitialize behind a canceled window");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
bool nativeChecked = args.Length > 1;
if (nativeChecked)
{
    string path = Path.GetFullPath(args[1]);
    NativeLibrary.SetDllImportResolver(typeof(NativeAudio).Assembly, (name, _, _) => name == "rocker_audio" ? NativeLibrary.Load(path) : nint.Zero);
    var audio = new AudioSession();
    var first = audio.EnsureCreated(); var same = audio.EnsureCreated();
    Check(ReferenceEquals(first, same) && audio.HostsCreated == 1, "Repeated views share exactly one native handle");
    Check(first.GetSnapshot().State == AudioState.Stopped && !first.GetMeters().Monitoring, "Construction opens no device and enables no monitoring");
    string mediaPath=Path.Combine(Path.GetTempPath(),"rocker-desktop-ticket-"+Guid.NewGuid().ToString("N")+".wav");
    try
    {
        using(var writer=new BinaryWriter(File.Create(mediaPath)))
        {
            int frames=4800;writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+frames*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(48000);writer.Write(96000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(frames*2);for(int sample=0;sample<frames;++sample)writer.Write((short)(Math.Sin(sample*Math.PI*2*440/48000)*8192));
        }
        ulong ticket=await first.PrepareFileWithIdentityAsync(mediaPath);
        Check(ticket>0 && await first.GetMediaSourceTicketAsync()==ticket,"A file preparation identifies the actual adopted source, not an old clip with the same speed");
        var inspected=await MediaInspection.ReadAsync(mediaPath);
        Check(Math.Abs(inspected.DurationSeconds-.1)<1e-9 && inspected.Peaks.Max()>.24,"Independent inspection returns real decoded duration and peaks");
        Check(await first.GetMediaSourceTicketAsync()==ticket && first.GetSnapshot().State==AudioState.Stopped,"Viewing a second waveform retains the original source and stopped device");
    }
    finally{if(File.Exists(mediaPath))File.Delete(mediaPath);}
    await audio.DisposeAsync(); await audio.DisposeAsync();
    Check(audio.Native is null, "App shutdown releases native owner once");
    Reject(() => audio.EnsureCreated(), "A closed audio session cannot create a replacement host");
}
var report = new { result = "passed", checks, nativeTestBackendVerified = nativeChecked, actualDeviceVerified = false, storageSingletonVerified = true, operationsScopedVerified = true };
if (args.Length > 0) await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(report));
Console.WriteLine($"PASS: {checks} application service, operation ownership, storage and audio lifetime checks.");
