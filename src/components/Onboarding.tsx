"use client";

import { useEffect, useState } from "react";
import { AudioInputSettings } from "@/components/AudioInputSettings";
import { useSettingsStore } from "@/stores/settingsStore";

const STEP_COUNT = 3;

/**
 * First-run onboarding (Bod 13). A full-screen guide shown until the user
 * finishes or skips it; the choice is persisted via settingsStore.onboarded.
 * Step 2 surfaces the existing AudioInputSettings as the intro audio
 * calibration.
 */
export function Onboarding() {
  const onboarded = useSettingsStore((state) => state.onboarded);
  const setOnboarded = useSettingsStore((state) => state.setOnboarded);

  const [hydrated, setHydrated] = useState(false);
  useEffect(() => {
    setHydrated(true);
  }, []);

  const [step, setStep] = useState(0);

  if (!hydrated || onboarded) return null;

  function finish() {
    setOnboarded(true);
  }

  return (
    <div className="fixed inset-0 z-40 flex flex-col bg-neutral-950">
      <div className="flex items-center justify-between border-b border-neutral-800 p-4">
        <span className="text-sm text-neutral-500">
          Krok {step + 1} / {STEP_COUNT}
        </span>
        <button
          type="button"
          onClick={finish}
          className="text-sm text-neutral-400 transition-colors hover:text-neutral-100"
        >
          Přeskočit
        </button>
      </div>

      <div className="flex-1 overflow-y-auto p-6">
        <div className="mx-auto max-w-xl">
          {step === 0 && (
            <div>
              <h2 className="text-2xl font-bold tracking-tight">
                Vítej v Rockeru
              </h2>
              <p className="mt-3 text-neutral-300">
                Rocker je tvůj parťák pro učení a hraní na elektrickou kytaru.
                Naladíš se, cvičíš s metronomem, projdeš lekce a riffy, sleduješ
                pokrok a procvičíš rockové i metalové techniky.
              </p>
              <p className="mt-3 text-neutral-400">
                Pojď si appku ve dvou krocích nastavit — ať za chvíli hraješ
                první riff.
              </p>
            </div>
          )}

          {step === 1 && (
            <div>
              <h2 className="text-2xl font-bold tracking-tight">
                Připoj kytaru
              </h2>
              <p className="mt-3 text-neutral-300">
                Na počítači připoj kytaru přes zvukovku, na mobilu stačí
                mikrofon. Povol přístup a vyber vstup — appka tak uslyší, co
                hraješ.
              </p>
              <div className="mt-4">
                <AudioInputSettings />
              </div>
              <p className="mt-3 text-sm text-neutral-500">
                Tohle můžeš kdykoliv změnit v Profilu. Když chceš, klidně
                přeskoč a nastav to později.
              </p>
            </div>
          )}

          {step === 2 && (
            <div>
              <h2 className="text-2xl font-bold tracking-tight">
                Tvůj první riff
              </h2>
              <p className="mt-3 text-neutral-300">
                Jsi připravený. Začni v Ladičce — nalaď kytaru — a pak v
                Knihovně otevři záložku Riffy. Zkus jednoduchý riff jako Smoke
                on the Water: zpomal si tempo, sleduj ukazatel a hraj podle
                tabu.
              </p>
              <p className="mt-3 text-neutral-400">
                Až ti to půjde, mrkni na obrazovku Metal — power chordy, gallop
                a palm mute.
              </p>
            </div>
          )}
        </div>
      </div>

      <div className="flex items-center justify-between gap-3 border-t border-neutral-800 p-4">
        <button
          type="button"
          disabled={step === 0}
          onClick={() => setStep((current) => current - 1)}
          className="rounded-lg border border-neutral-800 bg-neutral-900 px-5 py-2.5 text-sm transition-colors hover:border-neutral-700 disabled:opacity-40"
        >
          Zpět
        </button>
        {step < STEP_COUNT - 1 ? (
          <button
            type="button"
            onClick={() => setStep((current) => current + 1)}
            className="rounded-lg bg-amber-500 px-6 py-2.5 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
          >
            Dál
          </button>
        ) : (
          <button
            type="button"
            onClick={finish}
            className="rounded-lg bg-amber-500 px-6 py-2.5 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
          >
            Jdeme na to
          </button>
        )}
      </div>
    </div>
  );
}
