// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct RecordingSnapshot
{
    public uint StructSize,AbiVersion,State,Failure,Bus,SampleRate,Channels,Epoch;
    public ulong Token,WrittenFrames,CapturedFrames,FirstFrame,DroppedBlocks,HighWater;
    public uint Flags,DspLatencyFrames;
    public readonly bool Active=>State is 1 or 2 or 3;
    public readonly bool Complete=>State==4 && Failure==0 && (Flags&1)!=0;
    public readonly bool HeaderFinalized=>(Flags&1)!=0;
    public readonly double Duration=>SampleRate>0?WrittenFrames/(double)SampleRate:0;
}
internal static class RecordingMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_reserve_recording",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Reserve(AudioHandle host,out ulong graph,out ulong take);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_start_recording_reserved",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Start(IntPtr host,ulong graph,ulong take,[MarshalAs(UnmanagedType.LPUTF8Str)]string path,uint bus,uint bars);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_request_recording_stop",CallingConvention=CallingConvention.Cdecl)]internal static extern uint RequestStop(AudioHandle host);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_finish_recording",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Finish(IntPtr host);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_recording",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Get(AudioHandle host,ref RecordingSnapshot value);
}
public sealed partial class NativeAudio
{
    public async Task<ulong> StartRecordingAsync(string path,uint bus,uint countInBars=0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);if(bus>2 || countInBars>2)throw new ArgumentOutOfRangeException(nameof(bus));
        var owned=Path.GetFullPath(path);Task task;ulong take;
        lock(control){CheckAlive();CheckResult(RecordingMethods.Reserve(handle,out var graph,out take));task=ControlAsync(p=>RecordingMethods.Start(p,graph,take,owned,bus,countInBars));}
        await task;return take;
    }
    public Task StopRecordingAsync(){lock(control){CheckAlive();CheckResult(RecordingMethods.RequestStop(handle));return ControlAsync(RecordingMethods.Finish);}}
    public RecordingSnapshot GetRecording(){lock(control){CheckAlive();var value=new RecordingSnapshot{StructSize=(uint)Marshal.SizeOf<RecordingSnapshot>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(RecordingMethods.Get(handle,ref value));return value;}}
}
