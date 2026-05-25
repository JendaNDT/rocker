"use client";

import { useEffect, useState } from "react";
import { LoopPedal } from "@/components/looper/LoopPedal";
import { AbCompare } from "@/components/recorder/AbCompare";
import { RecorderControls } from "@/components/recorder/RecorderControls";
import { RecordingList } from "@/components/recorder/RecordingList";
import { Studio } from "@/components/studio/Studio";
import { PageContainer } from "@/components/PageContainer";
import { SplitColumns } from "@/components/SplitColumns";
import { useRecordingsStore } from "@/stores/recordingsStore";

type Tab = "recordings" | "studio" | "looper";

const TABS: { id: Tab; label: string }[] = [
  { id: "recordings", label: "Nahrávky" },
  { id: "studio", label: "Studio" },
  { id: "looper", label: "Looper" },
];

const SEGMENT_ACTIVE =
  "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

/**
 * Unified "Nahrávání" screen — recorder takes, the multi-track studio and the
 * looper under one roof with tabs (screen-consolidation plan, fáze D).
 */
export default function RecorderPage() {
  const load = useRecordingsStore((state) => state.load);
  const loaded = useRecordingsStore((state) => state.loaded);
  const [tab, setTab] = useState<Tab>("recordings");

  // Load the stored recordings from IndexedDB once, on first open.
  useEffect(() => {
    if (!loaded) void load();
  }, [loaded, load]);

  return (
    <PageContainer>
      <h1 className="text-3xl font-bold tracking-tight">Nahrávání</h1>
      <p className="mt-1 text-dim">
        Nahraj a porovnej si hru, vrstvi stopy ve Studiu nebo si zacykluj
        nápad v Looperu.
      </p>

      <div className="mt-6 inline-flex flex-wrap rounded-lg border border-line p-1">
        {TABS.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => setTab(item.id)}
            className={tab === item.id ? SEGMENT_ACTIVE : SEGMENT_IDLE}
          >
            {item.label}
          </button>
        ))}
      </div>

      {tab === "recordings" && (
        <>
          <div className="mt-6">
            <RecorderControls />
          </div>
          <SplitColumns
            left={
              <>
                <h2 className="mt-8 text-lg font-semibold">Tvoje nahrávky</h2>
                <RecordingList />
              </>
            }
            right={
              <>
                <h2 className="mt-8 text-lg font-semibold">Porovnání A/B</h2>
                <AbCompare />
              </>
            }
          />
        </>
      )}

      {tab === "studio" && (
        <div className="mt-6">
          <Studio />
        </div>
      )}

      {tab === "looper" && (
        <div className="mt-6">
          <LoopPedal />
        </div>
      )}
    </PageContainer>
  );
}
