"use client";

import { useState } from "react";
import { EarTraining } from "@/components/games/EarTraining";
import { FretboardGame } from "@/components/games/FretboardGame";

/** Which game is shown. */
type Mode = "fretboard" | "ear";

const TAB_ACTIVE =
  "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent";
const TAB_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

export default function GamesPage() {
  const [mode, setMode] = useState<Mode>("fretboard");

  return (
    <div className="mx-auto max-w-3xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">Hry</h1>
      <p className="mt-1 text-dim">
        Procvič si orientaci po hmatníku a sluch.
      </p>

      <div className="mt-6 inline-flex rounded-lg border border-line p-1">
        <button
          type="button"
          onClick={() => setMode("fretboard")}
          className={mode === "fretboard" ? TAB_ACTIVE : TAB_IDLE}
        >
          Hmatník
        </button>
        <button
          type="button"
          onClick={() => setMode("ear")}
          className={mode === "ear" ? TAB_ACTIVE : TAB_IDLE}
        >
          Sluch
        </button>
      </div>

      <div className="mt-4">
        {mode === "fretboard" ? <FretboardGame /> : <EarTraining />}
      </div>
    </div>
  );
}
