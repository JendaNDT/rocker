// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Rocker.Interop;

public enum AudioState : uint { Stopped, Running, Faulted }

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
    internal const uint AbiVersion = 1;
    private const string Library = "rocker_audio";
    [DllImport(Library, EntryPoint = "rocker_audio_get_abi_version", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint GetAbiVersion();
    [DllImport(Library, EntryPoint = "rocker_audio_create", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Create(in AudioConfig config, out IntPtr handle);
    [DllImport(Library, EntryPoint = "rocker_audio_start", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Start(AudioHandle handle);
    [DllImport(Library, EntryPoint = "rocker_audio_stop", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Stop(AudioHandle handle);
    [DllImport(Library, EntryPoint = "rocker_audio_get_snapshot", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Snapshot(AudioHandle handle, ref AudioSnapshot snapshot);
    [DllImport(Library, EntryPoint = "rocker_audio_copy_last_error", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint Error(AudioHandle handle, [Out] byte[]? buffer, uint capacity, out uint required);
    [DllImport(Library, EntryPoint = "rocker_audio_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Destroy(IntPtr handle);
}

public sealed class NativeAudio : IDisposable
{
    private readonly object control = new();
    private readonly AudioHandle handle;
    private bool disposed;

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

    public void Start() { lock (control) { CheckAlive(); CheckResult(NativeMethods.Start(handle)); } }
    public void Stop() { lock (control) { CheckAlive(); CheckResult(NativeMethods.Stop(handle)); } }
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
    public void Dispose()
    {
        lock (control)
        {
            if (disposed) return;
            handle.Dispose();
            disposed = true;
        }
    }
}
