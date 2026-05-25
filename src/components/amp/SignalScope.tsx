"use client";

import { useEffect, useRef } from "react";

// Logical canvas resolution; CSS stretches it to the container width.
const CANVAS_W = 600;
const CANVAS_H = 140;

/**
 * Oscilloscope visualiser — draws the live waveform read from a Web Audio
 * analyser onto a canvas. Part of the amp expansion (C2); fed by
 * AmpChain.outputAnalyser, so it shows the processed (post-effects) signal.
 */
export function SignalScope({
  analyser,
  active,
}: {
  analyser: AnalyserNode | null;
  active: boolean;
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!active || !analyser || !canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    // Theme colours — read once from the CSS variables (see theme-tokens.css).
    const styles = getComputedStyle(document.documentElement);
    const waveColor = styles.getPropertyValue("--accent").trim() || "#c8a35a";
    const lineColor = styles.getPropertyValue("--line").trim() || "#2a261f";

    const buffer = new Uint8Array(analyser.fftSize);
    let raf = 0;

    const draw = () => {
      analyser.getByteTimeDomainData(buffer);
      ctx.clearRect(0, 0, CANVAS_W, CANVAS_H);

      // Centre (silence) line.
      ctx.strokeStyle = lineColor;
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(0, CANVAS_H / 2);
      ctx.lineTo(CANVAS_W, CANVAS_H / 2);
      ctx.stroke();

      // Waveform — byte data runs 0..255 with 128 as the zero crossing.
      ctx.strokeStyle = waveColor;
      ctx.lineWidth = 2;
      ctx.beginPath();
      const step = CANVAS_W / buffer.length;
      for (let i = 0; i < buffer.length; i += 1) {
        const x = i * step;
        const y = (buffer[i] / 255) * CANVAS_H;
        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      }
      ctx.stroke();

      raf = requestAnimationFrame(draw);
    };
    draw();

    return () => cancelAnimationFrame(raf);
  }, [active, analyser]);

  return (
    <div className="rounded-lg border border-line bg-bg p-2">
      <canvas
        ref={canvasRef}
        width={CANVAS_W}
        height={CANVAS_H}
        className="h-auto w-full"
        aria-label="Vizualizér zvuku — průběh signálu"
      />
    </div>
  );
}
