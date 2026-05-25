"use client";

import { useEffect } from "react";
import { AbCompare } from "@/components/recorder/AbCompare";
import { RecorderControls } from "@/components/recorder/RecorderControls";
import { RecordingList } from "@/components/recorder/RecordingList";
import { useRecordingsStore } from "@/stores/recordingsStore";

export default function RecorderPage() {
  const load = useRecordingsStore((state) => state.load);
  const loaded = useRecordingsStore((state) => state.loaded);

  // Load the stored recordings from IndexedDB once, on first open.
  useEffect(() => {
    if (!loaded) void load();
  }, [loaded, load]);

  return (
    <div className="mx-auto max-w-2xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">Nahrávání</h1>
      <p className="mt-1 text-dim">
        Nahraj svoji hru a porovnej jednotlivé pokusy.
      </p>

      <div className="mt-6">
        <RecorderControls />
      </div>

      <h2 className="mt-8 text-lg font-semibold">Tvoje nahrávky</h2>
      <RecordingList />

      <h2 className="mt-8 text-lg font-semibold">Porovnání A/B</h2>
      <AbCompare />
    </div>
  );
}
