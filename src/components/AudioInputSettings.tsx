"use client";

import { useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { InputLevelMeter } from "@/components/InputLevelMeter";
import { useAudioStore } from "@/stores/audioStore";

/** Word rating for a measured round-trip latency. */
function latencyRating(ms: number): { text: string; className: string } {
  if (ms <= 15) return { text: "dobrá", className: "text-success" };
  if (ms <= 30) return { text: "vyšší", className: "text-accent" };
  return { text: "vysoká", className: "text-danger" };
}

/**
 * Microphone permission flow, audio input device picker, live level meter
 * and measured latency. Lives on the Profile screen.
 */
export function AudioInputSettings() {
  const status = useAudioStore((state) => state.status);
  const permission = useAudioStore((state) => state.permission);
  const devices = useAudioStore((state) => state.devices);
  const latencyMs = useAudioStore((state) => state.latencyMs);
  const error = useAudioStore((state) => state.error);

  const [selectedId, setSelectedId] = useState<string>("");

  const running = status === "running";

  function requestPermission() {
    void audioEngine.requestPermission();
  }

  function toggleInput() {
    if (running) {
      void audioEngine.stop();
    } else {
      void audioEngine.start(selectedId || undefined);
    }
  }

  function changeDevice(deviceId: string) {
    setSelectedId(deviceId);
    if (running) {
      void audioEngine.start(deviceId || undefined);
    }
  }

  // Permission not granted yet — explain why and offer to ask for it.
  if (permission !== "granted") {
    return (
      <div className="rounded-lg border border-line bg-surface p-4">
        <p className="text-sm text-text">
          Aplikace potřebuje přístup k mikrofonu nebo zvukovce, aby slyšela
          kytaru — pro ladičku, metronom a cvičení. Nic se nenahrává nikam ven,
          vše zůstává v tomto prohlížeči.
        </p>
        {permission === "denied" && (
          <p className="mt-3 text-sm text-danger">
            Přístup je zamítnutý. Povol ho v nastavení prohlížeče (ikona vlevo
            v adresním řádku) a zkus to znovu.
          </p>
        )}
        <button
          type="button"
          onClick={requestPermission}
          className="mt-4 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
        >
          Povolit mikrofon
        </button>
      </div>
    );
  }

  const latency = latencyMs === null ? null : latencyRating(latencyMs);

  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <label
        htmlFor="audio-input-device"
        className="block text-sm font-medium text-text"
      >
        Vstupní zařízení
      </label>
      <select
        id="audio-input-device"
        value={selectedId}
        onChange={(event) => changeDevice(event.target.value)}
        className="mt-2 w-full rounded-lg border border-line bg-bg px-3 py-2 text-sm text-text"
      >
        <option value="">Výchozí zařízení</option>
        {devices.map((device) => (
          <option key={device.deviceId} value={device.deviceId}>
            {device.label}
          </option>
        ))}
      </select>
      <p className="mt-2 text-xs text-muted">
        Na PC vyber zvukovku M-Track Solo, na mobilu nech výchozí mikrofon.
      </p>

      <div className="mt-4 flex items-center gap-3">
        <button
          type="button"
          onClick={toggleInput}
          className={
            running
              ? "rounded-lg border border-line bg-bg px-4 py-2 text-sm transition-colors hover:border-line-strong"
              : "rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
          }
        >
          {running ? "Zastavit vstup" : "Spustit vstup"}
        </button>
        <span
          className={running ? "text-sm text-accent" : "text-sm text-muted"}
        >
          {status === "starting"
            ? "Spouštím…"
            : running
              ? "Vstup běží"
              : "Vstup je zastavený"}
        </span>
      </div>

      <div className="mt-5">
        <InputLevelMeter />
      </div>

      {latency && (
        <p className="mt-4 text-sm text-dim">
          Latence:{" "}
          <span className="font-medium text-text">{latencyMs} ms</span>
          {" — "}
          <span className={latency.className}>{latency.text}</span>
        </p>
      )}

      {error && <p className="mt-3 text-sm text-danger">{error}</p>}
    </div>
  );
}
