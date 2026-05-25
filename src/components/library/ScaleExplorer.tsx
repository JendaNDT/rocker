"use client";

import { useState } from "react";
import type { Tuning } from "@/audio/tunings";
import { Fretboard } from "@/components/library/Fretboard";
import { type Notation, pitchClassName } from "@/lib/pitch";
import { DEFAULT_SCALE_ID, getScale, SCALES } from "@/lib/scales";

/** Root picker + scale picker + fretboard. */
export function ScaleExplorer({
  notation,
  tuning,
}: {
  notation: Notation;
  tuning: Tuning;
}) {
  const [scaleId, setScaleId] = useState<string>(DEFAULT_SCALE_ID);
  const [rootPitchClass, setRootPitchClass] = useState<number>(9); // A

  const scale = getScale(scaleId);

  return (
    <div>
      <div>
        <div className="text-sm font-medium text-text">
          Kořenový tón
        </div>
        <div className="mt-2 flex flex-wrap gap-1">
          {Array.from({ length: 12 }, (_, pitchClass) => (
            <button
              key={pitchClass}
              type="button"
              onClick={() => setRootPitchClass(pitchClass)}
              className={
                pitchClass === rootPitchClass
                  ? "rounded-md bg-accent px-2.5 py-1 text-sm font-semibold text-accent-fg"
                  : "rounded-md border border-line bg-surface px-2.5 py-1 text-sm text-dim transition-colors hover:text-text"
              }
            >
              {pitchClassName(pitchClass, notation)}
            </button>
          ))}
        </div>
      </div>

      <div className="mt-4">
        <label
          htmlFor="scale-select"
          className="text-sm font-medium text-text"
        >
          Stupnice
        </label>
        <select
          id="scale-select"
          value={scaleId}
          onChange={(event) => setScaleId(event.target.value)}
          className="mt-2 block w-full rounded-lg border border-line bg-bg px-3 py-2 text-sm text-text sm:w-72"
        >
          {SCALES.map((option) => (
            <option key={option.id} value={option.id}>
              {option.name}
            </option>
          ))}
        </select>
      </div>

      <div className="mt-5 overflow-x-auto rounded-lg border border-line bg-surface p-3">
        <Fretboard
          scale={scale}
          rootPitchClass={rootPitchClass}
          notation={notation}
          tuning={tuning}
        />
      </div>
    </div>
  );
}
