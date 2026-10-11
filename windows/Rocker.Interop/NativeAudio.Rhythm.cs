// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct RhythmSnapshot
{
    public uint StructSize,AbiVersion,State,Epoch,Bpm,Flags,LastCompletedBpm,Reserved;
    public ulong Token,StartFrame,Rounds;public double ElapsedBeats,RoundBeats;
    public readonly bool Active=>State is 1 or 2;
}
internal static class RhythmMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_reserve_rhythm",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Reserve(AudioHandle host,out ulong graph,out ulong trainer,out ulong phrase);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_start_rhythm_reserved",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Start(IntPtr host,ulong graph,ulong trainer,ulong phrase,[In]PreviewNote[] notes,uint count,double patternBeats,double roundBeats,uint bpm,uint speedUp,uint countIn);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_reserve_rhythm_stop",CallingConvention=CallingConvention.Cdecl)]internal static extern uint ReserveStop(AudioHandle host,out ulong ticket);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_finish_rhythm_stop",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Stop(IntPtr host,ulong ticket);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_rhythm",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Get(AudioHandle host,ref RhythmSnapshot value);
}
public sealed partial class NativeAudio
{
    public async Task<ulong> StartRhythmAsync(IReadOnlyList<PreviewNote> notes,double patternBeats,double roundBeats,uint bpm,bool speedUp,uint countInBeats=4)
    {
        var owned=notes.ToArray();if(owned.Length is <1 or >128||!double.IsFinite(patternBeats)||patternBeats is <=0 or >60||!double.IsFinite(roundBeats)||roundBeats is <=0 or >4096||bpm is <40 or >240||countInBeats is not(0 or 4 or 8))throw new ArgumentException("Neplatné rytmické cvičení.");
        Task task;ulong trainer;lock(control){CheckAlive();CheckResult(RhythmMethods.Reserve(handle,out var graph,out trainer,out var phrase));task=ControlAsync(p=>RhythmMethods.Start(p,graph,trainer,phrase,owned,(uint)owned.Length,patternBeats,roundBeats,bpm,speedUp?1u:0u,countInBeats));}await task;return trainer;
    }
    public Task StopRhythmAsync(){lock(control){CheckAlive();CheckResult(RhythmMethods.ReserveStop(handle,out var ticket));return ControlAsync(p=>RhythmMethods.Stop(p,ticket));}}
    public RhythmSnapshot GetRhythm(){lock(control){CheckAlive();var value=new RhythmSnapshot{StructSize=(uint)Marshal.SizeOf<RhythmSnapshot>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(RhythmMethods.Get(handle,ref value));return value;}}
}
