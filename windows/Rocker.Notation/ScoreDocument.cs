// SPDX-License-Identifier: AGPL-3.0-only
using AlphaTab;
using AlphaTab.Model;
using AlphaTab.Midi;
using AlphaTab.Importer;
using AlphaTab.Core.EcmaScript;
namespace Rocker.Notation;
public sealed record NotationTrack(int Index,string Name,bool Percussion);
public sealed record NotationOptions(int Track=0,int FirstBar=1,int PageBars=12,double Scale=1,int Width=700,bool AllTracksAudio=false);
public sealed record PlayedBar(int Number,double StartSeconds,double EndSeconds,double StartTick,double EndTick);
public sealed class ScoreDocument
{
    internal Score Score{get;}internal MidiTickLookup Ticks{get;}internal MidiFile FullMidi{get;}
    internal object RenderGate{get;}=new();
    public IReadOnlyList<NotationTrack> Tracks{get;}public IReadOnlyList<PlayedBar> Playback{get;}
    public string Title=>Score.Title;public int BarCount=>Score.MasterBars.Count;
    private readonly (double Tick,double Seconds,double Bpm)[] tempos;
    private ScoreDocument(Score score,MidiFile midi,MidiTickLookup ticks)
    {
        Score=score;FullMidi=midi;Ticks=ticks;Tracks=Array.AsReadOnly(score.Tracks.Select(t=>new NotationTrack((int)t.Index,t.Name,t.Staves.Any(s=>s.IsPercussion))).ToArray());
        var points=new List<(double Tick,double Seconds,double Bpm)>{(0,0,120)};double previous=0,seconds=0,bpm=120;
        foreach(var change in midi.Events.OfType<TempoChangeEvent>().OrderBy(e=>e.Tick)){if(!double.IsFinite(change.BeatsPerMinute) || change.BeatsPerMinute<=0)throw new InvalidDataException("Neplatné tempo partitury.");seconds+=(change.Tick-previous)*60/(midi.Division*bpm);previous=change.Tick;bpm=change.BeatsPerMinute;if(change.Tick==points[^1].Tick)points[^1]=(change.Tick,seconds,bpm);else points.Add((change.Tick,seconds,bpm));}tempos=points.ToArray();
        Playback=Array.AsReadOnly(ticks.MasterBars.Select(b=>new PlayedBar((int)b.MasterBar.Index+1,SecondsAtTick(b.Start),SecondsAtTick(b.End),b.Start,b.End)).ToArray());
    }
    public static ScoreDocument Load(byte[] bytes)
    {
        if(bytes.Length is 0 or >ScorePreview.MaximumFileBytes)throw new InvalidDataException("Notový soubor je prázdný nebo přesahuje limit 4 MB.");ScorePreview.ValidateZipSize(bytes);var settings=new Settings();settings.Core.UseWorkers=false;
        var score=ScoreLoader.LoadScoreFromBytes(new Uint8Array(bytes),settings);if(score.Tracks.Count is <1 or >16 || score.MasterBars.Count is <1 or >128)throw new InvalidDataException("Partitura podporuje nejvýše 16 stop a 128 taktů.");
        long notes=0;foreach(var track in score.Tracks)foreach(var staff in track.Staves)foreach(var bar in staff.Bars)foreach(var voice in bar.Voices)foreach(var beat in voice.Beats){notes+=beat.Notes.Count;if(notes>10000)throw new InvalidDataException("Partitura přesahuje 10 000 zapsaných tónů.");}
        var midi=new MidiFile();var generator=new MidiFileGenerator(score,settings,new AlphaSynthMidiFileHandler(midi,true));generator.Generate();if(midi.Events.Count>100000)throw new InvalidDataException("Partitura přesahuje 100 000 MIDI událostí.");return new(score,midi,generator.TickLookup);
    }
    public ScorePreview Render(NotationOptions? options=null){lock(RenderGate)return ScorePreview.Render(this,options??new());}
    public double SecondsAtTick(double tick)
    {
        if(!double.IsFinite(tick) || tick<0)throw new ArgumentOutOfRangeException(nameof(tick));var point=tempos[0];foreach(var next in tempos){if(next.Tick>tick)break;point=next;}return point.Seconds+(tick-point.Tick)*60/(FullMidi.Division*point.Bpm);
    }
    public string DescribeBar(int track,int number)
    {
        if(track<0 || track>=Tracks.Count || number<1 || number>BarCount)throw new ArgumentOutOfRangeException(nameof(number));
        lock(RenderGate)
        {
            var parts=new List<string>();
            foreach(var staff in Score.Tracks[track].Staves)foreach(var voice in staff.Bars[number-1].Voices)
                foreach(var beat in voice.Beats)
                {
                    var notes=beat.Notes.Select(note=>staff.IsPercussion?$"bicí MIDI {note.PercussionArticulation}":note.String>0?$"struna {note.String}, pražec {note.Fret}":$"tón MIDI {note.CalculateRealValue(false,false)}");
                    parts.Add($"{(int)beat.Duration}: "+(beat.Notes.Count==0?"pomlka":string.Join(" + ",notes)));
                }
            return $"{Tracks[track].Name}, takt {number}. Délky jsou jmenovatele not: "+string.Join("; ",parts)+".";
        }
    }
    public PlayedBar? AtSeconds(double seconds)=>double.IsFinite(seconds) && seconds>=0?Playback.FirstOrDefault(b=>seconds>=b.StartSeconds && seconds<b.EndSeconds):null;
    public (double Start,double End) LoopSeconds(int first,int last,int occurrence=0)
    {
        if(first<1 || last<first || last>BarCount || occurrence<0)throw new ArgumentException("Neplatný rozsah taktů.");var firstBars=Playback.Select((bar,index)=>(bar,index)).Where(p=>p.bar.Number==first).ToArray();if(occurrence>=firstBars.Length)throw new ArgumentOutOfRangeException(nameof(occurrence));var chosen=firstBars[occurrence];int index=chosen.index;
        // A range follows the actual expanded playback order. Refuse a repeat jump
        // within the selected contiguous range instead of inventing a time extent.
        for(int expected=first;expected<=last;++expected,++index)if(index>=Playback.Count || Playback[index].Number!=expected)throw new InvalidOperationException("Rozsah prochází opakováním nebo skokem. Vyber souvislé takty jedné hrané pasáže.");return(chosen.bar.StartSeconds,Playback[index-1].EndSeconds);
    }
    internal MidiFile MidiFor(NotationOptions options)
    {
        if(options.AllTracksAudio)return FullMidi;var selected=new MidiFile{Division=FullMidi.Division};foreach(var midiEvent in FullMidi.Events)if(midiEvent.Track==options.Track || midiEvent is TempoChangeEvent or TimeSignatureEvent)selected.AddEvent(midiEvent);return selected;
    }
}
