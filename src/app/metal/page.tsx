"use client";

import { useState } from "react";
import { AmpWidget } from "@/components/amp/AmpWidget";
import { GallopTrainer } from "@/components/metal/GallopTrainer";
import { PalmMuteStudio } from "@/components/metal/PalmMuteStudio";
import { PowerChordTrainer } from "@/components/metal/PowerChordTrainer";
import { PageContainer } from "@/components/PageContainer";
import { useWakeLock } from "@/components/useWakeLock";

type Tab = "power" | "gallop" | "palm";

const TABS: { id: Tab; label: string }[] = [
  { id: "power", label: "Power chord" },
  { id: "gallop", label: "Gallop" },
  { id: "palm", label: "Palm mute" },
];

const SEGMENT_ACTIVE =
  "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

/**
 * Metal screen — home for the Bod 12 rock/metal trainers: the Power Chord
 * Trainer, the Gallop Trainer and the Palm Mute Studio.
 */
export default function MetalPage() {
  useWakeLock();

  const [tab, setTab] = useState<Tab>("power");

  return (
    <PageContainer size="focused">
      <h1 className="text-3xl font-bold tracking-tight">Metal</h1>
      <p className="mt-1 text-dim">
        Trenažéry pro rockovou a metalovou hru.
      </p>

      <div className="mt-6">
        <AmpWidget />
      </div>

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

      <div className="mt-8">
        {tab === "power" && <PowerChordTrainer />}
        {tab === "gallop" && <GallopTrainer />}
        {tab === "palm" && <PalmMuteStudio />}
      </div>
    </PageContainer>
  );
}
