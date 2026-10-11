// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
[StructLayout(LayoutKind.Sequential,Pack=8)]
public readonly struct PreviewNote(double startSeconds,double durationSeconds,double frequency,double volume=.25)
{
    public readonly double StartSeconds=startSeconds,DurationSeconds=durationSeconds,Frequency=frequency,Volume=volume;
}
internal static class PreviewMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_reserve_preview",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Reserve(AudioHandle host,out ulong graph,out ulong phrase);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_preview_reserved",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Play(IntPtr host,ulong graph,ulong phrase,[In]PreviewNote[] notes,uint count,uint repeat,uint countIn);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_stop_preview",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Stop(AudioHandle host);
}
public sealed partial class NativeAudio
{
    public Task PlayPreviewAsync(IReadOnlyList<PreviewNote> notes,bool repeat=false,uint countInBeats=0)
    {
        var owned=notes.ToArray();if(owned.Length is <1 or >128 || countInBeats>32 || owned.Any(n=>!double.IsFinite(n.StartSeconds)||n.StartSeconds<0||!double.IsFinite(n.DurationSeconds)||n.DurationSeconds<=0||n.StartSeconds+n.DurationSeconds>60||!double.IsFinite(n.Frequency)||n.Frequency is <20 or >5000||!double.IsFinite(n.Volume)||n.Volume is <0 or >1))throw new ArgumentException("Neplatná zvuková ukázka.");
        lock(control){CheckAlive();CheckResult(PreviewMethods.Reserve(handle,out var graph,out var phrase));return ControlAsync(p=>PreviewMethods.Play(p,graph,phrase,owned,(uint)owned.Length,repeat?1u:0u,countInBeats));}
    }
    public Task StopPreviewAsync(){lock(control){CheckAlive();CheckResult(PreviewMethods.Stop(handle));return Task.CompletedTask;}}
}
