import { useState } from "react";
import type { MetronomePreset } from "@/lib/metronomeConfig";
import { TIME_SIGNATURES } from "@/lib/metronomeConfig";

/** Short human-readable label for a preset's time signature. */
function timeSignatureLabel(beatsPerBar: number): string {
  const match = TIME_SIGNATURES.find(
    (signature) => signature.beatsPerBar === beatsPerBar,
  );
  return match ? match.label : `${beatsPerBar} dob`;
}

/**
 * Saved metronome presets — two sections:
 *
 *  - "Tovární" (top): the read-only factory list. Each row only offers
 *    "Použít"; factory presets cannot be deleted, and the user is
 *    expected to load → tweak → save under a new name to "make it
 *    their own".
 *  - "Moje" (below): the user-saved list with the existing apply +
 *    delete behaviour and the save input.
 *
 * Purely presentational.
 */
export function PresetPanel({
  factoryPresets,
  presets,
  hydrated,
  onSave,
  onApply,
  onDelete,
}: {
  factoryPresets: readonly MetronomePreset[];
  presets: MetronomePreset[];
  hydrated: boolean;
  onSave: (name: string) => void;
  onApply: (preset: MetronomePreset) => void;
  onDelete: (id: string) => void;
}) {
  const [name, setName] = useState("");

  function save() {
    const trimmed = name.trim();
    if (trimmed.length === 0) return;
    onSave(trimmed);
    setName("");
  }

  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <div className="text-sm font-medium text-text">Presety</div>

      {factoryPresets.length > 0 && (
        <div className="mt-3">
          <div className="text-xs font-medium uppercase tracking-wide text-dim">
            Tovární
          </div>
          <ul className="mt-2 space-y-2">
            {factoryPresets.map((preset) => (
              <li
                key={preset.id}
                className="flex items-center gap-2 rounded-lg border border-line bg-surface-2 px-3 py-2"
              >
                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm text-text">
                    {preset.name}
                  </div>
                  <div className="text-xs text-dim tabular-nums">
                    {preset.bpm} BPM ·{" "}
                    {timeSignatureLabel(preset.beatsPerBar)}
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => onApply(preset)}
                  className="rounded-md border border-line px-3 py-1 text-xs transition-colors hover:border-line-strong"
                >
                  Použít
                </button>
              </li>
            ))}
          </ul>
          <p className="mt-2 text-xs text-muted">
            Tovární presety nejdou smazat. Když chceš vlastní verzi, načti
            preset, uprav a ulož pod novým jménem.
          </p>
        </div>
      )}

      <div className="mt-4">
        <div className="text-xs font-medium uppercase tracking-wide text-dim">
          Moje
        </div>
        <div className="mt-2 flex gap-2">
          <input
            type="text"
            value={name}
            onChange={(event) => setName(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter") save();
            }}
            placeholder="Název presetu"
            maxLength={40}
            aria-label="Název presetu"
            className="min-w-0 flex-1 rounded-lg border border-line bg-surface-2 px-3 py-2 text-sm"
          />
          <button
            type="button"
            onClick={save}
            disabled={name.trim().length === 0}
            className="rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90 disabled:opacity-40"
          >
            Uložit
          </button>
        </div>

        {hydrated && presets.length === 0 && (
          <p className="mt-3 text-xs text-muted">
            Zatím nemáš uložený žádný preset. Vyzkoušej tovární výše nebo
            si ulož aktuální nastavení.
          </p>
        )}

        {hydrated && presets.length > 0 && (
          <ul className="mt-3 space-y-2">
            {presets.map((preset) => (
              <li
                key={preset.id}
                className="flex items-center gap-2 rounded-lg border border-line bg-surface-2 px-3 py-2"
              >
                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm text-text">
                    {preset.name}
                  </div>
                  <div className="text-xs text-dim tabular-nums">
                    {preset.bpm} BPM ·{" "}
                    {timeSignatureLabel(preset.beatsPerBar)}
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => onApply(preset)}
                  className="rounded-md border border-line px-3 py-1 text-xs transition-colors hover:border-line-strong"
                >
                  Použít
                </button>
                <button
                  type="button"
                  onClick={() => onDelete(preset.id)}
                  aria-label={`Smazat preset ${preset.name}`}
                  className="rounded-md border border-line px-2 py-1 text-xs text-dim transition-colors hover:border-line-strong hover:text-text"
                >
                  ×
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
