"use client";

import { audioEngine } from "@/audio/AudioEngine";
import { playChord } from "@/audio/chordPlayer";
import { ChordDiagram } from "@/components/library/ChordDiagram";
import { InputLevelMeter } from "@/components/InputLevelMeter";
import { NoteCheck } from "@/components/practice/NoteCheck";
import { chordMidiNotes, getChord } from "@/lib/chords";
import type { LessonStep } from "@/lib/lessons";
import { useHydrated } from "@/hooks/useHydrated";
import { useAudioStore } from "@/stores/audioStore";
import { useSettingsStore } from "@/stores/settingsStore";

/** Renders a single lesson step (text, chord or note). */
export function StepView({ step }: { step: LessonStep }) {
  const handedness = useSettingsStore((state) => state.handedness);
  const running = useAudioStore((state) => state.status === "running");
  const hydrated = useHydrated();
  const mirror = hydrated && handedness === "left";

  return (
    <div className="rounded-lg border border-line bg-surface p-5">
      <h2 className="text-lg font-semibold">{step.title}</h2>
      <p className="mt-2 leading-relaxed text-text">{step.body}</p>

      {step.kind === "chord" &&
        (() => {
          const chord = getChord(step.chordId);
          if (!chord) return null;
          return (
            <div className="mt-4">
              <div className="mx-auto max-w-[150px]">
                <ChordDiagram chord={chord} mirror={mirror} />
              </div>
              <div className="mt-3 flex flex-wrap justify-center gap-2">
                <button
                  type="button"
                  onClick={() => void playChord(chordMidiNotes(chord))}
                  className="rounded-lg border border-line bg-bg px-4 py-2 text-sm transition-colors hover:border-line-strong"
                >
                  Přehrát akord
                </button>
                {!running && (
                  <button
                    type="button"
                    onClick={() => void audioEngine.ensureListening()}
                    className="rounded-lg border border-line bg-bg px-4 py-2 text-sm transition-colors hover:border-line-strong"
                  >
                    Hrát se mnou
                  </button>
                )}
              </div>
              {running && (
                <div className="mt-4">
                  <InputLevelMeter />
                </div>
              )}
            </div>
          );
        })()}

      {step.kind === "note" && <NoteCheck step={step} />}
    </div>
  );
}
