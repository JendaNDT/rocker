"use client";

import { RecordingItem } from "@/components/recorder/RecordingItem";
import { useRecordingsStore } from "@/stores/recordingsStore";

/** The list of stored takes, newest first. */
export function RecordingList() {
  const recordings = useRecordingsStore((state) => state.recordings);
  const loaded = useRecordingsStore((state) => state.loaded);

  if (!loaded) {
    return (
      <p className="mt-4 text-sm text-neutral-500">Načítám nahrávky…</p>
    );
  }

  if (recordings.length === 0) {
    return (
      <p className="mt-4 text-sm text-neutral-500">
        Zatím nemáš žádné nahrávky. Zapni vstup a něco si nahraj.
      </p>
    );
  }

  return (
    <div className="mt-4 space-y-2">
      {recordings.map((recording) => (
        <RecordingItem key={recording.id} recording={recording} />
      ))}
    </div>
  );
}
