"use client";

import { useEffect, useRef, useState } from "react";
import {
  playConfirmationChime,
  pulseHaptic,
} from "@/audio/confirmationSound";
import { ReferenceTone } from "@/audio/referenceTone";
import { nearestString } from "@/audio/tunings";
import type { Tuning } from "@/audio/tunings";
import { CentsBar } from "@/components/tuner/CentsBar";
import type { TunerReading } from "@/components/tuner/useTuner";
import { type Notation, midiToFrequency, midiToLabelIn } from "@/lib/pitch";
import { useSettingsStore } from "@/stores/settingsStore";

// Within this many cents a string counts as in tune.
const IN_TUNE_CENTS = 5;
// How long the string must stay in the in-tune zone before the chime fires.
// Filters out brief zero-crossings while the user is still turning the peg.
const CONFIRM_HOLD_MS = 400;

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
  // Read settings through refs so live toggles don't tear down the
  // pitch-detection loop or the reference-tone player.
  const volumeRef = useRef(
    useSettingsStore.getState().referenceToneVolume,
  );
  const confirmSoundRef = useRef(
    useSettingsStore.getState().tunerConfirmSound,
  );
  const confirmHapticRef = useRef(
    useSettingsStore.getState().tunerConfirmHaptic,
  );
  useEffect(() => {
    return useSettingsStore.subscribe((state) => {
      volumeRef.current = state.referenceToneVolume;
      confirmSoundRef.current = state.tunerConfirmSound;
      confirmHapticRef.current = state.tunerConfirmHaptic;
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

  // Confirmation chime / haptic when a string holds in-tune for ~400 ms.
  // confirmedIndexRef tracks which string has already chimed so the same
  // string can't keep re-triggering while the user sits in the zone — it
  // re-confirms only after leaving the zone (or after the user moves on
  // to a different string and comes back).
  const confirmedIndexRef = useRef<number | null>(null);
  useEffect(() => {
    // Moving to a different string clears the prior confirmation so the
    // new string can chime fresh — and so coming *back* to the original
    // string later also gets fresh feedback.
    if (activeIndex !== confirmedIndexRef.current) {
      confirmedIndexRef.current = null;
    }
    if (activeIndex === null || !activeInTune) {
      // Stopped playing, or drifted out of the zone — allow re-confirm
      // next time it lands in tune.
      if (!activeInTune && confirmedIndexRef.current === activeIndex) {
        confirmedIndexRef.current = null;
      }
      return;
    }
    if (confirmedIndexRef.current === activeIndex) return;

    const timer = setTimeout(() => {
      confirmedIndexRef.current = activeIndex;
      if (confirmSoundRef.current) playConfirmationChime();
      if (confirmHapticRef.current) pulseHaptic();
    }, CONFIRM_HOLD_MS);
    return () => clearTimeout(timer);
  }, [activeIndex, activeInTune]);

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
              ? "border-success text-success"
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
                  // Visible dot stays 28 px so it fits in the string slot, but
                  // a transparent ::before pad expands the hit area to ~48 px
                  // for thumbs on mobile (28 + 2×10 = 48).
                  isPlaying
                    ? "relative mx-auto mt-2 flex h-7 w-7 items-center justify-center rounded-md border border-accent bg-accent-soft text-accent before:absolute before:-inset-2.5 before:content-['']"
                    : "relative mx-auto mt-2 flex h-7 w-7 items-center justify-center rounded-md border border-line text-dim transition-colors hover:border-line-strong hover:text-text before:absolute before:-inset-2.5 before:content-['']"
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
