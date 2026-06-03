"use client";

import { useEffect, useRef, useState } from "react";
import { audioEngine } from "@/audio/audioEngine";
import { metronome } from "@/audio/metronome";
import { BeatIndicator } from "@/components/metronome/BeatIndicator";
import { bpmFromTaps, clampBpm } from "@/lib/tempo";
import { useMetronomeStore } from "@/stores/metronomeStore";
import { useMetronomeSettingsStore } from "@/stores/metronomeSettingsStore";

// A gap longer than this starts a fresh tap-tempo measurement.
const TAP_RESET_MS = 2000;
const MAX_TAPS = 6;

/**
 * Compact, collapsible metronome — a contextual quick-access on the practice
 * screen (screen-consolidation plan, fáze B). It shares the metronome engine
 * and the persisted settings with the full Metronom screen: it is the same
 * metronome, just reachable from Cvičení. The full screen stays for deeper
 * configuration (time signatures, sounds, presets, tempo trainer).
 */
export function MetronomeWidget() {
  const isPlaying = useMetronomeStore((state) => state.isPlaying);
  const bpm = useMetronomeStore((state) => state.bpm);
  const accentPattern = useMetronomeStore((state) => state.accentPattern);
  const currentBeat = useMetronomeStore((state) => state.currentBeat);

  const persistConfig = useMetronomeSettingsStore((state) => state.setConfig);

  const [expanded, setExpanded] = useState(false);
  const tapsRef = useRef<number[]>([]);

  // Load the saved configuration into the shared engine on mount. The engine
  // is a singleton the Power Chord Trainer also drives, leaving its own tempo
  // behind — re-applying here restores the user's metronome settings. The
  // metronome is intentionally not stopped on unmount: like the full Metronom
  // screen, the click keeps running across navigation (it is one metronome).
  useEffect(() => {
    const config = useMetronomeSettingsStore.getState().config;
    metronome.setBpm(config.bpm);
    metronome.setBeatsPerBar(config.beatsPerBar);
    metronome.setSubdivision(config.subdivision);
    metronome.setAccentPattern(config.accentPattern);
    metronome.setSound(config.sound);
    metronome.setVolume(config.volume);
    metronome.setAccentVolume(config.accentVolume);
    metronome.setRamp(config.ramp);
    metronome.setGap(config.gap);
    metronome.setSwing(config.swing);
    metronome.setCountInBars(config.countInBars);
    metronome.setPolyrhythm(config.polyrhythm);
    metronome.setSoundPattern(config.soundPattern);
    metronome.setSubdivisionSound(config.subdivisionSound);
  }, []);

  // Mirrors the engine's configuration into the persistent store after a
  // user-initiated change, so it survives a reload and stays in sync with
  // the full Metronom screen.
  function syncConfig() {
    const state = metronome.getState();
    persistConfig({
      bpm: state.bpm,
      beatsPerBar: state.beatsPerBar,
      subdivision: state.subdivision,
      accentPattern: state.accentPattern,
      sound: state.sound,
      volume: state.volume,
      accentVolume: state.accentVolume,
      ramp: state.ramp,
      gap: state.gap,
      swing: state.swing,
      countInBars: state.countInBars,
      polyrhythm: state.polyrhythm,
      soundPattern: state.soundPattern,
      subdivisionSound: state.subdivisionSound,
    });
  }

  function togglePlay() {
    void (async () => {
      const context = await audioEngine.getOutputContext();
      metronome.toggle(context);
    })();
  }

  function changeBpm(value: number) {
    metronome.setBpm(clampBpm(value));
    syncConfig();
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
    if (tapped !== null) changeBpm(tapped);
  }

  function cycleAccent(beat: number) {
    metronome.cycleAccent(beat);
    syncConfig();
  }

  return (
    <div className="rounded-lg border border-line bg-surface">
      <button
        type="button"
        onClick={() => setExpanded((value) => !value)}
        aria-expanded={expanded}
        className="flex w-full items-center justify-between px-4 py-2.5 text-sm font-medium text-text"
      >
        <span>
          Metronom
          {isPlaying && (
            <span className="text-dim"> — {bpm} BPM, běží</span>
          )}
        </span>
        <span className="text-dim" aria-hidden="true">
          {expanded ? "▲" : "▼"}
        </span>
      </button>

      {expanded && (
        <div className="border-t border-line px-4 py-4">
          <div className="flex items-center justify-center gap-3">
            <button
              type="button"
              aria-label="Snížit tempo"
              onClick={() => changeBpm(bpm - 5)}
              className="h-9 w-9 rounded-lg border border-line bg-bg text-lg transition-colors hover:border-line-strong"
            >
              −
            </button>
            <div className="text-center">
              <div className="text-3xl font-bold tabular-nums">{bpm}</div>
              <div className="text-xs text-muted">BPM</div>
            </div>
            <button
              type="button"
              aria-label="Zvýšit tempo"
              onClick={() => changeBpm(bpm + 5)}
              className="h-9 w-9 rounded-lg border border-line bg-bg text-lg transition-colors hover:border-line-strong"
            >
              +
            </button>
          </div>

          <div className="mt-3 flex justify-center">
            <button
              type="button"
              onClick={handleTap}
              className="rounded-lg border border-line bg-bg px-4 py-1.5 text-sm transition-colors hover:border-line-strong"
            >
              Naťukat tempo
            </button>
          </div>

          <div className="mt-4">
            <BeatIndicator
              accentPattern={accentPattern}
              currentBeat={currentBeat}
              onCycle={cycleAccent}
            />
          </div>

          <div className="mt-4 flex justify-center">
            <button
              type="button"
              onClick={togglePlay}
              className={
                isPlaying
                  ? "rounded-lg border border-line bg-bg px-8 py-2 text-sm font-semibold transition-colors hover:border-line-strong"
                  : "rounded-lg bg-accent px-8 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
              }
            >
              {isPlaying ? "Zastavit" : "Spustit"}
            </button>
          </div>

          <p className="mt-3 text-center text-xs text-muted">
            Plný metronom (takty, zvuky, presety) najdeš na obrazovce
            Metronom.
          </p>
        </div>
      )}
    </div>
  );
}
