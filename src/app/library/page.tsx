"use client";

import { useState } from "react";
import { ChordCard } from "@/components/library/ChordCard";
import { RiffLibrary } from "@/components/library/RiffLibrary";
import { ScaleExplorer } from "@/components/library/ScaleExplorer";
import { PageContainer } from "@/components/PageContainer";
import { CHORD_CATEGORY_LABELS, CHORDS } from "@/lib/chords";
import type { ChordCategory } from "@/lib/chords";
import { useResolvedLayoutMode } from "@/lib/layout";
import { useHydrated } from "@/lib/useHydrated";
import { useSettingsStore } from "@/stores/settingsStore";

type Tab = "chords" | "scales" | "riffs";

const CATEGORIES: ChordCategory[] = ["open", "barre", "power"];

const SEGMENT_ACTIVE =
  "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

// Chord grid columns, following the Auto / Desktop / Mobile switch so the
// column count stays consistent with the rest of the layout.
const CHORD_GRID = {
  desktop: "mt-5 grid grid-cols-5 gap-3",
  mobile: "mt-5 grid grid-cols-2 gap-3",
  auto: "mt-5 grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5",
};

export default function LibraryPage() {
  const [tab, setTab] = useState<Tab>("chords");
  const [category, setCategory] = useState<ChordCategory>("open");

  const handedness = useSettingsStore((state) => state.handedness);
  const setHandedness = useSettingsStore((state) => state.setHandedness);
  const notation = useSettingsStore((state) => state.notation);
  const hydrated = useHydrated();
  const layoutMode = useResolvedLayoutMode();
  const mirror = hydrated && handedness === "left";
  const activeNotation = hydrated ? notation : "english";

  const visibleChords = CHORDS.filter((chord) => chord.category === category);

  return (
    <PageContainer>
      <h1 className="text-3xl font-bold tracking-tight">Knihovna</h1>
      <p className="mt-1 text-dim">Akordy, stupnice a riffy.</p>

      <div className="mt-6 inline-flex rounded-lg border border-line p-1">
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
            <div className="inline-flex rounded-lg border border-line p-1">
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

            <div className="inline-flex rounded-lg border border-line p-1">
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

          <div className={CHORD_GRID[layoutMode]}>
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
    </PageContainer>
  );
}
