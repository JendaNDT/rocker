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
} from "@/lib/ampPresets";
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

/** Pushes every parameter of a settings object onto the chain. */
function applyToChain(chain: AmpChain, settings: AmpSettings): void {
  chain.setDrive(settings.drive);
  chain.setBass(settings.bass);
  chain.setMid(settings.mid);
  chain.setTreble(settings.treble);
  chain.setDelayMix(settings.delay);
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

  const latencyMs = useAudioStore((state) => state.latencyMs);
  const layoutMode = useResolvedLayoutMode();

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

  function setParam(key: keyof AmpSettings, value: number) {
    const next = { ...settings, [key]: value };
    setSettings(next);
    // Moving a slider takes the tone off any named preset.
    setPresetId(null);
    if (chainRef.current) applyToChain(chainRef.current, next);
  }

  /** Renders one parameter slider. Bipolar params (EQ) run -100..100. */
  function slider(label: string, key: keyof AmpSettings, bipolar: boolean) {
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
          </div>

          <div className={SLIDER_GRID[layoutMode]}>
            {slider("Zkreslení", "drive", false)}
            {slider("Basy", "bass", true)}
            {slider("Středy", "mid", true)}
            {slider("Výšky", "treble", true)}
            {slider("Delay", "delay", false)}
            {slider("Hlasitost", "level", false)}
          </div>
        </div>
      )}
    </div>
  );
}
