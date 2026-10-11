// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
public sealed record MetronomeConfiguration(bool Enabled=false,uint Bpm=100,uint Beats=4,uint Subdivision=1,uint Sound=0,uint SubSound=5,uint CountIn=0,
    bool Ramp=false,uint RampEvery=4,uint RampStep=5,uint RampTarget=160,bool Gap=false,uint AudibleBars=4,uint SilentBars=1,bool Poly=false,uint PolyTicks=3,uint PolySound=4,
    double Volume=0.8,double AccentVolume=1,double Swing=0.5,double PolyVolume=0.6,uint[]? Accents=null,uint[]? Sounds=null);
[StructLayout(LayoutKind.Sequential,Pack=8)]
internal struct MetronomeConfig
{
    public uint StructSize,AbiVersion,Enabled,Bpm,Beats,Subdivision,Sound,SubSound,CountIn,Ramp,RampEvery,RampStep,RampTarget,Gap,AudibleBars,SilentBars,Poly,PolyTicks,PolySound,Reserved;
    public double Volume,AccentVolume,Swing,PolyVolume;
    [MarshalAs(UnmanagedType.ByValArray,SizeConst=16)] public uint[] Accents;
    [MarshalAs(UnmanagedType.ByValArray,SizeConst=16)] public uint[] Sounds;
}
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct MetronomeSnapshot
{
    public uint StructSize,AbiVersion,Flags;public int Beat;public ulong Frame;public long Bar;public double Bpm,Phase;public uint SubdivisionTick,PolyTick;public ulong VoiceOverloads,PrimaryEvents,SecondaryEvents;
    public readonly bool Enabled=>(Flags&1)!=0;
    public readonly bool CountIn=>(Flags&2)!=0;
    public readonly bool SilentBar=>(Flags&4)!=0;
    public readonly bool Running=>(Flags&8)!=0;
}
internal static class MetronomeMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_set_metronome",CallingConvention=CallingConvention.Cdecl)] internal static extern uint Set(IntPtr host,in MetronomeConfig config);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_metronome",CallingConvention=CallingConvention.Cdecl)] internal static extern uint Get(AudioHandle host,ref MetronomeConfig config);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_metronome_snapshot",CallingConvention=CallingConvention.Cdecl)] internal static extern uint Snapshot(AudioHandle host,ref MetronomeSnapshot config);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_reserve_metronome_start",CallingConvention=CallingConvention.Cdecl)] internal static extern uint Reserve(AudioHandle host,out ulong ticket);
}
public sealed partial class NativeAudio
{
    public Task SetMetronomeAsync(MetronomeConfiguration value)
    {
        ArgumentNullException.ThrowIfNull(value);
        static uint[] Pattern(uint[]? supplied,bool sounds){if(supplied is not null && supplied.Length>16)throw new ArgumentException("Metronome patterns contain at most 16 beats.");var result=Enumerable.Range(0,16).Select(i=>sounds?5u:i==0?2u:1u).ToArray();if(supplied is not null)Array.Copy(supplied,result,supplied.Length);return result;}
        var config=new MetronomeConfig{StructSize=(uint)Marshal.SizeOf<MetronomeConfig>(),AbiVersion=NativeMethods.AbiVersion,Enabled=value.Enabled?1u:0u,Bpm=value.Bpm,Beats=value.Beats,Subdivision=value.Subdivision,Sound=value.Sound,SubSound=value.SubSound,CountIn=value.CountIn,Ramp=value.Ramp?1u:0u,RampEvery=value.RampEvery,RampStep=value.RampStep,RampTarget=value.RampTarget,Gap=value.Gap?1u:0u,AudibleBars=value.AudibleBars,SilentBars=value.SilentBars,Poly=value.Poly?1u:0u,PolyTicks=value.PolyTicks,PolySound=value.PolySound,Volume=value.Volume,AccentVolume=value.AccentVolume,Swing=value.Swing,PolyVolume=value.PolyVolume,Accents=Pattern(value.Accents,false),Sounds=Pattern(value.Sounds,true)};
        return ControlAsync(p=>MetronomeMethods.Set(p,in config));
    }
    public MetronomeConfiguration GetMetronome()
    {
        lock(control){CheckAlive();var config=new MetronomeConfig{StructSize=(uint)Marshal.SizeOf<MetronomeConfig>(),AbiVersion=NativeMethods.AbiVersion,Accents=new uint[16],Sounds=new uint[16]};CheckResult(MetronomeMethods.Get(handle,ref config));
            return new(config.Enabled!=0,config.Bpm,config.Beats,config.Subdivision,config.Sound,config.SubSound,config.CountIn,config.Ramp!=0,config.RampEvery,config.RampStep,config.RampTarget,config.Gap!=0,config.AudibleBars,config.SilentBars,config.Poly!=0,config.PolyTicks,config.PolySound,config.Volume,config.AccentVolume,config.Swing,config.PolyVolume,config.Accents,config.Sounds);}
    }
    public MetronomeSnapshot GetMetronomeSnapshot(){lock(control){CheckAlive();var value=new MetronomeSnapshot{StructSize=(uint)Marshal.SizeOf<MetronomeSnapshot>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(MetronomeMethods.Snapshot(handle,ref value));return value;}}
    public Task StartMetronomeAsync(){lock(control){CheckAlive();CheckResult(MetronomeMethods.Reserve(handle,out var ticket));return ControlAsync(pointer=>NativeMethods.StartReserved(pointer,ticket,3));}}
}
