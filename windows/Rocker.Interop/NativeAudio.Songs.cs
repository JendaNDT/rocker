// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
internal static class SongMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_reserve_media",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Reserve(AudioHandle host,out ulong ticket);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_prepare_file_reserved",CallingConvention=CallingConvention.Cdecl)]internal static extern uint File(IntPtr host,ulong ticket,[MarshalAs(UnmanagedType.LPUTF8Str)]string path,double speed,ulong cycles);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_prepare_midi_reserved",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Midi(IntPtr host,ulong ticket,byte[] data,uint count,double speed);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_prepare_file_for_loop",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Ramp(IntPtr host,[MarshalAs(UnmanagedType.LPUTF8Str)]string path,double speed,ulong cycles);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_cancel_ramp",CallingConvention=CallingConvention.Cdecl)]internal static extern uint CancelRamp(IntPtr host);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_copy_waveform",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Waveform(IntPtr host,byte[] buffer,uint capacity,out uint required);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_cancel_media",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Cancel(AudioHandle host);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_play_count_in_reserved",CallingConvention=CallingConvention.Cdecl)]internal static extern uint CountIn(IntPtr host,ulong ticket,uint bars,uint beats,double bpm);
}
public sealed partial class NativeAudio
{
    public Task PrepareFileForLoopAsync(string path,double speed,ulong afterCycles){ArgumentException.ThrowIfNullOrWhiteSpace(path);CheckSpeed(speed);if(afterCycles==0)throw new ArgumentOutOfRangeException(nameof(afterCycles));var owned=Path.GetFullPath(path);return PrepareAsync((pointer,ticket)=>SongMethods.File(pointer,ticket,owned,speed,afterCycles));}
    public Task CancelRampAsync()=>ControlAsync(SongMethods.CancelRamp);
    public void CancelMediaPreparation(){lock(control){CheckAlive();CheckResult(SongMethods.Cancel(handle));}}
    public async Task<float[]> GetWaveformAsync(){var values=await DeviceQueryAsync<float[]>(SongMethods.Waveform);if(values.Length>1024 || values.Any(v=>!float.IsFinite(v) || v<0))throw new InvalidDataException("Zvukový přehled není platný.");return values;}
    public Task PlayWithCountInAsync(uint bars,uint beats,double bpm){lock(control){CheckAlive();CheckResult(MetronomeMethods.Reserve(handle,out var ticket));return ControlAsync(p=>SongMethods.CountIn(p,ticket,bars,beats,bpm));}}
}
