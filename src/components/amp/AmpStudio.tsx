"use client";

import { useEffect, useRef, useState } from "react";
import { AmpChain } from "@/audio/ampChain";
import { audioEngine } from "@/audio/AudioEngine";
import { useResolvedLayoutMode } from "@/lib/layout";
import {
  AMP_PRESETS,
  DEFAULT_AMP_PRESET,
  type AmpPreset,
  type AmpSettings,
  type CabinetType,
  type DistortionType,
} from "@/lib/ampPresets";
import { useAmpPresetsStore } from "@/stores/ampPresetsStore";
import { useAudioStore } from "@/stores/audioStore";

// Parameter sliders: two columns on desktop, stacked on mobile.
const SLIDER_GRID = {
  desktop: "grid grid-cols-2 gap-x-8 gap-y-4",
  mobile: "space-y-4",
  auto: "space-y-4 lg:grid lg:grid-cols-2 lg:gap-x-8 lg:gap-y-4 lg:space-y-0",
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

/** Pushes every parameter of a settings object onto the chain. */
function applyToChain(chain: AmpChain, settings: AmpSettings): void {
  chain.setCompression(settings.compression);
  chain.setDrive(settings.drive);
  chain.setDistortionType(settings.distortion);
  chain.setBass(settings.bass);
  chain.setMid(settings.mid);
  chain.setTreble(settings.treble);
  chain.setCabinet(settings.cabinet);
  chain.setDelayMix(settings.delay);
  chain.setReverbMix(settings.reverb);
  chain.setChorusMix(settings.chorus);
  chain.setLevel(settings.level);
}

/**
 * Amp / effects studio for Bod 16. Routes the live guitar input through the
 * effects chain (drive, EQ, cabinet sim, delay) straight to the speakers,
 * with brand-free tone presets and per-parameter sliders.
 */
export function AmpStudio() {
  const chainRef = useRef<AmpChain | null>(null);
  const [active, setActive] = useState(false);
  const [starting, setStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [settings, setSettings] = useState<AmpSettings>(
    DEFAULT_AMP_PRESET.settings,
  );
  const [presetId, setPresetId] = useState<string | null>(
    DEFAULT_AMP_PRESET.id,
  );
  const [presetName, setPresetName] = useState("");

  const latencyMs = useAudioStore((state) => state.latencyMs);
  const layoutMode = useResolvedLayoutMode();
  const userPresets = useAmpPresetsStore((state) => state.presets);
  const savePreset = useAmpPresetsStore((state) => state.savePreset);
  const deletePreset = useAmpPresetsStore((state) => state.deletePreset);

  // Tear down the audio graph when leaving the screen.
  useEffect(() => {
    return () => {
      const chain = chainRef.current;
      if (chain) {
        audioEngine.getInputSource()?.disconnect(chain.input);
        chain.dispose();
        chainRef.current = null;
      }
    };
  }, []);

  async function start() {
    if (starting || active) return;
    setStarting(true);
    setError(null);
    const running = await audioEngine.ensureListening();
    setStarting(false);
    if (!running) {
      setError(
        "Nepodařilo se spustit zvukový vstup. Povol mikrofon v Profilu.",
      );
      return;
    }
    const source = audioEngine.getInputSource();
    if (!source) {
      setError("Zvukový vstup není dostupný.");
      return;
    }
    const chain = new AmpChain(source.context);
    applyToChain(chain, settings);
    source.connect(chain.input);
    chainRef.current = chain;
    setActive(true);
  }

  function stop() {
    const chain = chainRef.current;
    if (chain) {
      audioEngine.getInputSource()?.disconnect(chain.input);
      chain.dispose();
      chainRef.current = null;
    }
    setActive(false);
  }

  function applyPreset(preset: AmpPreset) {
    setSettings(preset.settings);
    setPresetId(preset.id);
    if (chainRef.current) applyToChain(chainRef.current, preset.settings);
  }

  /** Saves the current slider settings as a new named user preset. */
  function saveCurrentPreset() {
    const name = presetName.trim();
    if (!name) return;
    setPresetId(savePreset(name, settings));
    setPresetName("");
  }

  function removePreset(id: string) {
    deletePreset(id);
    // Deleting the active preset takes the tone onto "vlastní nastavení".
    if (presetId === id) setPresetId(null);
  }

  function setParam(key: NumericAmpKey, value: number) {
    const next = { ...settings, [key]: value };
    setSettings(next);
    // Moving a slider takes the tone off any named preset.
    setPresetId(null);
    if (chainRef.current) applyToChain(chainRef.current, next);
  }

  function setDistortion(type: DistortionType) {
    const next = { ...settings, distortion: type };
    setSettings(next);
    setPresetId(null);
    if (chainRef.current) applyToChain(chainRef.current, next);
  }

  function setCabinet(type: CabinetType) {
    const next = { ...settings, cabinet: type };
    setSettings(next);
    setPresetId(null);
    if (chainRef.current) applyToChain(chainRef.current, next);
  }

  /** Renders one parameter slider. Bipolar params (EQ) run -100..100. */
  function slider(label: string, key: NumericAmpKey, bipolar: boolean) {
    const display = Math.round(settings[key] * 100);
    return (
      <div key={key}>
        <label
          htmlFor={`amp-${key}`}
          className="block text-sm font-medium text-text"
        >
          {label}:{" "}
          {bipolar
            ? display > 0
              ? `+${display}`
              : display
            : `${display} %`}
        </label>
        <input
          id={`amp-${key}`}
          type="range"
          min={bipolar ? -100 : 0}
          max={100}
          value={display}
          onChange={(event) =>
            setParam(key, Number(event.target.value) / 100)
          }
          className="mt-2 w-full accent-accent"
        />
      </div>
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

          <div className={SLIDER_GRID[layoutMode]}>
            {slider("Komprese", "compression", false)}
            {slider("Zkreslení", "drive", false)}
            {slider("Basy", "bass", true)}
            {slider("Středy", "mid", true)}
            {slider("Výšky", "treble", true)}
            {slider("Delay", "delay", false)}
            {slider("Dozvuk", "reverb", false)}
            {slider("Chorus", "chorus", false)}
            {slider("Hlasitost", "level", false)}
          </div>
        </div>
      )}
    </div>
  );
}
