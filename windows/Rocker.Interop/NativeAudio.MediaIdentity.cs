// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Interop;

public sealed partial class NativeAudio
{
    [DllImport("rocker_audio",EntryPoint="rocker_audio_get_media_source_ticket",CallingConvention=CallingConvention.Cdecl)]
    private static extern uint MediaSourceTicket(nint host,out ulong ticket);
    public Task<ulong> PrepareFileWithIdentityAsync(string path,double speed=1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);if(path.Contains('\0'))throw new ArgumentException("Cesta obsahuje neplatný znak.");CheckSpeed(speed);
        return PrepareAsync((pointer,ticket)=>SongMethods.File(pointer,ticket,Path.GetFullPath(path),speed,0));
    }
    public Task<ulong> PrepareMidiWithIdentityAsync(byte[] bytes,double speed=1)
    {
        ArgumentNullException.ThrowIfNull(bytes);CheckSpeed(speed);if(bytes.Length is 0 or >4*1024*1024)throw new ArgumentException("MIDI musí mít nejvýše 4 MB.");
        var owned=(byte[])bytes.Clone();return PrepareAsync((pointer,ticket)=>SongMethods.Midi(pointer,ticket,owned,(uint)owned.Length,speed));
    }
    public async Task<ulong> GetMediaSourceTicketAsync()
    {
        ulong result=0;await ControlAsync(pointer=>MediaSourceTicket(pointer,out result));return result;
    }
}
