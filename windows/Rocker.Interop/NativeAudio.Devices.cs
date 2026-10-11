// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace Rocker.Interop;
public sealed record DeviceBackend(string Name, string[] Inputs, string[] Outputs, bool SeparateDevices);
public sealed record DeviceCatalog(int SchemaVersion, DeviceBackend[] Backends);
public sealed record DeviceCapabilities(int SchemaVersion, string[] Inputs, string[] Outputs, double[] Rates, uint[] Buffers, bool ControlPanel);
public sealed record DeviceConfiguration(string Backend, string Input, string Output, double SampleRate=0, uint BufferFrames=0, int InputChannel=-1, uint OutputFirst=0);
public sealed record DeviceDetails(int SchemaVersion, string? Backend, string? Input, string? Output, double SampleRate, uint BufferFrames, uint InputChannels, uint OutputChannels, int InputLatencyFrames, int OutputLatencyFrames, bool ControlPanel);
[StructLayout(LayoutKind.Sequential,Pack=8)]
internal struct DeviceConfig { public uint StructSize,AbiVersion;public double SampleRate;public uint BufferFrames;public int InputChannel;public uint OutputFirst,Reserved; }
internal static class DeviceMethods
{
    private const string Library="rocker_audio";
    [DllImport(Library,EntryPoint="rocker_audio_copy_devices",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Catalog(IntPtr host,byte[] buffer,uint capacity,out uint required);
    [DllImport(Library,EntryPoint="rocker_audio_copy_capabilities",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Capabilities(IntPtr host,[MarshalAs(UnmanagedType.LPUTF8Str)]string backend,[MarshalAs(UnmanagedType.LPUTF8Str)]string input,[MarshalAs(UnmanagedType.LPUTF8Str)]string output,byte[] buffer,uint capacity,out uint required);
    [DllImport(Library,EntryPoint="rocker_audio_copy_device_details",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Details(IntPtr host,byte[] buffer,uint capacity,out uint required);
    [DllImport(Library,EntryPoint="rocker_audio_configure_device",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Configure(IntPtr host,in DeviceConfig config,[MarshalAs(UnmanagedType.LPUTF8Str)]string backend,[MarshalAs(UnmanagedType.LPUTF8Str)]string input,[MarshalAs(UnmanagedType.LPUTF8Str)]string output);
    [DllImport(Library,EntryPoint="rocker_audio_show_control_panel",CallingConvention=CallingConvention.Cdecl)]
    internal static extern uint Panel(IntPtr host);
}
public sealed partial class NativeAudio
{
    private delegate uint JsonQuery(IntPtr host,byte[] bytes,uint capacity,out uint required);
    private static readonly JsonSerializerOptions DeviceJson=new(){PropertyNameCaseInsensitive=true,MaxDepth=8};
    private async Task<T> DeviceQueryAsync<T>(JsonQuery query)
    {
        T? result=default;
        await ControlAsync(pointer=> {
            var bytes=new byte[1024*1024+1];uint status=query(pointer,bytes,(uint)bytes.Length,out var required);
            if(status!=0)return status;
            if(required==0 || required>bytes.Length || bytes[required-1]!=0)throw new InvalidDataException("Zvukový ovladač vrátil neplatnou odpověď.");
            result=JsonSerializer.Deserialize<T>(bytes.AsSpan(0,(int)required-1),DeviceJson)??throw new InvalidDataException("Zvukový ovladač nevrátil údaje.");return 0;
        });return result!;
    }
    public async Task<DeviceCatalog> GetDevicesAsync()
    {
        var value=await DeviceQueryAsync<DeviceCatalog>(DeviceMethods.Catalog);
        if(value.SchemaVersion!=1 || value.Backends is null || value.Backends.Length>32 || value.Backends.Any(b=>b is null || !Name(b.Name,false) || b.Inputs is null || b.Outputs is null || b.Inputs.Length>512 || b.Outputs.Length>512 || b.Inputs.Concat(b.Outputs).Any(n=>!Name(n,false))))throw new InvalidDataException("Seznam zvukových zařízení není platný.");
        return value;
    }
    public async Task<DeviceCapabilities> GetCapabilitiesAsync(DeviceConfiguration choice)
    {
        ValidateDevice(choice);
        var value=await DeviceQueryAsync<DeviceCapabilities>((IntPtr p,byte[] b,uint c,out uint r)=>DeviceMethods.Capabilities(p,choice.Backend,choice.Input,choice.Output,b,c,out r));
        if(value.SchemaVersion!=1 || value.Inputs is null || value.Outputs is null || value.Rates is null || value.Buffers is null || value.Inputs.Length>64 || value.Outputs.Length>64 || value.Rates.Length>128 || value.Buffers.Length>256 || value.Inputs.Concat(value.Outputs).Any(n=>!Name(n,false)) || value.Rates.Any(r=>!double.IsFinite(r)||r<8000||r>192000) || value.Buffers.Any(b=>b<16||b>8192))throw new InvalidDataException("Možnosti zařízení nejsou platné.");return value;
    }
    public Task ConfigureDeviceAsync(DeviceConfiguration choice)
    {
        ValidateDevice(choice);
        var config=new DeviceConfig{StructSize=(uint)Marshal.SizeOf<DeviceConfig>(),AbiVersion=NativeMethods.AbiVersion,SampleRate=choice.SampleRate,BufferFrames=choice.BufferFrames,InputChannel=choice.InputChannel,OutputFirst=choice.OutputFirst};
        return ControlAsync(p=>DeviceMethods.Configure(p,in config,choice.Backend,choice.Input,choice.Output));
    }
    public Task ShowDeviceControlPanelAsync()=>ControlAsync(DeviceMethods.Panel);
    public Task<DeviceDetails> GetDeviceDetailsAsync()=>DeviceQueryAsync<DeviceDetails>(DeviceMethods.Details);
    private static bool Name(string? value,bool empty)=>value is not null && (empty || value.Length>0) && Encoding.UTF8.GetByteCount(value)<1024 && !value.Any(char.IsControl);
    private static void ValidateDevice(DeviceConfiguration value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if(!Name(value.Backend,false) || !Name(value.Input,true) || !Name(value.Output,false) || !double.IsFinite(value.SampleRate) || (value.SampleRate!=0 && (value.SampleRate<8000||value.SampleRate>192000)) || (value.BufferFrames!=0 && (value.BufferFrames<16||value.BufferFrames>8192)) || value.InputChannel < -1 || value.InputChannel>63 || (value.InputChannel>=0 && value.Input.Length==0) || value.OutputFirst>62)throw new ArgumentException("Neplatná konfigurace zvukového zařízení.");
    }
}
