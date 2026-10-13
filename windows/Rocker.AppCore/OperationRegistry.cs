// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
namespace Rocker.AppCore;

public sealed record OperationStatus(string Id, string Owner, string Kind, string Title, double? Progress,
    bool CanCancel, bool CancellationRequested, bool Exclusive);

public sealed class OperationRegistry : IDisposable
{
    private readonly object control = new();
    private readonly Dictionary<string, OperationLease> active = [];
    private bool disposed;
    public event EventHandler? Changed;
    public IReadOnlyList<OperationStatus> Snapshot()
    {
        lock (control) return active.Values.Select(l => l.Status).ToArray();
    }
    public OperationLease Begin(string owner, string kind, string title, bool cancellable = true, bool exclusive = false)
    {
        if (!UserData.ValidId(owner) || !UserData.ValidId(kind) || string.IsNullOrWhiteSpace(title) || title.Length > 512)
            throw new ArgumentException("Operace nemá platnou identitu.");
        OperationLease lease;
        lock (control)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (active.Count >= 64 || active.Values.Any(v => v.Status.Exclusive) || exclusive && active.Count != 0)
                throw new InvalidOperationException("Nejdřív dokonči nebo zruš probíhající operaci.");
            lease = new(this, new(Guid.NewGuid().ToString("N"), owner, kind, title, null, cancellable, false, exclusive));
            active.Add(lease.Status.Id, lease);
        }
        Changed?.Invoke(this, EventArgs.Empty); return lease;
    }
    internal void Update(OperationLease lease, double? progress)
    {
        if (progress is not null && (!double.IsFinite(progress.Value) || progress is < 0 or > 1)) throw new ArgumentException("Průběh není platný.");
        lock (control) { if (!active.ContainsKey(lease.Status.Id)) return; lease.SetProgress(progress); }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public bool Cancel(string id)
    {
        OperationLease? lease;
        lock (control)
        {
            if (!active.TryGetValue(id, out lease) || !lease.Status.CanCancel) return false;
            lease.MarkCancellationRequested();
        }
        lease.Cancel(); Changed?.Invoke(this, EventArgs.Empty); return true;
    }
    internal void End(OperationLease lease)
    {
        lock (control) active.Remove(lease.Status.Id);
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Dispose()
    {
        OperationLease[] owned;
        lock (control) { if (disposed) return; disposed = true; owned = active.Values.ToArray(); }
        foreach (var lease in owned) { lease.Cancel(); lease.Dispose(); }
    }
}
public sealed class OperationLease : IDisposable
{
    private readonly OperationRegistry registry;
    private readonly CancellationTokenSource cancellation = new();
    private int ended;
    public OperationStatus Status { get; private set; }
    public CancellationToken Cancellation => cancellation.Token;
    internal OperationLease(OperationRegistry registry, OperationStatus status) { this.registry = registry; Status = status; }
    internal void SetProgress(double? value) => Status = Status with { Progress = value };
    internal void MarkCancellationRequested() => Status = Status with { CancellationRequested = true };
    internal void Cancel() { try { cancellation.Cancel(); } catch (ObjectDisposedException) { } }
    public void Report(double? value) => registry.Update(this, value);
    public void Dispose() { if (Interlocked.Exchange(ref ended, 1) != 0) return; registry.End(this); cancellation.Dispose(); }
}
