// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.AppCore;

public sealed record StorageSession(DataRepository Database, Autosave Autosave, RecordingLibrary Recordings, ProjectRepository Projects);
public sealed class ApplicationServices : IAsyncDisposable
{
    private readonly object control = new();
    private Task<StorageSession>? initialize;
    private Task? disposal;
    public UserFiles Files { get; }
    public StorageSession? Storage { get; private set; }
    public OperationRegistry Operations { get; } = new();
    public AudioSession Audio { get; }
    public DocumentRegistry Documents { get; } = new();
    public TransportCoordinator Transport { get; } = new();
    public PracticeCoordinator Practice { get; }=new();
    public ExitCoordinator Exit { get; }=new();
    public CommandRouter Commands { get; }=new();
    public AppSettings Settings { get; set; } = new();
    public ContentCatalog? Catalog { get; private set; }
    public bool IsClosing { get; private set; }
    public ApplicationServices(UserFiles files, AudioSession? audio = null) { Files = files; Audio = audio ?? new(); }
    public ContentCatalog LoadCatalog(string path) => Catalog ??= ContentCatalog.Load(File.ReadAllBytes(path));
    public Task<StorageSession> InitializeStorageAsync()
    {
        lock (control)
        {
            ObjectDisposedException.ThrowIf(IsClosing, this);
            if (initialize is { IsFaulted: true } or { IsCanceled: true }) initialize = null;
            return initialize ??= OpenStorageAsync();
        }
    }
    private async Task<StorageSession> OpenStorageAsync()
    {
        using var operation = Operations.Begin("application", "storage", "Načítání místního profilu", cancellable: false);
        await BackupService.RecoverInterruptedAsync(Files);
        var database = await DataRepository.OpenAsync(Files);
        var session = new StorageSession(database, new(database), new(Files, database), new(Files, database));
        Storage = session; return session;
    }
    public async Task<DataSnapshot> ReadSnapshotAsync()
    {
        var storage = await InitializeStorageAsync(); return await storage.Database.SnapshotAsync();
    }
    public async Task FlushAsync()
    {
        if (Storage is { } storage) await storage.Autosave.FlushAsync();
    }
    public ValueTask DisposeAsync()
    {
        lock (control) return new(disposal ??= Task.Run(CloseAsync));
    }
    private async Task CloseAsync()
    {
        // A failed flush leaves services available so a canceled exit can retry.
        try
        {
            Task<StorageSession>? pending;
            lock(control) pending=initialize;
            if(pending is not null){try{await pending;}catch(Exception){ /* A failed profile load has no pending writer to flush. */ }}
            await FlushAsync(); IsClosing = true;
            Operations.Dispose();
            await Audio.DisposeAsync();
        }
        catch { lock (control) { disposal = null; if (Audio.Native is not null) IsClosing = false; } throw; }
    }
}
