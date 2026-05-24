"use client";

import { useEffect, useState } from "react";
import { multitrackEngine } from "@/audio/multitrackEngine";
import {
  type MultitrackProject,
  type Track,
  addTrack,
  audibleTracks,
  emptyProject,
  nextTrackName,
  removeTrack,
  setTrackGain,
  setTrackMuted,
} from "@/lib/multitrack";
import { formatDuration } from "@/lib/recordings";

const PRIMARY_BUTTON =
  "rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400 disabled:cursor-not-allowed disabled:opacity-50";
const SECONDARY_BUTTON =
  "rounded-lg border border-neutral-800 bg-neutral-950 px-4 py-2 text-sm transition-colors hover:border-neutral-700 disabled:cursor-not-allowed disabled:opacity-50";
const SMALL_BUTTON =
  "rounded-md border border-neutral-800 bg-neutral-950 px-2.5 py-1 text-xs transition-colors hover:border-neutral-700 disabled:cursor-not-allowed disabled:opacity-50";

/**
 * Multi-track studio for Bod 17. Records tracks over each other (overdub),
 * plays them together, and lets each track be muted, leveled or deleted.
 * The project lives in memory for this session; export comes in a later step.
 */
export function Studio() {
  const [project, setProject] = useState<MultitrackProject>(emptyProject());
  const [recording, setRecording] = useState(false);
  const [playing, setPlaying] = useState(false);
  const [starting, setStarting] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Stop any audio when leaving the screen.
  useEffect(() => {
    return () => {
      multitrackEngine.stopPlayback();
      void multitrackEngine.stopRecording();
    };
  }, []);

  const busy = recording || playing;

  async function startRecording() {
    if (starting || busy) return;
    setStarting(true);
    setError(null);
    const ok = await multitrackEngine.startRecording(audibleTracks(project));
    setStarting(false);
    if (!ok) {
      setError(
        "Nepodařilo se spustit nahrávání. Povol mikrofon v Profilu.",
      );
      return;
    }
    setRecording(true);
  }

  async function stopRecording() {
    if (!recording) return;
    setRecording(false);
    const result = await multitrackEngine.stopRecording();
    if (!result) return;
    setProject((current) => {
      const track: Track = {
        id: crypto.randomUUID(),
        name: nextTrackName(current),
        blob: result.blob,
        mimeType: result.mimeType,
        durationSec: result.durationSec,
        gain: 1,
        muted: false,
      };
      return addTrack(current, track);
    });
  }

  async function playAll() {
    if (busy) return;
    const tracks = audibleTracks(project);
    if (tracks.length === 0) return;
    const ok = await multitrackEngine.play(tracks, () => setPlaying(false));
    if (ok) setPlaying(true);
  }

  function stopPlay() {
    multitrackEngine.stopPlayback();
    setPlaying(false);
  }

  async function exportMix() {
    if (busy || exporting) return;
    const tracks = audibleTracks(project);
    if (tracks.length === 0) {
      setError("Žádné slyšitelné stopy k exportu.");
      return;
    }
    setExporting(true);
    setError(null);
    const blob = await multitrackEngine.mixdown(tracks);
    setExporting(false);
    if (!blob) {
      setError("Mixdown se nepodařil.");
      return;
    }
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "rocker-mix.wav";
    link.click();
    URL.revokeObjectURL(url);
  }

  return (
    <div className="rounded-lg border border-neutral-800 bg-neutral-900 p-4">
      <div className="rounded-lg border border-amber-600/40 bg-amber-500/10 p-3 text-sm text-amber-200">
        Nahrávej do sluchátek. Při hraní přes reproduktory se předchozí stopy
        nahrají znovu do té nové.
      </div>

      <div className="mt-4 flex flex-wrap gap-2">
        <button
          type="button"
          onClick={recording ? stopRecording : startRecording}
          disabled={starting || playing}
          className={recording ? SECONDARY_BUTTON : PRIMARY_BUTTON}
        >
          {starting
            ? "Spouštím…"
            : recording
              ? "Zastavit nahrávání"
              : "Nahrát stopu"}
        </button>
        <button
          type="button"
          onClick={playing ? stopPlay : playAll}
          disabled={recording || project.tracks.length === 0}
          className={SECONDARY_BUTTON}
        >
          {playing ? "Zastavit" : "Přehrát vše"}
        </button>
        <button
          type="button"
          onClick={exportMix}
          disabled={busy || exporting || project.tracks.length === 0}
          className={SECONDARY_BUTTON}
        >
          {exporting ? "Exportuji…" : "Exportovat mix (WAV)"}
        </button>
      </div>

      {recording && (
        <p className="mt-3 text-sm text-amber-500">
          Nahrávám… předchozí stopy ti hrají do sluchátek.
        </p>
      )}
      {error && <p className="mt-3 text-sm text-red-400">{error}</p>}

      {project.tracks.length === 0 ? (
        <p className="mt-5 text-sm text-neutral-500">
          Zatím žádné stopy. Klikni na „Nahrát stopu" a nahraj první vrstvu —
          třeba akordový podklad.
        </p>
      ) : (
        <div className="mt-5 space-y-2">
          {project.tracks.map((track, index) => (
            <div
              key={track.id}
              className="rounded-lg border border-neutral-800 bg-neutral-950 p-3"
            >
              <div className="flex items-center justify-between gap-3">
                <div className="min-w-0">
                  <div className="truncate text-sm font-medium">
                    {index + 1}. {track.name}
                  </div>
                  <div className="text-xs text-neutral-500">
                    {formatDuration(track.durationSec)}
                  </div>
                </div>
                <div className="flex shrink-0 gap-2">
                  <button
                    type="button"
                    onClick={() =>
                      setProject((p) =>
                        setTrackMuted(p, track.id, !track.muted),
                      )
                    }
                    disabled={busy}
                    className={SMALL_BUTTON}
                  >
                    {track.muted ? "Ztlumeno" : "Ztlumit"}
                  </button>
                  <button
                    type="button"
                    onClick={() =>
                      setProject((p) => removeTrack(p, track.id))
                    }
                    disabled={busy}
                    className={SMALL_BUTTON}
                  >
                    Smazat
                  </button>
                </div>
              </div>
              <div className="mt-2 flex items-center gap-2">
                <span className="text-xs text-neutral-500">Hlasitost</span>
                <input
                  type="range"
                  min={0}
                  max={100}
                  value={Math.round(track.gain * 100)}
                  disabled={busy}
                  onChange={(event) =>
                    setProject((p) =>
                      setTrackGain(
                        p,
                        track.id,
                        Number(event.target.value) / 100,
                      ),
                    )
                  }
                  className="w-full accent-amber-500"
                />
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
