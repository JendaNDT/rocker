// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct MeterSnapshot
{
    public uint StructSize,AbiVersion,Flags,Epoch;
    public ulong Frame;
    public double SampleRate,InputPeak,InputRms,ProcessedPeak,ProcessedRms,MixPeak,MixRms;
    public ulong InvalidSamples,InputClips,OutputClips,DroppedAnalysisBlocks;
    public readonly bool Monitoring=>(Flags&1)!=0;
    public readonly bool HasInput=>(Flags&2)!=0;
}
internal static class LiveMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_set_monitoring",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Monitor(AudioHandle host,uint enabled,double gain);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_meters",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Meters(AudioHandle host,ref MeterSnapshot snapshot);
}
public sealed partial class NativeAudio
{
    public void SetMonitoring(bool enabled,double gain=0.15)
    { lock(control){CheckAlive();CheckResult(LiveMethods.Monitor(handle,enabled?1u:0u,gain));} }
    public MeterSnapshot GetMeters()
    {
        lock(control){CheckAlive();var value=new MeterSnapshot{StructSize=(uint)Marshal.SizeOf<MeterSnapshot>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(LiveMethods.Meters(handle,ref value));return value;}
    }
}
