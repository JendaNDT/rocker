"use client";

import { useEffect, useRef, useState } from "react";
import { ReferenceTone } from "@/audio/referenceTone";
import { nearestString } from "@/audio/tunings";
import type { Tuning } from "@/audio/tunings";
import { CentsBar } from "@/components/tuner/CentsBar";
import type { TunerReading } from "@/components/tuner/useTuner";
import { type Notation, midiToFrequency, midiToLabelIn } from "@/lib/pitch";
import { useSettingsStore } from "@/stores/settingsStore";

// Within this many cents a string counts as in tune.
const IN_TUNE_CENTS = 5;

/**
 * Guided tuner — shows the six strings of a tuning, highlights the one being
 * played, and tracks which strings have already reached the in-tune zone.
 */
export function GuidedTuner({
  tuning,
  reading,
  notation,
}: {
  tuning: Tuning;
  reading: TunerReading | null;
  notation: Notation;
}) {
  const [tuned, setTuned] = useState<Set<number>>(new Set());
  const [playingIndex, setPlayingIndex] = useState<number | null>(null);
  const referenceTone = useRef<ReferenceTone | null>(null);
  // Read the volume through a ref so changing the slider mid-press doesn't
  // require restarting the tone — the next play() call picks up the value.
  const volumeRef = useRef(
    useSettingsStore.getState().referenceToneVolume,
  );
  useEffect(() => {
    return useSettingsStore.subscribe((state) => {
      volumeRef.current = state.referenceToneVolume;
    });
  }, []);

  // Build the reference-tone player once per mount; release it on unmount so
  // the AudioContext doesn't leak when the user leaves the screen.
  useEffect(() => {
    referenceTone.current = new ReferenceTone();
    return () => {
      referenceTone.current?.dispose();
      referenceTone.current = null;
    };
  }, []);

  function startReference(index: number, midi: number): void {
    referenceTone.current?.play(midiToFrequency(midi), volumeRef.current);
    setPlayingIndex(index);
  }

  function stopReference(): void {
    referenceTone.current?.stop();
    setPlayingIndex(null);
  }

  // Reset progress whenever the tuning changes.
  useEffect(() => {
    setTuned(new Set());
  }, [tuning.id]);

  const nearest = reading ? nearestString(tuning, reading.frequency) : null;
  const activeIndex = nearest?.index ?? null;
  const activeCents = nearest?.cents ?? 0;
  const activeInTune =
    nearest !== null && Math.abs(activeCents) <= IN_TUNE_CENTS;

  // Mark a string tuned once it reaches the in-tune zone. Depends only on the
  // primitive index/cents so it runs when those change, not every render.
  useEffect(() => {
    if (activeIndex !== null && Math.abs(activeCents) <= IN_TUNE_CENTS) {
      setTuned((prev) => {
        if (prev.has(activeIndex)) return prev;
        const next = new Set(prev);
        next.add(activeIndex);
        return next;
      });
    }
  }, [activeIndex, activeCents]);

  return (
    <div className="rounded-lg border border-line bg-surface p-6">
      <div className="flex gap-1.5">
        {tuning.strings.map((string, index) => {
          const isActive = index === activeIndex;
          const isTuned = tuned.has(index);
          const isPlaying = playingIndex === index;
          const variant = isActive
            ? "border-accent text-accent"
            : isTuned
              ? "border-success/60 text-success"
              : "border-line text-dim";
          const noteLabel = midiToLabelIn(string.midi, notation);
          return (
            <div
              key={string.note}
              className={`flex-1 rounded-md border px-1 py-2 text-center ${variant}`}
            >
              <div className="text-sm font-semibold">{noteLabel}</div>
              <div className="mt-0.5 h-4 text-xs">{isTuned ? "✓" : ""}</div>
              <button
                type="button"
                aria-label={`Přehrát ${noteLabel}`}
                aria-pressed={isPlaying}
                onPointerDown={(event) => {
                  event.preventDefault();
                  startReference(index, string.midi);
                }}
                onPointerUp={stopReference}
                onPointerLeave={() => {
                  if (isPlaying) stopReference();
                }}
                onPointerCancel={stopReference}
                onBlur={() => {
                  if (isPlaying) stopReference();
                }}
                className={
                  isPlaying
                    ? "mx-auto mt-2 flex h-7 w-7 items-center justify-center rounded-md border border-accent bg-accent-soft text-accent"
                    : "mx-auto mt-2 flex h-7 w-7 items-center justify-center rounded-md border border-line text-dim transition-colors hover:border-line-strong hover:text-text"
                }
                title="Přidrž pro přehrání referenčního tónu"
              >
                <span aria-hidden="true">♪</span>
              </button>
            </div>
          );
        })}
      </div>

      <div className="mt-6">
        {nearest ? (
          <>
            <p className="text-center text-sm text-dim">
              Struna{" "}
              <span className="font-semibold text-text">
                {midiToLabelIn(nearest.string.midi, notation)}
              </span>
            </p>
            <div className="mt-3">
              <CentsBar cents={activeCents} inTune={activeInTune} />
            </div>
            <p
              className={
                activeInTune
                  ? "mt-4 text-center font-semibold text-success"
                  : "mt-4 text-center font-semibold text-accent"
              }
            >
              {activeInTune
                ? "Naladěno"
                : `${activeCents > 0 ? "+" : ""}${activeCents} centů`}
            </p>
          </>
        ) : (
          <p className="py-6 text-center text-muted">
            Zahraj jednu strunu…
          </p>
        )}
      </div>
    </div>
  );
}
