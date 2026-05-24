"use client";

import { useEffect, useRef, useState } from "react";
import {
  type Marker,
  type RecordingMeta,
  formatDuration,
  recordingFileName,
  withMarker,
  withoutMarker,
} from "@/lib/recordings";
import { getRecording } from "@/lib/recordingsDb";
import { useRecordingsStore } from "@/stores/recordingsStore";

function formatDate(ms: number): string {
  return new Date(ms).toLocaleString("cs-CZ", {
    day: "numeric",
    month: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/**
 * One recorded take: editable name, date and length, basic playback (play /
 * pause, seek bar) and delete. The audio blob is fetched from IndexedDB only
 * when playback first starts, and the object URL is released on unmount.
 */
export function RecordingItem({ recording }: { recording: RecordingMeta }) {
  const removeRecording = useRecordingsStore((state) => state.removeRecording);
  const renameRecording = useRecordingsStore(
    (state) => state.renameRecording,
  );
  const setMarkers = useRecordingsStore((state) => state.setMarkers);

  const audioRef = useRef<HTMLAudioElement>(null);
  const urlRef = useRef<string | null>(null);

  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [editing, setEditing] = useState(false);
  const [draftName, setDraftName] = useState(recording.name);

  useEffect(() => {
    return () => {
      if (urlRef.current) URL.revokeObjectURL(urlRef.current);
    };
  }, []);

  // Loads the blob from IndexedDB and points the audio element at it. Returns
  // false if the recording could not be retrieved.
  async function ensureLoaded(): Promise<boolean> {
    if (urlRef.current) return true;
    const full = await getRecording(recording.id);
    if (!full) return false;
    const url = URL.createObjectURL(full.blob);
    urlRef.current = url;
    if (audioRef.current) audioRef.current.src = url;
    return true;
  }

  async function togglePlay() {
    const audio = audioRef.current;
    if (!audio) return;
    if (isPlaying) {
      audio.pause();
      return;
    }
    if (await ensureLoaded()) {
      void audio.play();
    }
  }

  function seek(value: number) {
    const audio = audioRef.current;
    if (audio && urlRef.current) audio.currentTime = value;
    setCurrentTime(value);
  }

  function saveName() {
    const name = draftName.trim();
    if (name && name !== recording.name) {
      void renameRecording(recording.id, name);
    } else {
      setDraftName(recording.name);
    }
    setEditing(false);
  }

  function handleDelete() {
    if (window.confirm(`Smazat nahrávku „${recording.name}"?`)) {
      void removeRecording(recording.id);
    }
  }

  function addMarker() {
    const defaultLabel = `Místo ${recording.markers.length + 1}`;
    const input = window.prompt("Pojmenuj místo:", defaultLabel);
    if (input === null) return;
    const marker: Marker = {
      id: crypto.randomUUID(),
      timeSec: currentTime,
      label: input.trim() || defaultLabel,
    };
    void setMarkers(recording.id, withMarker(recording.markers, marker));
  }

  function removeMarker(id: string) {
    void setMarkers(recording.id, withoutMarker(recording.markers, id));
  }

  async function jumpToMarker(timeSec: number) {
    if (await ensureLoaded()) {
      seek(timeSec);
    }
  }

  async function handleExport() {
    const full = await getRecording(recording.id);
    if (!full) return;
    const url = URL.createObjectURL(full.blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = recordingFileName(recording.name, recording.mimeType);
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }

  return (
    <div className="rounded-lg border border-neutral-800 bg-neutral-900 p-4">
      <audio
        ref={audioRef}
        hidden
        onTimeUpdate={(event) =>
          setCurrentTime(event.currentTarget.currentTime)
        }
        onPlay={() => setIsPlaying(true)}
        onPause={() => setIsPlaying(false)}
        onEnded={() => setIsPlaying(false)}
      />

      <div className="flex items-start justify-between gap-3">
        {editing ? (
          <div className="flex flex-1 flex-wrap items-center gap-2">
            <input
              type="text"
              value={draftName}
              onChange={(event) => setDraftName(event.target.value)}
              aria-label="Název nahrávky"
              className="min-w-0 flex-1 rounded-md border border-neutral-700 bg-neutral-950 px-2 py-1 text-sm"
            />
            <button
              type="button"
              onClick={saveName}
              className="rounded-md bg-amber-500 px-3 py-1 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
            >
              Uložit
            </button>
            <button
              type="button"
              onClick={() => {
                setDraftName(recording.name);
                setEditing(false);
              }}
              className="rounded-md border border-neutral-700 px-3 py-1 text-sm text-neutral-400 transition-colors hover:border-neutral-600"
            >
              Zrušit
            </button>
          </div>
        ) : (
          <div className="min-w-0">
            <div className="truncate font-medium">{recording.name}</div>
            <div className="mt-0.5 text-xs text-neutral-400">
              {formatDate(recording.createdAt)} ·{" "}
              {formatDuration(recording.durationSec)}
            </div>
          </div>
        )}

        {!editing && (
          <div className="flex shrink-0 gap-2">
            <button
              type="button"
              onClick={() => setEditing(true)}
              className="rounded-md border border-neutral-800 bg-neutral-950 px-2.5 py-1 text-xs text-neutral-400 transition-colors hover:border-neutral-700"
            >
              Přejmenovat
            </button>
            <button
              type="button"
              onClick={() => void handleExport()}
              className="rounded-md border border-neutral-800 bg-neutral-950 px-2.5 py-1 text-xs text-neutral-400 transition-colors hover:border-amber-600"
            >
              Exportovat
            </button>
            <button
              type="button"
              onClick={handleDelete}
              className="rounded-md border border-neutral-800 bg-neutral-950 px-2.5 py-1 text-xs text-neutral-400 transition-colors hover:border-red-800 hover:text-red-400"
            >
              Smazat
            </button>
          </div>
        )}
      </div>

      <div className="mt-3 flex items-center gap-3">
        <button
          type="button"
          onClick={() => void togglePlay()}
          className="rounded-lg bg-amber-500 px-4 py-1.5 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
        >
          {isPlaying ? "Pauza" : "Přehrát"}
        </button>
        <span className="text-xs tabular-nums text-neutral-400">
          {formatDuration(currentTime)} /{" "}
          {formatDuration(recording.durationSec)}
        </span>
      </div>

      <input
        type="range"
        min={0}
        max={recording.durationSec || 0}
        step={0.01}
        value={currentTime}
        onChange={(event) => seek(Number(event.target.value))}
        aria-label="Pozice v nahrávce"
        className="mt-2 w-full accent-amber-500"
      />

      <div className="mt-3">
        <div className="flex items-center justify-between gap-2">
          <span className="text-xs text-neutral-400">Markery</span>
          <button
            type="button"
            onClick={addMarker}
            className="rounded-md border border-neutral-800 bg-neutral-950 px-2.5 py-1 text-xs text-neutral-300 transition-colors hover:border-amber-600"
          >
            Přidat marker
          </button>
        </div>

        {recording.markers.length > 0 ? (
          <div className="mt-2 flex flex-wrap gap-1.5">
            {recording.markers.map((marker) => (
              <span
                key={marker.id}
                className="flex items-center gap-1 rounded-md border border-neutral-800 bg-neutral-950 py-1 pr-1 pl-2 text-xs"
              >
                <button
                  type="button"
                  onClick={() => void jumpToMarker(marker.timeSec)}
                  className="text-neutral-200 transition-colors hover:text-amber-500"
                >
                  {marker.label} · {formatDuration(marker.timeSec)}
                </button>
                <button
                  type="button"
                  onClick={() => removeMarker(marker.id)}
                  aria-label={`Smazat marker ${marker.label}`}
                  className="rounded px-1 text-neutral-500 transition-colors hover:text-red-400"
                >
                  ×
                </button>
              </span>
            ))}
          </div>
        ) : (
          <p className="mt-1 text-xs text-neutral-600">
            Zatím žádné markery. Přehrávej a označ si těžká místa.
          </p>
        )}
      </div>
    </div>
  );
}
