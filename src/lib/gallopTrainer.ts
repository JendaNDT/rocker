// Gallop Trainer engine — pure logic, no DOM, audio or React.
// Unit-tested in gallopTrainer.test.ts.
//
// A gallop is a short rhythm cell (one beat long) repeated continuously, e.g.
// an eighth plus two sixteenths. This engine turns a cell into note onset
// times for the audio player, tracks how many cells have been played, and —
// when speed-up is on — raises the tempo by 5% after every completed round.
// A round is CELLS_PER_ROUND cells; the audio player calls advanceCell once
// per played cell.

import type { GallopExercise } from "@/lib/metalExercises";
import { clampBpm } from "@/lib/tempo";

/** Cells in one round — the unit for speed-up and BPM records. */
export const CELLS_PER_ROUND = 8;

/** Tempo increase applied after a completed round when speed-up is on. */
export const SPEED_UP_RATIO = 0.05;

export interface GallopTrainerState {
  exercise: GallopExercise;
  /** Current tempo in BPM. */
  bpm: number;
  /** Total gallop cells completed since the trainer started. */
  cellsCompleted: number;
  /** Number of full rounds completed (a round = CELLS_PER_ROUND cells). */
  roundsCompleted: number;
  /** Whether the per-round +5% speed-up is enabled. */
  speedUpEnabled: boolean;
}

/**
 * Start beats of each note in a cell — the running sum of preceding note
 * lengths. For [0.5, 0.25, 0.25] this is [0, 0.5, 0.75].
 */
export function cellOnsets(cell: number[]): number[] {
  const onsets: number[] = [];
  let acc = 0;
  for (const length of cell) {
    onsets.push(acc);
    acc += length;
  }
  return onsets;
}

/** Total length of a cell in beats (the cells used here are one beat long). */
export function cellDurationBeats(cell: number[]): number {
  return cell.reduce((sum, length) => sum + length, 0);
}

/** Seconds per beat at a given tempo. */
export function secondsPerBeat(bpm: number): number {
  return 60 / bpm;
}

/** Note onset times of a cell in seconds, relative to the cell's start. */
export function cellOnsetsSeconds(cell: number[], bpm: number): number[] {
  const spb = secondsPerBeat(bpm);
  return cellOnsets(cell).map((beat) => beat * spb);
}

/** Total length of a cell in seconds at a given tempo. */
export function cellDurationSeconds(cell: number[], bpm: number): number {
  return cellDurationBeats(cell) * secondsPerBeat(bpm);
}

/** Creates a fresh trainer state for an exercise. */
export function startGallopTrainer(
  exercise: GallopExercise,
  speedUpEnabled: boolean,
): GallopTrainerState {
  return {
    exercise,
    bpm: clampBpm(exercise.startBpm),
    cellsCompleted: 0,
    roundsCompleted: 0,
    speedUpEnabled,
  };
}

/** Progress through the current round, from 0 up to (never reaching) 1. */
export function roundProgress(state: GallopTrainerState): number {
  return (state.cellsCompleted % CELLS_PER_ROUND) / CELLS_PER_ROUND;
}

/**
 * Advances the trainer by one played gallop cell. When the cell completes a
 * full round, roundsCompleted is bumped and — if speed-up is on — the tempo
 * is raised by SPEED_UP_RATIO (clamped into the valid BPM range).
 */
export function advanceCell(state: GallopTrainerState): GallopTrainerState {
  const cellsCompleted = state.cellsCompleted + 1;
  const completedRound = cellsCompleted % CELLS_PER_ROUND === 0;
  if (!completedRound) {
    return { ...state, cellsCompleted };
  }
  return {
    ...state,
    cellsCompleted,
    roundsCompleted: state.roundsCompleted + 1,
    bpm: state.speedUpEnabled
      ? clampBpm(state.bpm * (1 + SPEED_UP_RATIO))
      : state.bpm,
  };
}
