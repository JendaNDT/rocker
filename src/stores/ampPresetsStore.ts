import { create } from "zustand";
import { persist } from "zustand/middleware";
import {
  type AmpPreset,
  type AmpSettings,
  sanitizeUserPresets,
} from "@/lib/ampPresets";

/** Generates a unique id for a user-created preset. */
function newPresetId(): string {
  const random = Math.random().toString(36).slice(2, 8);
  return `user-${Date.now().toString(36)}-${random}`;
}

interface AmpPresetsState {
  /** User-created presets, in creation order. */
  presets: AmpPreset[];
  /** Saves the given settings as a new named preset; returns its id. */
  savePreset: (name: string, settings: AmpSettings) => string;
  /** Renames a preset. */
  renamePreset: (id: string, name: string) => void;
  /** Deletes a preset. */
  deletePreset: (id: string) => void;
}

/**
 * User-created amp presets, persisted in localStorage under
 * "rocker-amp-presets". The four built-in presets live in ampPresets.ts;
 * this store holds only the user's own. On rehydration the stored value
 * passes through sanitizeUserPresets so corrupt data can never crash the app.
 */
export const useAmpPresetsStore = create<AmpPresetsState>()(
  persist(
    (set) => ({
      presets: [],
      savePreset: (name, settings) => {
        const id = newPresetId();
        set((state) => ({
          presets: [...state.presets, { id, name: name.trim(), settings }],
        }));
        return id;
      },
      renamePreset: (id, name) =>
        set((state) => ({
          presets: state.presets.map((preset) =>
            preset.id === id ? { ...preset, name: name.trim() } : preset,
          ),
        })),
      deletePreset: (id) =>
        set((state) => ({
          presets: state.presets.filter((preset) => preset.id !== id),
        })),
    }),
    {
      name: "rocker-amp-presets",
      merge: (persisted, current) => {
        const stored = persisted as { presets?: unknown } | undefined;
        return { ...current, presets: sanitizeUserPresets(stored?.presets) };
      },
    },
  ),
);
