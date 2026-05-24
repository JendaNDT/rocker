// Exercises for the Surgical Practice Mode — short single-note phrases.
// Adjacent notes are always different so self-paced detection can advance
// reliably (it cannot tell two identical consecutive notes apart).

export interface Exercise {
  id: string;
  name: string;
  description: string;
  /** MIDI notes of the phrase, in order. */
  notes: number[];
}

export const EXERCISES: Exercise[] = [
  {
    id: "spider",
    name: "Pavoučí cvičení 1-2-3-4",
    description: "Rozehřátí prstů — pražce 1 až 4 na dvou strunách.",
    notes: [41, 42, 43, 44, 46, 47, 48, 49],
  },
  {
    id: "pentatonika-nahoru",
    name: "A moll pentatonika nahoru",
    description: "Pentatonická stupnice vzestupně — základ rockových sól.",
    notes: [45, 48, 50, 52, 55, 57],
  },
  {
    id: "pentatonika-dolu",
    name: "A moll pentatonika dolů",
    description: "Pentatonická stupnice sestupně.",
    notes: [57, 55, 52, 50, 48, 45],
  },
  {
    id: "dur-stupnice",
    name: "C dur stupnice",
    description: "Durová stupnice přes celou oktávu.",
    notes: [48, 50, 52, 53, 55, 57, 59, 60],
  },
  {
    id: "melodicky-rif",
    name: "Melodický riff",
    description: "Krátká fráze na procvičení přechodů mezi tóny.",
    notes: [40, 43, 45, 43, 40, 45],
  },
];

export function getExercise(id: string): Exercise | undefined {
  return EXERCISES.find((exercise) => exercise.id === id);
}
