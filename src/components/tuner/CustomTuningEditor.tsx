"use client";

import { useState } from "react";
import type { Tuning, TuningString } from "@/audio/tunings";
import { midiToFrequency, midiToLabelIn, type Notation } from "@/lib/pitch";
import {
  MAX_TUNING_MIDI,
  MIN_TUNING_MIDI,
  useCustomTuningsStore,
} from "@/stores/customTuningsStore";

/**
 * Inline editor for creating a new custom tuning. Starts from the
 * currently selected tuning (so the user "tweaks" rather than starts
 * blank) and lets them shift each string by half-steps within
 * [MIN_TUNING_MIDI, MAX_TUNING_MIDI]. On save we add the entry to the
 * custom store and switch the active tuning to it via the supplied
 * callback.
 *
 * Editing existing customs is intentionally NOT supported — the workflow
 * is "open, tweak, save under a new name; delete the old one". Keeps the
 * UI a single mode and avoids edge cases around in-flight rename / id
 * stability.
 */
export function CustomTuningEditor({
  baseTuning,
  notation,
  onSaved,
  onCancel,
}: {
  baseTuning: Tuning;
  notation: Notation;
  onSaved: (newTuningId: string) => void;
  onCancel: () => void;
}) {
  const addTuning = useCustomTuningsStore((state) => state.addTuning);
  const [label, setLabel] = useState("");
  // Local working copy of the string set. Starts as a clone of the base
  // tuning so the user sees what they're tweaking and can revert any
  // single string by half-step buttons.
  const [strings, setStrings] = useState<TuningString[]>(() =>
    baseTuning.strings.map((string) => ({ ...string })),
  );
  const [error, setError] = useState<string | null>(null);

  function shift(index: number, delta: number): void {
    setStrings((prev) => {
      const next = prev.slice();
      const current = next[index];
      const nextMidi = current.midi + delta;
      if (nextMidi < MIN_TUNING_MIDI || nextMidi > MAX_TUNING_MIDI) {
        return prev;
      }
      next[index] = {
        midi: nextMidi,
        note: midiToLabelIn(nextMidi, notation),
      };
      return next;
    });
  }

  function reset(): void {
    setStrings(baseTuning.strings.map((string) => ({ ...string })));
    setError(null);
  }

  function save(): void {
    const id = addTuning(label, strings);
    if (id === null) {
      setError(
        label.trim().length === 0
          ? "Zadej název."
          : "Nepodařilo se uložit (limit 20 vlastních ladění).",
      );
      return;
    }
    onSaved(id);
  }

  return (
    <div className="mt-4 rounded-lg border border-line bg-surface p-4">
      <h3 className="text-sm font-semibold">Nové vlastní ladění</h3>
      <p className="mt-1 text-xs text-muted">
        Začni z {baseTuning.label}. Tlačítky ± posuneš strunu o půltón
        nahoru/dolů. Až bude hotové, dej mu název a ulož.
      </p>

      <div className="mt-4 space-y-2">
        {strings.map((string, index) => {
          const freq = midiToFrequency(string.midi).toFixed(1);
          const baseLabel = midiToLabelIn(
            baseTuning.strings[index].midi,
            notation,
          );
          const changed = string.midi !== baseTuning.strings[index].midi;
          return (
            <div
              key={`string-${index}`}
              className="flex items-center gap-3 rounded-md border border-line bg-bg px-3 py-2"
            >
              <span className="w-10 text-xs text-dim">
                Struna {strings.length - index}
              </span>
              <button
                type="button"
                onClick={() => shift(index, -1)}
                aria-label={`Snížit strunu ${strings.length - index} o půltón`}
                className="rounded-md border border-line bg-surface px-3 py-1 text-sm text-text transition-colors hover:border-line-strong disabled:cursor-not-allowed disabled:opacity-50"
                disabled={string.midi <= MIN_TUNING_MIDI}
              >
                −
              </button>
              <div className="flex-1 text-center">
                <div className="text-sm font-semibold text-text">
                  {midiToLabelIn(string.midi, notation)}
                </div>
                <div className="text-xs text-muted">
                  {freq} Hz
                  {changed && (
                    <span className="ml-1 text-accent">
                      (původně {baseLabel})
                    </span>
                  )}
                </div>
              </div>
              <button
                type="button"
                onClick={() => shift(index, +1)}
                aria-label={`Zvýšit strunu ${strings.length - index} o půltón`}
                className="rounded-md border border-line bg-surface px-3 py-1 text-sm text-text transition-colors hover:border-line-strong disabled:cursor-not-allowed disabled:opacity-50"
                disabled={string.midi >= MAX_TUNING_MIDI}
              >
                +
              </button>
            </div>
          );
        })}
      </div>

      <button
        type="button"
        onClick={reset}
        className="mt-3 text-xs text-dim transition-colors hover:text-text"
      >
        Vrátit do výchozího stavu ({baseTuning.label})
      </button>

      <label
        htmlFor="custom-tuning-label"
        className="mt-4 block text-sm font-medium text-text"
      >
        Název
      </label>
      <input
        id="custom-tuning-label"
        type="text"
        value={label}
        onChange={(event) => setLabel(event.target.value)}
        placeholder="třeba Open A nebo Půltón dolů"
        className="mt-1 w-full rounded-lg border border-line bg-bg px-3 py-2 text-sm text-text"
        maxLength={40}
      />

      {error && <p className="mt-3 text-sm text-danger">{error}</p>}

      <div className="mt-4 flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={save}
          className="rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
        >
          Uložit
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="rounded-lg border border-line bg-surface px-4 py-2 text-sm transition-colors hover:border-line-strong"
        >
          Zrušit
        </button>
      </div>
    </div>
  );
}
