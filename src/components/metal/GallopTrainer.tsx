"use client";

import { useEffect, useRef, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { gallopPlayer } from "@/audio/gallopPlayer";
import { DEFAULT_TUNING_ID } from "@/audio/tunings";
import { GALLOP_EXERCISES } from "@/lib/metalExercises";
import type { GallopExercise } from "@/lib/metalExercises";
import {
  advanceCell,
  roundProgress,
  startGallopTrainer,
} from "@/lib/gallopTrainer";
import type { GallopTrainerState } from "@/lib/gallopTrainer";
import { type Notation, pitchClassName } from "@/lib/pitch";
import { getPowerChord } from "@/lib/powerChords";
import { resolveTuning } from "@/lib/resolveTuning";
import { clampBpm } from "@/lib/tempo";
import { useHydrated } from "@/lib/useHydrated";
import { useCustomTuningsStore } from "@/stores/customTuningsStore";
import { useGallopPlayerStore } from "@/stores/gallopPlayerStore";
import { useProgressStore } from "@/stores/progressStore";
import { useSettingsStore } from "@/stores/settingsStore";

const SEGMENT_ACTIVE =
  "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

/** Human-readable note value for a length in beats. */
function noteLabel(beats: number): string {
  if (beats === 1) return "1/4";
  if (beats === 0.5) return "1/8";
  if (beats === 0.25) return "1/16";
  return `${beats}`;
}

/**
 * Gallop Trainer — plays a gallop rhythm cell against the dedicated gallop
 * player. Each played cell advances the pure engine
 * (src/lib/gallopTrainer.ts); a completed round records the tempo via
 * progressStore.recordBpm. Optionally ramps the tempo up 5% per round.
 */
export function GallopTrainer() {
  const [state, setState] = useState<GallopTrainerState>(() =>
    startGallopTrainer(GALLOP_EXERCISES[0], false),
  );

  // Mirror of the latest state, read by the cell effect without stale
  // closures and without re-subscribing on every state change.
  const stateRef = useRef(state);
  stateRef.current = state;
  const prevCellsRef = useRef(0);

  const isPlaying = useGallopPlayerStore((store) => store.isPlaying);
  const cellsPlayed = useGallopPlayerStore((store) => store.cellsPlayed);
  const currentOnset = useGallopPlayerStore((store) => store.currentOnset);

  const bpmRecords = useProgressStore((store) => store.bpmRecords);
  const storedNotation = useSettingsStore((store) => store.notation);
  const storedTuningId = useSettingsStore((store) => store.tuningId);
  const customTunings = useCustomTuningsStore((store) => store.tunings);
  const hydrated = useHydrated();
  const record = hydrated ? bpmRecords[state.exercise.id] : undefined;
  const notation = hydrated ? storedNotation : "english";
  // Power chord root is resolved against the active tuning. Before
  // hydration use the default tuning so prerendered HTML matches.
  const openMidi = resolveTuning(
    hydrated ? storedTuningId : DEFAULT_TUNING_ID,
    hydrated ? customTunings : [],
  ).strings.map((string) => string.midi);

  // Advance the engine once per played gallop cell.
  useEffect(() => {
    if (!isPlaying) return;
    const prevCells = prevCellsRef.current;
    if (cellsPlayed <= prevCells) return;
    prevCellsRef.current = cellsPlayed;

    let next = stateRef.current;
    for (let i = prevCells; i < cellsPlayed; i += 1) {
      const before = next;
      next = advanceCell(next);
      if (next.roundsCompleted > before.roundsCompleted) {
        // Record the tempo the just-finished round was actually played at.
        useProgressStore.getState().recordBpm(before.exercise.id, before.bpm);
      }
    }
    setState(next);
  }, [cellsPlayed, isPlaying]);

  // Keep the player tempo in sync with the engine (manual changes and the
  // per-round speed-up both flow through state.bpm).
  useEffect(() => {
    if (isPlaying) gallopPlayer.setBpm(state.bpm);
  }, [state.bpm, isPlaying]);

  // Stop the player when leaving the trainer.
  useEffect(() => () => gallopPlayer.stop(), []);

  function togglePlay() {
    if (gallopPlayer.getState().isPlaying) {
      gallopPlayer.stop();
      return;
    }
    void (async () => {
      const context = await audioEngine.getOutputContext();
      setState((current) => ({
        ...current,
        cellsCompleted: 0,
        roundsCompleted: 0,
      }));
      prevCellsRef.current = 0;
      gallopPlayer.setCell(stateRef.current.exercise.cell);
      gallopPlayer.setBpm(stateRef.current.bpm);
      gallopPlayer.start(context);
    })();
  }

  function chooseExercise(exercise: GallopExercise) {
    gallopPlayer.stop();
    prevCellsRef.current = 0;
    setState((current) => startGallopTrainer(exercise, current.speedUpEnabled));
  }

  function adjustBpm(delta: number) {
    setState((current) => ({
      ...current,
      bpm: clampBpm(current.bpm + delta),
    }));
  }

  function setSpeedUp(enabled: boolean) {
    setState((current) => ({ ...current, speedUpEnabled: enabled }));
  }

  const chord = getPowerChord(state.exercise.chordId, openMidi);
  const chordName = chord
    ? `${pitchClassName(chord.rootMidi, notation)}5`
    : "—";
  const progressPercent = Math.round(roundProgress(state) * 100);

  return (
    <div>
      {/* Exercise picker */}
      <div className="inline-flex flex-wrap rounded-lg border border-line p-1">
        {GALLOP_EXERCISES.map((exercise) => (
          <button
            key={exercise.id}
            type="button"
            onClick={() => chooseExercise(exercise)}
            className={
              exercise.id === state.exercise.id ? SEGMENT_ACTIVE : SEGMENT_IDLE
            }
          >
            {exercise.name}
          </button>
        ))}
      </div>
      <p className="mt-2 text-sm text-dim">
        {state.exercise.description}
      </p>

      {/* Rhythm visualisation */}
      <div className="mt-6 rounded-xl border border-line bg-surface p-5">
        <div className="text-xs text-muted">Rytmus gallopu</div>
        <div className="mt-2 flex gap-1.5">
          {state.exercise.cell.map((length, index) => (
            <div
              key={index}
              style={{ flexGrow: length }}
              className={`flex h-16 items-center justify-center rounded-lg border text-sm font-medium transition-colors ${
                index === currentOnset
                  ? "border-accent bg-accent/20 text-accent"
                  : "border-line bg-bg text-muted"
              }`}
            >
              {noteLabel(length)}
            </div>
          ))}
        </div>
        <p className="mt-3 text-sm text-dim">
          Hraj na{" "}
          <span className="font-semibold text-text">{chordName}</span>{" "}
          s palm mute.
        </p>

        {/* Round progress */}
        <div className="mt-4">
          <div className="h-2 overflow-hidden rounded-full bg-surface-2">
            <div
              className="h-full bg-accent transition-all duration-100"
              style={{ width: `${progressPercent}%` }}
            />
          </div>
          <div className="mt-1 text-xs text-muted">
            Odehraná kola: {state.roundsCompleted}
          </div>
        </div>
      </div>

      {/* Tempo */}
      <div className="mt-6 flex items-center justify-center gap-3">
        <button
          type="button"
          aria-label="Snížit tempo"
          onClick={() => adjustBpm(-5)}
          className="h-10 w-10 rounded-lg border border-line bg-surface text-lg transition-colors hover:border-line-strong"
        >
          −
        </button>
        <div className="text-center">
          <div className="text-3xl font-bold tabular-nums">{state.bpm}</div>
          <div className="text-xs text-muted">BPM</div>
        </div>
        <button
          type="button"
          aria-label="Zvýšit tempo"
          onClick={() => adjustBpm(5)}
          className="h-10 w-10 rounded-lg border border-line bg-surface text-lg transition-colors hover:border-line-strong"
        >
          +
        </button>
      </div>

      {record !== undefined && (
        <p className="mt-2 text-center text-sm text-dim">
          Tvůj rekord v tomto cvičení: {record} BPM
        </p>
      )}

      <label className="mt-4 flex items-center justify-center gap-2 text-sm text-text">
        <input
          type="checkbox"
          checked={state.speedUpEnabled}
          onChange={(event) => setSpeedUp(event.target.checked)}
          className="h-4 w-4 accent-accent"
        />
        Postupně zrychlovat (+5 % po každém kole)
      </label>

      {/* Start / stop */}
      <div className="mt-6 flex justify-center">
        <button
          type="button"
          onClick={togglePlay}
          className={
            isPlaying
              ? "rounded-lg border border-line bg-surface px-10 py-3 text-base font-semibold transition-colors hover:border-line-strong"
              : "rounded-lg bg-accent px-10 py-3 text-base font-semibold text-accent-fg transition-colors hover:bg-accent/90"
          }
        >
          {isPlaying ? "Zastavit" : "Spustit"}
        </button>
      </div>
    </div>
  );
}
