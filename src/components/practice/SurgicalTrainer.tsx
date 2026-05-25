"use client";

import { useEffect, useRef, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { useTuner } from "@/components/tuner/useTuner";
import { EXERCISES } from "@/lib/exercises";
import type { Exercise } from "@/lib/exercises";
import { midiToLabel } from "@/lib/pitch";
import {
  drillNotes,
  feedNote,
  REQUIRED_CLEAN_REPS,
  startSurgical,
} from "@/lib/surgicalEngine";
import type { SurgicalState } from "@/lib/surgicalEngine";
import { useAudioStore } from "@/stores/audioStore";

/**
 * Surgical Practice Mode — self-paced single-note practice. Detects each note
 * the learner plays and advances through the exercise. The drill phase (the
 * isolated-segment workout) is built in Krok 8.3.
 */
export function SurgicalTrainer() {
  const [state, setState] = useState<SurgicalState | null>(null);
  const lastFedRef = useRef<number | null>(null);
  const { reading } = useTuner();
  const running = useAudioStore((store) => store.status === "running");

  const detectedMidi = reading?.note.midi ?? null;

  // Feed a note into the engine only when a new distinct note is detected.
  useEffect(() => {
    if (detectedMidi === null) return;
    const pitchClass = ((detectedMidi % 12) + 12) % 12;
    if (lastFedRef.current === pitchClass) return;
    lastFedRef.current = pitchClass;
    setState((current) =>
      current && current.phase !== "complete"
        ? feedNote(current, detectedMidi)
        : current,
    );
  }, [detectedMidi]);

  // Release the microphone when leaving.
  useEffect(() => {
    return () => {
      void audioEngine.stop();
    };
  }, []);

  function chooseExercise(exercise: Exercise) {
    lastFedRef.current = null;
    setState(startSurgical(exercise));
  }

  function backToPicker() {
    void audioEngine.stop();
    setState(null);
  }

  if (!state) {
    return (
      <div>
        <p className="text-sm text-dim">
          Vyber cvičení. Hraješ tón po tónu, aplikace tě posouvá po správně
          zahraných tónech. Když uděláš chybu, na konci ji společně vypilujeme.
        </p>
        <div className="mt-4 space-y-2">
          {EXERCISES.map((exercise) => (
            <button
              key={exercise.id}
              type="button"
              onClick={() => chooseExercise(exercise)}
              className="block w-full rounded-lg border border-line bg-surface px-4 py-3 text-left transition-colors hover:border-accent"
            >
              <div className="font-medium">{exercise.name}</div>
              <div className="mt-0.5 text-sm text-dim">
                {exercise.description}
              </div>
            </button>
          ))}
        </div>
      </div>
    );
  }

  if (state.phase === "complete") {
    return (
      <div className="rounded-lg border border-line bg-surface p-6 text-center">
        <div className="text-lg font-semibold text-success">
          Cvičení zvládnuto ✓
        </div>
        <p className="mt-1 text-sm text-dim">{state.exercise.name}</p>
        <div className="mt-4 flex justify-center gap-2">
          <button
            type="button"
            onClick={() => chooseExercise(state.exercise)}
            className="rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
          >
            Znovu
          </button>
          <button
            type="button"
            onClick={backToPicker}
            className="rounded-lg border border-line bg-bg px-4 py-2 text-sm transition-colors hover:border-line-strong"
          >
            Zpět na výběr
          </button>
        </div>
      </div>
    );
  }

  if (state.phase === "drill") {
    const drillWindow = drillNotes(state);
    const drillTarget =
      state.exercise.notes[state.drillStart + state.drillPosition];
    return (
      <div>
        <div className="flex items-center justify-between">
          <button
            type="button"
            onClick={backToPicker}
            className="text-sm text-dim transition-colors hover:text-text"
          >
            Zpět na výběr
          </button>
          <span className="text-sm text-muted">
            {state.exercise.name}
          </span>
        </div>

        <div className="mt-4 rounded-lg border border-accent/60 bg-surface p-4">
          <div className="font-semibold text-accent">
            Drill — problémové místo
          </div>
          <p className="mt-1 text-sm text-dim">
            Izolovali jsme úsek, kde se chybovalo. Zahraj ho{" "}
            {REQUIRED_CLEAN_REPS}× po sobě čistě — když mineš, série se počítá
            od začátku.
          </p>
        </div>

        <div className="mt-4 flex justify-center gap-2 rounded-lg border border-line bg-surface p-4">
          {drillWindow.map((midi, index) => {
            const done = index < state.drillPosition;
            const current = index === state.drillPosition;
            const variant = current
              ? "border-accent text-accent"
              : done
                ? "border-success/60 text-success"
                : "border-line text-muted";
            return (
              <div
                key={index}
                className={`rounded-md border px-3 py-2 text-sm font-semibold ${variant}`}
              >
                {midiToLabel(midi)}
              </div>
            );
          })}
        </div>

        <div className="mt-4 flex items-center justify-center gap-2">
          {Array.from({ length: REQUIRED_CLEAN_REPS }, (_, index) => (
            <div
              key={index}
              className={`h-3 w-3 rounded-full ${
                index < state.cleanReps ? "bg-success" : "bg-surface-3"
              }`}
            />
          ))}
          <span className="ml-2 text-sm text-dim">
            Čisté repetice: {state.cleanReps} / {REQUIRED_CLEAN_REPS}
          </span>
        </div>

        <div className="mt-4 rounded-lg border border-line bg-surface p-5 text-center">
          <div className="text-sm text-muted">Zahraj tón</div>
          <div className="mt-1 text-3xl font-bold text-accent">
            {midiToLabel(drillTarget)}
          </div>
          {running ? (
            <p className="mt-3 text-sm text-dim">
              {reading ? `Slyším ${reading.note.label}` : "Poslouchám…"}
            </p>
          ) : (
            <button
              type="button"
              onClick={() => void audioEngine.ensureListening()}
              className="mt-4 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
            >
              Spustit poslech
            </button>
          )}
        </div>
      </div>
    );
  }

  // Run phase.
  const targetMidi = state.exercise.notes[state.position];
  return (
    <div>
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={backToPicker}
          className="text-sm text-dim transition-colors hover:text-text"
        >
          Zpět na výběr
        </button>
        <span className="text-sm text-muted">{state.exercise.name}</span>
      </div>

      <div className="mt-4 flex gap-2 overflow-x-auto rounded-lg border border-line bg-surface p-4">
        {state.exercise.notes.map((midi, index) => {
          const done = index < state.position;
          const current = index === state.position;
          const variant = current
            ? "border-accent text-accent"
            : done
              ? "border-success/60 text-success"
              : "border-line text-muted";
          return (
            <div
              key={index}
              className={`shrink-0 rounded-md border px-3 py-2 text-sm font-semibold ${variant}`}
            >
              {midiToLabel(midi)}
            </div>
          );
        })}
      </div>

      <div className="mt-4 rounded-lg border border-line bg-surface p-5 text-center">
        <div className="text-sm text-muted">Zahraj tón</div>
        <div className="mt-1 text-3xl font-bold text-accent">
          {midiToLabel(targetMidi)}
        </div>
        {running ? (
          <p className="mt-3 text-sm text-dim">
            {reading ? `Slyším ${reading.note.label}` : "Poslouchám…"}
          </p>
        ) : (
          <button
            type="button"
            onClick={() => void audioEngine.ensureListening()}
            className="mt-4 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
          >
            Spustit poslech
          </button>
        )}
        {running && state.errorCounts[state.position] > 0 && (
          <p className="mt-2 text-sm text-accent">
            Minul jsi — zkus ten tón znovu.
          </p>
        )}
      </div>
    </div>
  );
}
