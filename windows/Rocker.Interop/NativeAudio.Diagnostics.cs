// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct DiagnosticSnapshot
{
    public uint StructSize,AbiVersion,Epoch,Flags;
    public ulong Callbacks,Frames,TotalNanoseconds,LastNanoseconds,MaxNanoseconds,OverBudget,AnalysisDropped,AnalysisHighWater,DeviceFaults,DeviceStarts,StopRequests,MediaErrors,InvalidCallbacks;
    public long DriverXruns;public double SampleRate;
    public uint MinFrames,MaxFrames,LastFailureReason,SourceMode;
    public ulong ConfigurationFailures;
    public readonly bool Running=>(Flags&1)!=0;
    public readonly bool DriverXrunsKnown=>(Flags&2)!=0;
    public readonly double AverageMicroseconds=>Callbacks>0?TotalNanoseconds/(double)Callbacks/1000:0;
    public readonly double MaxMicroseconds=>MaxNanoseconds/1000.0;
    public readonly double GraphBudgetPercent=>Frames>0 && SampleRate>0?TotalNanoseconds/(Frames/SampleRate*1e9)*100:0;
}
internal static class DiagnosticMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_diagnostics",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Get(AudioHandle host,ref DiagnosticSnapshot value);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_refresh_diagnostics",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Refresh(IntPtr host);
}
public sealed partial class NativeAudio
{
    public DiagnosticSnapshot GetDiagnostics(){lock(control){CheckAlive();var value=new DiagnosticSnapshot{StructSize=(uint)Marshal.SizeOf<DiagnosticSnapshot>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(DiagnosticMethods.Get(handle,ref value));return value;}}
    public Task RefreshDiagnosticsAsync()=>ControlAsync(DiagnosticMethods.Refresh);
}
