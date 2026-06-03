"use client";

import { useEffect, useState } from "react";
import { audioEngine } from "@/audio/audioEngine";
import { riffPlayer } from "@/audio/riffPlayer";
import { useWakeLock } from "@/hooks/useWakeLock";
import {
  METAL_RIFFS,
  RIFF_DIFFICULTY_LABELS,
  getMetalRiff,
  riffDurationBeats,
} from "@/lib/metalRiffs";
import type { MetalRiff, RiffDifficulty } from "@/lib/metalRiffs";
import { useRiffPlayerStore } from "@/stores/riffPlayerStore";

type DifficultyFilter = "all" | RiffDifficulty;

const DIFFICULTY_FILTERS: { id: DifficultyFilter; label: string }[] = [
  { id: "all", label: "Vše" },
  { id: "beginner", label: "Začátečník" },
  { id: "intermediate", label: "Mírně pokročilý" },
];

const SLOWDOWNS = [
  { label: "50 %", value: 0.5 },
  { label: "75 %", value: 0.75 },
  { label: "100 %", value: 1 },
];

const SEGMENT_ACTIVE =
  "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

// Tab geometry (SVG user units).
const STRING_GAP = 16;
const BEAT_W = 56;
const MARGIN_LEFT = 26;
const MARGIN_TOP = 16;
const MARGIN_RIGHT = 16;
const MARGIN_BOTTOM = 20;
// String labels by index — 0 = low E ... 5 = high E.
const STRING_LABELS = ["E", "A", "D", "G", "B", "e"];

/**
 * SVG tab of a riff, with a playhead driven by the riff player. With `large`
 * the SVG scales to fill its container (used by the music-stand view);
 * otherwise it renders at a fixed size in a horizontally scrollable box.
 */
function RiffTab({
  riff,
  positionBeats,
  isPlaying,
  large = false,
}: {
  riff: MetalRiff;
  positionBeats: number;
  isPlaying: boolean;
  large?: boolean;
}) {
  const loopBeats = riffDurationBeats(riff);
  const width = MARGIN_LEFT + loopBeats * BEAT_W + MARGIN_RIGHT;
  const height = MARGIN_TOP + STRING_GAP * 5 + MARGIN_BOTTOM;
  const rowY = (stringIndex: number) =>
    MARGIN_TOP + (5 - stringIndex) * STRING_GAP;
  const beatX = (beat: number) => MARGIN_LEFT + beat * BEAT_W;
  const lastBeatLine = Math.floor(loopBeats + 1e-6);

  const svg = (
    <svg
      viewBox={`0 0 ${width} ${height}`}
      width={large ? undefined : width}
      height={large ? undefined : height}
      className={large ? "h-auto w-full" : undefined}
      role="img"
      aria-label={`Tab riffu ${riff.title}`}
    >
      {/* Beat gridlines and numbers */}
      {Array.from({ length: lastBeatLine + 1 }, (_, beat) => (
        <g key={`beat-${beat}`}>
          <line
            x1={beatX(beat)}
            y1={rowY(5)}
            x2={beatX(beat)}
            y2={rowY(0)}
            stroke="#2a2a2a"
            strokeWidth={1}
          />
          <text
            x={beatX(beat)}
            y={height - 6}
            textAnchor="middle"
            fontSize={9}
            fill="#737373"
          >
            {beat + 1}
          </text>
        </g>
      ))}

      {/* Strings */}
      {STRING_LABELS.map((label, index) => (
        <g key={`string-${index}`}>
          <line
            x1={beatX(0)}
            y1={rowY(index)}
            x2={beatX(loopBeats)}
            y2={rowY(index)}
            stroke="#525252"
            strokeWidth={1}
          />
          <text
            x={MARGIN_LEFT - 9}
            y={rowY(index) + 3}
            textAnchor="end"
            fontSize={9}
            fill="#737373"
          >
            {label}
          </text>
        </g>
      ))}

      {/* Notes */}
      {riff.notes.map((note, index) => {
        const muted = note.technique === "palm-mute";
        const x = beatX(note.start);
        const y = rowY(note.stringIndex);
        return (
          <g key={`note-${index}`}>
            <rect
              x={x - 11}
              y={y - 7.5}
              width={22}
              height={15}
              rx={3}
              fill={muted ? "#f59e0b" : "#d4d4d4"}
            />
            <text
              x={x}
              y={y + 3.5}
              textAnchor="middle"
              fontSize={10}
              fontWeight={600}
              fill="#0a0a0a"
            >
              {note.fret}
            </text>
          </g>
        );
      })}

      {/* Playhead */}
      {isPlaying && (
        <line
          x1={beatX(positionBeats)}
          y1={rowY(5) - 5}
          x2={beatX(positionBeats)}
          y2={rowY(0) + 5}
          stroke="#f59e0b"
          strokeWidth={2}
        />
      )}
    </svg>
  );

  return large ? svg : <div className="overflow-x-auto">{svg}</div>;
}

/**
 * Music-stand view — a full-screen, distraction-free presentation of a riff
 * tab. Prop the device in landscape and the wide tab fills the screen. Keeps
 * the screen awake and asks for browser fullscreen (a no-op where the
 * Fullscreen API is unavailable).
 */
function RiffStandView({
  riff,
  positionBeats,
  isPlaying,
  slowdown,
  onTogglePlay,
  onChangeSlowdown,
  onClose,
}: {
  riff: MetalRiff;
  positionBeats: number;
  isPlaying: boolean;
  slowdown: number;
  onTogglePlay: () => void;
  onChangeSlowdown: (value: number) => void;
  onClose: () => void;
}) {
  useWakeLock();

  useEffect(() => {
    const root = document.documentElement;
    if (typeof root.requestFullscreen === "function") {
      root.requestFullscreen().catch(() => {});
    }

    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKey);

    return () => {
      document.removeEventListener("keydown", onKey);
      if (
        document.fullscreenElement &&
        typeof document.exitFullscreen === "function"
      ) {
        document.exitFullscreen().catch(() => {});
      }
    };
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-bg p-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-bold tracking-tight">{riff.title}</h2>
        <button
          type="button"
          onClick={onClose}
          aria-label="Zavřít stojan"
          className="rounded-lg border border-line bg-surface px-3 py-1.5 text-sm transition-colors hover:border-line-strong"
        >
          Zavřít
        </button>
      </div>

      <div className="flex flex-1 items-center justify-center overflow-auto py-4">
        <RiffTab
          riff={riff}
          positionBeats={positionBeats}
          isPlaying={isPlaying}
          large
        />
      </div>

      <div className="flex flex-wrap items-center justify-center gap-3">
        <div className="inline-flex rounded-lg border border-line p-1">
          {SLOWDOWNS.map((option) => (
            <button
              key={option.value}
              type="button"
              onClick={() => onChangeSlowdown(option.value)}
              className={
                slowdown === option.value ? SEGMENT_ACTIVE : SEGMENT_IDLE
              }
            >
              {option.label}
            </button>
          ))}
        </div>
        <button
          type="button"
          onClick={onTogglePlay}
          className={
            isPlaying
              ? "rounded-lg border border-line bg-surface px-10 py-3 text-base font-semibold transition-colors hover:border-line-strong"
              : "rounded-lg bg-accent px-10 py-3 text-base font-semibold text-accent-fg transition-colors hover:bg-accent/90"
          }
        >
          {isPlaying ? "Zastavit" : "Přehrát"}
        </button>
      </div>
    </div>
  );
}

/**
 * Riff library — browse the metal/rock riffs, read the tab and play each one
 * back at a reduced tempo. Playback is the synthesised RiffPlayer, so no
 * microphone is needed.
 */
export function RiffLibrary() {
  const [difficulty, setDifficulty] = useState<DifficultyFilter>("all");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [slowdown, setSlowdown] = useState(1);
  const [standMode, setStandMode] = useState(false);

  const isPlaying = useRiffPlayerStore((store) => store.isPlaying);
  const positionBeats = useRiffPlayerStore((store) => store.positionBeats);

  // Stop the player when leaving the library.
  useEffect(() => {
    return () => {
      riffPlayer.stop();
    };
  }, []);

  const selectedRiff = selectedId ? getMetalRiff(selectedId) : undefined;

  function selectRiff(id: string) {
    riffPlayer.stop();
    setSlowdown(1);
    setStandMode(false);
    setSelectedId(id);
  }

  function backToList() {
    riffPlayer.stop();
    setStandMode(false);
    setSelectedId(null);
  }

  function togglePlay(riff: MetalRiff) {
    if (riffPlayer.getState().isPlaying) {
      riffPlayer.stop();
      return;
    }
    void (async () => {
      const context = await audioEngine.getOutputContext();
      riffPlayer.setRiff(riff);
      riffPlayer.setBpm(Math.round(riff.bpm * slowdown));
      riffPlayer.start(context);
    })();
  }

  function changeSlowdown(value: number, riff: MetalRiff) {
    setSlowdown(value);
    riffPlayer.setBpm(Math.round(riff.bpm * value));
  }

  // --- Music-stand view ---------------------------------------------------
  if (selectedRiff && standMode) {
    return (
      <RiffStandView
        riff={selectedRiff}
        positionBeats={positionBeats}
        isPlaying={isPlaying}
        slowdown={slowdown}
        onTogglePlay={() => togglePlay(selectedRiff)}
        onChangeSlowdown={(value) => changeSlowdown(value, selectedRiff)}
        onClose={() => setStandMode(false)}
      />
    );
  }

  // --- Riff detail --------------------------------------------------------
  if (selectedRiff) {
    return (
      <div>
        <button
          type="button"
          onClick={backToList}
          className="text-sm text-dim transition-colors hover:text-text"
        >
          ← Zpět na výpis
        </button>

        <h2 className="mt-3 text-2xl font-bold tracking-tight">
          {selectedRiff.title}
        </h2>
        <p className="mt-1 text-sm text-dim">
          {selectedRiff.artist ? `${selectedRiff.artist} · ` : ""}
          {selectedRiff.style} · {selectedRiff.focus} ·{" "}
          {RIFF_DIFFICULTY_LABELS[selectedRiff.difficulty]}
        </p>

        <div className="mt-5 rounded-xl border border-line bg-surface p-4">
          <RiffTab
            riff={selectedRiff}
            positionBeats={positionBeats}
            isPlaying={isPlaying}
          />
          <p className="mt-2 text-xs text-muted">
            Oranžové noty se hrají s palm mute. Čísla jsou pražce.
          </p>
        </div>

        {/* Slow-down */}
        <div className="mt-5">
          <div className="text-sm font-medium text-text">Tempo</div>
          <div className="mt-2 flex items-center gap-3">
            <div className="inline-flex rounded-lg border border-line p-1">
              {SLOWDOWNS.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  onClick={() => changeSlowdown(option.value, selectedRiff)}
                  className={
                    slowdown === option.value ? SEGMENT_ACTIVE : SEGMENT_IDLE
                  }
                >
                  {option.label}
                </button>
              ))}
            </div>
            <span className="text-sm text-muted">
              {Math.round(selectedRiff.bpm * slowdown)} BPM
            </span>
          </div>
        </div>

        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <button
            type="button"
            onClick={() => togglePlay(selectedRiff)}
            className={
              isPlaying
                ? "rounded-lg border border-line bg-surface px-10 py-3 text-base font-semibold transition-colors hover:border-line-strong"
                : "rounded-lg bg-accent px-10 py-3 text-base font-semibold text-accent-fg transition-colors hover:bg-accent/90"
            }
          >
            {isPlaying ? "Zastavit" : "Přehrát"}
          </button>
          <button
            type="button"
            onClick={() => setStandMode(true)}
            className="rounded-lg border border-line bg-surface px-6 py-3 text-base font-semibold transition-colors hover:border-line-strong"
          >
            Stojan
          </button>
        </div>
      </div>
    );
  }

  // --- Riff list ----------------------------------------------------------
  const visibleRiffs =
    difficulty === "all"
      ? METAL_RIFFS
      : METAL_RIFFS.filter((riff) => riff.difficulty === difficulty);

  return (
    <div>
      <div className="inline-flex flex-wrap rounded-lg border border-line p-1">
        {DIFFICULTY_FILTERS.map((filter) => (
          <button
            key={filter.id}
            type="button"
            onClick={() => setDifficulty(filter.id)}
            className={
              difficulty === filter.id ? SEGMENT_ACTIVE : SEGMENT_IDLE
            }
          >
            {filter.label}
          </button>
        ))}
      </div>

      <div className="mt-5 space-y-2">
        {visibleRiffs.map((riff) => (
          <button
            key={riff.id}
            type="button"
            onClick={() => selectRiff(riff.id)}
            className="block w-full rounded-lg border border-line bg-surface px-4 py-3 text-left transition-colors hover:border-accent"
          >
            <div className="flex items-baseline justify-between gap-3">
              <span className="font-medium">{riff.title}</span>
              <span className="shrink-0 text-xs text-muted">
                {RIFF_DIFFICULTY_LABELS[riff.difficulty]}
              </span>
            </div>
            <div className="mt-0.5 text-sm text-dim">
              {riff.artist ? `${riff.artist} · ` : ""}
              {riff.style} · {riff.focus}
            </div>
          </button>
        ))}
      </div>
    </div>
  );
}
