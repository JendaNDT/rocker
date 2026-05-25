"use client";

import { useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { DEFAULT_TUNING_ID, getTuning, TUNINGS } from "@/audio/tunings";
import { GuidedTuner } from "@/components/tuner/GuidedTuner";
import { TunerDisplay } from "@/components/tuner/TunerDisplay";
import { useTuner } from "@/components/tuner/useTuner";
import { useWakeLock } from "@/components/useWakeLock";
import { useHydrated } from "@/lib/useHydrated";
import { useAudioStore } from "@/stores/audioStore";
import { useSettingsStore } from "@/stores/settingsStore";

type TunerMode = "guided" | "chromatic";

export default function TunerPage() {
  useWakeLock();

  const status = useAudioStore((state) => state.status);
  const permission = useAudioStore((state) => state.permission);
  const error = useAudioStore((state) => state.error);
  const storedTuningId = useSettingsStore((state) => state.tuningId);
  const setTuningId = useSettingsStore((state) => state.setTuningId);
  const notation = useSettingsStore((state) => state.notation);
  const { reading } = useTuner();
  const hydrated = useHydrated();

  const [mode, setMode] = useState<TunerMode>("guided");

  const running = status === "running";
  // Before hydration use the defaults so the prerendered HTML matches.
  const tuningId = hydrated ? storedTuningId : DEFAULT_TUNING_ID;
  const tuning = getTuning(tuningId);
  const activeNotation = hydrated ? notation : "english";

  function startTuner() {
    void (async () => {
      if (permission !== "granted") {
        const result = await audioEngine.requestPermission();
        if (result !== "granted") return;
      }
      await audioEngine.start();
    })();
  }

  function stopTuner() {
    void audioEngine.stop();
  }

  return (
    <div className="mx-auto max-w-2xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">Ladička</h1>
      <p className="mt-1 text-dim">
        Zahraj strunu a slaď značku na střed.
      </p>

      <div className="mt-6 flex flex-wrap items-center gap-3">
        <div className="inline-flex rounded-lg border border-line p-1">
          <button
            type="button"
            onClick={() => setMode("guided")}
            className={
              mode === "guided"
                ? "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent"
                : "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text"
            }
          >
            Vedený
          </button>
          <button
            type="button"
            onClick={() => setMode("chromatic")}
            className={
              mode === "chromatic"
                ? "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent"
                : "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text"
            }
          >
            Chromatický
          </button>
        </div>

        {mode === "guided" && (
          <select
            value={tuningId}
            onChange={(event) => setTuningId(event.target.value)}
            aria-label="Ladění"
            className="rounded-lg border border-line bg-bg px-3 py-2 text-sm text-text"
          >
            {TUNINGS.map((option) => (
              <option key={option.id} value={option.id}>
                {option.label}
              </option>
            ))}
          </select>
        )}
      </div>

      <div className="mt-6">
        {running ? (
          <>
            {mode === "guided" ? (
              <GuidedTuner
                tuning={tuning}
                reading={reading}
                notation={activeNotation}
              />
            ) : (
              <TunerDisplay reading={reading} notation={activeNotation} />
            )}
            <div className="mt-6 flex items-center gap-3">
              <button
                type="button"
                onClick={stopTuner}
                className="rounded-lg border border-line bg-surface px-4 py-2 text-sm transition-colors hover:border-line-strong"
              >
                Zastavit ladičku
              </button>
              <span className="text-sm text-accent">Ladička běží</span>
            </div>
          </>
        ) : (
          <div className="rounded-lg border border-line bg-surface p-6">
            <p className="text-sm text-text">
              Spusť ladičku a zahraj jednotlivé struny. Vstupní zařízení se dá
              vybrat v nastavení (ozubené kolo).
            </p>
            {permission === "denied" && (
              <p className="mt-2 text-sm text-danger">
                Přístup k mikrofonu je zamítnutý — povol ho v prohlížeči a zkus
                to znovu.
              </p>
            )}
            <button
              type="button"
              onClick={startTuner}
              className="mt-4 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
            >
              {status === "starting" ? "Spouštím…" : "Spustit ladičku"}
            </button>
          </div>
        )}
        {error && <p className="mt-3 text-sm text-danger">{error}</p>}
      </div>
    </div>
  );
}
