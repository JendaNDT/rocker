// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;

public sealed record InspectedMedia(double DurationSeconds, float[] Peaks);
public static class MediaInspection
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint Cancel(nint context);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_inspect_file",CallingConvention=CallingConvention.Cdecl)]
    private static extern uint Inspect([MarshalAs(UnmanagedType.LPUTF8Str)]string source,[Out]float[] peaks,uint capacity,out double duration,Cancel cancel,nint context);
    public static async Task<InspectedMedia> ReadAsync(string path,CancellationToken cancellation=default)
    {
        string source=Path.GetFullPath(path);var peaks=new float[1024];
        Cancel cancel=_=>cancellation.IsCancellationRequested?1u:0u;
        cancellation.ThrowIfCancellationRequested();
        var result=await Task.Run(()=>{uint error=Inspect(source,peaks,1024,out double duration,cancel,0);return(error,duration);});
        GC.KeepAlive(cancel);cancellation.ThrowIfCancellationRequested();
        if(result.error!=0 || !double.IsFinite(result.duration) || result.duration<=0 || peaks.Any(p=>!float.IsFinite(p) || p<0))
            throw new InvalidDataException("Zvukový soubor nelze přečíst. Původní soubor a přehrávání zůstávají zachované.");
        return new(result.duration,peaks);
    }
}
