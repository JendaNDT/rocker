// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
public sealed record AmplifierConfiguration(bool Enabled=false,uint Distortion=1,uint Cabinet=0,double Compression=0,double Drive=0.1,double Bass=0.1,double Mid=0,double Treble=0.15,double Delay=0.1,double Reverb=0,double Chorus=0,double Level=0.6);
[StructLayout(LayoutKind.Sequential,Pack=8)]
internal struct AmpConfig {public uint StructSize,AbiVersion,Enabled,Distortion,Cabinet,Reserved;public double Compression,Drive,Bass,Mid,Treble,Delay,Reverb,Chorus,Level;}
internal static class AmpMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_set_amp",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Set(IntPtr host,in AmpConfig config);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_amp",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Get(AudioHandle host,ref AmpConfig config);
}
public sealed partial class NativeAudio
{
    public Task SetAmplifierAsync(AmplifierConfiguration value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var config=new AmpConfig{StructSize=(uint)Marshal.SizeOf<AmpConfig>(),AbiVersion=NativeMethods.AbiVersion,Enabled=value.Enabled?1u:0u,Distortion=value.Distortion,Cabinet=value.Cabinet,Compression=value.Compression,Drive=value.Drive,Bass=value.Bass,Mid=value.Mid,Treble=value.Treble,Delay=value.Delay,Reverb=value.Reverb,Chorus=value.Chorus,Level=value.Level};
        return ControlAsync(p=>AmpMethods.Set(p,in config));
    }
    public AmplifierConfiguration GetAmplifier()
    {
        lock(control){CheckAlive();var config=new AmpConfig{StructSize=(uint)Marshal.SizeOf<AmpConfig>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(AmpMethods.Get(handle,ref config));return new(config.Enabled!=0,config.Distortion,config.Cabinet,config.Compression,config.Drive,config.Bass,config.Mid,config.Treble,config.Delay,config.Reverb,config.Chorus,config.Level);}
    }
}
