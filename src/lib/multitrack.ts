// Multi-track project model — pure logic, no DOM or audio. Part of Bod 17.
// Unit-tested in multitrack.test.ts. Every helper is immutable: it returns a
// new project rather than mutating the given one.

/** One recorded track in a multi-track project. */
export interface Track {
  id: string;
  /** Czech display name, e.g. "Stopa 1". */
  name: string;
  /** The recorded audio. */
  blob: Blob;
  /** MIME type of the blob. */
  mimeType: string;
  /** Wall-clock length of the track in seconds. */
  durationSec: number;
  /** Playback gain, 0..1. */
  gain: number;
  /** Whether the track is silenced during playback. */
  muted: boolean;
}

/** Track metadata without the audio blob — for lists and UI. */
export type TrackMeta = Omit<Track, "blob">;

/** A multi-track recording project. */
export interface MultitrackProject {
  tracks: Track[];
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** Clamps a value into 0..1; non-finite input becomes 0. */
function clamp01(value: number): number {
  if (!Number.isFinite(value)) return 0;
  return Math.max(0, Math.min(1, value));
}

/** A fresh project with no tracks. */
export function emptyProject(): MultitrackProject {
  return { tracks: [] };
}

/** A display name for the next track ("Stopa 1", "Stopa 2", …). */
export function nextTrackName(project: MultitrackProject): string {
  return `Stopa ${project.tracks.length + 1}`;
}

/** Appends a track to the project. */
export function addTrack(
  project: MultitrackProject,
  track: Track,
): MultitrackProject {
  return { tracks: [...project.tracks, track] };
}

/** Removes the track with the given id. */
export function removeTrack(
  project: MultitrackProject,
  id: string,
): MultitrackProject {
  return { tracks: project.tracks.filter((track) => track.id !== id) };
}

/** Applies a patch to one track, leaving the others untouched. */
function patchTrack(
  project: MultitrackProject,
  id: string,
  patch: Partial<Track>,
): MultitrackProject {
  return {
    tracks: project.tracks.map((track) =>
      track.id === id ? { ...track, ...patch } : track,
    ),
  };
}

/** Sets whether a track is muted. */
export function setTrackMuted(
  project: MultitrackProject,
  id: string,
  muted: boolean,
): MultitrackProject {
  return patchTrack(project, id, { muted });
}

/** Sets a track's playback gain (clamped to 0..1). */
export function setTrackGain(
  project: MultitrackProject,
  id: string,
  gain: number,
): MultitrackProject {
  return patchTrack(project, id, { gain: clamp01(gain) });
}

/** Renames a track. */
export function renameTrack(
  project: MultitrackProject,
  id: string,
  name: string,
): MultitrackProject {
  return patchTrack(project, id, { name });
}

/** Tracks that should be heard — non-muted and with audible gain. */
export function audibleTracks(project: MultitrackProject): Track[] {
  return project.tracks.filter((track) => !track.muted && track.gain > 0);
}

/** Strips the audio blob, leaving just the metadata. */
export function toTrackMeta(track: Track): TrackMeta {
  const { blob: _blob, ...meta } = track;
  void _blob;
  return meta;
}

// --- Type guards ----------------------------------------------------------
// Data read back from storage can be corrupt or written by an older app
// version. Every loaded value passes through these guards; broken records
// are dropped so the app never crashes on bad data (see AGENTS.md).

/** Coerces an unknown value into a Track, or null if it is malformed. */
export function sanitizeTrack(value: unknown): Track | null {
  if (!isPlainObject(value)) return null;
  const { id, name, blob, mimeType, durationSec, gain, muted } = value;
  if (typeof id !== "string") return null;
  if (typeof name !== "string") return null;
  if (!(blob instanceof Blob)) return null;
  if (typeof mimeType !== "string") return null;
  if (
    typeof durationSec !== "number" ||
    !Number.isFinite(durationSec) ||
    durationSec < 0
  ) {
    return null;
  }
  return {
    id,
    name,
    blob,
    mimeType,
    durationSec,
    gain:
      typeof gain === "number" && Number.isFinite(gain) ? clamp01(gain) : 1,
    muted: muted === true,
  };
}

/**
 * Coerces an unknown value into a MultitrackProject, dropping malformed
 * tracks. Always returns a usable project.
 */
export function sanitizeProject(value: unknown): MultitrackProject {
  if (!isPlainObject(value)) return emptyProject();
  if (!Array.isArray(value.tracks)) return emptyProject();
  return {
    tracks: value.tracks
      .map(sanitizeTrack)
      .filter((track): track is Track => track !== null),
  };
}
