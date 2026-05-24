// Recordings — pure logic + types, no DOM. Unit-tested in recordings.test.ts.
// Part of Bod 11: take capture, playback, A/B compare, markers and export.

/** A marked position inside a recording. */
export interface Marker {
  id: string;
  /** Position in the recording, in seconds. */
  timeSec: number;
  /** Czech user label. */
  label: string;
}

/** Metadata for a recording — everything except the audio blob. */
export interface RecordingMeta {
  id: string;
  /** User-facing name. */
  name: string;
  /** Unix-ms timestamp of when the take was recorded. */
  createdAt: number;
  /** Length of the take in seconds. */
  durationSec: number;
  /** MIME type of the audio blob, e.g. "audio/webm". */
  mimeType: string;
  /** Marked positions, kept sorted by time. */
  markers: Marker[];
}

/** A full recording: metadata plus the captured audio blob. */
export interface Recording extends RecordingMeta {
  blob: Blob;
}

/** Formats a duration in seconds as "m:ss". */
export function formatDuration(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds < 0) return "0:00";
  const minutes = Math.floor(seconds / 60);
  const rest = Math.floor(seconds % 60);
  return `${minutes}:${rest.toString().padStart(2, "0")}`;
}

/** Returns a copy of the markers sorted by time, ascending. */
export function sortMarkers(markers: Marker[]): Marker[] {
  return [...markers].sort((a, b) => a.timeSec - b.timeSec);
}

/** Adds a marker and returns the new, time-sorted list. */
export function withMarker(markers: Marker[], marker: Marker): Marker[] {
  return sortMarkers([...markers, marker]);
}

/** Removes the marker with the given id. */
export function withoutMarker(markers: Marker[], id: string): Marker[] {
  return markers.filter((marker) => marker.id !== id);
}

/** File extension for a recording MIME type, including the leading dot. */
export function extensionForMimeType(mimeType: string): string {
  const lower = mimeType.toLowerCase();
  if (lower.includes("ogg")) return ".ogg";
  if (lower.includes("mp4") || lower.includes("m4a")) return ".m4a";
  if (lower.includes("mpeg")) return ".mp3";
  if (lower.includes("wav")) return ".wav";
  return ".webm";
}

/**
 * A safe download filename for a recording: the name with characters that are
 * illegal in filenames replaced by "_", plus the format extension.
 */
export function recordingFileName(name: string, mimeType: string): string {
  const illegal = /[\\/:*?"<>|]/g;
  const cleaned = name.replace(illegal, "_").trim();
  const safeName = cleaned.length > 0 ? cleaned : "nahravka";
  return `${safeName}${extensionForMimeType(mimeType)}`;
}

/** Strips the audio blob, leaving just the metadata. */
export function toMeta(recording: Recording): RecordingMeta {
  return {
    id: recording.id,
    name: recording.name,
    createdAt: recording.createdAt,
    durationSec: recording.durationSec,
    mimeType: recording.mimeType,
    markers: recording.markers,
  };
}

// --- Type guards ----------------------------------------------------------
// Records read back from IndexedDB can be corrupt or written by an older app
// version. Every loaded value passes through these guards; broken records are
// dropped so the app never crashes on bad data (see AGENTS.md).

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** Coerces an unknown value into a Marker, or null if it is malformed. */
export function sanitizeMarker(value: unknown): Marker | null {
  if (!isPlainObject(value)) return null;
  const { id, timeSec, label } = value;
  if (typeof id !== "string") return null;
  if (typeof timeSec !== "number" || !Number.isFinite(timeSec) || timeSec < 0) {
    return null;
  }
  if (typeof label !== "string") return null;
  return { id, timeSec, label };
}

/** Coerces an unknown value into a Recording, or null if it is malformed. */
export function sanitizeRecording(value: unknown): Recording | null {
  if (!isPlainObject(value)) return null;
  const { id, name, createdAt, durationSec, mimeType, markers, blob } = value;
  if (typeof id !== "string") return null;
  if (typeof name !== "string") return null;
  if (typeof createdAt !== "number" || !Number.isFinite(createdAt)) return null;
  if (
    typeof durationSec !== "number" ||
    !Number.isFinite(durationSec) ||
    durationSec < 0
  ) {
    return null;
  }
  if (typeof mimeType !== "string") return null;
  if (!(blob instanceof Blob)) return null;

  const safeMarkers = Array.isArray(markers)
    ? markers
        .map(sanitizeMarker)
        .filter((marker): marker is Marker => marker !== null)
    : [];

  return {
    id,
    name,
    createdAt,
    durationSec,
    mimeType,
    markers: sortMarkers(safeMarkers),
    blob,
  };
}
