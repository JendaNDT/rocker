"use client";

import { isHapticAvailable } from "@/audio/confirmationSound";
import { useSettingsStore } from "@/stores/settingsStore";

/**
 * Tuner-specific settings panel for the Profile screen. Currently holds:
 *   - reference tone volume slider (Phase 1)
 *   - in-tune confirmation toggles (Phase 2)
 * Likely to grow as the tuner gains features.
 */
export function TunerSettings() {
  const volume = useSettingsStore((state) => state.referenceToneVolume);
  const setVolume = useSettingsStore((state) => state.setReferenceToneVolume);
  const confirmSound = useSettingsStore((state) => state.tunerConfirmSound);
  const setConfirmSound = useSettingsStore(
    (state) => state.setTunerConfirmSound,
  );
  const confirmHaptic = useSettingsStore((state) => state.tunerConfirmHaptic);
  const setConfirmHaptic = useSettingsStore(
    (state) => state.setTunerConfirmHaptic,
  );

  const percent = Math.round(volume * 100);
  // navigator.vibrate is only available in the browser. During SSR the
  // helper returns false; after hydration it reports the real capability.
  const hapticSupported = isHapticAvailable();

  return (
    <div className="space-y-4">
      <div className="rounded-lg border border-line bg-surface p-4">
        <label
          htmlFor="reference-tone-volume"
          className="flex items-center justify-between text-sm font-medium text-text"
        >
          <span>Hlasitost referenčního tónu</span>
          <span className="text-xs text-dim">{percent} %</span>
        </label>
        <input
          id="reference-tone-volume"
          type="range"
          min={0}
          max={1}
          step={0.01}
          value={volume}
          onChange={(event) => setVolume(Number(event.target.value))}
          aria-label="Hlasitost referenčního tónu"
          className="mt-2 w-full accent-accent"
        />
        <p className="mt-2 text-xs text-muted">
          Kolik je slyšet kytarově obarvený tón, který Ladička v Vedeném
          režimu zahraje, když přidržíš tlačítko u struny.
        </p>
      </div>

      <div className="rounded-lg border border-line bg-surface p-4">
        <h3 className="text-sm font-medium text-text">
          Konfirmace naladění
        </h3>
        <p className="mt-1 text-xs text-muted">
          Krátké pípnutí (a na mobilu i vibrace), když struna ve Vedeném
          režimu vydrží naladěná zhruba 400 ms.
        </p>
        <label className="mt-3 flex items-center gap-2 text-sm text-text">
          <input
            type="checkbox"
            checked={confirmSound}
            onChange={(event) => setConfirmSound(event.target.checked)}
            className="h-4 w-4 accent-accent"
          />
          Přehrát zvukový signál
        </label>
        <label
          className={
            hapticSupported
              ? "mt-2 flex items-center gap-2 text-sm text-text"
              : "mt-2 flex items-center gap-2 text-sm text-muted"
          }
        >
          <input
            type="checkbox"
            checked={confirmHaptic}
            disabled={!hapticSupported}
            onChange={(event) => setConfirmHaptic(event.target.checked)}
            className="h-4 w-4 accent-accent disabled:cursor-not-allowed"
          />
          Vibrovat na mobilu
          {!hapticSupported && (
            <span className="text-xs text-dim">
              (toto zařízení vibrace nepodporuje)
            </span>
          )}
        </label>
      </div>
    </div>
  );
}
