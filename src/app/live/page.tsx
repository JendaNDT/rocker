"use client";

import { useEffect, useRef, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { metronome } from "@/audio/metronome";
import { recorder } from "@/audio/recorder";
import { PageContainer } from "@/components/PageContainer";
import { TunerDisplay } from "@/components/tuner/TunerDisplay";
import { useTuner } from "@/components/tuner/useTuner";
import { useWakeLock } from "@/components/useWakeLock";
import { formatDuration, type Recording } from "@/lib/recordings";
import { bpmFromTaps, clampBpm, MAX_BPM, MIN_BPM } from "@/lib/tempo";
import { useHydrated } from "@/lib/useHydrated";
import { useAudioStore } from "@/stores/audioStore";
import { useMetronomeStore } from "@/stores/metronomeStore";
import { useRecordingsStore } from "@/stores/recordingsStore";
import { useSettingsStore } from "@/stores/settingsStore";

// A gap longer than this starts a fresh tap-tempo measurement.
const TAP_RESET_MS = 2000;
const MAX_TAPS = 6;

/**
 * Live mode — a stripped-down "guitar in hand" screen.
 *
 * One column, three big sections (Tuner, Metronome, Rec). No menus, no
 * cards-within-cards, no settings — those live on the dedicated screens.
 * The wake lock is on so the screen does not blank during a hands-busy
 * session. Audio engine, metronome and recorder are the same singletons
 * the rest of the app uses, so any setting picked elsewhere (tuning,
 * accent pattern, sound, …) is reflected here.
 */
export default function LivePage() {
  useWakeLock();

  // Audio engine
  const status = useAudioStore((state) => state.status);
  const permission = useAudioStore((state) => state.permission);
  const engineError = useAudioStore((state) => state.error);
  const running = status === "running";

  // Tuner — useTuner subscribes to the running audio engine.
  const notation = useSettingsStore((state) => state.notation);
  const hydrated = useHydrated();
  const { reading } = useTuner();

  // Metronome
  const isPlaying = useMetronomeStore((state) => state.isPlaying);
  const bpm = useMetronomeStore((state) => state.bpm);
  const tapsRef = useRef<number[]>([]);

  // Recorder
  const recordingsCount = useRecordingsStore((s) => s.recordings.length);
  const addRecording = useRecordingsStore((s) => s.addRecording);
  const [isRecording, setIsRecording] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const [busy, setBusy] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  // Live elapsed timer while recording.
  useEffect(() => {
    if (!isRecording) return;
    const start = performance.now();
    setElapsed(0);
    const id = window.setInterval(() => {
      setElapsed((performance.now() - start) / 1000);
    }, 200);
    return () => window.clearInterval(id);
  }, [isRecording]);

  // Don't leave a running recorder behind when leaving the screen.
  useEffect(() => {
    return () => {
      if (recorder.isRecording()) void recorder.stop();
    };
  }, []);

  async function enableInput() {
    setLocalError(null);
    if (permission !== "granted") {
      const result = await audioEngine.requestPermission();
      if (result !== "granted") return;
    }
    await audioEngine.start();
  }

  async function toggleMetronome() {
    const context = await audioEngine.getOutputContext();
    metronome.toggle(context);
  }

  function changeBpm(next: number) {
    metronome.setBpm(clampBpm(next));
  }

  function handleTap() {
    const now = performance.now();
    const last = tapsRef.current[tapsRef.current.length - 1];
    if (last && now - last > TAP_RESET_MS) tapsRef.current = [];
    tapsRef.current.push(now);
    if (tapsRef.current.length > MAX_TAPS) tapsRef.current.shift();
    const next = bpmFromTaps(tapsRef.current);
    if (next) changeBpm(next);
  }

  function startRecording() {
    setLocalError(null);
    const stream = audioEngine.getRecordingStream();
    if (!stream) {
      setLocalError("Vstup neběží — zapni nejdřív zvukový vstup.");
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

  return (
    <PageContainer size="focused">
      <h1 className="text-2xl font-semibold">Live</h1>
      <p className="mt-1 text-sm text-dim">
        Hraní s kytarou v ruce — ladička, metronom a nahrávání po ruce.
      </p>

      {/* Ladička */}
      <section className="mt-6" aria-label="Ladička">
        {!running ? (
          <div className="rounded-lg border border-line bg-surface p-6 text-center">
            <p className="text-text">Pro start zapni zvukový vstup.</p>
            {permission === "denied" && (
              <p className="mt-2 text-sm text-danger">
                Přístup k mikrofonu zamítnut — povol ho v prohlížeči.
              </p>
            )}
            <button
              type="button"
              onClick={() => void enableInput()}
              className="mt-4 min-h-12 rounded-lg bg-accent px-6 py-3 text-base font-semibold text-accent-fg transition-colors hover:bg-accent/90"
            >
              {status === "starting" ? "Spouštím…" : "Zapnout vstup"}
            </button>
          </div>
        ) : (
          <TunerDisplay
            reading={reading}
            notation={hydrated ? notation : "english"}
          />
        )}
      </section>

      {/* Metronom */}
      <section
        className="mt-6 rounded-lg border border-line bg-surface p-6"
        aria-label="Metronom"
      >
        <div className="flex items-center justify-between gap-4">
          <div>
            <div className="text-6xl font-bold tabular-nums">{bpm}</div>
            <div className="text-sm text-muted">BPM</div>
          </div>
          <button
            type="button"
            onClick={() => void toggleMetronome()}
            aria-pressed={isPlaying}
            className="min-h-12 rounded-lg bg-accent px-6 py-3 text-base font-semibold text-accent-fg transition-colors hover:bg-accent/90"
          >
            {isPlaying ? "Stop" : "Start"}
          </button>
        </div>

        <div className="mt-5 flex items-center gap-3">
          <button
            type="button"
            aria-label="Snížit tempo"
            onClick={() => changeBpm(bpm - 1)}
            className="h-12 w-12 rounded-lg border border-line bg-bg text-lg transition-colors hover:border-line-strong"
          >
            −
          </button>
          <input
            type="range"
            min={MIN_BPM}
            max={MAX_BPM}
            value={bpm}
            onChange={(event) => changeBpm(Number(event.target.value))}
            aria-label="Tempo"
            className="flex-1 accent-accent"
          />
          <button
            type="button"
            aria-label="Zvýšit tempo"
            onClick={() => changeBpm(bpm + 1)}
            className="h-12 w-12 rounded-lg border border-line bg-bg text-lg transition-colors hover:border-line-strong"
          >
            +
          </button>
        </div>

        <button
          type="button"
          onClick={handleTap}
          className="mt-4 min-h-12 w-full rounded-lg border border-line bg-bg px-6 py-3 text-sm transition-colors hover:border-line-strong"
        >
          Naťukat tempo
        </button>
      </section>

      {/* Nahrávání */}
      <section
        className="mt-6 rounded-lg border border-line bg-surface p-6"
        aria-label="Nahrávání"
      >
        {!isRecording ? (
          <button
            type="button"
            onClick={startRecording}
            disabled={busy || !running}
            className="flex min-h-12 w-full items-center justify-center gap-3 rounded-lg bg-danger px-6 py-3 text-base font-semibold text-text transition-colors hover:bg-danger disabled:opacity-50"
          >
            <span className="h-4 w-4 rounded-full bg-surface" />
            {busy
              ? "Ukládám…"
              : running
                ? "Nahrávat"
                : "Zapni nejdřív vstup"}
          </button>
        ) : (
          <button
            type="button"
            onClick={() => void stopRecording()}
            disabled={busy}
            className="flex min-h-12 w-full items-center justify-center gap-3 rounded-lg border border-line-strong bg-bg px-6 py-3 text-base font-semibold transition-colors hover:border-line-strong disabled:opacity-50"
          >
            <span className="h-4 w-4 bg-danger" />
            Zastavit — nahrávám {formatDuration(elapsed)}
          </button>
        )}
        {(localError || engineError) && (
          <p className="mt-3 text-sm text-danger" role="status">
            {localError ?? engineError}
          </p>
        )}
      </section>
    </PageContainer>
  );
}
