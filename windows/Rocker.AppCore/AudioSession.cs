// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Interop;
namespace Rocker.AppCore;

public sealed class AudioSession : IAsyncDisposable
{
    private readonly object control = new();
    private readonly Func<NativeAudio> create;
    private NativeAudio? native;
    private Task? disposal;
    public NativeAudio? Native { get { lock (control) return disposal is null ? native : null; } }
    public int HostsCreated { get; private set; }
    public Exception? StartupError { get; private set; }
    public AudioSession(Func<NativeAudio>? create = null) => this.create = create ?? (() => new NativeAudio());
    public NativeAudio EnsureCreated()
    {
        lock (control)
        {
            ObjectDisposedException.ThrowIf(disposal is not null, this);
            if (native is not null) return native;
            try { native = create(); ++HostsCreated; StartupError = null; return native; }
            catch (Exception error) { StartupError = error; throw; }
        }
    }
    public ValueTask DisposeAsync()
    {
        lock (control) return new(disposal ??= DisposeOwnedAsync(native));
    }
    private static async Task DisposeOwnedAsync(NativeAudio? owned) { if (owned is not null) await owned.DisposeAsync(); }
}
