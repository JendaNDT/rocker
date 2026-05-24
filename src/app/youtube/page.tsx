"use client";

import { useEffect, useState } from "react";
import { useYouTubePlayer } from "@/components/youtube/useYouTubePlayer";
import { useWakeLock } from "@/components/useWakeLock";
import { useHydrated } from "@/lib/useHydrated";
import { extractVideoId } from "@/lib/youtube";
import { useYtLoopStore } from "@/stores/ytLoopStore";

const PLAYER_ID = "rocker-yt-player";
const RATES = [0.25, 0.5, 0.75, 1, 1.25, 1.5];
const POLL_MS = 120;

function formatTime(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds < 0) return "0:00";
  const minutes = Math.floor(seconds / 60);
  const rest = Math.floor(seconds % 60);
  return `${minutes}:${rest.toString().padStart(2, "0")}`;
}

export default function YoutubePage() {
  useWakeLock();

  const player = useYouTubePlayer(PLAYER_ID);
  const { getCurrentTime, getDuration, seekTo } = player;

  const loops = useYtLoopStore((state) => state.loops);
  const addLoop = useYtLoopStore((state) => state.addLoop);
  const removeLoop = useYtLoopStore((state) => state.removeLoop);
  const hydrated = useHydrated();

  const [urlInput, setUrlInput] = useState("");
  const [videoId, setVideoId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [rate, setRate] = useState(1);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [loopStart, setLoopStart] = useState<number | null>(null);
  const [loopEnd, setLoopEnd] = useState<number | null>(null);
  const [labelInput, setLabelInput] = useState("");

  // Poll the player for the current time and keep the A/B loop tight.
  useEffect(() => {
    if (videoId === null) return;
    const id = setInterval(() => {
      const time = getCurrentTime();
      setCurrentTime(time);
      setDuration(getDuration());
      if (loopStart !== null && loopEnd !== null && loopEnd > loopStart) {
        if (time >= loopEnd) seekTo(loopStart);
      }
    }, POLL_MS);
    return () => clearInterval(id);
  }, [videoId, loopStart, loopEnd, getCurrentTime, getDuration, seekTo]);

  function loadVideo() {
    const id = extractVideoId(urlInput);
    if (!id) {
      setError("Tohle nevypadá jako odkaz na YouTube video.");
      return;
    }
    setError(null);
    player.load(id);
    player.setRate(rate);
    setLoopStart(null);
    setLoopEnd(null);
    setVideoId(id);
  }

  function changeRate(value: number) {
    setRate(value);
    player.setRate(value);
  }

  function toPercent(time: number): number {
    return duration > 0
      ? Math.max(0, Math.min(100, (time / duration) * 100))
      : 0;
  }

  function handleBarClick(event: React.MouseEvent<HTMLDivElement>) {
    if (duration <= 0) return;
    const rect = event.currentTarget.getBoundingClientRect();
    const ratio = (event.clientX - rect.left) / rect.width;
    seekTo(Math.max(0, Math.min(1, ratio)) * duration);
  }

  const loopReady =
    loopStart !== null && loopEnd !== null && loopEnd > loopStart;

  function saveLoop() {
    if (!loopReady || videoId === null || loopStart === null || loopEnd === null) {
      return;
    }
    addLoop({
      videoId,
      label: labelInput.trim() || "Úsek",
      start: loopStart,
      end: loopEnd,
      rate,
    });
    setLabelInput("");
  }

  function applyLoop(start: number, end: number, savedRate: number) {
    setLoopStart(start);
    setLoopEnd(end);
    changeRate(savedRate);
    seekTo(start);
  }

  const savedLoops =
    hydrated && videoId !== null
      ? loops.filter((loop) => loop.videoId === videoId)
      : [];

  return (
    <div className="mx-auto max-w-3xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">
        YouTube Practice Player
      </h1>
      <p className="mt-1 text-neutral-400">
        Cvič podle libovolného videa — vlož odkaz na YouTube.
      </p>

      <div className="mt-6 flex flex-wrap gap-2">
        <input
          type="text"
          value={urlInput}
          onChange={(event) => setUrlInput(event.target.value)}
          placeholder="Odkaz na YouTube video"
          className="min-w-0 flex-1 rounded-lg border border-neutral-800 bg-neutral-950 px-3 py-2 text-sm text-neutral-100"
        />
        <button
          type="button"
          onClick={loadVideo}
          disabled={!player.ready}
          className="rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400 disabled:opacity-60"
        >
          Načíst
        </button>
      </div>

      {error && <p className="mt-2 text-sm text-red-400">{error}</p>}
      {!player.ready && (
        <p className="mt-2 text-sm text-neutral-500">Načítám přehrávač…</p>
      )}

      <div className="mt-4 aspect-video w-full overflow-hidden rounded-lg border border-neutral-800 bg-black">
        <div id={PLAYER_ID} className="h-full w-full" />
      </div>

      {videoId !== null && (
        <>
          <div
            onClick={handleBarClick}
            className="relative mt-4 h-3 cursor-pointer overflow-hidden rounded-full bg-neutral-800"
          >
            {loopReady && (
              <div
                className="pointer-events-none absolute inset-y-0 border-x border-amber-500 bg-amber-500/20"
                style={{
                  left: `${toPercent(loopStart)}%`,
                  width: `${toPercent(loopEnd) - toPercent(loopStart)}%`,
                }}
              />
            )}
            <div
              className="pointer-events-none absolute inset-y-0 w-0.5 bg-amber-400"
              style={{ left: `${toPercent(currentTime)}%` }}
            />
          </div>
          <div className="mt-1 text-sm tabular-nums text-neutral-500">
            {formatTime(currentTime)} / {formatTime(duration)}
          </div>

          <div className="mt-4">
            <div className="text-sm font-medium text-neutral-300">
              Rychlost
            </div>
            <div className="mt-2 flex flex-wrap gap-2">
              {RATES.map((value) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => changeRate(value)}
                  className={
                    rate === value
                      ? "rounded-md bg-amber-500 px-3 py-1.5 text-sm font-semibold text-neutral-950"
                      : "rounded-md border border-neutral-800 bg-neutral-900 px-3 py-1.5 text-sm text-neutral-400 transition-colors hover:text-neutral-100"
                  }
                >
                  {Math.round(value * 100)} %
                </button>
              ))}
            </div>
          </div>

          <div className="mt-5 border-t border-neutral-800 pt-4">
            <div className="text-sm font-medium text-neutral-300">
              Smyčka A–B
            </div>
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => setLoopStart(currentTime)}
                className="rounded-lg border border-neutral-800 bg-neutral-900 px-3 py-1.5 text-sm transition-colors hover:border-neutral-700"
              >
                Bod A {loopStart !== null && `(${formatTime(loopStart)})`}
              </button>
              <button
                type="button"
                onClick={() => setLoopEnd(currentTime)}
                className="rounded-lg border border-neutral-800 bg-neutral-900 px-3 py-1.5 text-sm transition-colors hover:border-neutral-700"
              >
                Bod B {loopEnd !== null && `(${formatTime(loopEnd)})`}
              </button>
              <button
                type="button"
                onClick={() => {
                  setLoopStart(null);
                  setLoopEnd(null);
                }}
                className="rounded-lg border border-neutral-800 bg-neutral-900 px-3 py-1.5 text-sm text-neutral-400 transition-colors hover:border-neutral-700"
              >
                Zrušit smyčku
              </button>
            </div>

            {loopReady && (
              <div className="mt-3 flex flex-wrap gap-2">
                <input
                  type="text"
                  value={labelInput}
                  onChange={(event) => setLabelInput(event.target.value)}
                  placeholder="Název úseku (např. sólo 1)"
                  className="min-w-0 flex-1 rounded-lg border border-neutral-800 bg-neutral-950 px-3 py-1.5 text-sm text-neutral-100"
                />
                <button
                  type="button"
                  onClick={saveLoop}
                  className="rounded-lg bg-amber-500 px-3 py-1.5 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
                >
                  Uložit úsek
                </button>
              </div>
            )}
          </div>

          {savedLoops.length > 0 && (
            <div className="mt-5 border-t border-neutral-800 pt-4">
              <div className="text-sm font-medium text-neutral-300">
                Uložené úseky
              </div>
              <div className="mt-2 space-y-2">
                {savedLoops.map((loop) => (
                  <div
                    key={loop.id}
                    className="flex items-center justify-between rounded-lg border border-neutral-800 bg-neutral-900 px-3 py-2"
                  >
                    <div className="min-w-0">
                      <div className="truncate text-sm font-medium">
                        {loop.label}
                      </div>
                      <div className="text-xs text-neutral-500">
                        {formatTime(loop.start)}–{formatTime(loop.end)} ·{" "}
                        {Math.round(loop.rate * 100)} %
                      </div>
                    </div>
                    <div className="ml-3 flex shrink-0 gap-2">
                      <button
                        type="button"
                        onClick={() =>
                          applyLoop(loop.start, loop.end, loop.rate)
                        }
                        className="rounded-md border border-neutral-800 bg-neutral-950 px-2.5 py-1 text-xs transition-colors hover:border-amber-600"
                      >
                        Načíst
                      </button>
                      <button
                        type="button"
                        onClick={() => removeLoop(loop.id)}
                        className="rounded-md border border-neutral-800 bg-neutral-950 px-2.5 py-1 text-xs text-neutral-400 transition-colors hover:border-neutral-700"
                      >
                        Smazat
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
