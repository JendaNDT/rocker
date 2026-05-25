"use client";

import { useEffect, useRef, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { metronome } from "@/audio/metronome";
import { BeatIndicator } from "@/components/metronome/BeatIndicator";
import { PresetPanel } from "@/components/metronome/PresetPanel";
import { TrainerPanel } from "@/components/metronome/TrainerPanel";
import { PageContainer } from "@/components/PageContainer";
import { SplitColumns } from "@/components/SplitColumns";
import { useWakeLock } from "@/components/useWakeLock";
import type {
  GapConfig,
  MetronomePreset,
  RampConfig,
} from "@/lib/metronomeConfig";
import { CLICK_SOUNDS, TIME_SIGNATURES } from "@/lib/metronomeConfig";
import { bpmFromTaps, clampBpm, MAX_BPM, MIN_BPM } from "@/lib/tempo";
import { useHydrated } from "@/lib/useHydrated";
import { useMetronomeStore } from "@/stores/metronomeStore";
import { useMetronomeSettingsStore } from "@/stores/metronomeSettingsStore";

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
  const accentPattern = useMetronomeStore((state) => state.accentPattern);
  const sound = useMetronomeStore((state) => state.sound);
  const volume = useMetronomeStore((state) => state.volume);
  const accentVolume = useMetronomeStore((state) => state.accentVolume);
  const currentBeat = useMetronomeStore((state) => state.currentBeat);
  const ramp = useMetronomeStore((state) => state.ramp);
  const gap = useMetronomeStore((state) => state.gap);

  const persistConfig = useMetronomeSettingsStore((state) => state.setConfig);
  const presets = useMetronomeSettingsStore((state) => state.presets);
  const savePreset = useMetronomeSettingsStore((state) => state.savePreset);
  const deletePreset = useMetronomeSettingsStore((state) => state.deletePreset);
  const hydrated = useHydrated();

  const [haptics, setHaptics] = useState(false);
  const tapsRef = useRef<number[]>([]);

  // Load the saved configuration into the engine once on mount. The engine is
  // a singleton shared with the Power Chord Trainer, which leaves its own
  // tempo behind — re-applying here restores the user's metronome settings.
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
  }, []);

  // Vibrate on each beat when haptics are enabled (Android only — desktop
  // and iOS simply ignore navigator.vibrate).
  useEffect(() => {
    if (!haptics || !isPlaying || currentBeat < 0) return;
    if (typeof navigator !== "undefined" && navigator.vibrate) {
      navigator.vibrate(currentBeat === 0 ? 35 : 18);
    }
  }, [currentBeat, haptics, isPlaying]);

  // Mirrors the engine's current configuration into the persistent store, so
  // it survives a reload. Called after every user-initiated change.
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
    });
  }

  function togglePlay() {
    void (async () => {
      const context = await audioEngine.getOutputContext();
      metronome.toggle(context);
    })();
  }

  function changeBpm(value: number) {
    metronome.setBpm(value);
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
    if (tapped !== null) changeBpm(clampBpm(tapped));
  }

  function changeTimeSignature(beats: number, accents: typeof accentPattern) {
    metronome.setBeatsPerBar(beats);
    metronome.setAccentPattern(accents);
    syncConfig();
  }

  function changeSubdivision(value: number) {
    metronome.setSubdivision(value);
    syncConfig();
  }

  function cycleAccent(beat: number) {
    metronome.cycleAccent(beat);
    syncConfig();
  }

  function changeSound(value: (typeof CLICK_SOUNDS)[number]["id"]) {
    metronome.setSound(value);
    syncConfig();
  }

  function changeVolume(value: number) {
    metronome.setVolume(value);
    syncConfig();
  }

  function changeAccentVolume(value: number) {
    metronome.setAccentVolume(value);
    syncConfig();
  }

  function changeRamp(patch: Partial<RampConfig>) {
    metronome.setRamp(patch);
    syncConfig();
  }

  function changeGap(patch: Partial<GapConfig>) {
    metronome.setGap(patch);
    syncConfig();
  }

  function applyPreset(preset: MetronomePreset) {
    metronome.setBpm(preset.bpm);
    metronome.setBeatsPerBar(preset.beatsPerBar);
    metronome.setSubdivision(preset.subdivision);
    metronome.setAccentPattern(preset.accentPattern);
    metronome.setSound(preset.sound);
    metronome.setVolume(preset.volume);
    metronome.setAccentVolume(preset.accentVolume);
    metronome.setRamp(preset.ramp);
    metronome.setGap(preset.gap);
    syncConfig();
  }

  function handleSavePreset(name: string) {
    // Make sure the store holds the latest engine state before snapshotting.
    syncConfig();
    savePreset(name);
  }

  function handleDeletePreset(id: string) {
    const preset = presets.find((item) => item.id === id);
    const label = preset ? `„${preset.name}"` : "tento preset";
    if (
      typeof window !== "undefined" &&
      !window.confirm(`Smazat preset ${label}?`)
    ) {
      return;
    }
    deletePreset(id);
  }

  // Left column — the "playing" surface watched while practising.
  const playingColumn = (
    <>
      <div className="mt-8 text-center">
        <div className="text-6xl font-bold tabular-nums">{bpm}</div>
        <div className="text-sm text-muted">BPM</div>
      </div>

      <div className="mt-5 flex items-center gap-3">
        <button
          type="button"
          aria-label="Snížit tempo"
          onClick={() => changeBpm(bpm - 1)}
          className="h-10 w-10 rounded-lg border border-line bg-surface text-lg transition-colors hover:border-line-strong"
        >
          −
        </button>
        <input
          type="range"
          min={MIN_BPM}
          max={MAX_BPM}
          value={bpm}
          onChange={(event) => changeBpm(Number(event.target.value))}
          aria-label="Tempo"
          className="flex-1 accent-accent"
        />
        <button
          type="button"
          aria-label="Zvýšit tempo"
          onClick={() => changeBpm(bpm + 1)}
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
        <div className="text-sm font-medium text-text">Přízvuky</div>
        <div className="mt-3">
          <BeatIndicator
            accentPattern={accentPattern}
            currentBeat={currentBeat}
            onCycle={cycleAccent}
          />
        </div>
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
    </>
  );

  // Right column — settings.
  const settingsColumn = (
    <>
      <div className="mt-8">
        <div className="text-sm font-medium text-text">Takt</div>
        <div className="mt-2 inline-flex flex-wrap rounded-lg border border-line p-1">
          {TIME_SIGNATURES.map((signature) => (
            <button
              key={signature.id}
              type="button"
              onClick={() =>
                changeTimeSignature(
                  signature.beatsPerBar,
                  signature.defaultAccents,
                )
              }
              className={
                beatsPerBar === signature.beatsPerBar
                  ? SEGMENT_ACTIVE
                  : SEGMENT_IDLE
              }
            >
              {signature.label}
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
              onClick={() => changeSubdivision(option.value)}
              className={
                subdivision === option.value ? SEGMENT_ACTIVE : SEGMENT_IDLE
              }
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      <div className="mt-6">
        <div className="text-sm font-medium text-text">Zvuk</div>
        <div className="mt-2 inline-flex flex-wrap rounded-lg border border-line p-1">
          {CLICK_SOUNDS.map((option) => (
            <button
              key={option.id}
              type="button"
              onClick={() => changeSound(option.id)}
              className={sound === option.id ? SEGMENT_ACTIVE : SEGMENT_IDLE}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      <div className="mt-6 space-y-4">
        <div>
          <div className="flex justify-between text-sm font-medium text-text">
            <span>Hlasitost metronomu</span>
            <span className="text-dim tabular-nums">
              {Math.round(volume * 100)} %
            </span>
          </div>
          <input
            type="range"
            min={0}
            max={1}
            step={0.01}
            value={volume}
            onChange={(event) => changeVolume(Number(event.target.value))}
            aria-label="Hlasitost metronomu"
            className="mt-1 w-full accent-accent"
          />
        </div>
        <div>
          <div className="flex justify-between text-sm font-medium text-text">
            <span>Hlasitost přízvuku</span>
            <span className="text-dim tabular-nums">
              {Math.round(accentVolume * 100)} %
            </span>
          </div>
          <input
            type="range"
            min={0}
            max={1}
            step={0.01}
            value={accentVolume}
            onChange={(event) => changeAccentVolume(Number(event.target.value))}
            aria-label="Hlasitost přízvuku"
            className="mt-1 w-full accent-accent"
          />
        </div>
      </div>

      <div className="mt-8">
        <div className="text-sm font-medium text-text">Trenažér tempa</div>
        <div className="mt-3">
          <TrainerPanel
            ramp={ramp}
            gap={gap}
            onChangeRamp={changeRamp}
            onChangeGap={changeGap}
          />
        </div>
      </div>

      <div className="mt-6">
        <PresetPanel
          presets={presets}
          hydrated={hydrated}
          onSave={handleSavePreset}
          onApply={applyPreset}
          onDelete={handleDeletePreset}
        />
      </div>

      <div className="mt-6">
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
    </>
  );

  return (
    <PageContainer>
      <h1 className="text-3xl font-bold tracking-tight">Metronom</h1>
      <p className="mt-1 text-dim">Tempo, takt a doby.</p>
      <SplitColumns left={playingColumn} right={settingsColumn} />
    </PageContainer>
  );
}
