// Ear training logic — pure, no DOM or audio. Part of Bod 19.
// Unit-tested in earTraining.test.ts.

/** An interval the ear-training game can quiz. */
export interface IntervalDef {
  /** Czech display name. */
  name: string;
  /** Distance in semitones above the root. */
  semitones: number;
}

/** A chord quality the ear-training game can quiz. */
export interface ChordQualityDef {
  /** Czech display name. */
  name: string;
  /** Semitone offsets from the root, starting at 0. */
  intervals: number[];
}

/** Intervals quizzed by ear training. */
export const INTERVALS: IntervalDef[] = [
  { name: "Malá sekunda", semitones: 1 },
  { name: "Velká sekunda", semitones: 2 },
  { name: "Malá tercie", semitones: 3 },
  { name: "Velká tercie", semitones: 4 },
  { name: "Čistá kvarta", semitones: 5 },
  { name: "Čistá kvinta", semitones: 7 },
  { name: "Velká sexta", semitones: 9 },
  { name: "Oktáva", semitones: 12 },
];

/** Chord qualities quizzed by ear training. */
export const CHORD_QUALITIES: ChordQualityDef[] = [
  { name: "Dur", intervals: [0, 4, 7] },
  { name: "Moll", intervals: [0, 3, 7] },
  { name: "Zmenšený", intervals: [0, 3, 6] },
  { name: "Zvětšený", intervals: [0, 4, 8] },
];

/**
 * Picks a random index 0..count-1, never repeating `previous`. `random` is
 * injectable so tests can be deterministic.
 */
export function pickIndex(
  count: number,
  previous: number | null,
  random: () => number = Math.random,
): number {
  const pick = Math.min(count - 1, Math.floor(random() * count));
  if (previous === null || count < 2 || pick !== previous) return pick;
  return (pick + 1) % count;
}

/** A random MIDI root note in a comfortable mid range (C3..B3). */
export function pickRoot(random: () => number = Math.random): number {
  return 48 + Math.min(11, Math.floor(random() * 12));
}
