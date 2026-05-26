"use client";

import { useEffect, useRef, useState } from "react";
import { AmpChain } from "@/audio/ampChain";
import { audioEngine } from "@/audio/AudioEngine";
import type { AmpSettings } from "@/lib/ampPresets";

/** Pushes every parameter of a settings object onto the chain. */
export function applyToChain(chain: AmpChain, settings: AmpSettings): void {
  chain.setCompression(settings.compression);
  chain.setDrive(settings.drive);
  chain.setDistortionType(settings.distortion);
  chain.setBass(settings.bass);
  chain.setMid(settings.mid);
  chain.setTreble(settings.treble);
  chain.setCabinet(settings.cabinet);
  chain.setDelayMix(settings.delay);
  chain.setReverbMix(settings.reverb);
  chain.setChorusMix(settings.chorus);
  chain.setLevel(settings.level);
}

export interface LocalAmpChain {
  /** Whether the chain is currently wired up to the speakers. */
  active: boolean;
  /** True between requesting input permission and the chain coming up. */
  starting: boolean;
  /** User-facing error from the last failed start, or null. */
  error: string | null;
  /** Post-effects analyser, or null when not running. */
  outputAnalyser: AnalyserNode | null;
  /** Post-effects MediaStream for MediaRecorder capture, or null. */
  recordingStream: MediaStream | null;
  /** Spin the chain up. */
  start: () => Promise<void>;
  /** Tear it down. */
  stop: () => Promise<void>;
}

/**
 * Owns one AmpChain instance per mounted component. The chain disposes
 * when the component unmounts — by design: Next.js `output: 'export'`
 * navigation is a full document reload, which destroys the AudioContext
 * anyway. Each screen that wants an aparát (full /amp page, or the
 * AmpWidget on /practice and /metal) gets its own instance.
 *
 * Settings are read from the caller (typically `useAmpStore`) and pushed
 * onto the chain whenever they change.
 */
export function useLocalAmpChain(settings: AmpSettings): LocalAmpChain {
  const chainRef = useRef<AmpChain | null>(null);
  const [active, setActive] = useState(false);
  const [starting, setStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [outputAnalyser, setOutputAnalyser] = useState<AnalyserNode | null>(
    null,
  );
  const [recordingStream, setRecordingStream] = useState<MediaStream | null>(
    null,
  );

  // Apply the latest settings whenever they change and the chain is live.
  useEffect(() => {
    const chain = chainRef.current;
    if (chain) applyToChain(chain, settings);
  }, [settings]);

  // Tear the chain down on unmount.
  useEffect(() => {
    return () => {
      const chain = chainRef.current;
      if (!chain) return;
      audioEngine.getInputSource()?.disconnect(chain.input);
      chain.dispose();
      chainRef.current = null;
    };
  }, []);

  async function start() {
    if (starting || active) return;
    setStarting(true);
    setError(null);
    const running = await audioEngine.ensureListening();
    if (!running) {
      setStarting(false);
      setError(
        "Nepodařilo se spustit zvukový vstup. Povol mikrofon v Profilu.",
      );
      return;
    }
    const source = audioEngine.getInputSource();
    if (!source) {
      setStarting(false);
      setError("Zvukový vstup není dostupný.");
      return;
    }
    const context = await audioEngine.getOutputContext();
    const chain = new AmpChain(context);
    applyToChain(chain, settings);
    source.connect(chain.input);
    chainRef.current = chain;
    setOutputAnalyser(chain.outputAnalyser);
    setRecordingStream(chain.recordingStream);
    setStarting(false);
    setActive(true);
  }

  async function stop() {
    const chain = chainRef.current;
    if (chain) {
      audioEngine.getInputSource()?.disconnect(chain.input);
      chain.dispose();
      chainRef.current = null;
    }
    setOutputAnalyser(null);
    setRecordingStream(null);
    setActive(false);
  }

  return {
    active,
    starting,
    error,
    outputAnalyser,
    recordingStream,
    start,
    stop,
  };
}
