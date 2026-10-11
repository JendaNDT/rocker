// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Rocker.Interop;

public enum AudioState : uint { Stopped, Running, Faulted, Starting, Reconfiguring, Stopping, Closing }

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct AudioSnapshot
{
    public uint StructSize, AbiVersion;
    public AudioState State;
    public uint Reserved;
    public double SampleRate;
    public uint BufferFrames, OutputChannels;
    public ulong RenderedFrames;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct TransportSnapshot
{
    public uint StructSize, AbiVersion, Ready, Playing, Ended, Reserved;
    public double DurationSeconds, PositionSeconds, Speed, Volume;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct AudioConfig
{
    public uint StructSize, AbiVersion;
    public double FrequencyHz, Amplitude;
}

internal sealed class AudioHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal AudioHandle(IntPtr value) : base(true) => SetHandle(value);
    protected override bool ReleaseHandle() { NativeMethods.Destroy(handle); return true; }
}

internal static class NativeMethods
{
    internal const uint AbiVersion = 8;
    private const string Library = "rocker_audio";
    [DllImport(Library, EntryPoint = "rocker_audio_get_abi_version", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint GetAbiVersion();
    [DllImport(Library, EntryPoint = "rocker_audio_create", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Create(in AudioConfig config, out IntPtr handle);
    [DllImport(Library, EntryPoint = "rocker_audio_start", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Start(AudioHandle handle);
    [DllImport(Library,EntryPoint="rocker_audio_reserve_start",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint ReserveStart(AudioHandle handle,out ulong ticket);
    [DllImport(Library,EntryPoint="rocker_audio_start_reserved",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint StartReserved(IntPtr handle,ulong ticket,uint media);
    [DllImport(Library, EntryPoint="rocker_audio_start",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint StartRaw(IntPtr handle);
    [DllImport(Library, EntryPoint="rocker_audio_play",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint PlayRaw(IntPtr handle);
    [DllImport(Library, EntryPoint="rocker_audio_stop",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint StopRaw(IntPtr handle);
    [DllImport(Library,EntryPoint="rocker_audio_reserve_stop",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint ReserveStop(AudioHandle handle,out ulong ticket);
    [DllImport(Library,EntryPoint="rocker_audio_stop_reserved",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint StopReserved(IntPtr handle,ulong ticket);
    [DllImport(Library, EntryPoint="rocker_audio_request_stop",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint RequestStop(AudioHandle handle);
    [DllImport(Library, EntryPoint = "rocker_audio_stop", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Stop(AudioHandle handle);
    [DllImport(Library, EntryPoint = "rocker_audio_get_snapshot", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Snapshot(AudioHandle handle, ref AudioSnapshot snapshot);
    [DllImport(Library, EntryPoint = "rocker_audio_prepare_file", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint PrepareFile(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, double speed);
    [DllImport(Library, EntryPoint = "rocker_audio_prepare_midi", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint PrepareMidi(IntPtr handle, byte[] bytes, uint count, double speed);
    [DllImport(Library, EntryPoint = "rocker_audio_play", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Play(AudioHandle handle);
    [DllImport(Library, EntryPoint = "rocker_audio_seek", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Seek(AudioHandle handle, double seconds);
    [DllImport(Library, EntryPoint="rocker_audio_seek",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint SeekRaw(IntPtr handle,double seconds);
    [DllImport(Library, EntryPoint = "rocker_audio_set_volume", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Volume(AudioHandle handle, double volume);
    [DllImport(Library, EntryPoint = "rocker_audio_get_transport", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Transport(AudioHandle handle, ref TransportSnapshot snapshot);
    [DllImport(Library, EntryPoint = "rocker_audio_copy_last_error", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Error(AudioHandle handle, [Out] byte[]? buffer, uint capacity, out uint required);
    [DllImport(Library, EntryPoint = "rocker_audio_copy_last_error", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint ErrorRaw(IntPtr handle, [Out] byte[]? buffer, uint capacity, out uint required);
    [DllImport(Library, EntryPoint = "rocker_audio_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Destroy(IntPtr handle);
}

public sealed partial class NativeAudio : IDisposable, IAsyncDisposable
{
    private readonly object control = new();
    private readonly AudioHandle handle;
    private bool disposed;
    private bool preparing;
    private readonly List<Task> workers = new(64);
    private Task? disposal;

    public NativeAudio()
    {
        if (NativeMethods.GetAbiVersion() != NativeMethods.AbiVersion)
            throw new InvalidOperationException("Zvuková knihovna patří k jiné verzi aplikace.");
        var config = new AudioConfig
        {
            StructSize = (uint)Marshal.SizeOf<AudioConfig>(), AbiVersion = NativeMethods.AbiVersion,
            FrequencyHz = 440, Amplitude = 0.02
        };
        var result = NativeMethods.Create(in config, out var pointer);
        if (result != 0 || pointer == IntPtr.Zero)
            throw new InvalidOperationException($"Zvukové jádro se nepodařilo vytvořit (stav {result}).");
        handle = new AudioHandle(pointer);
    }

    public Task StartAsync() => ReservedStartAsync(0);
    public Task StartLiveAsync()=>ReservedStartAsync(2);
    private Task ReservedStartAsync(uint mode)
    {
        lock(control) {CheckAlive();CheckResult(NativeMethods.ReserveStart(handle,out var ticket));return ControlAsync(pointer=>NativeMethods.StartReserved(pointer,ticket,mode));}
    }
    public Task SeekAsync(double seconds) => ControlAsync(pointer=>NativeMethods.SeekRaw(pointer,seconds));
    public Task PlayAsync() => ReservedStartAsync(1);
    public Task StopAsync()
    {
        lock(control) { CheckAlive(); CheckResult(NativeMethods.ReserveStop(handle,out var ticket)); return ControlAsync(pointer=>NativeMethods.StopReserved(pointer,ticket)); }
    }
    private Task ControlAsync(Func<IntPtr,uint> operation)
    {
        lock(control)
        {
            CheckAlive();workers.RemoveAll(task=>task.IsCompleted);
            if(workers.Count>=64)throw new InvalidOperationException("Zvuk má příliš mnoho čekajících operací.");
            bool retained=false;handle.DangerousAddRef(ref retained);
            try
            {
                var pointer=handle.DangerousGetHandle();
                var task=Task.Run(()=> {try {uint result=operation(pointer);if(result!=0)throw new InvalidOperationException($"Zvuková operace selhala (stav {result}). {ReadWorkerError(pointer)}");}finally{handle.DangerousRelease();}});
                workers.Add(task);return task;
            }
            catch{if(retained)handle.DangerousRelease();throw;}
        }
    }
    public void Start() { lock (control) { CheckAlive(); CheckResult(NativeMethods.Start(handle)); } }
    public void Stop() { lock (control) { CheckAlive(); CheckResult(NativeMethods.Stop(handle)); } }
    public void Play() { lock (control) { CheckAlive(); CheckResult(NativeMethods.Play(handle)); } }
    public void Seek(double seconds) { lock (control) { CheckAlive(); CheckResult(NativeMethods.Seek(handle, seconds)); } }
    public void SetVolume(double volume) { lock (control) { CheckAlive(); CheckResult(NativeMethods.Volume(handle, volume)); } }
    public Task PrepareFileAsync(string path, double speed = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Contains('\0')) throw new ArgumentException("Cesta obsahuje neplatný znak.", nameof(path));
        CheckSpeed(speed);
        return PrepareAsync(pointer => NativeMethods.PrepareFile(pointer, Path.GetFullPath(path), speed));
    }
    public Task PrepareMidiAsync(byte[] bytes, double speed = 1)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        CheckSpeed(speed);
        // The worker owns an immutable copy; callers can reuse their MIDI buffer.
        if (bytes.Length is 0 or > 4 * 1024 * 1024) throw new ArgumentException("MIDI musí mít nejvýše 4 MB.", nameof(bytes));
        var owned = (byte[])bytes.Clone();
        return PrepareAsync(pointer => NativeMethods.PrepareMidi(pointer, owned, (uint)owned.Length, speed));
    }
    private Task PrepareAsync(Func<IntPtr, uint> operation)
    {
        lock (control)
        {
            CheckAlive();
            workers.RemoveAll(task=>task.IsCompleted);
            if(workers.Count>=64)throw new InvalidOperationException("Zvuk má příliš mnoho čekajících operací.");
            if (preparing) throw new InvalidOperationException("Zvuk se už načítá. Počkej na dokončení.");
            bool retained = false;
            handle.DangerousAddRef(ref retained);
            try
            {
                var pointer = handle.DangerousGetHandle();
                preparing = true;
                var task = Task.Run(() =>
                {
                    try
                    {
                        var result = operation(pointer);
                        if (result != 0) throw new InvalidOperationException($"Načtení zvuku selhalo (stav {result}). {ReadWorkerError(pointer)}");
                    }
                    finally { lock (control) preparing = false; handle.DangerousRelease(); }
                });
                workers.Add(task);return task;
            }
            catch { preparing = false; if (retained) handle.DangerousRelease(); throw; }
        }
    }
    private static void CheckSpeed(double speed)
    {
        if (!double.IsFinite(speed) || speed < 0.25 || speed > 2) throw new ArgumentOutOfRangeException(nameof(speed), "Rychlost musí být 0,25× až 2×.");
    }
    private static string ReadWorkerError(IntPtr pointer)
    {
        if (NativeMethods.ErrorRaw(pointer, null, 0, out var required) != 0 || required == 0 || required > 65536)
            return "Zvuková chyba bez dostupných podrobností.";
        var bytes = new byte[required];
        if (NativeMethods.ErrorRaw(pointer, bytes, required, out _) != 0) return "Podrobnosti zvukové chyby nelze načíst.";
        var end = Array.IndexOf(bytes, (byte)0);
        return Encoding.UTF8.GetString(bytes.AsSpan(0, end < 0 ? bytes.Length : end));
    }
    public TransportSnapshot GetTransport()
    {
        lock (control)
        {
            CheckAlive();
            var snapshot = new TransportSnapshot { StructSize = (uint)Marshal.SizeOf<TransportSnapshot>(), AbiVersion = NativeMethods.AbiVersion };
            CheckResult(NativeMethods.Transport(handle, ref snapshot));
            return snapshot;
        }
    }
    public AudioSnapshot GetSnapshot()
    {
        lock (control)
        {
            CheckAlive();
            var snapshot = new AudioSnapshot
            {
                StructSize = (uint)Marshal.SizeOf<AudioSnapshot>(), AbiVersion = NativeMethods.AbiVersion
            };
            CheckResult(NativeMethods.Snapshot(handle, ref snapshot));
            return snapshot;
        }
    }
    public string GetLastError()
    {
        lock (control) { CheckAlive(); return ReadLastError(); }
    }
    private string ReadLastError()
    {
        if (NativeMethods.Error(handle, null, 0, out var required) != 0 || required == 0 || required > 65536)
            return "Zvuková chyba bez dostupných podrobností.";
        var bytes = new byte[required];
        if (NativeMethods.Error(handle, bytes, required, out _) != 0)
            return "Podrobnosti zvukové chyby nelze načíst.";
        return Encoding.UTF8.GetString(bytes.AsSpan(0, Array.IndexOf(bytes, (byte)0)));
    }
    private void CheckResult(uint result)
    {
        if (result != 0) throw new InvalidOperationException($"Zvuková operace selhala (stav {result}). {ReadLastError()}");
    }
    private void CheckAlive() => ObjectDisposedException.ThrowIf(disposed, this);
    public ValueTask DisposeAsync()
    {
        lock(control)
        {
            if(disposal is not null)return new(disposal);
            disposed=true;NativeMethods.RequestStop(handle);
            bool retained=false;handle.DangerousAddRef(ref retained);
            var pointer=handle.DangerousGetHandle();var pending=workers.ToArray();handle.Dispose();
            disposal=Task.Run(async ()=>
            {
                try
                {
                    NativeMethods.StopRaw(pointer);
                    try{await Task.WhenAll(pending);}catch(Exception){ /* Worker errors are observed by their original callers. */ }
                }
                finally{handle.DangerousRelease();}
            });
            return new(disposal);
        }
    }
    public void Dispose()=>DisposeAsync().GetAwaiter().GetResult();
}
