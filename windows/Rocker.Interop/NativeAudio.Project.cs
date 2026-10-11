// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;
public sealed record ProjectAudioTrack(string Path,double Gain=1,double Pan=0,bool Muted=false,bool Solo=false,long OffsetFrames=0,long TrimStartFrames=0);
[StructLayout(LayoutKind.Sequential,Pack=8)]
public struct ClipInfo{public uint StructSize,AbiVersion;public double SampleRate;public ulong Frames;public uint Channels,Flags;}
[StructLayout(LayoutKind.Sequential,Pack=8)]
internal struct ProjectTrackNative
{
    public uint StructSize,AbiVersion;public IntPtr Path;public double Gain,Pan;public uint Flags,Reserved;public ulong OffsetFrames,TrimStartFrames;
}
internal sealed class ProjectTrackBuffer:IDisposable
{
    public ProjectTrackNative[] Values{get;}
    public ProjectTrackBuffer(IReadOnlyList<ProjectAudioTrack> tracks)
    {
        if(tracks.Count is <1 or >256)throw new ArgumentException("Projekt musí mít 1 až 256 stop.");Values=new ProjectTrackNative[tracks.Count];
        try{for(int i=0;i<tracks.Count;++i){var track=tracks[i];if(!double.IsFinite(track.Gain)||track.Gain is <0 or >1||!double.IsFinite(track.Pan)||track.Pan is < -1 or >1||track.OffsetFrames is <0 or >uint.MaxValue||track.TrimStartFrames<0)throw new ArgumentException("Neplatné parametry stopy.");Values[i]=new(){StructSize=(uint)Marshal.SizeOf<ProjectTrackNative>(),AbiVersion=NativeMethods.AbiVersion,Path=Marshal.StringToCoTaskMemUTF8(System.IO.Path.GetFullPath(track.Path)),Gain=track.Gain,Pan=track.Pan,Flags=(track.Muted?1u:0u)|(track.Solo?2u:0u),OffsetFrames=(ulong)track.OffsetFrames,TrimStartFrames=(ulong)track.TrimStartFrames};}}catch{Dispose();throw;}
    }
    public void Dispose(){for(int i=0;i<Values.Length;++i){if(Values[i].Path!=IntPtr.Zero)Marshal.FreeCoTaskMem(Values[i].Path);Values[i].Path=IntPtr.Zero;}}
}
internal static class ProjectMethods
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_clear_media_reserved",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Clear(IntPtr host,ulong ticket);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_clip_info",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Clip(AudioHandle host,ref ClipInfo value);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_project_track_info",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Info(IntPtr host,ulong ticket,uint index,ref ClipInfo value);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_prepare_project_reserved",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Prepare(IntPtr host,ulong ticket,uint rate,[In]ProjectTrackNative[] tracks,uint count);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_project_track",CallingConvention=CallingConvention.Cdecl)]internal static extern uint Update(IntPtr host,ulong ticket,uint index,double gain,double pan,uint flags);
}
public sealed partial class NativeAudio
{
    public Task ClearMediaAsync()=>PrepareAsync((pointer,ticket)=>ProjectMethods.Clear(pointer,ticket));
    public ClipInfo GetClipInfo(){lock(control){CheckAlive();var value=new ClipInfo{StructSize=(uint)Marshal.SizeOf<ClipInfo>(),AbiVersion=NativeMethods.AbiVersion};CheckResult(ProjectMethods.Clip(handle,ref value));return value;}}
    public async Task<ClipInfo> GetProjectTrackInfoAsync(ulong ticket,uint index){var value=new ClipInfo{StructSize=(uint)Marshal.SizeOf<ClipInfo>(),AbiVersion=NativeMethods.AbiVersion};await ControlAsync(p=>ProjectMethods.Info(p,ticket,index,ref value));return value;}
    public async Task<ulong> PrepareProjectAsync(IReadOnlyList<ProjectAudioTrack> tracks,uint sampleRate)
    {
        if(sampleRate is <8000 or >192000)throw new ArgumentOutOfRangeException(nameof(sampleRate));var owned=tracks.ToArray();ulong generation=0;
        await PrepareAsync((pointer,ticket)=>{generation=ticket;using var buffer=new ProjectTrackBuffer(owned);return ProjectMethods.Prepare(pointer,ticket,sampleRate,buffer.Values,(uint)buffer.Values.Length);});return generation;
    }
    public Task SetProjectTrackAsync(ulong ticket,uint index,double gain,double pan,bool muted,bool solo)=>ControlAsync(p=>ProjectMethods.Update(p,ticket,index,gain,pan,(muted?1u:0u)|(solo?2u:0u)));
}
public static class ProjectExport
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate uint Cancel(IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void Progress(IntPtr context,double fraction);
    [DllImport("rocker_audio",EntryPoint="rocker_audio_export_project",CallingConvention=CallingConvention.Cdecl)]private static extern uint Export([In]ProjectTrackNative[] tracks,uint count,uint rate,[MarshalAs(UnmanagedType.LPUTF8Str)]string destination,double start,double end,Cancel cancel,Progress progress,IntPtr context);
    public static async Task WavAsync(IReadOnlyList<ProjectAudioTrack> tracks,uint rate,string destination,double start=0,double end=0,IProgress<double>? progress=null,CancellationToken cancellation=default)
    {
        if(rate is <8000 or >192000||!double.IsFinite(start)||!double.IsFinite(end)||start<0||(end!=0&&end<=start))throw new ArgumentException("Neplatný rozsah nebo frekvence exportu.");var owned=tracks.ToArray();string target=System.IO.Path.GetFullPath(destination);if(owned.Any(t=>string.Equals(System.IO.Path.GetFullPath(t.Path),target,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)))throw new ArgumentException("Export nesmí přepsat zdrojovou stopu.");string temporary=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(target)!,".rocker-project-export-"+Guid.NewGuid().ToString("N")+".wav");Cancel cancel=_=>cancellation.IsCancellationRequested?1u:0u;Progress update=(_,value)=>{try{progress?.Report(value);}catch(Exception){}};
        try{cancellation.ThrowIfCancellationRequested();var result=await Task.Run(()=>{using var buffer=new ProjectTrackBuffer(owned);return Export(buffer.Values,(uint)buffer.Values.Length,rate,temporary,start,end,cancel,update,IntPtr.Zero);});GC.KeepAlive(cancel);GC.KeepAlive(update);cancellation.ThrowIfCancellationRequested();if(result!=0)throw new IOException($"Export projektu se nezdařil (stav {result}). Původní soubory zůstávají zachované.");File.Move(temporary,target,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
