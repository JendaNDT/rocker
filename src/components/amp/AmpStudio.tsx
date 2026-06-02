"use client";

import { useEffect, useRef, useState } from "react";
import { recorder } from "@/audio/recorder";
import { SignalLevelMeter } from "@/components/amp/SignalLevelMeter";
import { SignalScope } from "@/components/amp/SignalScope";
import { useAmpEngine } from "@/components/amp/useAmpEngine";
import { InputLevelMeter } from "@/components/InputLevelMeter";
import { KnobControl } from "@/components/widgets/KnobControl";
import { useResolvedLayoutMode } from "@/lib/layout";
import {
  AMP_PRESETS,
  type AmpPreset,
  type AmpSettings,
  type CabinetType,
  type DistortionType,
} from "@/lib/ampPresets";
import { type Recording, formatDuration } from "@/lib/recordings";
import { useAmpPresetsStore } from "@/stores/ampPresetsStore";
import { useAmpStore } from "@/stores/ampStore";
import { useAudioStore } from "@/stores/audioStore";
import { useRecordingsStore } from "@/stores/recordingsStore";

// Parameter knobs: flex-wrap so they stack neatly on narrow screens and
// fan out across the row on desktop. Each knob is fixed-width (w-20).
const KNOB_GRID = {
  desktop: "flex flex-wrap items-end gap-x-5 gap-y-4 justify-start",
  mobile: "flex flex-wrap items-end gap-x-3 gap-y-3 justify-center",
  auto: "flex flex-wrap items-end gap-x-3 gap-y-3 justify-center lg:gap-x-5 lg:justify-start",
};

const PRIMARY_BUTTON =
  "rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90 disabled:cursor-not-allowed disabled:opacity-50";
const SECONDARY_BUTTON =
  "rounded-lg border border-line bg-bg px-4 py-2 text-sm transition-colors hover:border-line-strong disabled:cursor-not-allowed disabled:opacity-50";
const SEGMENT_ACTIVE =
  "rounded-md bg-accent px-3 py-1.5 text-sm font-semibold text-accent-fg";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

/** Keys of AmpSettings whose value is a number — the slider-driven params. */
type NumericAmpKey = {
  [K in keyof AmpSettings]: AmpSettings[K] extends number ? K : never;
}[keyof AmpSettings];

/** Distortion-type options for the selector, in chain order of intensity. */
const DISTORTION_OPTIONS: { type: DistortionType; label: string }[] = [
  { type: "overdrive", label: "Overdrive" },
  { type: "distortion", label: "Distortion" },
  { type: "fuzz", label: "Fuzz" },
];

/** Cabinet options for the selector. */
const CABINET_OPTIONS: { type: CabinetType; label: string }[] = [
  { type: "modern", label: "Moderní" },
  { type: "vintage", label: "Vintage" },
  { type: "open", label: "Otevřený" },
];

/**
 * Amp / effects studio for Bod 16. Routes the live guitar input through the
 * effects chain (drive, EQ, cabinet sim, delay) straight to the speakers,
 * with brand-free tone presets and per-parameter sliders.
 *
 * The chain is local to this component: it spins up on Zapnout, lives
 * until either Vypnout or unmount. Settings and presetId are shared
 * across screens through useAmpStore.
 */
export function AmpStudio() {
  const settings = useAmpStore((s) => s.settings);
  const presetId = useAmpStore((s) => s.presetId);
  const storeSetSettings = useAmpStore((s) => s.setSettings);
  const storeSetPresetId = useAmpStore((s) => s.setPresetId);

  const {
    active,
    starting,
    error,
    outputAnalyser,
    recordingStream,
    start: startChain,
    stop: stopChain,
  } = useAmpEngine();

  const [presetName, setPresetName] = useState("");
  // A/B compare — two stored tone snapshots the user can flip between.
  const [slotA, setSlotA] = useState<AmpSettings | null>(null);
  const [slotB, setSlotB] = useState<AmpSettings | null>(null);
  // Recording the amped output into the shared recordings store.
  const [recording, setRecording] = useState(false);
  const [savingTake, setSavingTake] = useState(false);
  const [takeElapsed, setTakeElapsed] = useState(0);

  const latencyMs = useAudioStore((state) => state.latencyMs);
  const layoutMode = useResolvedLayoutMode();
  const userPresets = useAmpPresetsStore((state) => state.presets);
  const savePreset = useAmpPresetsStore((state) => state.savePreset);
  const deletePreset = useAmpPresetsStore((state) => state.deletePreset);
  const addRecording = useRecordingsStore((state) => state.addRecording);
  const recordingsCount = useRecordingsStore(
    (state) => state.recordings.length,
  );
  const recordingsLoaded = useRecordingsStore((state) => state.loaded);
  const loadRecordings = useRecordingsStore((state) => state.load);

  // Load the recordings list so a new amp take gets a correct number and
  // shows up alongside the others in the Nahrávání screen.
  useEffect(() => {
    if (!recordingsLoaded) void loadRecordings();
  }, [recordingsLoaded, loadRecordings]);

  // Live elapsed timer while an amp take is being recorded.
  useEffect(() => {
    if (!recording) return;
    const startedAt = performance.now();
    setTakeElapsed(0);
    const id = window.setInterval(() => {
      setTakeElapsed((performance.now() - startedAt) / 1000);
    }, 200);
    return () => window.clearInterval(id);
  }, [recording]);

  async function start() {
    await startChain();
  }

  async function stop() {
    // Finish and save a take in progress before tearing the chain down.
    if (recorder.isRecording()) await stopTake();
    await stopChain();
  }

  /** Starts capturing the amped (post-effects) output. */
  function startTake() {
    if (!recordingStream || recorder.isRecording()) return;
    if (recorder.start(recordingStream)) {
      setRecording(true);
    }
  }

  /** Stops the take and saves the amped recording to the recordings store. */
  async function stopTake() {
    setSavingTake(true);
    const result = await recorder.stop();
    setRecording(false);
    if (result) {
      const take: Recording = {
        id: crypto.randomUUID(),
        name: `Aparát ${recordingsCount + 1}`,
        createdAt: Date.now(),
        durationSec: result.durationSec,
        mimeType: result.mimeType,
        markers: [],
        blob: result.blob,
      };
      await addRecording(take);
    }
    setSavingTake(false);
  }

  // Leaving /amp no longer tears the aparát down (it lives in the shared
  // engine now), so a take in progress would otherwise keep recording with no
  // UI to stop it. Finish and save it on unmount. A ref keeps the latest
  // stopTake closure so the saved take gets the correct number.
  const stopTakeRef = useRef(stopTake);
  stopTakeRef.current = stopTake;
  useEffect(() => {
    return () => {
      if (recorder.isRecording()) void stopTakeRef.current();
    };
  }, []);

  function applyPreset(preset: AmpPreset) {
    storeSetSettings(preset.settings, preset.id);
  }

  /** Saves the current slider settings as a new named user preset. */
  function saveCurrentPreset() {
    const name = presetName.trim();
    if (!name) return;
    storeSetPresetId(savePreset(name, settings));
    setPresetName("");
  }

  function removePreset(id: string) {
    deletePreset(id);
    // Deleting the active preset takes the tone onto "vlastní nastavení".
    if (presetId === id) storeSetPresetId(null);
  }

  /** Stores the current settings into A/B slot for later comparison. */
  function storeSlot(target: "A" | "B") {
    if (target === "A") setSlotA(settings);
    else setSlotB(settings);
  }

  /** Recalls a stored A/B snapshot and applies it to the chain. */
  function recallSlot(stored: AmpSettings) {
    storeSetSettings(stored, null);
  }

  function setParam(key: NumericAmpKey, value: number) {
    // Moving a slider takes the tone off any named preset.
    storeSetSettings({ ...settings, [key]: value }, null);
  }

  function setDistortion(type: DistortionType) {
    storeSetSettings({ ...settings, distortion: type }, null);
  }

  function setCabinet(type: CabinetType) {
    storeSetSettings({ ...settings, cabinet: type }, null);
  }

  /**
   * Renders one parameter knob. Internally each parameter is stored as
   * 0..1 (or -1..1 for bipolar EQ); the knob shows the value as 0..100 or
   * -100..+100 so the user reads it as a percentage / signed dB-ish nudge.
   */
  function knob(label: string, key: NumericAmpKey, bipolar: boolean) {
    const display = Math.round(settings[key] * 100);
    return (
      <KnobControl
        key={key}
        label={label}
        value={display}
        min={bipolar ? -100 : 0}
        max={100}
        step={1}
        fineStep={0.5}
        bipolar={bipolar}
        formatValue={(v) =>
          bipolar
            ? v > 0
              ? `+${Math.round(v)}`
              : `${Math.round(v)}`
            : `${Math.round(v)} %`
        }
        ariaLabel={
          bipolar
            ? `${label}, rozsah −100 až +100`
            : `${label}, rozsah 0 až 100`
        }
        onChange={(next) => setParam(key, next / 100)}
      />
    );
  }

  const activePresetName =
    AMP_PRESETS.find((preset) => preset.id === presetId)?.name ??
    userPresets.find((preset) => preset.id === presetId)?.name ??
    "vlastní nastavení";

  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <div className="rounded-lg border border-accent/40 bg-accent/10 p-3 text-sm text-accent">
        Použij sluchátka nebo zvukovku (M-Track Solo). Při hraní přes
        reproduktory a mikrofon může vzniknout zpětná vazba — nepříjemné
        houkání.
      </div>

      <div className="mt-4 flex items-center gap-3">
        <button
          type="button"
          onClick={active ? stop : start}
          disabled={starting}
          className={active ? SECONDARY_BUTTON : PRIMARY_BUTTON}
        >
          {starting
            ? "Spouštím…"
            : active
              ? "Vypnout efekt"
              : "Zapnout efekt"}
        </button>
        {active && latencyMs !== null && (
          <span className="text-sm text-dim">
            Latence:{" "}
            <span className="font-medium text-text">
              {latencyMs} ms
            </span>
          </span>
        )}
      </div>

      {error && <p className="mt-3 text-sm text-danger">{error}</p>}

      {active && (
        <div className="mt-5 space-y-5">
          <div className="space-y-3">
            <InputLevelMeter />
            <SignalLevelMeter
              analyser={outputAnalyser}
              active={active}
              label="Úroveň výstupu"
            />
            <SignalScope analyser={outputAnalyser} active={active} />
          </div>

          <div className="rounded-lg border border-line bg-bg p-3">
            <div className="flex flex-wrap items-center gap-3">
              {recording ? (
                <button
                  type="button"
                  onClick={() => void stopTake()}
                  disabled={savingTake}
                  className="rounded-lg border border-line-strong bg-surface px-5 py-2 text-sm font-semibold transition-colors hover:border-line-strong disabled:opacity-60"
                >
                  Zastavit nahrávání
                </button>
              ) : (
                <button
                  type="button"
                  onClick={startTake}
                  disabled={savingTake}
                  className="flex items-center gap-2 rounded-lg bg-danger px-5 py-2 text-sm font-semibold text-text transition-colors hover:bg-danger disabled:opacity-60"
                >
                  <span className="h-2.5 w-2.5 rounded-full bg-surface" />
                  Nahrát
                </button>
              )}
              <span className="text-sm text-dim">
                {recording
                  ? `Nahrávám ${formatDuration(takeElapsed)}`
                  : savingTake
                    ? "Ukládám…"
                    : "Záznam zvuku po efektech — uloží se do Nahrávání."}
              </span>
            </div>
          </div>

          <div>
            <div className="text-sm font-medium text-text">
              Preset:{" "}
              <span className="text-dim">{activePresetName}</span>
            </div>
            <div className="mt-2 inline-flex flex-wrap rounded-lg border border-line p-1">
              {AMP_PRESETS.map((preset) => (
                <button
                  key={preset.id}
                  type="button"
                  onClick={() => applyPreset(preset)}
                  className={
                    presetId === preset.id ? SEGMENT_ACTIVE : SEGMENT_IDLE
                  }
                >
                  {preset.name}
                </button>
              ))}
            </div>

            {userPresets.length > 0 && (
              <div className="mt-2 flex flex-wrap gap-2">
                {userPresets.map((preset) => (
                  <div
                    key={preset.id}
                    className={
                      presetId === preset.id
                        ? "flex items-center gap-1 rounded-md border border-accent bg-accent/10 py-1 pr-1 pl-3 text-sm text-accent"
                        : "flex items-center gap-1 rounded-md border border-line bg-bg py-1 pr-1 pl-3 text-sm text-dim"
                    }
                  >
                    <button
                      type="button"
                      onClick={() => applyPreset(preset)}
                      className="transition-colors hover:text-text"
                    >
                      {preset.name}
                    </button>
                    <button
                      type="button"
                      onClick={() => removePreset(preset.id)}
                      aria-label={`Smazat preset ${preset.name}`}
                      className="rounded px-1 text-muted transition-colors hover:text-danger"
                    >
                      ×
                    </button>
                  </div>
                ))}
              </div>
            )}

            <div className="mt-3 flex flex-wrap items-center gap-2">
              <input
                type="text"
                value={presetName}
                onChange={(event) => setPresetName(event.target.value)}
                placeholder="Název vlastního presetu"
                aria-label="Název vlastního presetu"
                className="min-w-0 flex-1 rounded-lg border border-line bg-bg px-3 py-1.5 text-sm text-text sm:w-56 sm:flex-none"
              />
              <button
                type="button"
                onClick={saveCurrentPreset}
                disabled={presetName.trim().length === 0}
                className={SECONDARY_BUTTON}
              >
                Uložit jako preset
              </button>
            </div>
          </div>

          <div>
            <div className="text-sm font-medium text-text">
              Porovnání A/B
            </div>
            <p className="mt-1 text-xs text-muted">
              Ulož si dvě varianty zvuku a přepínej mezi nimi.
            </p>
            <div className="mt-2 flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => storeSlot("A")}
                className={SECONDARY_BUTTON}
              >
                Uložit do A
              </button>
              <button
                type="button"
                onClick={() => slotA && recallSlot(slotA)}
                disabled={slotA === null}
                className={SECONDARY_BUTTON}
              >
                Načíst A
              </button>
              <button
                type="button"
                onClick={() => storeSlot("B")}
                className={SECONDARY_BUTTON}
              >
                Uložit do B
              </button>
              <button
                type="button"
                onClick={() => slotB && recallSlot(slotB)}
                disabled={slotB === null}
                className={SECONDARY_BUTTON}
              >
                Načíst B
              </button>
            </div>
          </div>

          <div>
            <div className="text-sm font-medium text-text">
              Typ zkreslení
            </div>
            <div className="mt-2 inline-flex flex-wrap rounded-lg border border-line p-1">
              {DISTORTION_OPTIONS.map((option) => (
                <button
                  key={option.type}
                  type="button"
                  onClick={() => setDistortion(option.type)}
                  className={
                    settings.distortion === option.type
                      ? SEGMENT_ACTIVE
                      : SEGMENT_IDLE
                  }
                >
                  {option.label}
                </button>
              ))}
            </div>
          </div>

          <div>
            <div className="text-sm font-medium text-text">Reprobox</div>
            <div className="mt-2 inline-flex flex-wrap rounded-lg border border-line p-1">
              {CABINET_OPTIONS.map((option) => (
                <button
                  key={option.type}
                  type="button"
                  onClick={() => setCabinet(option.type)}
                  className={
                    settings.cabinet === option.type
                      ? SEGMENT_ACTIVE
                      : SEGMENT_IDLE
                  }
                >
                  {option.label}
                </button>
              ))}
            </div>
          </div>

          <div className={KNOB_GRID[layoutMode]}>
            {knob("Komprese", "compression", false)}
            {knob("Zkreslení", "drive", false)}
            {knob("Basy", "bass", true)}
            {knob("Středy", "mid", true)}
            {knob("Výšky", "treble", true)}
            {knob("Delay", "delay", false)}
            {knob("Dozvuk", "reverb", false)}
            {knob("Chorus", "chorus", false)}
            {knob("Hlasitost", "level", false)}
          </div>
        </div>
      )}
    </div>
  );
}
