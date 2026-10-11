// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct ClockSnapshot
{
    public uint StructSize,AbiVersion,Flags,Epoch;
    public ulong Frame,Beat;
    public double SampleRate,BeatPhase,Bpm;
    public uint DspLatencyFrames,Reserved;
    public ulong LoopCycles;
    public readonly bool Running=>(Flags&1)!=0;
    public readonly double ElapsedSeconds=>SampleRate>0?Frame/SampleRate:0;
}
internal static class ClockMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_clock",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Clock(AudioHandle host,ref ClockSnapshot clock);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_set_tempo",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Tempo(AudioHandle host,double bpm);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_set_loop",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Loop(IntPtr host,double start,double end,uint enabled);
}
public sealed partial class NativeAudio
{
    public Task SetLoopAsync(double start,double end,bool enabled)=>ControlAsync(p=>ClockMethods.Loop(p,start,end,enabled?1u:0u));
    public void SetTempo(double bpm){lock(control){CheckAlive();CheckResult(ClockMethods.Tempo(handle,bpm));}}
    public ClockSnapshot GetClock(){lock(control){CheckAlive();var value=new ClockSnapshot{StructSize=(uint)Marshal.SizeOf<ClockSnapshot>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(ClockMethods.Clock(handle,ref value));return value;}}
}
