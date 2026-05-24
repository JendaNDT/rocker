"use client";

import { useState } from "react";
import { Fretboard } from "@/components/library/Fretboard";
import { type Notation, pitchClassName } from "@/lib/pitch";
import { DEFAULT_SCALE_ID, getScale, SCALES } from "@/lib/scales";

/** Root picker + scale picker + fretboard. */
export function ScaleExplorer({ notation }: { notation: Notation }) {
  const [scaleId, setScaleId] = useState<string>(DEFAULT_SCALE_ID);
  const [rootPitchClass, setRootPitchClass] = useState<number>(9); // A

  const scale = getScale(scaleId);

  return (
    <div>
      <div>
        <div className="text-sm font-medium text-neutral-300">
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
                  ? "rounded-md bg-amber-500 px-2.5 py-1 text-sm font-semibold text-neutral-950"
                  : "rounded-md border border-neutral-800 bg-neutral-900 px-2.5 py-1 text-sm text-neutral-400 transition-colors hover:text-neutral-100"
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
          className="text-sm font-medium text-neutral-300"
        >
          Stupnice
        </label>
        <select
          id="scale-select"
          value={scaleId}
          onChange={(event) => setScaleId(event.target.value)}
          className="mt-2 block w-full rounded-lg border border-neutral-800 bg-neutral-950 px-3 py-2 text-sm text-neutral-100 sm:w-72"
        >
          {SCALES.map((option) => (
            <option key={option.id} value={option.id}>
              {option.name}
            </option>
          ))}
        </select>
      </div>

      <div className="mt-5 overflow-x-auto rounded-lg border border-neutral-800 bg-neutral-900 p-3">
        <Fretboard
          scale={scale}
          rootPitchClass={rootPitchClass}
          notation={notation}
        />
      </div>
    </div>
  );
}
