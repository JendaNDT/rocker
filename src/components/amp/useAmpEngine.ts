"use client";

import { ampEngine } from "@/audio/ampEngine";
import { useAmpEngineStore } from "@/stores/ampEngineStore";

export interface AmpEngineHandle {
  /** Whether the shared chain is currently wired to the speakers. */
  active: boolean;
  /** True between requesting input permission and the chain coming up. */
  starting: boolean;
  /** User-facing error from the last failed start, or null. */
  error: string | null;
  /** Post-effects analyser, or null when not running. */
  outputAnalyser: AnalyserNode | null;
  /** Post-effects MediaStream for MediaRecorder capture, or null. */
  recordingStream: MediaStream | null;
  /** Spin the shared chain up. */
  start: () => Promise<void>;
  /** Tear it down. */
  stop: () => Promise<void>;
}

/**
 * Reads the shared AmpEngine for any screen that hosts an aparát (full
 * /amp page, or the AmpWidget on /practice and /metal). Unlike the old
 * useLocalAmpChain, this does NOT create or dispose a chain on mount/unmount —
 * the single chain lives in the module-level engine and survives navigation.
 * Every consumer mirrors the same running chain, so toggling it on one screen
 * is reflected everywhere.
 *
 * The analyser and recording stream are read straight from the engine; they
 * are valid only while `active`, and re-read whenever the active flag flips.
 */
export function useAmpEngine(): AmpEngineHandle {
  const status = useAmpEngineStore((s) => s.status);
  const active = useAmpEngineStore((s) => s.active);
  const error = useAmpEngineStore((s) => s.error);

  return {
    active,
    starting: status === "starting",
    error,
    outputAnalyser: active ? ampEngine.getOutputAnalyser() : null,
    recordingStream: active ? ampEngine.getRecordingStream() : null,
    start: () => ampEngine.start(),
    stop: () => ampEngine.stop(),
  };
}
