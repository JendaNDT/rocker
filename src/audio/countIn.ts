// One-shot count-in — a few clicks before playback starts.

import { audioEngine } from "@/audio/audioEngine";

/**
 * Plays `beats` count-in clicks at the given tempo and resolves once the
 * count-in is over (one beat after the last click), so the caller can start
 * playback exactly on the downbeat.
 */
export async function playCountIn(beats: number, bpm: number): Promise<void> {
  const context = await audioEngine.getOutputContext();
  const interval = 60 / bpm;
  const lead = 0.1;
  const startAt = context.currentTime + lead;

  for (let beat = 0; beat < beats; beat += 1) {
    const time = startAt + beat * interval;
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.connect(gain);
    gain.connect(context.destination);
    oscillator.frequency.value = beat === 0 ? 1600 : 1000;
    gain.gain.setValueAtTime(0.4, time);
    gain.gain.exponentialRampToValueAtTime(0.0001, time + 0.05);
    oscillator.start(time);
    oscillator.stop(time + 0.06);
  }

  // Wait against the AudioContext clock, not a single wall-clock setTimeout:
  // a timer that fires late would start playback after the downbeat. Re-check
  // each step so the count-in resolves right on the audio clock's end time.
  const endTime = startAt + beats * interval;
  while (context.currentTime < endTime) {
    const remainingMs = (endTime - context.currentTime) * 1000;
    await new Promise((resolve) =>
      setTimeout(resolve, Math.min(remainingMs, 50)),
    );
  }
}
