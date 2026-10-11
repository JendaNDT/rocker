// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct LoopSnapshot
{
    public uint StructSize,AbiVersion,State,Failure,Layers,Bus,SampleRate,Flags;
    public ulong Frames,Position,Cycles,StartFrame,Acknowledged,Capacity;
    public uint DelayFrames,Reserved;
    public float Gain0,Gain1,Gain2,Gain3,Gain4,Gain5,Gain6,Gain7;
    public readonly bool Active=>State is >=1 and <=5;
    public readonly double Duration=>SampleRate>0?Frames/(double)SampleRate:0;
    public readonly float Gain(int layer)=>layer switch{0=>Gain0,1=>Gain1,2=>Gain2,3=>Gain3,4=>Gain4,5=>Gain5,6=>Gain6,7=>Gain7,_=>throw new ArgumentOutOfRangeException(nameof(layer))};
}
internal static class LooperMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_loop_command",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Command(IntPtr host,ulong graph,uint kind,uint bus,uint bars,uint beats,uint aligned,uint delay,uint layer,double gain);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_loop",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Get(AudioHandle host,ref LoopSnapshot value);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_set_loop_metadata",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Metadata(IntPtr host,uint bus,uint flags,uint delay);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_load_loop_layer",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Load(IntPtr host,[MarshalAs(UnmanagedType.LPUTF8Str)]string path,uint layer,double gain);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_export_loop",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Export(IntPtr host,[MarshalAs(UnmanagedType.LPUTF8Str)]string path,int layer);
}
public sealed partial class NativeAudio
{
    public Task LoopCommandAsync(uint kind,uint bus=0,uint countInBars=0,uint beats=4,bool aligned=false,uint delayFrames=0,uint layer=0,double gain=.7)
    {
        if(kind is <1 or >8 || bus>1 || countInBars>2 || beats is <1 or >16 || delayFrames>192000 || layer>7 || !double.IsFinite(gain) || gain is <0 or >1)throw new ArgumentOutOfRangeException(nameof(kind));
        lock(control){CheckAlive();ulong graph=0;if(kind is 1 or 3 or 5)CheckResult(MetronomeMethods.Reserve(handle,out graph));return ControlAsync(p=>LooperMethods.Command(p,graph,kind,bus,countInBars,beats,aligned?1u:0u,delayFrames,layer,gain));}
    }
    public LoopSnapshot GetLoop(){lock(control){CheckAlive();var value=new LoopSnapshot{StructSize=(uint)Marshal.SizeOf<LoopSnapshot>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(LooperMethods.Get(handle,ref value));return value;}}
    public Task SetLoopMetadataAsync(uint bus,bool partial,uint delay,bool delayMeasured=false){if(bus>1||delay>192000)throw new ArgumentOutOfRangeException(nameof(bus));return ControlAsync(p=>LooperMethods.Metadata(p,bus,(partial?1u:0u)|(delayMeasured?4u:0u),delay));}
    public Task LoadLoopLayerAsync(string path,uint layer,double gain=.7){ArgumentException.ThrowIfNullOrWhiteSpace(path);if(layer>7||!double.IsFinite(gain)||gain is <0 or >1)throw new ArgumentOutOfRangeException(nameof(layer));var owned=Path.GetFullPath(path);return ControlAsync(p=>LooperMethods.Load(p,owned,layer,gain));}
    public Task ExportLoopAsync(string newPath,int layer=-1){ArgumentException.ThrowIfNullOrWhiteSpace(newPath);if(layer is < -1 or >7)throw new ArgumentOutOfRangeException(nameof(layer));var owned=Path.GetFullPath(newPath);return ControlAsync(p=>LooperMethods.Export(p,owned,layer));}
}
