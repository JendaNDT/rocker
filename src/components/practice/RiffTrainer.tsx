"use client";

import { useEffect, useRef, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { playCountIn } from "@/audio/countIn";
import { Waveform } from "@/components/practice/Waveform";
import { computePeaks } from "@/lib/waveform";

const MIN_SPEED = 0.5;
const MAX_SPEED = 1.25;
const SPEED_STEP = 0.05;
const WAVEFORM_BUCKETS = 240;
const COUNT_IN_BEATS = 4;

function formatTime(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds < 0) return "0:00";
  const minutes = Math.floor(seconds / 60);
  const rest = Math.floor(seconds % 60);
  return `${minutes}:${rest.toString().padStart(2, "0")}`;
}

function roundSpeed(value: number): number {
  return Math.round(value * 100) / 100;
}

/**
 * Riff trainer — loads a local audio file and plays it back with a
 * pitch-preserving speed control, A/B looping, a waveform overview, a
 * count-in and an optional gradual tempo ramp.
 */
export function RiffTrainer() {
  const audioRef = useRef<HTMLAudioElement>(null);
  const objectUrlRef = useRef<string | null>(null);
  const loopCountRef = useRef(0);
  const mountedRef = useRef(true);

  const [fileName, setFileName] = useState<string | null>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [countingIn, setCountingIn] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [speed, setSpeed] = useState(1);
  const [peaks, setPeaks] = useState<number[]>([]);
  const [loopStart, setLoopStart] = useState<number | null>(null);
  const [loopEnd, setLoopEnd] = useState<number | null>(null);
  const [countInEnabled, setCountInEnabled] = useState(false);
  const [countInBpm, setCountInBpm] = useState(90);
  const [rampEnabled, setRampEnabled] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Release the object URL and mark unmounted when the component goes away.
  useEffect(() => {
    return () => {
      mountedRef.current = false;
      if (objectUrlRef.current) URL.revokeObjectURL(objectUrlRef.current);
    };
  }, []);

  function applySpeed(value: number) {
    const audio = audioRef.current;
    if (audio) {
      audio.preservesPitch = true;
      audio.playbackRate = value;
    }
  }

  // While playing, keep the A/B loop tight and apply the optional tempo ramp.
  useEffect(() => {
    if (!isPlaying) return;
    if (loopStart === null || loopEnd === null || loopEnd <= loopStart) return;

    loopCountRef.current = 0;
    let raf = 0;
    const tick = () => {
      const audio = audioRef.current;
      if (audio && audio.currentTime >= loopEnd) {
        audio.currentTime = loopStart;
        loopCountRef.current += 1;
        if (rampEnabled && loopCountRef.current % 2 === 0) {
          setSpeed((prev) => {
            const next = Math.min(MAX_SPEED, roundSpeed(prev + SPEED_STEP));
            applySpeed(next);
            return next;
          });
        }
      }
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [isPlaying, loopStart, loopEnd, rampEnabled]);

  async function decodeWaveform(file: File) {
    try {
      const arrayBuffer = await file.arrayBuffer();
      const context = await audioEngine.getOutputContext();
      const audioBuffer = await context.decodeAudioData(arrayBuffer);
      setPeaks(computePeaks(audioBuffer.getChannelData(0), WAVEFORM_BUCKETS));
    } catch {
      setPeaks([]);
    }
  }

  function handleFile(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    if (objectUrlRef.current) URL.revokeObjectURL(objectUrlRef.current);

    const url = URL.createObjectURL(file);
    objectUrlRef.current = url;
    setFileName(file.name);
    setCurrentTime(0);
    setDuration(0);
    setIsPlaying(false);
    setPeaks([]);
    setLoopStart(null);
    setLoopEnd(null);
    setError(null);

    const audio = audioRef.current;
    if (audio) {
      audio.src = url;
      audio.load();
    }
    void decodeWaveform(file);
  }

  function togglePlay() {
    const audio = audioRef.current;
    if (!audio || !fileName || countingIn) return;
    if (!audio.paused) {
      audio.pause();
      return;
    }
    if (countInEnabled) {
      setCountingIn(true);
      void playCountIn(COUNT_IN_BEATS, countInBpm).then(() => {
        if (!mountedRef.current) return;
        setCountingIn(false);
        void audio.play();
      });
    } else {
      void audio.play();
    }
  }

  function seek(value: number) {
    const audio = audioRef.current;
    if (audio) audio.currentTime = value;
    setCurrentTime(value);
  }

  function changeSpeed(value: number) {
    setSpeed(value);
    applySpeed(value);
  }

  const loopReady =
    loopStart !== null && loopEnd !== null && loopEnd > loopStart;

  const playLabel = countingIn ? "Odpočet…" : isPlaying ? "Pauza" : "Přehrát";

  return (
    <div>
      <audio
        ref={audioRef}
        hidden
        onTimeUpdate={(event) => setCurrentTime(event.currentTarget.currentTime)}
        onLoadedMetadata={(event) => {
          setDuration(event.currentTarget.duration);
          event.currentTarget.preservesPitch = true;
          event.currentTarget.playbackRate = speed;
        }}
        onPlay={() => setIsPlaying(true)}
        onPause={() => setIsPlaying(false)}
        onEnded={() => setIsPlaying(false)}
        onError={() => {
          setIsPlaying(false);
          setError(
            "Soubor se nepodařilo přehrát — zkus jiný (podporované jsou hlavně MP3 a WAV).",
          );
        }}
      />

      <label className="inline-block cursor-pointer rounded-lg border border-line bg-surface px-4 py-2 text-sm transition-colors hover:border-accent">
        Vybrat skladbu (MP3 / WAV)
        <input
          type="file"
          accept="audio/*"
          onChange={handleFile}
          className="hidden"
        />
      </label>

      {!fileName && (
        <p className="mt-3 text-sm text-muted">
          Zatím není vybraná žádná skladba. Nahraj si vlastní soubor a procvičuj
          si těžké pasáže pomalu.
        </p>
      )}

      {fileName && (
        <div className="mt-4 rounded-lg border border-line bg-surface p-5">
          <p className="truncate text-sm text-dim">{fileName}</p>
          {error && <p className="mt-2 text-sm text-danger">{error}</p>}

          <div className="mt-4">
            <Waveform
              peaks={peaks}
              duration={duration}
              currentTime={currentTime}
              loopStart={loopStart}
              loopEnd={loopEnd}
              onSeek={seek}
            />
          </div>

          <div className="mt-4 flex items-center gap-4">
            <button
              type="button"
              onClick={togglePlay}
              disabled={countingIn}
              className="rounded-lg bg-accent px-6 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90 disabled:opacity-60"
            >
              {playLabel}
            </button>
            <span className="text-sm tabular-nums text-dim">
              {formatTime(currentTime)} / {formatTime(duration)}
            </span>
          </div>

          <input
            type="range"
            min={0}
            max={duration || 0}
            step={0.01}
            value={currentTime}
            onChange={(event) => seek(Number(event.target.value))}
            aria-label="Pozice ve skladbě"
            className="mt-4 w-full accent-accent"
          />

          <div className="mt-5 flex items-center gap-3">
            <span className="w-24 text-sm text-text">
              Rychlost {Math.round(speed * 100)} %
            </span>
            <input
              type="range"
              min={MIN_SPEED}
              max={MAX_SPEED}
              step={SPEED_STEP}
              value={speed}
              onChange={(event) => changeSpeed(Number(event.target.value))}
              aria-label="Rychlost přehrávání"
              className="flex-1 accent-accent"
            />
          </div>

          <div className="mt-5 border-t border-line pt-4">
            <div className="text-sm font-medium text-text">
              Smyčka A–B
            </div>
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => setLoopStart(currentTime)}
                className="rounded-lg border border-line bg-bg px-3 py-1.5 text-sm transition-colors hover:border-line-strong"
              >
                Bod A {loopStart !== null && `(${formatTime(loopStart)})`}
              </button>
              <button
                type="button"
                onClick={() => setLoopEnd(currentTime)}
                className="rounded-lg border border-line bg-bg px-3 py-1.5 text-sm transition-colors hover:border-line-strong"
              >
                Bod B {loopEnd !== null && `(${formatTime(loopEnd)})`}
              </button>
              <button
                type="button"
                onClick={() => {
                  setLoopStart(null);
                  setLoopEnd(null);
                }}
                className="rounded-lg border border-line bg-bg px-3 py-1.5 text-sm text-dim transition-colors hover:border-line-strong"
              >
                Zrušit smyčku
              </button>
            </div>
            <p className="mt-2 text-xs text-muted">
              {loopReady
                ? "Smyčka je aktivní — přehrávač se vrací z bodu B do bodu A."
                : "Nastav bod A a bod B (B musí být za A) a přehrávač bude pasáž opakovat."}
            </p>
          </div>

          <div className="mt-5 space-y-3 border-t border-line pt-4">
            <div className="text-sm font-medium text-text">
              Odpočet a zrychlování
            </div>

            <label className="flex items-center gap-2 text-sm text-text">
              <input
                type="checkbox"
                checked={countInEnabled}
                onChange={(event) => setCountInEnabled(event.target.checked)}
                className="h-4 w-4 accent-accent"
              />
              Odpočet před spuštěním (4 doby)
            </label>

            {countInEnabled && (
              <div className="flex items-center gap-3 pl-6">
                <span className="w-28 text-sm text-dim">
                  Tempo odpočtu {countInBpm}
                </span>
                <input
                  type="range"
                  min={60}
                  max={160}
                  step={1}
                  value={countInBpm}
                  onChange={(event) =>
                    setCountInBpm(Number(event.target.value))
                  }
                  aria-label="Tempo odpočtu"
                  className="flex-1 accent-accent"
                />
              </div>
            )}

            <label className="flex items-center gap-2 text-sm text-text">
              <input
                type="checkbox"
                checked={rampEnabled}
                onChange={(event) => setRampEnabled(event.target.checked)}
                className="h-4 w-4 accent-accent"
              />
              Postupně zrychlovat (+5 % každé 2 opakování smyčky)
            </label>
          </div>
        </div>
      )}
    </div>
  );
}
