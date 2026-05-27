"use client";

import { useEffect, useRef, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { metronome } from "@/audio/metronome";
import { DEFAULT_TUNING_ID, getTuning } from "@/audio/tunings";
import { POWER_CHORD_EXERCISES } from "@/lib/metalExercises";
import type { PowerChordExercise } from "@/lib/metalExercises";
import { getPowerChord } from "@/lib/powerChords";
import type { PowerChord } from "@/lib/powerChords";
import {
  activeChordId,
  advanceBeat,
  nextChordId,
  roundProgress,
  startPowerChordTrainer,
} from "@/lib/powerChordTrainer";
import type { PowerChordTrainerState } from "@/lib/powerChordTrainer";
import { type Notation, pitchClassName } from "@/lib/pitch";
import { clampBpm } from "@/lib/tempo";
import { useHydrated } from "@/lib/useHydrated";
import { useMetronomeStore } from "@/stores/metronomeStore";
import { useProgressStore } from "@/stores/progressStore";
import { useSettingsStore } from "@/stores/settingsStore";

// Mini-neck geometry (SVG user units).
const NECK_FRETS = 12;
const FRET_W = 24;
const STRING_GAP = 13;
const MARGIN_LEFT = 24;
const MARGIN_TOP = 12;
const MARGIN_BOTTOM = 16;
const NECK_W = MARGIN_LEFT + (NECK_FRETS + 1) * FRET_W + 4;
const NECK_H = MARGIN_TOP + STRING_GAP * 5 + MARGIN_BOTTOM;
const INLAY_FRETS = [3, 5, 7, 9, 12];

const SEGMENT_ACTIVE =
  "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

function stringY(stringIndex: number): number {
  return MARGIN_TOP + stringIndex * STRING_GAP;
}

function fretCenterX(fret: number): number {
  return MARGIN_LEFT + fret * FRET_W + FRET_W / 2;
}

/** Small fretboard strip showing a power chord (root, fifth, octave). */
function PowerChordNeck({
  chord,
  name,
  stringNames,
}: {
  chord: PowerChord;
  name: string;
  /** Open-string names of the active tuning, low to high. */
  stringNames: string[];
}) {
  const shape = [
    { stringIndex: chord.rootStringIndex, fret: chord.rootFret, root: true },
    {
      stringIndex: chord.rootStringIndex + 1,
      fret: chord.rootFret + 2,
      root: false,
    },
    {
      stringIndex: chord.rootStringIndex + 2,
      fret: chord.rootFret + 2,
      root: false,
    },
  ];

  return (
    <svg
      viewBox={`0 0 ${NECK_W} ${NECK_H}`}
      className="w-full max-w-md"
      role="img"
      aria-label={`Pozice power chordu ${name} na hmatníku`}
    >
      {/* Strings */}
      {stringNames.map((name, index) => (
        <g key={index}>
          <line
            x1={MARGIN_LEFT}
            y1={stringY(index)}
            x2={MARGIN_LEFT + (NECK_FRETS + 1) * FRET_W}
            y2={stringY(index)}
            stroke="#525252"
            strokeWidth={1}
          />
          <text
            x={MARGIN_LEFT - 8}
            y={stringY(index) + 3}
            textAnchor="end"
            fontSize={8}
            fill="#737373"
          >
            {name}
          </text>
        </g>
      ))}

      {/* Nut and fret wires */}
      {Array.from({ length: NECK_FRETS + 1 }, (_, k) => {
        const x = MARGIN_LEFT + (k + 1) * FRET_W;
        return (
          <line
            key={k}
            x1={x}
            y1={stringY(0)}
            x2={x}
            y2={stringY(5)}
            stroke={k === 0 ? "#a3a3a3" : "#404040"}
            strokeWidth={k === 0 ? 2 : 1}
          />
        );
      })}

      {/* Inlay fret numbers */}
      {INLAY_FRETS.map((fret) => (
        <text
          key={fret}
          x={fretCenterX(fret)}
          y={NECK_H - 4}
          textAnchor="middle"
          fontSize={7}
          fill="#737373"
        >
          {fret}
        </text>
      ))}

      {/* Power chord dots */}
      {shape.map((note, index) => (
        <circle
          key={index}
          cx={fretCenterX(note.fret)}
          cy={stringY(note.stringIndex)}
          r={5}
          fill={note.root ? "#f59e0b" : "#92400e"}
        />
      ))}
    </svg>
  );
}

/**
 * Power Chord Trainer — plays a sequence of power chords against the shared
 * metronome. Each metronome beat advances the pure engine
 * (src/lib/powerChordTrainer.ts); a completed round records the tempo via
 * progressStore.recordBpm. Optionally ramps the tempo up 5% per round.
 */
export function PowerChordTrainer() {
  const [state, setState] = useState<PowerChordTrainerState>(() =>
    startPowerChordTrainer(POWER_CHORD_EXERCISES[0], false),
  );

  // Mirror of the latest state, so the beat effect reads it without stale
  // closures and without re-subscribing on every state change.
  const stateRef = useRef(state);
  stateRef.current = state;
  const prevBeatRef = useRef(-1);

  const isPlaying = useMetronomeStore((store) => store.isPlaying);
  const currentBeat = useMetronomeStore((store) => store.currentBeat);

  const bpmRecords = useProgressStore((store) => store.bpmRecords);
  const storedNotation = useSettingsStore((store) => store.notation);
  const storedTuningId = useSettingsStore((store) => store.tuningId);
  const hydrated = useHydrated();
  const record = hydrated ? bpmRecords[state.exercise.id] : undefined;
  const notation = hydrated ? storedNotation : "english";
  // Active tuning — power chord roots and the mini-neck string labels are
  // resolved against it. Before hydration use the default tuning.
  const tuning = getTuning(hydrated ? storedTuningId : DEFAULT_TUNING_ID);
  const openMidi = tuning.strings.map((string) => string.midi);
  const stringNames = tuning.strings.map((string) =>
    pitchClassName(string.midi, notation),
  );

  // Advance the engine once per metronome beat.
  useEffect(() => {
    if (!isPlaying) {
      prevBeatRef.current = -1;
      return;
    }
    if (currentBeat < 0 || currentBeat === prevBeatRef.current) return;
    prevBeatRef.current = currentBeat;

    const prev = stateRef.current;
    const next = advanceBeat(prev);
    if (next.roundsCompleted > prev.roundsCompleted) {
      // Record the tempo the just-finished round was actually played at.
      useProgressStore.getState().recordBpm(prev.exercise.id, prev.bpm);
    }
    setState(next);
  }, [currentBeat, isPlaying]);

  // Keep the metronome tempo in sync with the engine (manual changes and the
  // per-round speed-up both flow through state.bpm).
  useEffect(() => {
    if (isPlaying) metronome.setBpm(state.bpm);
  }, [state.bpm, isPlaying]);

  // Stop the shared metronome when leaving the trainer.
  useEffect(() => () => metronome.stop(), []);

  function togglePlay() {
    if (metronome.getState().isPlaying) {
      metronome.stop();
      return;
    }
    void (async () => {
      const context = await audioEngine.getOutputContext();
      setState((current) => ({
        ...current,
        elapsedBeats: 0,
        roundsCompleted: 0,
      }));
      prevBeatRef.current = -1;
      metronome.setSubdivision(1);
      metronome.setBeatsPerBar(4);
      metronome.setBpm(stateRef.current.bpm);
      // Drop any drum-mode overrides the user may have left on the metronome
      // screen — the trainer expects a plain click on every beat.
      metronome.setSoundPattern(undefined);
      metronome.setSubdivisionSound(undefined);
      metronome.start(context);
    })();
  }

  function chooseExercise(exercise: PowerChordExercise) {
    metronome.stop();
    prevBeatRef.current = -1;
    setState((current) =>
      startPowerChordTrainer(exercise, current.speedUpEnabled),
    );
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

  const activeChord = getPowerChord(activeChordId(state), openMidi);
  const nextChord = getPowerChord(nextChordId(state), openMidi);
  const progressPercent = Math.round(roundProgress(state) * 100);
  const activeName = activeChord
    ? `${pitchClassName(activeChord.rootMidi, notation)}5`
    : "—";
  const nextName = nextChord
    ? `${pitchClassName(nextChord.rootMidi, notation)}5`
    : "—";

  return (
    <div>
      {/* Exercise picker */}
      <div className="inline-flex flex-wrap rounded-lg border border-line p-1">
        {POWER_CHORD_EXERCISES.map((exercise) => (
          <button
            key={exercise.id}
            type="button"
            onClick={() => chooseExercise(exercise)}
            className={
              exercise.id === state.exercise.id
                ? SEGMENT_ACTIVE
                : SEGMENT_IDLE
            }
          >
            {exercise.name}
          </button>
        ))}
      </div>
      <p className="mt-2 text-sm text-dim">
        {state.exercise.description}
      </p>

      {/* Current chord */}
      <div className="mt-6 rounded-xl border border-line bg-surface p-5">
        <div className="flex items-baseline justify-between">
          <div>
            <div className="text-xs text-muted">Hraj teď</div>
            <div className="text-4xl font-bold tracking-tight text-accent">
              {activeName}
            </div>
            {activeChord && (
              <div className="mt-1 text-sm text-dim">
                {6 - activeChord.rootStringIndex}. struna,{" "}
                {activeChord.rootFret === 0
                  ? "naprázdno"
                  : `${activeChord.rootFret}. pražec`}
              </div>
            )}
          </div>
          <div className="text-right">
            <div className="text-xs text-muted">Příští</div>
            <div className="text-xl font-semibold text-text">
              {nextName}
            </div>
          </div>
        </div>

        {activeChord && (
          <div className="mt-4 flex justify-center">
            <PowerChordNeck
              chord={activeChord}
              name={activeName}
              stringNames={stringNames}
            />
          </div>
        )}

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
