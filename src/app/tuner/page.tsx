"use client";

import { useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { DEFAULT_TUNING_ID, TUNINGS } from "@/audio/tunings";
import { CustomTuningEditor } from "@/components/tuner/CustomTuningEditor";
import { GuidedTuner } from "@/components/tuner/GuidedTuner";
import { TunerDisplay } from "@/components/tuner/TunerDisplay";
import { useTuner } from "@/components/tuner/useTuner";
import { PageContainer } from "@/components/PageContainer";
import { useWakeLock } from "@/hooks/useWakeLock";
import { resolveTuning } from "@/lib/resolveTuning";
import { useHydrated } from "@/hooks/useHydrated";
import { useAudioStore } from "@/stores/audioStore";
import {
  isCustomTuningId,
  useCustomTuningsStore,
} from "@/stores/customTuningsStore";
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
  const customTunings = useCustomTuningsStore((state) => state.tunings);
  const removeCustomTuning = useCustomTuningsStore(
    (state) => state.removeTuning,
  );
  const { reading } = useTuner();
  const hydrated = useHydrated();

  const [mode, setMode] = useState<TunerMode>("guided");
  const [editing, setEditing] = useState(false);

  const running = status === "running";
  // Before hydration use the defaults so the prerendered HTML matches.
  const tuningId = hydrated ? storedTuningId : DEFAULT_TUNING_ID;
  const tuning = resolveTuning(tuningId, hydrated ? customTunings : []);
  const activeNotation = hydrated ? notation : "english";
  const isCustomActive = hydrated && isCustomTuningId(tuningId);

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

  function handleSaved(newId: string): void {
    setTuningId(newId);
    setEditing(false);
  }

  function handleDeleteCustom(): void {
    if (!isCustomActive) return;
    const confirmed =
      typeof window !== "undefined" &&
      window.confirm(
        `Smazat vlastní ladění „${tuning.label}"? Tahle akce se nedá vrátit.`,
      );
    if (!confirmed) return;
    removeCustomTuning(tuningId);
    // Reset to the factory default so no zombie id stays in storage.
    setTuningId(DEFAULT_TUNING_ID);
  }

  return (
    <PageContainer size="focused">
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
            <optgroup label="Tovární">
              {TUNINGS.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.label}
                </option>
              ))}
            </optgroup>
            {hydrated && customTunings.length > 0 && (
              <optgroup label="Vlastní">
                {customTunings.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.label}
                  </option>
                ))}
              </optgroup>
            )}
          </select>
        )}
      </div>

      {mode === "guided" && !editing && (
        <div className="mt-3 flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={() => setEditing(true)}
            className="rounded-lg border border-line bg-surface px-3 py-1.5 text-sm text-text transition-colors hover:border-line-strong"
          >
            Vytvořit vlastní ladění…
          </button>
          {isCustomActive && (
            <button
              type="button"
              onClick={handleDeleteCustom}
              className="rounded-lg border border-line bg-surface px-3 py-1.5 text-sm text-danger transition-colors hover:border-danger"
            >
              Smazat „{tuning.label}"
            </button>
          )}
        </div>
      )}

      {mode === "guided" && editing && (
        <CustomTuningEditor
          baseTuning={tuning}
          notation={activeNotation}
          onSaved={handleSaved}
          onCancel={() => setEditing(false)}
        />
      )}

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
    </PageContainer>
  );
}
