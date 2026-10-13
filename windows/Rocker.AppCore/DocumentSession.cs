// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
namespace Rocker.AppCore;

// Session state follows the document, never the window or the current keyboard focus.
public sealed class DocumentSession
{
    private readonly Dictionary<string, object?> state = [];
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public DesktopDocumentKey? Key { get; }
    public NavigationTarget Target { get; set; }
    public bool IsAlive { get; private set; } = true;
    public bool Dirty { get; set; }
    public DocumentSession(NavigationTarget target, DesktopDocumentKey? key = null) { WorkspaceViews.Validate(target); Target = target; Key = key; }
    public T Get<T>(string name, Func<T> initialize)
    {
        if (!state.TryGetValue(name, out var value)) state.Add(name, value = initialize());
        return (T)value!;
    }
    public void Set<T>(string name, T value) => state[name] = value;
    public void Close() => IsAlive = false;
}

public sealed class DocumentContext
{
    private readonly AsyncLocal<DocumentSession?> current = new();
    public DocumentSession Default { get; } = new(new("01"));
    public DocumentSession Active { get; set; }
    public DocumentSession Current => current.Value ?? Active;
    public DocumentContext() => Active = Default;
    public void Select(DocumentSession session) { Active=session; if(current.Value is not null) current.Value=session; }
    public IDisposable Enter(DocumentSession session)
    {
        var previous = current.Value; current.Value = session;
        return new Scope(() => current.Value = previous);
    }
    private sealed class Scope(Action restore) : IDisposable
    {
        private Action? release = restore;
        public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
    }
}

public sealed class DocumentRegistry
{
    private readonly Dictionary<DesktopDocumentKey, DocumentSession> sessions = [];
    public IReadOnlyList<DocumentSession> All => sessions.Values.Where(s => s.IsAlive).ToArray();
    public DocumentSession Open(DesktopDocumentKey key, NavigationTarget target)
    {
        if (sessions.TryGetValue(key, out var existing) && existing.IsAlive) return existing;
        if (All.Count >= 64) throw new InvalidOperationException("Je otevřeno příliš mnoho dokumentů.");
        var session = new DocumentSession(target, key); sessions[key] = session; return session;
    }
    public void Close(DocumentSession session)
    {
        if (session.Key is { } key && sessions.GetValueOrDefault(key) == session) sessions.Remove(key);
        session.Close();
    }
}
