// Guitar tunings. Each string is stored as a MIDI note number; the target
// frequency is derived via midiToFrequency (see src/lib/pitch.ts).
// Strings are ordered low to high (6th string first, 1st string last).

import { centsBetween, midiToFrequency } from "@/lib/pitch";

export interface TuningString {
  /** Display label, e.g. "E2". */
  note: string;
  /** MIDI note number. */
  midi: number;
}

export interface Tuning {
  id: string;
  label: string;
  strings: TuningString[];
}

export const TUNINGS: Tuning[] = [
  {
    id: "standard",
    label: "Standardní (E A D G B E)",
    strings: [
      { note: "E2", midi: 40 },
      { note: "A2", midi: 45 },
      { note: "D3", midi: 50 },
      { note: "G3", midi: 55 },
      { note: "B3", midi: 59 },
      { note: "E4", midi: 64 },
    ],
  },
  {
    id: "drop-d",
    label: "Drop D (D A D G B E)",
    strings: [
      { note: "D2", midi: 38 },
      { note: "A2", midi: 45 },
      { note: "D3", midi: 50 },
      { note: "G3", midi: 55 },
      { note: "B3", midi: 59 },
      { note: "E4", midi: 64 },
    ],
  },
  {
    id: "eb-standard",
    label: "Eb standard (Eb Ab Db Gb Bb Eb)",
    strings: [
      { note: "D#2", midi: 39 },
      { note: "G#2", midi: 44 },
      { note: "C#3", midi: 49 },
      { note: "F#3", midi: 54 },
      { note: "A#3", midi: 58 },
      { note: "D#4", midi: 63 },
    ],
  },
  {
    id: "d-standard",
    label: "D standard (D G C F A D)",
    strings: [
      { note: "D2", midi: 38 },
      { note: "G2", midi: 43 },
      { note: "C3", midi: 48 },
      { note: "F3", midi: 53 },
      { note: "A3", midi: 57 },
      { note: "D4", midi: 62 },
    ],
  },
  {
    id: "drop-c",
    label: "Drop C (C G C F A D)",
    strings: [
      { note: "C2", midi: 36 },
      { note: "G2", midi: 43 },
      { note: "C3", midi: 48 },
      { note: "F3", midi: 53 },
      { note: "A3", midi: 57 },
      { note: "D4", midi: 62 },
    ],
  },
  {
    id: "drop-b",
    label: "Drop B (B F# B E G# C#)",
    strings: [
      { note: "B1", midi: 35 },
      { note: "F#2", midi: 42 },
      { note: "B2", midi: 47 },
      { note: "E3", midi: 52 },
      { note: "G#3", midi: 56 },
      { note: "C#4", midi: 61 },
    ],
  },
  {
    id: "open-g",
    label: "Open G (D G D G B D)",
    strings: [
      { note: "D2", midi: 38 },
      { note: "G2", midi: 43 },
      { note: "D3", midi: 50 },
      { note: "G3", midi: 55 },
      { note: "B3", midi: 59 },
      { note: "D4", midi: 62 },
    ],
  },
  {
    id: "open-d",
    label: "Open D (D A D F# A D)",
    strings: [
      { note: "D2", midi: 38 },
      { note: "A2", midi: 45 },
      { note: "D3", midi: 50 },
      { note: "F#3", midi: 54 },
      { note: "A3", midi: 57 },
      { note: "D4", midi: 62 },
    ],
  },
  {
    id: "dadgad",
    label: "DADGAD (D A D G A D)",
    strings: [
      { note: "D2", midi: 38 },
      { note: "A2", midi: 45 },
      { note: "D3", midi: 50 },
      { note: "G3", midi: 55 },
      { note: "A3", midi: 57 },
      { note: "D4", midi: 62 },
    ],
  },
];

export const DEFAULT_TUNING_ID = "standard";

export function getTuning(id: string): Tuning {
  return TUNINGS.find((tuning) => tuning.id === id) ?? TUNINGS[0];
}

export interface NearestString {
  /** Index of the string within the tuning (0 = lowest). */
  index: number;
  string: TuningString;
  /** Deviation of the played frequency from this string in cents. */
  cents: number;
}

/**
 * Finds which string of the tuning a played frequency is closest to.
 * Returns null for non-positive input.
 */
export function nearestString(
  tuning: Tuning,
  frequency: number,
): NearestString | null {
  if (!Number.isFinite(frequency) || frequency <= 0) return null;

  let best: NearestString | null = null;
  tuning.strings.forEach((string, index) => {
    const cents = centsBetween(frequency, midiToFrequency(string.midi));
    if (best === null || Math.abs(cents) < Math.abs(best.cents)) {
      best = { index, string, cents };
    }
  });
  return best;
}
