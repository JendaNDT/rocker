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
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct RecordingAlignment {public uint StructSize,AbiVersion;public ulong Token,MediaTicket;public double FirstProjectFrame,ProjectRate,CaptureRate;public uint Flags,Failure,DspFrames,Reserved;public readonly bool Started=>(Flags&2)!=0;public readonly bool Continuous=>(Flags&4)!=0 && Failure==0;}
internal static class RecordingMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_start_project_recording_reserved",CallingConvention=CallingConvention.Cdecl)]internal static extern uint StartProject(IntPtr host,ulong graph,ulong take,[MarshalAs(UnmanagedType.LPUTF8Str)]string path,uint bus,uint bars,ulong project);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_recording_alignment",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Alignment(AudioHandle host,ref RecordingAlignment value);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_reserve_recording",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Reserve(AudioHandle host,out ulong graph,out ulong take);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_start_recording_reserved",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Start(IntPtr host,ulong graph,ulong take,[MarshalAs(UnmanagedType.LPUTF8Str)]string path,uint bus,uint bars);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_request_recording_stop",CallingConvention=CallingConvention.Cdecl)]internal static extern uint RequestStop(AudioHandle host);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_finish_recording",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Finish(IntPtr host);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_recording",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Get(AudioHandle host,ref RecordingSnapshot value);
}
public sealed partial class NativeAudio
{
    public async Task<ulong> StartRecordingAsync(string path,uint bus,uint countInBars=0,ulong projectTicket=0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);if(bus>2 || countInBars>2)throw new ArgumentOutOfRangeException(nameof(bus));
        var owned=Path.GetFullPath(path);Task task;ulong take;
        lock(control){CheckAlive();CheckResult(RecordingMethods.Reserve(handle,out var graph,out take));task=ControlAsync(p=>projectTicket==0?RecordingMethods.Start(p,graph,take,owned,bus,countInBars):RecordingMethods.StartProject(p,graph,take,owned,bus,countInBars,projectTicket));}
        await task;return take;
    }
    public Task StopRecordingAsync(){lock(control){CheckAlive();CheckResult(RecordingMethods.RequestStop(handle));return ControlAsync(RecordingMethods.Finish);}}
    public RecordingAlignment GetRecordingAlignment(){lock(control){CheckAlive();var value=new RecordingAlignment{StructSize=(uint)Marshal.SizeOf<RecordingAlignment>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(RecordingMethods.Alignment(handle,ref value));return value;}}
    public RecordingSnapshot GetRecording(){lock(control){CheckAlive();var value=new RecordingSnapshot{StructSize=(uint)Marshal.SizeOf<RecordingSnapshot>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(RecordingMethods.Get(handle,ref value));return value;}}
}
