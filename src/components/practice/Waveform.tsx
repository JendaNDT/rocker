"use client";

import { useEffect, useRef } from "react";

const CANVAS_W = 800;
const CANVAS_H = 96;

/**
 * Static waveform drawn on a canvas, with the playhead and the A/B loop
 * region overlaid as positioned elements. Clicking seeks.
 */
export function Waveform({
  peaks,
  duration,
  currentTime,
  loopStart,
  loopEnd,
  onSeek,
}: {
  peaks: number[];
  duration: number;
  currentTime: number;
  loopStart: number | null;
  loopEnd: number | null;
  onSeek: (time: number) => void;
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (!canvas || !context) return;

    context.clearRect(0, 0, CANVAS_W, CANVAS_H);
    if (peaks.length === 0) return;

    const barWidth = CANVAS_W / peaks.length;
    context.fillStyle = "#525252";
    peaks.forEach((peak, index) => {
      const barHeight = Math.max(1, peak * CANVAS_H);
      context.fillRect(
        index * barWidth,
        (CANVAS_H - barHeight) / 2,
        Math.max(1, barWidth - 1),
        barHeight,
      );
    });
  }, [peaks]);

  const toPercent = (time: number) =>
    duration > 0 ? Math.max(0, Math.min(100, (time / duration) * 100)) : 0;

  function handleClick(event: React.MouseEvent<HTMLDivElement>) {
    if (duration <= 0) return;
    const rect = event.currentTarget.getBoundingClientRect();
    const ratio = (event.clientX - rect.left) / rect.width;
    onSeek(Math.max(0, Math.min(1, ratio)) * duration);
  }

  const hasLoop =
    loopStart !== null && loopEnd !== null && loopEnd > loopStart;

  return (
    <div
      onClick={handleClick}
      className="relative h-24 w-full cursor-pointer overflow-hidden rounded-lg border border-neutral-800 bg-neutral-950"
    >
      <canvas
        ref={canvasRef}
        width={CANVAS_W}
        height={CANVAS_H}
        className="h-full w-full"
      />

      {hasLoop && (
        <div
          className="pointer-events-none absolute inset-y-0 border-x border-amber-500 bg-amber-500/15"
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
  );
}
