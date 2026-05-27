"use client";

import { useEffect, useRef, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { metronome } from "@/audio/metronome";
import { BeatIndicator } from "@/components/metronome/BeatIndicator";
import { Pendulum } from "@/components/metronome/Pendulum";
import { PolyrhythmPanel } from "@/components/metronome/PolyrhythmPanel";
import { PresetPanel } from "@/components/metronome/PresetPanel";
import { TrainerPanel } from "@/components/metronome/TrainerPanel";
import { PageContainer } from "@/components/PageContainer";
import { SplitColumns } from "@/components/SplitColumns";
import { useWakeLock } from "@/components/useWakeLock";
import { KnobControl } from "@/components/widgets/KnobControl";
import type {
  ClickSoundId,
  GapConfig,
  MetronomePreset,
  PolyrhythmConfig,
  RampConfig,
} from "@/lib/metronomeConfig";
import { CLICK_SOUNDS, TIME_SIGNATURES } from "@/lib/metronomeConfig";
import { FACTORY_PRESETS, isFactoryPreset } from "@/lib/metronomePresets";
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
  const swing = useMetronomeStore((state) => state.swing);
  const countInBars = useMetronomeStore((state) => state.countInBars);
  const isCountingIn = useMetronomeStore((state) => state.isCountingIn);
  const currentBar = useMetronomeStore((state) => state.currentBar);
  const polyrhythm = useMetronomeStore((state) => state.polyrhythm);
  const soundPattern = useMetronomeStore((state) => state.soundPattern);
  const subdivisionSound = useMetronomeStore((state) => state.subdivisionSound);

  const persistConfig = useMetronomeSettingsStore((state) => state.setConfig);
  const presets = useMetronomeSettingsStore((state) => state.presets);
  const savePreset = useMetronomeSettingsStore((state) => state.savePreset);
  const deletePreset = useMetronomeSettingsStore((state) => state.deletePreset);
  const visualMode = useMetronomeSettingsStore((state) => state.visualMode);
  const setVisualMode = useMetronomeSettingsStore(
    (state) => state.setVisualMode,
  );
  const hydrated = useHydrated();

  const [haptics, setHaptics] = useState(false);
  // "Pokročilé" is a session-only toggle — when on, the user can pick a
  // different sound for every beat (drum mode) and a separate sound for
  // subdivision ticks (hi-hat over kick/snare). Per Jenda's choice 3A, the
  // underlying data is kept when the toggle is flipped off, so reopening
  // brings the same drum setup back.
  const [advancedOpen, setAdvancedOpen] = useState(false);
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
    metronome.setSwing(config.swing);
    metronome.setCountInBars(config.countInBars);
    metronome.setPolyrhythm(config.polyrhythm);
    metronome.setSoundPattern(config.soundPattern);
    metronome.setSubdivisionSound(config.subdivisionSound);
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

  function changeBeatSound(beat: number, value: ClickSoundId | null) {
    metronome.setBeatSound(beat, value);
    syncConfig();
  }

  function changeSubdivisionSound(value: ClickSoundId | undefined) {
    metronome.setSubdivisionSound(value);
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

  function changeSwing(value: number) {
    metronome.setSwing(value);
    syncConfig();
  }

  function changeCountInBars(value: number) {
    metronome.setCountInBars(value);
    syncConfig();
  }

  function changePolyrhythm(patch: Partial<PolyrhythmConfig>) {
    metronome.setPolyrhythm(patch);
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
    metronome.setSwing(preset.swing);
    metronome.setCountInBars(preset.countInBars);
    metronome.setPolyrhythm(preset.polyrhythm);
    metronome.setSoundPattern(preset.soundPattern);
    metronome.setSubdivisionSound(preset.subdivisionSound);
    syncConfig();
  }

  function handleSavePreset(name: string) {
    // Make sure the store holds the latest engine state before snapshotting.
    syncConfig();
    savePreset(name);
  }

  function handleDeletePreset(id: string) {
    // Defensive: the UI never wires a delete button for factory presets,
    // but if some old saved data sneaks a factory- id into `presets`, drop
    // the request rather than persisting a "user-deleted factory" entry.
    if (isFactoryPreset(id)) return;
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
        <div className="flex items-center justify-between gap-3">
          <div className="text-sm font-medium text-text">Přízvuky</div>
          <div className="flex items-center gap-2">
            <div className="inline-flex rounded-md border border-line p-0.5">
              <button
                type="button"
                onClick={() => setVisualMode("dots")}
                aria-pressed={visualMode === "dots"}
                className={
                  visualMode === "dots"
                    ? "rounded bg-surface-2 px-2 py-0.5 text-xs text-accent"
                    : "rounded px-2 py-0.5 text-xs text-dim transition-colors hover:text-text"
                }
              >
                Body
              </button>
              <button
                type="button"
                onClick={() => setVisualMode("pendulum")}
                aria-pressed={visualMode === "pendulum"}
                className={
                  visualMode === "pendulum"
                    ? "rounded bg-surface-2 px-2 py-0.5 text-xs text-accent"
                    : "rounded px-2 py-0.5 text-xs text-dim transition-colors hover:text-text"
                }
              >
                Kyvadlo
              </button>
            </div>
            <button
              type="button"
              onClick={() => setAdvancedOpen((open) => !open)}
              aria-expanded={advancedOpen}
              className="rounded-md border border-line bg-surface px-2 py-0.5 text-xs text-dim transition-colors hover:border-line-strong hover:text-text"
            >
              {advancedOpen ? "Skrýt pokročilé" : "Pokročilé"}
            </button>
          </div>
        </div>
        <div className="mt-3">
          {visualMode === "pendulum" ? (
            <Pendulum />
          ) : (
            <BeatIndicator
              accentPattern={accentPattern}
              currentBeat={currentBeat}
              onCycle={cycleAccent}
              soundPattern={soundPattern}
              globalSound={sound}
              showSoundDropdowns={advancedOpen}
              onBeatSoundChange={changeBeatSound}
            />
          )}
        </div>
        {advancedOpen && subdivision >= 2 ? (
          <div className="mt-4 rounded-lg border border-line bg-surface p-3">
            <label className="flex items-center justify-between gap-3 text-sm text-text">
              <span>
                Zvuk subdivizí{" "}
                <span className="text-xs text-muted">(přebíjí každý tik mezi dobami)</span>
              </span>
              <select
                aria-label="Zvuk subdivizí"
                value={subdivisionSound ?? ""}
                onChange={(event) => {
                  const value = event.target.value;
                  changeSubdivisionSound(
                    value === "" ? undefined : (value as ClickSoundId),
                  );
                }}
                className="rounded border border-line bg-surface px-2 py-1 text-xs text-text"
              >
                <option value="">Stejný jako doba</option>
                {CLICK_SOUNDS.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <p className="mt-2 text-xs text-muted">
              Tip: Hi-hat pro subdivize + Cvak/Dřevo na doby = jednoduchý
              bicí groove.
            </p>
          </div>
        ) : null}
      </div>

      <div className="mt-8">
        <div className="text-center text-xs font-medium text-dim">Předtakt</div>
        <div className="mt-2 flex justify-center">
          <div className="inline-flex rounded-lg border border-line p-1">
            {[
              { value: 0, label: "Bez" },
              { value: 1, label: "1 takt" },
              { value: 2, label: "2 takty" },
            ].map((option) => (
              <button
                key={option.value}
                type="button"
                onClick={() => changeCountInBars(option.value)}
                className={
                  countInBars === option.value ? SEGMENT_ACTIVE : SEGMENT_IDLE
                }
              >
                {option.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="mt-4 flex flex-col items-center gap-2">
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
        {isPlaying && isCountingIn && countInBars > 0 ? (
          <div className="text-xs text-accent" aria-live="polite">
            Předtakt: {countInBars + currentBar + 1}/{countInBars}{" "}
            {countInBars === 1 ? "takt" : "takty"}
          </div>
        ) : null}
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

      <div className="mt-6 flex flex-wrap items-end gap-6">
        <KnobControl
          label="Hlasitost metronomu"
          value={Math.round(volume * 100)}
          min={0}
          max={100}
          step={1}
          fineStep={0.5}
          formatValue={(v) => `${Math.round(v)} %`}
          ariaLabel="Hlasitost metronomu, rozsah 0 až 100"
          onChange={(next) => changeVolume(next / 100)}
        />
        <KnobControl
          label="Hlasitost přízvuku"
          value={Math.round(accentVolume * 100)}
          min={0}
          max={100}
          step={1}
          fineStep={0.5}
          formatValue={(v) => `${Math.round(v)} %`}
          ariaLabel="Hlasitost přízvuku, rozsah 0 až 100"
          onChange={(next) => changeAccentVolume(next / 100)}
        />
      </div>

      <div className="mt-8">
        <div className="text-sm font-medium text-text">Trenažér tempa</div>
        <div className="mt-3">
          <TrainerPanel
            ramp={ramp}
            gap={gap}
            swing={swing}
            subdivision={subdivision}
            onChangeRamp={changeRamp}
            onChangeGap={changeGap}
            onChangeSwing={changeSwing}
          />
        </div>
      </div>

      <div className="mt-6">
        <PresetPanel
          factoryPresets={FACTORY_PRESETS}
          presets={presets}
          hydrated={hydrated}
          onSave={handleSavePreset}
          onApply={applyPreset}
          onDelete={handleDeletePreset}
        />
      </div>

      <div className="mt-6">
        <PolyrhythmPanel
          polyrhythm={polyrhythm}
          subdivision={subdivision}
          onChange={changePolyrhythm}
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
