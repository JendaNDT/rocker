"use client";

import type { ClickSoundId, PolyrhythmConfig } from "@/lib/metronomeConfig";
import {
  CLICK_SOUNDS,
  POLYRHYTHM_MAX_TICKS,
  POLYRHYTHM_MIN_TICKS,
} from "@/lib/metronomeConfig";

const SEGMENT_ACTIVE =
  "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

/** Builds the segmented options 2..7 with stable React keys. */
const TICK_OPTIONS = Array.from(
  { length: POLYRHYTHM_MAX_TICKS - POLYRHYTHM_MIN_TICKS + 1 },
  (_, index) => POLYRHYTHM_MIN_TICKS + index,
);

/**
 * Polyrhythm panel — secondary click layer running its own tick count
 * against the main pulse. Drives the "3 against 4" classic.
 *
 * Purely presentational: changes are reported through onChange.
 */
export function PolyrhythmPanel({
  polyrhythm,
  subdivision,
  onChange,
}: {
  polyrhythm: PolyrhythmConfig;
  /** Current main subdivision — used in the descriptive hint. */
  subdivision: number;
  onChange: (patch: Partial<PolyrhythmConfig>) => void;
}) {
  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <label className="flex items-center gap-2 text-sm font-medium text-text">
        <input
          type="checkbox"
          checked={polyrhythm.enabled}
          onChange={(event) => onChange({ enabled: event.target.checked })}
          className="h-4 w-4 accent-accent"
        />
        Polyrytmus
      </label>
      {polyrhythm.enabled && (
        <div className="mt-3 space-y-3">
          <div>
            <div className="text-sm text-text">Poměr</div>
            <div className="mt-2 inline-flex flex-wrap rounded-lg border border-line p-1">
              {TICK_OPTIONS.map((option) => (
                <button
                  key={option}
                  type="button"
                  onClick={() => onChange({ ticksPerBeat: option })}
                  className={
                    polyrhythm.ticksPerBeat === option
                      ? SEGMENT_ACTIVE
                      : SEGMENT_IDLE
                  }
                >
                  {option}
                </button>
              ))}
            </div>
            <p className="mt-2 text-xs text-muted">
              Druhá vrstva hraje {polyrhythm.ticksPerBeat} not na dobu proti{" "}
              {subdivision} hlavního metronomu (
              {polyrhythm.ticksPerBeat === subdivision
                ? "stejné dělení — polyrytmus nebude slyšet"
                : `cvik ${polyrhythm.ticksPerBeat} proti ${subdivision}`}
              ).
            </p>
          </div>

          <label className="flex items-center justify-between gap-3 text-sm text-text">
            <span>Zvuk</span>
            <select
              aria-label="Zvuk polyrytmu"
              value={polyrhythm.sound}
              onChange={(event) =>
                onChange({ sound: event.target.value as ClickSoundId })
              }
              className="rounded border border-line bg-surface px-2 py-1 text-xs text-text"
            >
              {CLICK_SOUNDS.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <div>
            <div className="flex justify-between text-sm text-text">
              <span>Hlasitost vrstvy</span>
              <span className="text-dim tabular-nums">
                {Math.round(polyrhythm.volume * 100)} %
              </span>
            </div>
            <input
              type="range"
              min={0}
              max={100}
              value={Math.round(polyrhythm.volume * 100)}
              aria-label="Hlasitost polyrytmu"
              onChange={(event) =>
                onChange({ volume: Number(event.target.value) / 100 })
              }
              className="mt-1 w-full accent-accent"
            />
          </div>
        </div>
      )}
    </div>
  );
}
