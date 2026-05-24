"use client";

import { useEffect, useState } from "react";
import { type LoopState, loopPedal } from "@/audio/loopPedal";

const PRIMARY_BUTTON =
  "rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400 disabled:cursor-not-allowed disabled:opacity-50";
const SECONDARY_BUTTON =
  "rounded-lg border border-neutral-800 bg-neutral-950 px-4 py-2 text-sm transition-colors hover:border-neutral-700 disabled:cursor-not-allowed disabled:opacity-50";

/**
 * Loop pedal for Bod 17. Records a base loop, plays it back on repeat, and
 * overdubs further layers over the running loop. Mirrors the engine's state
 * (see src/audio/loopPedal.ts) to drive the controls.
 */
export function LoopPedal() {
  const [state, setState] = useState<LoopState>("idle");
  const [layers, setLayers] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Stop and clear the loop when leaving the screen.
  useEffect(() => {
    return () => loopPedal.clear();
  }, []);

  async function recordBase() {
    if (busy) return;
    setBusy(true);
    setError(null);
    const ok = await loopPedal.startBase();
    setBusy(false);
    if (!ok) {
      setError(
        "Nepodařilo se spustit nahrávání. Povol mikrofon v Profilu.",
      );
      return;
    }
    setState("recordingBase");
  }

  async function stopBase() {
    if (busy) return;
    setBusy(true);
    await loopPedal.stopBase();
    setBusy(false);
    setState(loopPedal.getState());
    setLayers(loopPedal.getLayerCount());
  }

  async function recordOverdub() {
    if (busy) return;
    setBusy(true);
    setError(null);
    const ok = await loopPedal.startOverdub();
    setBusy(false);
    if (!ok) {
      setError("Nepodařilo se spustit nahrávání vrstvy.");
      return;
    }
    setState("recordingOverdub");
  }

  async function stopOverdub() {
    if (busy) return;
    setBusy(true);
    await loopPedal.stopOverdub();
    setBusy(false);
    setState(loopPedal.getState());
    setLayers(loopPedal.getLayerCount());
  }

  function clear() {
    loopPedal.clear();
    setState("idle");
    setLayers(0);
    setError(null);
  }

  return (
    <div className="rounded-lg border border-neutral-800 bg-neutral-900 p-4">
      <div className="rounded-lg border border-amber-600/40 bg-amber-500/10 p-3 text-sm text-amber-200">
        Nahrávej do sluchátek. Při hraní přes reproduktory se hrající smyčka
        nahraje znovu do nové vrstvy.
      </div>

      <div className="mt-4">
        {state === "idle" && (
          <button
            type="button"
            onClick={recordBase}
            disabled={busy}
            className={PRIMARY_BUTTON}
          >
            {busy ? "Spouštím…" : "Nahrát smyčku"}
          </button>
        )}
        {state === "recordingBase" && (
          <button
            type="button"
            onClick={stopBase}
            disabled={busy}
            className={PRIMARY_BUTTON}
          >
            Zastavit a spustit smyčku
          </button>
        )}
        {state === "looping" && (
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={recordOverdub}
              disabled={busy}
              className={PRIMARY_BUTTON}
            >
              Přidat vrstvu
            </button>
            <button
              type="button"
              onClick={clear}
              className={SECONDARY_BUTTON}
            >
              Smazat smyčku
            </button>
          </div>
        )}
        {state === "recordingOverdub" && (
          <button
            type="button"
            onClick={stopOverdub}
            disabled={busy}
            className={PRIMARY_BUTTON}
          >
            Zastavit vrstvu
          </button>
        )}
      </div>

      <p className="mt-3 text-sm text-neutral-400">
        {state === "idle" &&
          "Nahraj krátkou smyčku — po zastavení se hned začne opakovat."}
        {state === "recordingBase" &&
          "Nahrávám smyčku… zahraj jeden cyklus a zastav."}
        {state === "looping" && `Smyčka hraje. Vrstev: ${layers}.`}
        {state === "recordingOverdub" &&
          "Nahrávám vrstvu přes hrající smyčku… zastav na konci cyklu."}
      </p>

      {error && <p className="mt-3 text-sm text-red-400">{error}</p>}
    </div>
  );
}
