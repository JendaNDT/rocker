"use client";

import { useEffect, useState } from "react";
import { nearestString } from "@/audio/tunings";
import type { Tuning } from "@/audio/tunings";
import { CentsBar } from "@/components/tuner/CentsBar";
import type { TunerReading } from "@/components/tuner/useTuner";
import { type Notation, midiToLabelIn } from "@/lib/pitch";

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

  // Reset progress whenever the tuning changes.
  useEffect(() => {
    setTuned(new Set());
  }, [tuning.id]);

  const nearest = reading ? nearestString(tuning, reading.frequency) : null;
  const activeIndex = nearest?.index ?? null;
  const activeCents = nearest?.cents ?? 0;
  const activeInTune =
    nearest !== null && Math.abs(activeCents) <= IN_TUNE_CENTS;

  // Mark a string tuned once it reaches the in-tune zone.
  useEffect(() => {
    if (nearest && Math.abs(nearest.cents) <= IN_TUNE_CENTS) {
      const index = nearest.index;
      setTuned((prev) => {
        if (prev.has(index)) return prev;
        const next = new Set(prev);
        next.add(index);
        return next;
      });
    }
  }, [nearest?.index, nearest?.cents, nearest]);

  return (
    <div className="rounded-lg border border-neutral-800 bg-neutral-900 p-6">
      <div className="flex gap-1.5">
        {tuning.strings.map((string, index) => {
          const isActive = index === activeIndex;
          const isTuned = tuned.has(index);
          const variant = isActive
            ? "border-amber-500 text-amber-500"
            : isTuned
              ? "border-emerald-500/60 text-emerald-400"
              : "border-neutral-800 text-neutral-400";
          return (
            <div
              key={string.note}
              className={`flex-1 rounded-md border px-1 py-2 text-center ${variant}`}
            >
              <div className="text-sm font-semibold">
                {midiToLabelIn(string.midi, notation)}
              </div>
              <div className="mt-0.5 h-4 text-xs">{isTuned ? "✓" : ""}</div>
            </div>
          );
        })}
      </div>

      <div className="mt-6">
        {nearest ? (
          <>
            <p className="text-center text-sm text-neutral-400">
              Struna{" "}
              <span className="font-semibold text-neutral-100">
                {midiToLabelIn(nearest.string.midi, notation)}
              </span>
            </p>
            <div className="mt-3">
              <CentsBar cents={activeCents} inTune={activeInTune} />
            </div>
            <p
              className={
                activeInTune
                  ? "mt-4 text-center font-semibold text-emerald-400"
                  : "mt-4 text-center font-semibold text-amber-500"
              }
            >
              {activeInTune
                ? "Naladěno"
                : `${activeCents > 0 ? "+" : ""}${activeCents} centů`}
            </p>
          </>
        ) : (
          <p className="py-6 text-center text-neutral-500">
            Zahraj jednu strunu…
          </p>
        )}
      </div>
    </div>
  );
}
