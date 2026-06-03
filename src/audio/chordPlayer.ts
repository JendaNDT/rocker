// Chord preview — strums a set of MIDI notes through the output AudioContext.
// Each note is a short synthesised pluck; notes are staggered to mimic a strum.

import { audioEngine } from "@/audio/audioEngine";
import { midiToFrequency } from "@/lib/pitch";

const STRUM_STEP_S = 0.03;
const NOTE_PEAK = 0.16;
const NOTE_LENGTH_S = 1.7;

export async function playChord(midiNotes: number[]): Promise<void> {
  if (midiNotes.length === 0) return;
  const context = await audioEngine.getOutputContext();
  const startAt = context.currentTime + 0.02;

  midiNotes.forEach((midi, index) => {
    const time = startAt + index * STRUM_STEP_S;
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.type = "triangle";
    oscillator.frequency.value = midiToFrequency(midi);
    oscillator.connect(gain);
    gain.connect(context.destination);

    gain.gain.setValueAtTime(0.0001, time);
    gain.gain.exponentialRampToValueAtTime(NOTE_PEAK, time + 0.008);
    gain.gain.exponentialRampToValueAtTime(0.0001, time + NOTE_LENGTH_S);

    oscillator.start(time);
    oscillator.stop(time + NOTE_LENGTH_S + 0.05);
  });
}
