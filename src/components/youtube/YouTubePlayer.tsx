"use client";

import { useEffect, useState } from "react";
import { useYouTubePlayer } from "@/components/youtube/useYouTubePlayer";
import { useResolvedLayoutMode } from "@/lib/layout";
import { useHydrated } from "@/lib/useHydrated";
import { extractVideoId } from "@/lib/youtube";
import { useYtLoopStore } from "@/stores/ytLoopStore";

const PLAYER_ID = "rocker-yt-player";
const RATES = [0.25, 0.5, 0.75, 1, 1.25, 1.5];
const POLL_MS = 120;

// Video next to its controls on desktop, stacked on mobile. The player div
// keeps a fixed position in the DOM so the YouTube iframe is never remounted.
const VIDEO_GRID = {
  desktop: "mt-4 grid grid-cols-2 items-start gap-x-8",
  mobile: "mt-4",
  auto: "mt-4 lg:grid lg:grid-cols-2 lg:items-start lg:gap-x-8",
};

function formatTime(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds < 0) return "0:00";
  const minutes = Math.floor(seconds / 60);
  const rest = Math.floor(seconds % 60);
  return `${minutes}:${rest.toString().padStart(2, "0")}`;
}

/**
 * YouTube practice player — load a video by URL and practise with speed
 * control, an A/B loop and saved sections. Extracted from the former
 * /youtube page so it can be hosted in the "Hraj se skladbou" tab of the
 * practice screen (screen-consolidation plan, fáze C).
 */
export function YouTubePlayer() {
  const player = useYouTubePlayer(PLAYER_ID, () =>
    setError(
      "Tohle video se nepodařilo přehrát — může být soukromé, smazané nebo nedovoluje přehrávání mimo YouTube.",
    ),
  );
  const { getCurrentTime, getDuration, seekTo } = player;

  const loops = useYtLoopStore((state) => state.loops);
  const addLoop = useYtLoopStore((state) => state.addLoop);
  const removeLoop = useYtLoopStore((state) => state.removeLoop);
  const hydrated = useHydrated();
  const layoutMode = useResolvedLayoutMode();

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
    if (
      !loopReady ||
      videoId === null ||
      loopStart === null ||
      loopEnd === null
    ) {
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
    <>
      <div className="mt-6 flex flex-wrap gap-2">
        <input
          type="text"
          value={urlInput}
          onChange={(event) => setUrlInput(event.target.value)}
          placeholder="Odkaz na YouTube video"
          className="min-w-0 flex-1 rounded-lg border border-line bg-bg px-3 py-2 text-sm text-text"
        />
        <button
          type="button"
          onClick={loadVideo}
          disabled={!player.ready}
          className="rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90 disabled:opacity-60"
        >
          Načíst
        </button>
      </div>

      {error && <p className="mt-2 text-sm text-danger">{error}</p>}
      {!player.ready && (
        <p className="mt-2 text-sm text-muted">Načítám přehrávač…</p>
      )}

      <div className={VIDEO_GRID[layoutMode]}>
        <div>
          <div className="aspect-video w-full overflow-hidden rounded-lg border border-line bg-black">
            <div id={PLAYER_ID} className="h-full w-full" />
          </div>

          {videoId !== null && (
            <>
              <div
                onClick={handleBarClick}
                className="relative mt-4 h-3 cursor-pointer overflow-hidden rounded-full bg-surface-2"
              >
                {loopReady && (
                  <div
                    className="pointer-events-none absolute inset-y-0 border-x border-accent bg-accent/20"
                    style={{
                      left: `${toPercent(loopStart)}%`,
                      width: `${toPercent(loopEnd) - toPercent(loopStart)}%`,
                    }}
                  />
                )}
                <div
                  className="pointer-events-none absolute inset-y-0 w-0.5 bg-accent"
                  style={{ left: `${toPercent(currentTime)}%` }}
                />
              </div>
              <div className="mt-1 text-sm tabular-nums text-muted">
                {formatTime(currentTime)} / {formatTime(duration)}
              </div>
            </>
          )}
        </div>

        <div>
          {videoId === null ? (
            <p className="mt-4 text-sm text-muted">
              Načti video a objeví se ovládání rychlosti a smyček A–B.
            </p>
          ) : (
            <>
              <div className="mt-4">
                <div className="text-sm font-medium text-text">Rychlost</div>
                <div className="mt-2 flex flex-wrap gap-2">
                  {RATES.map((value) => (
                    <button
                      key={value}
                      type="button"
                      onClick={() => changeRate(value)}
                      className={
                        rate === value
                          ? "rounded-md bg-accent px-3 py-1.5 text-sm font-semibold text-accent-fg"
                          : "rounded-md border border-line bg-surface px-3 py-1.5 text-sm text-dim transition-colors hover:text-text"
                      }
                    >
                      {Math.round(value * 100)} %
                    </button>
                  ))}
                </div>
              </div>

              <div className="mt-5 border-t border-line pt-4">
                <div className="text-sm font-medium text-text">Smyčka A–B</div>
                <div className="mt-2 flex flex-wrap items-center gap-2">
                  <button
                    type="button"
                    onClick={() => setLoopStart(currentTime)}
                    className="rounded-lg border border-line bg-surface px-3 py-1.5 text-sm transition-colors hover:border-line-strong"
                  >
                    Bod A {loopStart !== null && `(${formatTime(loopStart)})`}
                  </button>
                  <button
                    type="button"
                    onClick={() => setLoopEnd(currentTime)}
                    className="rounded-lg border border-line bg-surface px-3 py-1.5 text-sm transition-colors hover:border-line-strong"
                  >
                    Bod B {loopEnd !== null && `(${formatTime(loopEnd)})`}
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setLoopStart(null);
                      setLoopEnd(null);
                    }}
                    className="rounded-lg border border-line bg-surface px-3 py-1.5 text-sm text-dim transition-colors hover:border-line-strong"
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
                      className="min-w-0 flex-1 rounded-lg border border-line bg-bg px-3 py-1.5 text-sm text-text"
                    />
                    <button
                      type="button"
                      onClick={saveLoop}
                      className="rounded-lg bg-accent px-3 py-1.5 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
                    >
                      Uložit úsek
                    </button>
                  </div>
                )}
              </div>

              {savedLoops.length > 0 && (
                <div className="mt-5 border-t border-line pt-4">
                  <div className="text-sm font-medium text-text">
                    Uložené úseky
                  </div>
                  <div className="mt-2 space-y-2">
                    {savedLoops.map((loop) => (
                      <div
                        key={loop.id}
                        className="flex items-center justify-between rounded-lg border border-line bg-surface px-3 py-2"
                      >
                        <div className="min-w-0">
                          <div className="truncate text-sm font-medium">
                            {loop.label}
                          </div>
                          <div className="text-xs text-muted">
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
                            className="rounded-md border border-line bg-bg px-2.5 py-1 text-xs transition-colors hover:border-accent"
                          >
                            Načíst
                          </button>
                          <button
                            type="button"
                            onClick={() => removeLoop(loop.id)}
                            className="rounded-md border border-line bg-bg px-2.5 py-1 text-xs text-dim transition-colors hover:border-line-strong"
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
      </div>
    </>
  );
}
