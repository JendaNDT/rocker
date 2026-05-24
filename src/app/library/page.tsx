"use client";

import { useState } from "react";
import { ChordCard } from "@/components/library/ChordCard";
import { RiffLibrary } from "@/components/library/RiffLibrary";
import { ScaleExplorer } from "@/components/library/ScaleExplorer";
import { CHORD_CATEGORY_LABELS, CHORDS } from "@/lib/chords";
import type { ChordCategory } from "@/lib/chords";
import { useHydrated } from "@/lib/useHydrated";
import { useSettingsStore } from "@/stores/settingsStore";

type Tab = "chords" | "scales" | "riffs";

const CATEGORIES: ChordCategory[] = ["open", "barre", "power"];

const SEGMENT_ACTIVE =
  "rounded-md bg-neutral-800 px-3 py-1.5 text-sm text-amber-500";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-neutral-400 transition-colors hover:text-neutral-100";

export default function LibraryPage() {
  const [tab, setTab] = useState<Tab>("chords");
  const [category, setCategory] = useState<ChordCategory>("open");

  const handedness = useSettingsStore((state) => state.handedness);
  const setHandedness = useSettingsStore((state) => state.setHandedness);
  const notation = useSettingsStore((state) => state.notation);
  const hydrated = useHydrated();
  const mirror = hydrated && handedness === "left";
  const activeNotation = hydrated ? notation : "english";

  const visibleChords = CHORDS.filter((chord) => chord.category === category);

  return (
    <div className="mx-auto max-w-4xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">Knihovna</h1>
      <p className="mt-1 text-neutral-400">Akordy, stupnice a riffy.</p>

      <div className="mt-6 inline-flex rounded-lg border border-neutral-800 p-1">
        <button
          type="button"
          onClick={() => setTab("chords")}
          className={tab === "chords" ? SEGMENT_ACTIVE : SEGMENT_IDLE}
        >
          Akordy
        </button>
        <button
          type="button"
          onClick={() => setTab("scales")}
          className={tab === "scales" ? SEGMENT_ACTIVE : SEGMENT_IDLE}
        >
          Stupnice
        </button>
        <button
          type="button"
          onClick={() => setTab("riffs")}
          className={tab === "riffs" ? SEGMENT_ACTIVE : SEGMENT_IDLE}
        >
          Riffy
        </button>
      </div>

      {tab === "chords" && (
        <>
          <div className="mt-6 flex flex-wrap items-center gap-3">
            <div className="inline-flex rounded-lg border border-neutral-800 p-1">
              {CATEGORIES.map((value) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => setCategory(value)}
                  className={category === value ? SEGMENT_ACTIVE : SEGMENT_IDLE}
                >
                  {CHORD_CATEGORY_LABELS[value]}
                </button>
              ))}
            </div>

            <div className="inline-flex rounded-lg border border-neutral-800 p-1">
              <button
                type="button"
                onClick={() => setHandedness("right")}
                className={
                  handedness === "right" ? SEGMENT_ACTIVE : SEGMENT_IDLE
                }
              >
                Praváci
              </button>
              <button
                type="button"
                onClick={() => setHandedness("left")}
                className={
                  handedness === "left" ? SEGMENT_ACTIVE : SEGMENT_IDLE
                }
              >
                Leváci
              </button>
            </div>
          </div>

          <div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4">
            {visibleChords.map((chord) => (
              <ChordCard
                key={chord.id}
                chord={chord}
                mirror={mirror}
                notation={activeNotation}
              />
            ))}
          </div>
        </>
      )}

      {tab === "scales" && (
        <div className="mt-6">
          <ScaleExplorer notation={activeNotation} />
        </div>
      )}

      {tab === "riffs" && (
        <div className="mt-6">
          <RiffLibrary />
        </div>
      )}
    </div>
  );
}
