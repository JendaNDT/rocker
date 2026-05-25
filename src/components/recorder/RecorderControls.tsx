"use client";

import { useEffect, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { recorder } from "@/audio/recorder";
import { type Recording, formatDuration } from "@/lib/recordings";
import { useAudioStore } from "@/stores/audioStore";
import { useRecordingsStore } from "@/stores/recordingsStore";

/**
 * Recording controls: enables the audio input (same flow as the tuner), then
 * captures a take via the MediaRecorder wrapper and stores it.
 */
export function RecorderControls() {
  const status = useAudioStore((state) => state.status);
  const permission = useAudioStore((state) => state.permission);
  const engineError = useAudioStore((state) => state.error);
  const recordingsCount = useRecordingsStore(
    (state) => state.recordings.length,
  );
  const addRecording = useRecordingsStore((state) => state.addRecording);
  const storageAvailable = useRecordingsStore(
    (state) => state.storageAvailable,
  );

  const [isRecording, setIsRecording] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const [busy, setBusy] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  const running = status === "running";
  const supported = recorder.isSupported();

  // Live elapsed timer while a take is being recorded.
  useEffect(() => {
    if (!isRecording) return;
    const start = performance.now();
    setElapsed(0);
    const id = window.setInterval(() => {
      setElapsed((performance.now() - start) / 1000);
    }, 200);
    return () => window.clearInterval(id);
  }, [isRecording]);

  async function enableInput() {
    setLocalError(null);
    if (permission !== "granted") {
      const result = await audioEngine.requestPermission();
      if (result !== "granted") return;
    }
    await audioEngine.start();
  }

  function startRecording() {
    setLocalError(null);
    const stream = audioEngine.getInputStream();
    if (!stream) {
      setLocalError("Vstup neběží — nejdřív zapni zvukový vstup.");
      return;
    }
    if (recorder.start(stream)) {
      setIsRecording(true);
    } else {
      setLocalError("Nahrávání se nepodařilo spustit.");
    }
  }

  async function stopRecording() {
    setBusy(true);
    const result = await recorder.stop();
    setIsRecording(false);
    if (result) {
      const recording: Recording = {
        id: crypto.randomUUID(),
        name: `Pokus ${recordingsCount + 1}`,
        createdAt: Date.now(),
        durationSec: result.durationSec,
        mimeType: result.mimeType,
        markers: [],
        blob: result.blob,
      };
      await addRecording(recording);
    }
    setBusy(false);
  }

  if (!supported) {
    return (
      <div className="rounded-lg border border-line bg-surface p-6">
        <p className="text-sm text-danger">
          Tento prohlížeč neumí nahrávat zvuk (chybí MediaRecorder).
        </p>
      </div>
    );
  }

  return (
    <div className="rounded-lg border border-line bg-surface p-6">
      {!storageAvailable && (
        <p className="mb-4 rounded-lg border border-accent bg-accent-soft p-3 text-sm text-accent">
          Úložiště prohlížeče není dostupné — nahrávky se na tomto zařízení
          neuloží a po zavření zmizí.
        </p>
      )}

      {!running && (
        <>
          <p className="text-sm text-text">
            Pro nahrávání zapni zvukový vstup. Zařízení (M-Track Solo nebo
            mikrofon) vybereš v nastavení.
          </p>
          {permission === "denied" && (
            <p className="mt-2 text-sm text-danger">
              Přístup k mikrofonu je zamítnutý — povol ho v prohlížeči a zkus
              to znovu.
            </p>
          )}
          <button
            type="button"
            onClick={() => void enableInput()}
            className="mt-4 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
          >
            {status === "starting" ? "Spouštím…" : "Zapnout zvukový vstup"}
          </button>
        </>
      )}

      {running && !isRecording && (
        <div className="flex items-center gap-4">
          <button
            type="button"
            onClick={startRecording}
            disabled={busy}
            className="flex items-center gap-2 rounded-lg bg-danger px-6 py-2.5 text-sm font-semibold text-text transition-colors hover:bg-danger disabled:opacity-60"
          >
            <span className="h-3 w-3 rounded-full bg-surface" />
            {busy ? "Ukládám…" : "Nahrávat"}
          </button>
          <span className="text-sm text-dim">Vstup běží</span>
        </div>
      )}

      {isRecording && (
        <div className="flex items-center gap-4">
          <button
            type="button"
            onClick={() => void stopRecording()}
            disabled={busy}
            className="flex items-center gap-2 rounded-lg border border-line-strong bg-bg px-6 py-2.5 text-sm font-semibold transition-colors hover:border-line-strong disabled:opacity-60"
          >
            <span className="h-3 w-3 bg-danger" />
            Zastavit
          </button>
          <span className="flex items-center gap-2 text-sm tabular-nums text-danger">
            <span className="h-2 w-2 animate-pulse rounded-full bg-danger" />
            Nahrávám {formatDuration(elapsed)}
          </span>
        </div>
      )}

      {(localError || engineError) && (
        <p className="mt-3 text-sm text-danger">
          {localError ?? engineError}
        </p>
      )}
    </div>
  );
}
