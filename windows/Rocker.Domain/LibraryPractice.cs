// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace Rocker.Domain;
public sealed record RiffNote(int StringIndex,int Fret,double Start,double Duration,string Technique);
public sealed record PracticeRiff(string Id,string Title,string Style,string Focus,string Tuning,double Bpm,string Difficulty,IReadOnlyList<RiffNote> Notes);
public sealed record EarQuestion(string Name,IReadOnlyList<int> Intervals);
public sealed record GameScore(int Score=0,int Streak=0);
public sealed record GameAnswer(GameScore Score,bool Correct);
public static class LibraryPractice
{
    public static IReadOnlyList<EarQuestion> EarIntervals {get;}=Array.AsReadOnly(new EarQuestion[]{new("Malá sekunda",[0,1]),new("Velká sekunda",[0,2]),new("Malá tercie",[0,3]),new("Velká tercie",[0,4]),new("Čistá kvarta",[0,5]),new("Čistá kvinta",[0,7]),new("Velká sexta",[0,9]),new("Oktáva",[0,12])});
    public static IReadOnlyList<EarQuestion> EarChords {get;}=Array.AsReadOnly(new EarQuestion[]{new("Dur",[0,4,7]),new("Moll",[0,3,7]),new("Zmenšený",[0,3,6]),new("Zvětšený",[0,4,8])});
    public static GameAnswer Answer(GameScore score,int expected,int chosen,bool assisted=false)=>new(chosen==expected?assisted?new(score.Score,0):new(checked(score.Score+1),checked(score.Streak+1)):score with{Streak=0},chosen==expected);
    public static double RiffBeats(PracticeRiff riff)=>riff.Notes.Max(n=>n.Start+n.Duration);
    public static IReadOnlyList<int> RiffTuning(ContentCatalog catalog,PracticeRiff riff)=>catalog.Find("tunings",riff.Tuning)?.GetProperty("strings").EnumerateArray().Select(s=>s.GetProperty("midi").GetInt32()).ToArray()??throw new InvalidDataException("Ladění riffu chybí.");
    public static IReadOnlyList<TeachingNote> RiffPreview(PracticeRiff riff,IReadOnlyList<int> tuning,double bpm)
    {
        if(tuning.Count!=6 || riff.Notes.Count is <1 or >128 || !double.IsFinite(bpm) || bpm is <40 or >240)throw new ArgumentException("Neplatný náhled riffu.");
        return riff.Notes.OrderBy(n=>n.Start).Select(n=>new TeachingNote(n.Start,n.Technique=="palm-mute"?Math.Min(n.Duration,.12*bpm/60):n.Duration,tuning[n.StringIndex]+n.Fret,n.Technique=="palm-mute"?.16:.25)).ToArray();
    }
    public static SurgicalExercise? RiffExercise(ContentCatalog catalog,PracticeRiff riff)
    {
        var notes=riff.Notes.OrderBy(n=>n.Start).ToArray();if(notes.GroupBy(n=>n.Start).Any(g=>g.Count()>1) || notes.Zip(notes.Skip(1),(a,b)=>a.Start+a.Duration>b.Start+1e-9).Any(x=>x))return null;
        var tuning=RiffTuning(catalog,riff);return new("riff:"+riff.Id,"Riff · "+riff.Title,"Pomalý tónový drill. Ladění: "+riff.Tuning,notes.Select(n=>tuning[n.StringIndex]+n.Fret).ToArray());
    }
    public static int[] ScaleNotes(JsonElement scale,int rootMidi)=>scale.GetProperty("intervals").EnumerateArray().Select(n=>rootMidi+n.GetInt32()).Append(rootMidi+12).ToArray();
    public static string Technique(string technique)=>technique switch{"palm-mute"=>"Palm mute","slide"=>"Slide","bend"=>"Bend","hammer-on"=>"Hammer-on","pull-off"=>"Pull-off",_=>"Normální"};
}
