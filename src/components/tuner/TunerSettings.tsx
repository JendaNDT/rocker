"use client";

import { useSettingsStore } from "@/stores/settingsStore";

/**
 * Tuner-specific settings panel for the Profile screen. Today: volume of
 * the reference tone played by the Guided tuner when the user holds a
 * string's "Přehrát" button. Likely to grow as the tuner gains features.
 */
export function TunerSettings() {
  const volume = useSettingsStore((state) => state.referenceToneVolume);
  const setVolume = useSettingsStore((state) => state.setReferenceToneVolume);

  const percent = Math.round(volume * 100);

  return (
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
        Kolik je slyšet kytarově obarvený tón, který Ladička v Vedeném režimu
        zahraje, když přidržíš tlačítko u struny.
      </p>
    </div>
  );
}
