// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace Rocker.Domain;
public sealed record TeachingNote(double Start,double Duration,int Midi,double Volume=.25);
public static class TeachingPreview
{
    public static IReadOnlyList<TeachingNote> Chord(JsonElement chord,IReadOnlyList<int>? tuning=null,bool arpeggio=false)
    {
        tuning??=[40,45,50,55,59,64];if(tuning.Count!=6)throw new ArgumentException("Kytara má šest strun.");
        var notes=new List<TeachingNote>();int index=0;
        foreach(var fret in chord.GetProperty("frets").EnumerateArray())
        {
            if(fret.ValueKind==JsonValueKind.Number && fret.GetInt32()>=0)notes.Add(new(arpeggio?notes.Count*.18:notes.Count*.018,1.2,tuning[index]+fret.GetInt32()));++index;
        }
        return notes;
    }
}
