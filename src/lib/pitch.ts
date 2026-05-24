// Pure music maths — no DOM, no audio, no React. Unit-tested in pitch.test.ts.

const A4_FREQUENCY = 440;
const A4_MIDI = 69;

// Note names, index 0 = C. International naming. The German/Czech H/B
// naming is provided by pitchClassName and the notation helpers below.
export const NOTE_NAMES = [
  "C",
  "C#",
  "D",
  "D#",
  "E",
  "F",
  "F#",
  "G",
  "G#",
  "A",
  "A#",
  "B",
] as const;

export interface NoteInfo {
  /** MIDI note number of the nearest note. */
  midi: number;
  /** Note name without octave, e.g. "E". */
  name: string;
  /** Octave number in scientific pitch notation, e.g. 2 for E2. */
  octave: number;
  /** Note label with octave, e.g. "E2". */
  label: string;
  /** Deviation from the nearest note in cents, range roughly -50..+50. */
  cents: number;
}

/** Frequency (Hz) of a MIDI note number. */
export function midiToFrequency(midi: number): number {
  return A4_FREQUENCY * 2 ** ((midi - A4_MIDI) / 12);
}

/** Note label ("E2") of a MIDI note number. */
export function midiToLabel(midi: number): string {
  const name = NOTE_NAMES[((midi % 12) + 12) % 12];
  const octave = Math.floor(midi / 12) - 1;
  return `${name}${octave}`;
}

/**
 * Resolves a detected frequency to the nearest note and the deviation in
 * cents. Returns null for non-positive / invalid input.
 */
export function frequencyToNote(frequency: number): NoteInfo | null {
  if (!Number.isFinite(frequency) || frequency <= 0) return null;

  const midiExact = A4_MIDI + 12 * Math.log2(frequency / A4_FREQUENCY);
  const midi = Math.round(midiExact);
  const cents = Math.round((midiExact - midi) * 100);
  const name = NOTE_NAMES[((midi % 12) + 12) % 12];
  const octave = Math.floor(midi / 12) - 1;

  return { midi, name, octave, label: `${name}${octave}`, cents };
}

/**
 * Deviation of `frequency` from `targetFrequency` in cents.
 * Positive = sharp (too high), negative = flat (too low).
 */
export function centsBetween(frequency: number, targetFrequency: number): number {
  if (frequency <= 0 || targetFrequency <= 0) return 0;
  return Math.round(1200 * Math.log2(frequency / targetFrequency));
}

export type Notation = "english" | "german";

// German/Czech naming: pitch class 10 (A#/B-flat) is written "B" and pitch
// class 11 is written "H". Sharps are kept as in NOTE_NAMES — this toggle
// only swaps H/B, not the full German Cis/Dis system.
const GERMAN_NOTE_NAMES = [
  "C",
  "C#",
  "D",
  "D#",
  "E",
  "F",
  "F#",
  "G",
  "G#",
  "A",
  "B",
  "H",
] as const;

/** Note name for a pitch class (0 = C) in the chosen notation. */
export function pitchClassName(pitchClass: number, notation: Notation): string {
  const index = ((Math.round(pitchClass) % 12) + 12) % 12;
  return (notation === "german" ? GERMAN_NOTE_NAMES : NOTE_NAMES)[index];
}

/** Note label with octave (e.g. "H2") for a MIDI note in the chosen notation. */
export function midiToLabelIn(midi: number, notation: Notation): string {
  const octave = Math.floor(midi / 12) - 1;
  return `${pitchClassName(midi, notation)}${octave}`;
}

/**
 * Re-letters a chord name's root for the chosen notation: in German notation
 * a "B..." chord becomes "H..." and a "Bb..." chord becomes "B...". Other
 * chord names are returned unchanged.
 */
export function localizeChordName(name: string, notation: Notation): string {
  if (notation !== "german") return name;
  if (name.startsWith("Bb")) return `B${name.slice(2)}`;
  if (name.startsWith("B")) return `H${name.slice(1)}`;
  return name;
}
