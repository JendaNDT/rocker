// Exercises for the Bod 12 rock/metal trainers — the Power Chord Trainer and
// the Gallop Trainer. Pure data + types, no audio or React. Power chord ids
// reference src/lib/powerChords.ts; metalExercises.test.ts checks they all
// resolve.

/** A drill for the Power Chord Trainer: power chords played in tempo. */
export interface PowerChordExercise {
  id: string;
  /** Czech display name. */
  name: string;
  /** Czech one-line description. */
  description: string;
  /** Power chord ids (see powerChords.ts) in playing order. */
  sequence: string[];
  /** How many metronome beats each chord is held. */
  beatsPerChord: number;
  /** Suggested starting tempo in BPM. */
  startBpm: number;
}

/** A rhythm drill for the Gallop Trainer. */
export interface GallopExercise {
  id: string;
  name: string;
  description: string;
  /**
   * One repeating rhythm cell, as note lengths in beats (a quarter note is 1).
   * The classic gallop is an eighth plus two sixteenths: [0.5, 0.25, 0.25].
   */
  cell: number[];
  /** Power chord id the rhythm is played on. */
  chordId: string;
  startBpm: number;
}

export const POWER_CHORD_EXERCISES: PowerChordExercise[] = [
  {
    id: "adjacent-frets",
    name: "Sousední pražce",
    description:
      "E5, F5, F#5, G5 po nízké E struně — čistý přesun o jeden pražec.",
    sequence: ["e-0", "e-1", "e-2", "e-3"],
    beatsPerChord: 2,
    startBpm: 70,
  },
  {
    id: "string-switch",
    name: "Přechod mezi strunami",
    description: "Střídání kořene na E a A struně — E5, A5, G5, C5.",
    sequence: ["e-0", "a-0", "e-3", "a-3"],
    beatsPerChord: 2,
    startBpm: 70,
  },
  {
    id: "power-jumps",
    name: "Přeskoky pozic",
    description: "Větší skoky po hmatníku — E5, G5, A5, C5 a zpět.",
    sequence: ["e-0", "e-3", "e-5", "e-8", "e-5", "e-3"],
    beatsPerChord: 1,
    startBpm: 80,
  },
];

export const GALLOP_EXERCISES: GallopExercise[] = [
  {
    id: "basic-gallop",
    name: "Základní gallop",
    description:
      "Klasický gallop (osminka plus dvě šestnáctky) na E5 — rytmus Iron Maiden.",
    cell: [0.5, 0.25, 0.25],
    chordId: "e-0",
    startBpm: 90,
  },
  {
    id: "reverse-gallop",
    name: "Reverzní gallop",
    description: "Obrácený gallop (dvě šestnáctky plus osminka) na E5.",
    cell: [0.25, 0.25, 0.5],
    chordId: "e-0",
    startBpm: 90,
  },
];

/** Looks up a Power Chord Trainer exercise by id. */
export function getPowerChordExercise(
  id: string,
): PowerChordExercise | undefined {
  return POWER_CHORD_EXERCISES.find((exercise) => exercise.id === id);
}

/** Looks up a Gallop Trainer exercise by id. */
export function getGallopExercise(id: string): GallopExercise | undefined {
  return GALLOP_EXERCISES.find((exercise) => exercise.id === id);
}
