// Surgical Practice Mode engine — pure state machine, no DOM or React.
// Unit-tested in surgicalEngine.test.ts.
//
// Flow: the learner plays the whole exercise note by note ("run"). Wrong
// notes are counted per position. When the run ends with mistakes, the engine
// isolates the worst spot ("drill") and requires three clean repetitions of a
// short window before the exercise counts as complete.

import type { Exercise } from "@/lib/exercises";

export type SurgicalPhase = "run" | "drill" | "complete";

export const DRILL_LENGTH = 3;
export const REQUIRED_CLEAN_REPS = 3;

export interface SurgicalState {
  exercise: Exercise;
  phase: SurgicalPhase;
  /** Next expected note index during the run. */
  position: number;
  /** Wrong-attempt count per exercise-note index. */
  errorCounts: number[];
  /** Start index of the drill window within exercise.notes. */
  drillStart: number;
  drillLength: number;
  /** Position within the drill window. */
  drillPosition: number;
  /** Consecutive clean repetitions achieved in the drill. */
  cleanReps: number;
}

function samePitchClass(played: number, expected: number): boolean {
  return (((played % 12) + 12) % 12) === (((expected % 12) + 12) % 12);
}

function worstIndex(errorCounts: readonly number[]): number {
  let worst = -1;
  let max = 0;
  errorCounts.forEach((count, index) => {
    if (count > max) {
      max = count;
      worst = index;
    }
  });
  return worst;
}

export function startSurgical(exercise: Exercise): SurgicalState {
  return {
    exercise,
    phase: "run",
    position: 0,
    errorCounts: exercise.notes.map(() => 0),
    drillStart: 0,
    drillLength: 0,
    drillPosition: 0,
    cleanReps: 0,
  };
}

function beginDrill(state: SurgicalState): SurgicalState {
  const total = state.exercise.notes.length;
  const length = Math.min(DRILL_LENGTH, total);
  const worst = worstIndex(state.errorCounts);
  const start = Math.max(0, Math.min(worst - 1, total - length));
  return {
    ...state,
    phase: "drill",
    drillStart: start,
    drillLength: length,
    drillPosition: 0,
    cleanReps: 0,
  };
}

/** Feeds a detected note into the engine and returns the next state. */
export function feedNote(
  state: SurgicalState,
  playedMidi: number,
): SurgicalState {
  if (state.phase === "run") {
    const expected = state.exercise.notes[state.position];
    if (!samePitchClass(playedMidi, expected)) {
      const errorCounts = state.errorCounts.slice();
      errorCounts[state.position] += 1;
      return { ...state, errorCounts };
    }
    const position = state.position + 1;
    if (position < state.exercise.notes.length) {
      return { ...state, position };
    }
    // Run finished — drill if there were mistakes, otherwise done.
    if (worstIndex(state.errorCounts) === -1) {
      return { ...state, position, phase: "complete" };
    }
    return beginDrill({ ...state, position });
  }

  if (state.phase === "drill") {
    const expected =
      state.exercise.notes[state.drillStart + state.drillPosition];
    if (!samePitchClass(playedMidi, expected)) {
      // A wrong note resets the current repetition and the clean streak.
      return { ...state, drillPosition: 0, cleanReps: 0 };
    }
    const drillPosition = state.drillPosition + 1;
    if (drillPosition < state.drillLength) {
      return { ...state, drillPosition };
    }
    const cleanReps = state.cleanReps + 1;
    if (cleanReps >= REQUIRED_CLEAN_REPS) {
      return { ...state, drillPosition: 0, cleanReps, phase: "complete" };
    }
    return { ...state, drillPosition: 0, cleanReps };
  }

  return state;
}

/** The note the learner should play right now, or null when complete. */
export function currentTarget(state: SurgicalState): number | null {
  if (state.phase === "run") {
    return state.exercise.notes[state.position] ?? null;
  }
  if (state.phase === "drill") {
    return (
      state.exercise.notes[state.drillStart + state.drillPosition] ?? null
    );
  }
  return null;
}

/** The slice of notes being drilled (empty outside the drill phase). */
export function drillNotes(state: SurgicalState): number[] {
  if (state.phase !== "drill") return [];
  return state.exercise.notes.slice(
    state.drillStart,
    state.drillStart + state.drillLength,
  );
}
