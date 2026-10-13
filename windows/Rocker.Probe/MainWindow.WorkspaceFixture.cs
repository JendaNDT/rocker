// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using System.Buffers.Binary;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    // Only the isolated diagnostic run calls this. Never played through a device
    // and never presented as a real guitar take: two seconds of authored tones.
    private async Task<RecordingData> CreateWorkspaceWaveformFixtureAsync()
    {
        const int rate=48000, frames=rate*2;
        var draft=await recordingLibrary!.BeginAsync("Ukázka průběhu · syntetické tóny",rate,1,0);
        byte[] wave=new byte[44+frames*3];"RIFF"u8.CopyTo(wave);"WAVEfmt "u8.CopyTo(wave.AsSpan(8));"data"u8.CopyTo(wave.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(wave.AsSpan(4),(uint)(36+frames*3));BinaryPrimitives.WriteUInt32LittleEndian(wave.AsSpan(16),16);
        BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(20),1);BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(22),1);
        BinaryPrimitives.WriteUInt32LittleEndian(wave.AsSpan(24),rate);BinaryPrimitives.WriteUInt32LittleEndian(wave.AsSpan(28),rate*3);
        BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(32),3);BinaryPrimitives.WriteUInt16LittleEndian(wave.AsSpan(34),24);BinaryPrimitives.WriteUInt32LittleEndian(wave.AsSpan(40),frames*3);
        int[] midi=[64,67,69,72];
        for(int i=0;i<frames;++i)
        {
            double t=i/(double)rate, local=t%0.5;
            double envelope=Math.Min(1,local*100)*Math.Exp(-local*6);
            int sample=(int)(Math.Sin(2*Math.PI*Music.MidiToFrequency(midi[(int)(t*2)])*t)*envelope*.3*8388607);
            int offset=44+i*3;wave[offset]=(byte)sample;wave[offset+1]=(byte)(sample>>8);wave[offset+2]=(byte)(sample>>16);
        }
        await File.WriteAllBytesAsync(recordingLibrary.PendingPath(draft.Id),wave);
        var recording=await recordingLibrary.FinishAsync(draft,new(rate,1,0,frames,null,null,0,false,false))??throw new InvalidOperationException("Synthetic waveform fixture was not published.");
        recording=recording with{Markers=[new("tone-2",.5,"Druhý tón"),new("tone-3",1,"Třetí tón")]};await recordingLibrary.SaveAsync(recording);return recording;
    }
}
