// Fretboard memory game logic — pure, no DOM. Part of Bod 19.
// Unit-tested in fretboardGame.test.ts.

/**
 * Pitch class (0 = C … 11 = B) of the note `fret` semitones above an open
 * string of the given MIDI note.
 */
export function noteAtFret(openMidi: number, fret: number): number {
  return (((openMidi + fret) % 12) + 12) % 12;
}

/**
 * Picks a random target pitch class for the next question, never repeating
 * `previous`. `random` is injectable so tests can be deterministic.
 */
export function pickTarget(
  previous: number | null,
  random: () => number = Math.random,
): number {
  const pick = Math.min(11, Math.floor(random() * 12));
  if (previous === null || pick !== previous) return pick;
  return (pick + 1) % 12;
}
