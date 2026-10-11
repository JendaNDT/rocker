// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace Rocker.Domain;
public static class RhythmPreview
{
    public static IReadOnlyList<TeachingNote> Power(PowerChordExercise exercise,IReadOnlyList<int> tuning)
    {
        var notes=new List<TeachingNote>();for(int i=0;i<exercise.Sequence.Count;++i){var chord=MusicShapes.GetPowerChord(exercise.Sequence[i],tuning)??throw new InvalidDataException("Neplatná poloha power chordu.");foreach(var midi in MusicShapes.PowerChordMidiNotes(chord))notes.Add(new(i*exercise.BeatsPerChord,exercise.BeatsPerChord,midi));}return notes;
    }
    public static IReadOnlyList<TeachingNote> Gallop(GallopExercise exercise,IReadOnlyList<int> tuning)
    {
        var chord=MusicShapes.GetPowerChord(exercise.ChordId,tuning)??throw new InvalidDataException("Neplatná poloha power chordu.");var notes=new List<TeachingNote>();double beat=0;foreach(var duration in exercise.Cell){foreach(var midi in MusicShapes.PowerChordMidiNotes(chord))notes.Add(new(beat,duration,midi));beat+=duration;}return notes;
    }
    public static JsonElement? Shape(PowerChord chord,IReadOnlyList<int> tuning)
    {
        int[] positions=[chord.RootFret,chord.RootMidi+7-tuning[chord.RootStringIndex+1],chord.RootMidi+12-tuning[chord.RootStringIndex+2]];if(positions.Any(f=>f is <0 or >24)||positions.Max()-Math.Max(1,positions.Where(f=>f>0).DefaultIfEmpty(1).Min())>3)return null;
        object[] frets=Enumerable.Repeat<object>("x",6).ToArray();int?[] fingers=new int?[6];for(int i=0;i<3;++i){int index=chord.RootStringIndex+i;frets[index]=positions[i];fingers[index]=positions[i]==0?null:i==0?1:i==1?3:4;}
        return JsonSerializer.SerializeToElement(new{name=chord.Name,baseFret=Math.Max(1,positions.Where(f=>f>0).DefaultIfEmpty(1).Min()),frets,fingers,barre=(object?)null});
    }
}
public sealed record PalmProfile(string Device,string Tuning,string Instrument,PalmMuteCalibration Calibration,double Sensitivity=1,string Pickup="bridge");
