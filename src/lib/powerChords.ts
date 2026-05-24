// Movable power chords for the Power Chord Trainer (Bod 12).
//
// A power chord is a root plus its fifth (and usually the octave). The shape
// is movable: this module models roots on the low E string (index 0) and the
// A string (index 1). String ordering matches src/lib/chords.ts — index 0 is
// the low E string, index 5 the high E string.

/** Open-string MIDI notes for standard tuning, low E (index 0) to high E. */
const STANDARD_OPEN_MIDI = [40, 45, 50, 55, 59, 64];

/** Pitch-class names, index 0 = C. Sharps only (no flats). */
const PITCH_CLASSES = [
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
];

/** Highest fret the trainer uses for power chord roots. */
export const MAX_POWER_CHORD_FRET = 12;

export interface PowerChord {
  /** Stable id, unique across the fretboard, e.g. "e-3" or "a-5". */
  id: string;
  /** Display name, e.g. "G5". */
  name: string;
  /** Root pitch-class name, e.g. "G" or "F#". */
  rootName: string;
  /** String the root sits on: 0 = low E, 1 = A. */
  rootStringIndex: number;
  /** Fret of the root (0 = open string). */
  rootFret: number;
  /** MIDI note number of the root. */
  rootMidi: number;
}

/** Returns the pitch-class name of a MIDI note (e.g. 43 -> "G"). */
export function midiToPitchClass(midi: number): string {
  return PITCH_CLASSES[((Math.round(midi) % 12) + 12) % 12];
}

/**
 * Builds a movable power chord whose root sits at the given string and fret.
 * rootStringIndex must be 0 (low E) or 1 (A).
 */
export function powerChordAt(
  rootStringIndex: number,
  rootFret: number,
): PowerChord {
  const rootMidi = STANDARD_OPEN_MIDI[rootStringIndex] + rootFret;
  const rootName = midiToPitchClass(rootMidi);
  const stringLetter = rootStringIndex === 0 ? "e" : "a";
  return {
    id: `${stringLetter}-${rootFret}`,
    name: `${rootName}5`,
    rootName,
    rootStringIndex,
    rootFret,
    rootMidi,
  };
}

/**
 * MIDI notes a power chord sounds: root, fifth (+7) and octave (+12).
 * These are the notes the pitch detector should expect.
 */
export function powerChordMidiNotes(chord: PowerChord): number[] {
  return [chord.rootMidi, chord.rootMidi + 7, chord.rootMidi + 12];
}

/** All power chords from the open string to MAX_POWER_CHORD_FRET, E then A. */
export const POWER_CHORDS: PowerChord[] = [
  ...Array.from({ length: MAX_POWER_CHORD_FRET + 1 }, (_, fret) =>
    powerChordAt(0, fret),
  ),
  ...Array.from({ length: MAX_POWER_CHORD_FRET + 1 }, (_, fret) =>
    powerChordAt(1, fret),
  ),
];

/** Looks up a power chord by its id. */
export function getPowerChord(id: string): PowerChord | undefined {
  return POWER_CHORDS.find((chord) => chord.id === id);
}
