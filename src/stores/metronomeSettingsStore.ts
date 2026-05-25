import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { MetronomeConfig, MetronomePreset } from "@/lib/metronomeConfig";
import {
  defaultMetronomeConfig,
  sanitizeMetronomeConfig,
} from "@/lib/metronomeConfig";
import { createPreset, sanitizePresetList } from "@/lib/metronomePresets";

interface MetronomeSettingsState {
  /** The user's last-used metronome configuration. */
  config: MetronomeConfig;
  /** Named saved configurations. */
  presets: MetronomePreset[];
  /** Merges a patch into the stored configuration. */
  setConfig: (patch: Partial<MetronomeConfig>) => void;
  /** Saves the current configuration as a named preset. */
  savePreset: (name: string) => void;
  /** Removes a preset by id. */
  deletePreset: (id: string) => void;
}

/**
 * Persistent metronome settings, stored in localStorage under
 * "rocker-metronome". Holds the user's preferred configuration and saved
 * presets.
 *
 * The metronome engine (audio layer) stays the runtime source of truth; this
 * store only remembers preferences across sessions. Trainers that drive the
 * engine directly (Power Chord Trainer) never touch this store, so they
 * cannot clobber the saved configuration.
 */
export const useMetronomeSettingsStore = create<MetronomeSettingsState>()(
  persist(
    (set) => ({
      config: defaultMetronomeConfig(),
      presets: [],
      setConfig: (patch) =>
        set((state) => ({
          config: sanitizeMetronomeConfig({ ...state.config, ...patch }),
        })),
      savePreset: (name) =>
        set((state) => {
          if (name.trim().length === 0) return state;
          return {
            presets: [...state.presets, createPreset(name, state.config)],
          };
        }),
      deletePreset: (id) =>
        set((state) => ({
          presets: state.presets.filter((preset) => preset.id !== id),
        })),
    }),
    {
      name: "rocker-metronome",
      // Stored data can be corrupt or written by an older app version; every
      // field is re-validated here so the app never starts in a broken state
      // (see AGENTS.md).
      merge: (persisted, current) => {
        const safe = { ...current };
        if (typeof persisted === "object" && persisted !== null) {
          const stored = persisted as Record<string, unknown>;
          safe.config = sanitizeMetronomeConfig(stored.config);
          safe.presets = sanitizePresetList(stored.presets);
        }
        return safe;
      },
    },
  ),
);
