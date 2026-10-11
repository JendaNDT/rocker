// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
public static class MediaExport
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate uint Cancel(IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void Progress(IntPtr context,double fraction);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_export_wav",CallingConvention=CallingConvention.Cdecl)]private static extern uint Export([MarshalAs(UnmanagedType.LPUTF8Str)]string source,[MarshalAs(UnmanagedType.LPUTF8Str)]string destination,double start,double end,Cancel cancel,Progress progress,IntPtr context);
    public static async Task WavAsync(string source,string destination,double start=0,double end=0,IProgress<double>? progress=null,CancellationToken cancellation=default)
    {
        if(!double.IsFinite(start)||!double.IsFinite(end)||start<0||(end!=0&&end<=start))throw new ArgumentOutOfRangeException(nameof(start));
        var target=Path.GetFullPath(destination);var input=Path.GetFullPath(source);if(string.Equals(input,target,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new ArgumentException("Export musí mít jinou cestu než zdroj.");
        var temporary=Path.Combine(Path.GetDirectoryName(target)!,".rocker-export-"+Guid.NewGuid().ToString("N")+".wav");
        Cancel cancel=_=>cancellation.IsCancellationRequested?1u:0u;Progress update=(_,value)=>{try{progress?.Report(value);}catch(Exception){ /* A progress observer must not unwind through the ABI. */ }};
        try{cancellation.ThrowIfCancellationRequested();var result=await Task.Run(()=>Export(input,temporary,start,end,cancel,update,IntPtr.Zero));GC.KeepAlive(cancel);GC.KeepAlive(update);cancellation.ThrowIfCancellationRequested();if(result!=0)throw new IOException($"Export WAV se nezdařil (stav {result}). Původní nahrávka zůstává zachovaná.");File.Move(temporary,target,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
