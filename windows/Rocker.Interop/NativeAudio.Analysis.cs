// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct AnalysisSnapshot
{
    public uint StructSize,AbiVersion,Flags,Epoch;
    public ulong Frame,Sequence,HitCount,OnsetFrame,CalibrationToken;
    public double SampleRate,Rms,Peak,Frequency,Clarity,Centroid,HitCentroid,WindowSeconds,AgeSeconds,CalibrationProgress,RecommendedGate;
    public readonly bool ReferencePlaying=>(Flags&128)!=0;
    public readonly bool ConfirmationPlaying=>(Flags&256)!=0;
    public readonly bool PitchDetected=>(Flags&1)!=0;
    public readonly bool Ready=>(Flags&2)!=0;
    public readonly bool ActiveInput=>(Flags&4)!=0;
    public readonly bool Calibrating=>(Flags&8)!=0;
    public readonly bool CalibrationComplete=>(Flags&16)!=0;
    public readonly bool CalibrationInterrupted=>(Flags&32)!=0;
    public readonly bool Stale=>(Flags&64)!=0;
}
internal static class AnalysisMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_reserve_reference",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Reserve(AudioHandle host,uint kind,out ulong graphTicket,out ulong referenceTicket);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_reference",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Reference(IntPtr host,ulong ticket,ulong referenceTicket,uint enabled,double frequency,double volume,uint confirmation);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_analysis",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Get(AudioHandle host,ref AnalysisSnapshot value);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_set_noise_gate",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Gate(AudioHandle host,double value);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_calibrate_noise",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Calibrate(AudioHandle host,out ulong token);
}
public sealed partial class NativeAudio
{
    public Task PlayReferenceAsync(double frequency,double volume,bool confirmation=false)
    {if(!double.IsFinite(frequency) || frequency<20 || frequency>5000)throw new ArgumentOutOfRangeException(nameof(frequency));if(!double.IsFinite(volume) || volume<0 || volume>1)throw new ArgumentOutOfRangeException(nameof(volume));lock(control){CheckAlive();CheckResult(AnalysisMethods.Reserve(handle,confirmation?2u:0u,out var ticket,out var referenceTicket));return ControlAsync(pointer=>AnalysisMethods.Reference(pointer,ticket,referenceTicket,1,frequency,volume,confirmation?1u:0u));}}
    public Task StopReferenceAsync(){lock(control){CheckAlive();CheckResult(AnalysisMethods.Reserve(handle,1,out var ticket,out var referenceTicket));return ControlAsync(pointer=>AnalysisMethods.Reference(pointer,ticket,referenceTicket,0,440,0,0));}}
    public AnalysisSnapshot GetAnalysis(){lock(control){CheckAlive();var value=new AnalysisSnapshot{StructSize=(uint)Marshal.SizeOf<AnalysisSnapshot>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(AnalysisMethods.Get(handle,ref value));return value;}}
    public void SetNoiseGate(double value){if(!double.IsFinite(value) || value<0 || value>.5)throw new ArgumentOutOfRangeException(nameof(value));lock(control){CheckAlive();CheckResult(AnalysisMethods.Gate(handle,value));}}
    public ulong CalibrateNoise(){lock(control){CheckAlive();CheckResult(AnalysisMethods.Calibrate(handle,out var token));return token;}}
}
