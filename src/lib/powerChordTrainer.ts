// Power Chord Trainer engine — pure logic, no DOM, audio or React.
// Unit-tested in powerChordTrainer.test.ts.
//
// The learner picks a PowerChordExercise (a sequence of power chords) and
// plays it against the metronome. The component feeds one beat at a time into
// advanceBeat; the engine tracks which chord is active, counts completed
// rounds of the sequence, and — when speed-up is on — raises the tempo by 5%
// after every completed round.

import type { PowerChordExercise } from "@/lib/metalExercises";
import { clampBpm } from "@/lib/tempo";

/** Tempo increase applied after a completed round when speed-up is on. */
export const SPEED_UP_RATIO = 0.05;

export interface PowerChordTrainerState {
  exercise: PowerChordExercise;
  /** Current metronome tempo in BPM. */
  bpm: number;
  /** Total metronome beats elapsed since the trainer started. */
  elapsedBeats: number;
  /** Number of full sequence rounds completed. */
  roundsCompleted: number;
  /** Whether the per-round +5% speed-up is enabled. */
  speedUpEnabled: boolean;
}

/** Beats in one full round of an exercise (the whole sequence played once). */
export function roundBeats(exercise: PowerChordExercise): number {
  return exercise.sequence.length * exercise.beatsPerChord;
}

/** Creates a fresh trainer state for an exercise. */
export function startPowerChordTrainer(
  exercise: PowerChordExercise,
  speedUpEnabled: boolean,
): PowerChordTrainerState {
  return {
    exercise,
    bpm: clampBpm(exercise.startBpm),
    elapsedBeats: 0,
    roundsCompleted: 0,
    speedUpEnabled,
  };
}

/** Index in the sequence of the chord currently being played. */
export function activeChordIndex(state: PowerChordTrainerState): number {
  const beatInRound = state.elapsedBeats % roundBeats(state.exercise);
  return Math.floor(beatInRound / state.exercise.beatsPerChord);
}

/** Index in the sequence of the chord coming up next. */
export function nextChordIndex(state: PowerChordTrainerState): number {
  return (activeChordIndex(state) + 1) % state.exercise.sequence.length;
}

/** Power chord id currently being played. */
export function activeChordId(state: PowerChordTrainerState): string {
  return state.exercise.sequence[activeChordIndex(state)];
}

/** Power chord id coming up next. */
export function nextChordId(state: PowerChordTrainerState): string {
  return state.exercise.sequence[nextChordIndex(state)];
}

/**
 * Progress through the current round, from 0 (round start) up to but never
 * reaching 1 — completing a round wraps the value back to 0.
 */
export function roundProgress(state: PowerChordTrainerState): number {
  const total = roundBeats(state.exercise);
  return (state.elapsedBeats % total) / total;
}

/**
 * Advances the trainer by one metronome beat. When the beat completes a full
 * round, roundsCompleted is bumped and — if speed-up is on — the tempo is
 * raised by SPEED_UP_RATIO (clamped into the valid BPM range).
 */
export function advanceBeat(
  state: PowerChordTrainerState,
): PowerChordTrainerState {
  const elapsedBeats = state.elapsedBeats + 1;
  const completedRound = elapsedBeats % roundBeats(state.exercise) === 0;
  if (!completedRound) {
    return { ...state, elapsedBeats };
  }
  return {
    ...state,
    elapsedBeats,
    roundsCompleted: state.roundsCompleted + 1,
    bpm: state.speedUpEnabled
      ? clampBpm(state.bpm * (1 + SPEED_UP_RATIO))
      : state.bpm,
  };
}
