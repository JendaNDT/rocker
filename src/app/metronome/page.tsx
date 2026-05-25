"use client";

import { useEffect, useRef, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { metronome } from "@/audio/metronome";
import { BeatIndicator } from "@/components/metronome/BeatIndicator";
import { useWakeLock } from "@/components/useWakeLock";
import { bpmFromTaps, clampBpm, MAX_BPM, MIN_BPM } from "@/lib/tempo";
import { useMetronomeStore } from "@/stores/metronomeStore";

const BAR_OPTIONS = [
  { label: "4/4", beats: 4 },
  { label: "3/4", beats: 3 },
  { label: "6/8", beats: 6 },
];

const SUBDIVISION_OPTIONS = [
  { label: "Čtvrťové", value: 1 },
  { label: "Osminy", value: 2 },
  { label: "Trioly", value: 3 },
  { label: "Šestnáctiny", value: 4 },
];

// A gap longer than this starts a fresh tap-tempo measurement.
const TAP_RESET_MS = 2000;
const MAX_TAPS = 6;

const SEGMENT_ACTIVE =
  "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

export default function MetronomePage() {
  useWakeLock();

  const isPlaying = useMetronomeStore((state) => state.isPlaying);
  const bpm = useMetronomeStore((state) => state.bpm);
  const beatsPerBar = useMetronomeStore((state) => state.beatsPerBar);
  const subdivision = useMetronomeStore((state) => state.subdivision);
  const accentFirst = useMetronomeStore((state) => state.accentFirst);
  const currentBeat = useMetronomeStore((state) => state.currentBeat);

  const [haptics, setHaptics] = useState(false);
  const tapsRef = useRef<number[]>([]);

  // Vibrate on each beat when haptics are enabled (Android only — desktop
  // and iOS simply ignore navigator.vibrate).
  useEffect(() => {
    if (!haptics || !isPlaying || currentBeat < 0) return;
    if (typeof navigator !== "undefined" && navigator.vibrate) {
      navigator.vibrate(currentBeat === 0 ? 35 : 18);
    }
  }, [currentBeat, haptics, isPlaying]);

  function togglePlay() {
    void (async () => {
      const context = await audioEngine.getOutputContext();
      metronome.toggle(context);
    })();
  }

  function handleTap() {
    const now = performance.now();
    const taps = tapsRef.current;
    if (taps.length > 0 && now - taps[taps.length - 1] > TAP_RESET_MS) {
      taps.length = 0;
    }
    taps.push(now);
    if (taps.length > MAX_TAPS) taps.shift();

    const tapped = bpmFromTaps(taps);
    if (tapped !== null) {
      metronome.setBpm(clampBpm(tapped));
    }
  }

  return (
    <div className="mx-auto max-w-xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">Metronom</h1>
      <p className="mt-1 text-dim">Tempo, takt a doby.</p>

      <div className="mt-8 text-center">
        <div className="text-6xl font-bold tabular-nums">{bpm}</div>
        <div className="text-sm text-muted">BPM</div>
      </div>

      <div className="mt-5 flex items-center gap-3">
        <button
          type="button"
          aria-label="Snížit tempo"
          onClick={() => metronome.setBpm(bpm - 1)}
          className="h-10 w-10 rounded-lg border border-line bg-surface text-lg transition-colors hover:border-line-strong"
        >
          −
        </button>
        <input
          type="range"
          min={MIN_BPM}
          max={MAX_BPM}
          value={bpm}
          onChange={(event) => metronome.setBpm(Number(event.target.value))}
          aria-label="Tempo"
          className="flex-1 accent-accent"
        />
        <button
          type="button"
          aria-label="Zvýšit tempo"
          onClick={() => metronome.setBpm(bpm + 1)}
          className="h-10 w-10 rounded-lg border border-line bg-surface text-lg transition-colors hover:border-line-strong"
        >
          +
        </button>
      </div>

      <div className="mt-4 flex justify-center">
        <button
          type="button"
          onClick={handleTap}
          className="rounded-lg border border-line bg-surface px-6 py-2 text-sm transition-colors hover:border-line-strong"
        >
          Naťukat tempo
        </button>
      </div>

      <div className="mt-8">
        <div className="text-sm font-medium text-text">Takt</div>
        <div className="mt-2 inline-flex rounded-lg border border-line p-1">
          {BAR_OPTIONS.map((option) => (
            <button
              key={option.label}
              type="button"
              onClick={() => metronome.setBeatsPerBar(option.beats)}
              className={
                beatsPerBar === option.beats ? SEGMENT_ACTIVE : SEGMENT_IDLE
              }
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      <div className="mt-6">
        <div className="text-sm font-medium text-text">Doby</div>
        <div className="mt-2 inline-flex flex-wrap rounded-lg border border-line p-1">
          {SUBDIVISION_OPTIONS.map((option) => (
            <button
              key={option.value}
              type="button"
              onClick={() => metronome.setSubdivision(option.value)}
              className={
                subdivision === option.value ? SEGMENT_ACTIVE : SEGMENT_IDLE
              }
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      <div className="mt-6 space-y-2">
        <label className="flex items-center gap-2 text-sm text-text">
          <input
            type="checkbox"
            checked={accentFirst}
            onChange={(event) => metronome.setAccentFirst(event.target.checked)}
            className="h-4 w-4 accent-accent"
          />
          Akcent na první dobu
        </label>
        <label className="flex items-center gap-2 text-sm text-text">
          <input
            type="checkbox"
            checked={haptics}
            onChange={(event) => setHaptics(event.target.checked)}
            className="h-4 w-4 accent-accent"
          />
          Vibrace na dobu (mobil)
        </label>
      </div>

      <div className="mt-10">
        <BeatIndicator beatsPerBar={beatsPerBar} currentBeat={currentBeat} />
      </div>

      <div className="mt-8 flex justify-center">
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
