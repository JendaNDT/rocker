// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
namespace Rocker.Infrastructure;
public sealed class Autosave(DataRepository database)
{
    private readonly object control = new();
    private readonly Dictionary<(string Kind,string Id),DataDocument> pending = [];
    private readonly SemaphoreSlim flush = new(1,1);
    public event EventHandler<Exception>? Failed;
    private CancellationTokenSource? delay;
    public void Queue(DataDocument document)
    {
        var owned = UserData.Normalize(document);
        lock (control)
        {
            pending[(owned.Kind,owned.Id)] = owned;
            delay?.Cancel(); delay?.Dispose(); delay = new();
            _ = DelayedAsync(delay.Token);
        }
    }
    private async Task DelayedAsync(CancellationToken cancellation)
    {
        try { await Task.Delay(800,cancellation); await FlushAsync(cancellation); }
        catch (OperationCanceledException) { }
        catch (Exception error) { Failed?.Invoke(this,error); }
    }
    public async Task FlushAsync(CancellationToken cancellation = default)
    {
        await flush.WaitAsync(cancellation);
        try
        {
            DataDocument[] batch; lock (control) batch = pending.Values.ToArray();
            if (batch.Length == 0) return;
            await database.SaveAsync(batch,cancellation:cancellation);
            lock (control)
                foreach (var entry in batch)
                    if (pending.TryGetValue((entry.Kind,entry.Id),out var current) && ReferenceEquals(current,entry)) pending.Remove((entry.Kind,entry.Id));
        }
        finally { flush.Release(); }
    }
}
