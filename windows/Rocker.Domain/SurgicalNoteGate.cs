// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.Domain;
public sealed record PlayedNoteObservation(uint Epoch,ulong Frame,double Rate,int? Midi,bool Active,bool Silent,ulong HitCount,ulong OnsetFrame,double WindowSeconds);
// Consume distinct stable pitch changes, genuine silence/re-articulation or a
// later native onset. A held note and a delayed report of its first onset never
// count twice. Timing uses input frames, not UI refresh frequency.
public sealed class SurgicalNoteGate
{
    private uint epoch;
    private int? lastPitch,candidate;
    private ulong candidateFrame,lastFedFrame,quietFrame,lastHit;
    private bool quiet, released;
    public void Reset(PlayedNoteObservation observation)
    {
        epoch=observation.Epoch;lastPitch=observation.Midi is int midi?MusicShapes.NoteAtFret(midi,0):null;candidate=null;lastFedFrame=observation.Frame;lastHit=observation.HitCount;quiet=released=false;
    }
    public int? Observe(PlayedNoteObservation value)
    {
        if(!value.Active || !double.IsFinite(value.Rate) || value.Rate<=0 || !double.IsFinite(value.WindowSeconds) || value.WindowSeconds<0)return null;
        if(value.Epoch!=epoch || value.HitCount<lastHit || value.Frame<lastFedFrame){Reset(value);return null;}
        if(value.Midi is not int midi)
        {
            candidate=null;if(value.Silent){if(!quiet){quiet=true;quietFrame=value.Frame;}if(value.Frame>=quietFrame && (value.Frame-quietFrame)/value.Rate>=.04)released=true;}else quiet=false;return null;
        }
        quiet=false;int pitch=MusicShapes.NoteAtFret(midi,0);
        if(candidate!=pitch){candidate=pitch;candidateFrame=value.Frame;}
        double settle=released && lastPitch==pitch?Math.Max(.04,value.WindowSeconds):.04;
        if(value.Frame<candidateFrame || (value.Frame-candidateFrame)/value.Rate<settle)return null;
        bool newOnset=value.HitCount>lastHit && value.OnsetFrame>lastFedFrame && value.Frame>=value.OnsetFrame && (value.Frame-value.OnsetFrame)/value.Rate>=value.WindowSeconds;
        if(lastPitch==pitch && !released && !newOnset)return null;
        lastPitch=pitch;lastFedFrame=value.Frame;lastHit=value.HitCount;released=false;return midi;
    }
}
