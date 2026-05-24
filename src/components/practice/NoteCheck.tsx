"use client";

import { useEffect, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { useTuner } from "@/components/tuner/useTuner";
import type { NoteStep } from "@/lib/lessons";
import { useAudioStore } from "@/stores/audioStore";

/**
 * "Try it" check for a note step. Listens via pitch detection and confirms
 * once the learner plays the target note. Matching is by pitch class, so an
 * octave-off detection (common on low strings) still counts.
 */
export function NoteCheck({ step }: { step: NoteStep }) {
  const status = useAudioStore((state) => state.status);
  const error = useAudioStore((state) => state.error);
  const { reading } = useTuner();
  const [passed, setPassed] = useState(false);

  const running = status === "running";
  const matched =
    reading !== null && reading.note.midi % 12 === step.targetMidi % 12;

  useEffect(() => {
    if (matched) setPassed(true);
  }, [matched]);

  return (
    <div className="mt-4 rounded-lg border border-neutral-800 bg-neutral-950 p-4 text-center">
      <div className="text-sm text-neutral-500">Cílový tón</div>
      <div className="mt-1 text-2xl font-bold text-amber-500">
        {step.targetLabel}
      </div>

      {!running && !passed && (
        <button
          type="button"
          onClick={() => void audioEngine.ensureListening()}
          className="mt-3 rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
        >
          Poslouchat
        </button>
      )}

      {running && !passed && (
        <p className="mt-3 text-sm text-neutral-400">
          {reading ? `Slyším ${reading.note.label}…` : "Hraj cílový tón…"}
        </p>
      )}

      {passed && (
        <p className="mt-3 font-semibold text-emerald-400">Správně zahráno ✓</p>
      )}

      {error && <p className="mt-3 text-sm text-red-400">{error}</p>}
    </div>
  );
}
