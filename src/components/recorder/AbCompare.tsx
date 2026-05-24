"use client";

import { useEffect, useRef, useState } from "react";
import { formatDuration } from "@/lib/recordings";
import { getRecording } from "@/lib/recordingsDb";
import { useRecordingsStore } from "@/stores/recordingsStore";

type Side = "A" | "B";

/**
 * A/B comparison of two takes. Each side has its own preloaded <audio>
 * element, so switching A<->B is instant; the playback position carries over
 * so the same moment is heard in both takes.
 */
export function AbCompare() {
  const recordings = useRecordingsStore((state) => state.recordings);

  const [aId, setAId] = useState<string | null>(null);
  const [bId, setBId] = useState<string | null>(null);
  const [active, setActive] = useState<Side>("A");
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);

  const audioA = useRef<HTMLAudioElement>(null);
  const audioB = useRef<HTMLAudioElement>(null);
  const urlA = useRef<string | null>(null);
  const urlB = useRef<string | null>(null);

  // Pick sensible defaults and repair selections that point at a deleted take.
  useEffect(() => {
    const exists = (id: string | null) =>
      id !== null && recordings.some((item) => item.id === id);
    setAId((current) => (exists(current) ? current : recordings[0]?.id ?? null));
    setBId((current) =>
      exists(current)
        ? current
        : recordings[1]?.id ?? recordings[0]?.id ?? null,
    );
  }, [recordings]);

  // Load side A's blob whenever its selection changes.
  useEffect(() => {
    let cancelled = false;
    if (urlA.current) {
      URL.revokeObjectURL(urlA.current);
      urlA.current = null;
    }
    if (aId) {
      void getRecording(aId).then((recording) => {
        if (cancelled || !recording) return;
        const url = URL.createObjectURL(recording.blob);
        urlA.current = url;
        if (audioA.current) audioA.current.src = url;
      });
    }
    return () => {
      cancelled = true;
    };
  }, [aId]);

  // Load side B's blob whenever its selection changes.
  useEffect(() => {
    let cancelled = false;
    if (urlB.current) {
      URL.revokeObjectURL(urlB.current);
      urlB.current = null;
    }
    if (bId) {
      void getRecording(bId).then((recording) => {
        if (cancelled || !recording) return;
        const url = URL.createObjectURL(recording.blob);
        urlB.current = url;
        if (audioB.current) audioB.current.src = url;
      });
    }
    return () => {
      cancelled = true;
    };
  }, [bId]);

  // Release object URLs on unmount.
  useEffect(() => {
    return () => {
      if (urlA.current) URL.revokeObjectURL(urlA.current);
      if (urlB.current) URL.revokeObjectURL(urlB.current);
    };
  }, []);

  if (recordings.length < 2) {
    return (
      <p className="mt-4 text-sm text-neutral-500">
        Na porovnání potřebuješ aspoň dvě nahrávky.
      </p>
    );
  }

  const activeAudio = () => (active === "A" ? audioA.current : audioB.current);

  function togglePlay() {
    const audio = activeAudio();
    if (!audio) return;
    if (isPlaying) {
      audio.pause();
    } else {
      void audio.play();
    }
  }

  function switchTo(side: Side) {
    if (side === active) return;
    const from = activeAudio();
    const to = side === "A" ? audioA.current : audioB.current;
    const wasPlaying = isPlaying;
    const position = from?.currentTime ?? 0;

    from?.pause();
    setActive(side);
    if (to) {
      to.currentTime = position;
      if (wasPlaying) void to.play();
    }
  }

  const activeDuration =
    (active === "A"
      ? recordings.find((item) => item.id === aId)?.durationSec
      : recordings.find((item) => item.id === bId)?.durationSec) ?? 0;

  function renderHandlers(side: Side) {
    return {
      onTimeUpdate: (event: React.SyntheticEvent<HTMLAudioElement>) => {
        if (active === side) setCurrentTime(event.currentTarget.currentTime);
      },
      onPlay: () => {
        if (active === side) setIsPlaying(true);
      },
      onPause: () => {
        if (active === side) setIsPlaying(false);
      },
      onEnded: () => {
        if (active === side) setIsPlaying(false);
      },
    };
  }

  return (
    <div className="mt-4 rounded-lg border border-neutral-800 bg-neutral-900 p-4">
      <audio ref={audioA} hidden {...renderHandlers("A")} />
      <audio ref={audioB} hidden {...renderHandlers("B")} />

      <div className="grid gap-3 sm:grid-cols-2">
        <label className="text-sm">
          <span className="text-neutral-400">Take A</span>
          <select
            value={aId ?? ""}
            onChange={(event) => setAId(event.target.value)}
            className="mt-1 w-full rounded-md border border-neutral-800 bg-neutral-950 px-2 py-1.5 text-sm text-neutral-100"
          >
            {recordings.map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>
        <label className="text-sm">
          <span className="text-neutral-400">Take B</span>
          <select
            value={bId ?? ""}
            onChange={(event) => setBId(event.target.value)}
            className="mt-1 w-full rounded-md border border-neutral-800 bg-neutral-950 px-2 py-1.5 text-sm text-neutral-100"
          >
            {recordings.map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="mt-4 flex items-center gap-3">
        <button
          type="button"
          onClick={togglePlay}
          className="rounded-lg bg-amber-500 px-4 py-1.5 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
        >
          {isPlaying ? "Pauza" : "Přehrát"}
        </button>

        <div className="inline-flex rounded-lg border border-neutral-800 p-1">
          {(["A", "B"] as Side[]).map((side) => (
            <button
              key={side}
              type="button"
              onClick={() => switchTo(side)}
              className={
                active === side
                  ? "rounded-md bg-neutral-800 px-4 py-1 text-sm font-semibold text-amber-500"
                  : "rounded-md px-4 py-1 text-sm text-neutral-400 transition-colors hover:text-neutral-100"
              }
            >
              {side}
            </button>
          ))}
        </div>

        <span className="text-xs tabular-nums text-neutral-400">
          {formatDuration(currentTime)} / {formatDuration(activeDuration)}
        </span>
      </div>

      <p className="mt-3 text-xs text-neutral-500">
        Přepínač A/B drží pozici — slyšíš stejné místo v obou pokusech.
      </p>
    </div>
  );
}
