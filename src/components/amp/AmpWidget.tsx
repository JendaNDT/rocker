"use client";

import Link from "next/link";
import { useLocalAmpChain } from "@/components/amp/useLocalAmpChain";
import { InputLevelMeter } from "@/components/InputLevelMeter";
import { AMP_PRESETS } from "@/lib/ampPresets";
import { useAmpStore } from "@/stores/ampStore";
import { useAudioStore } from "@/stores/audioStore";

const PRIMARY_BUTTON =
  "rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90 disabled:cursor-not-allowed disabled:opacity-60";
const SECONDARY_BUTTON =
  "rounded-lg border border-line bg-bg px-4 py-2 text-sm transition-colors hover:border-line-strong";
const PRESET_ACTIVE =
  "rounded-md bg-accent px-3 py-1.5 text-xs font-semibold text-accent-fg";
const PRESET_IDLE =
  "rounded-md border border-line bg-bg px-3 py-1.5 text-xs text-dim transition-colors hover:border-line-strong hover:text-text";

/**
 * Compact aparát card meant to live on practice-adjacent screens
 * (Cvičení, Metal). Owns its own local AmpChain instance — the chain
 * disposes when the widget unmounts (e.g. the user navigates to another
 * screen). Settings and presetId are shared with the full AmpStudio
 * through useAmpStore, so a tone tweaked here shows up in /amp and
 * vice versa.
 *
 * The widget keeps controls deliberately small: ON/OFF, the four built-in
 * presets, latency, an input level meter and a link to the full screen.
 * Saving / deleting custom presets and tweaking individual parameters
 * stays on /amp where there's space for it.
 */
export function AmpWidget() {
  const settings = useAmpStore((s) => s.settings);
  const presetId = useAmpStore((s) => s.presetId);
  const setSettings = useAmpStore((s) => s.setSettings);
  const { active, starting, error, start, stop } = useLocalAmpChain(settings);
  const latencyMs = useAudioStore((s) => s.latencyMs);

  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <div className="text-sm font-semibold text-text">Aparát</div>
          <div className="text-xs text-muted">
            {active
              ? latencyMs !== null
                ? `Hraje — latence ${latencyMs} ms`
                : "Hraje"
              : "Vypnutý"}
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Link
            href="/amp"
            className="text-xs text-dim underline-offset-2 hover:text-text hover:underline"
          >
            Otevřít plný Aparát →
          </Link>
          <button
            type="button"
            onClick={() => {
              if (active) void stop();
              else void start();
            }}
            disabled={starting}
            className={active ? SECONDARY_BUTTON : PRIMARY_BUTTON}
          >
            {starting ? "Spouštím…" : active ? "Vypnout" : "Zapnout"}
          </button>
        </div>
      </div>

      {error && <p className="mt-3 text-sm text-danger">{error}</p>}

      {/* Preset chooser — quick switch between the four built-in tones.
          User-saved presets stay on /amp for now. */}
      <div className="mt-3 flex flex-wrap items-center gap-2">
        <span className="text-xs text-dim">Preset:</span>
        {AMP_PRESETS.map((preset) => (
          <button
            key={preset.id}
            type="button"
            onClick={() => setSettings(preset.settings, preset.id)}
            className={
              presetId === preset.id ? PRESET_ACTIVE : PRESET_IDLE
            }
          >
            {preset.name}
          </button>
        ))}
      </div>

      {active && (
        <div className="mt-4">
          <InputLevelMeter />
        </div>
      )}
    </div>
  );
}
