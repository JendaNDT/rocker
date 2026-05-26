"use client";

import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import {
  DEFAULT_AMP_PRESET,
  type AmpSettings,
  sanitizeAmpSettings,
} from "@/lib/ampPresets";

interface AmpStoreState {
  /** Current parameter values, shared across every screen that hosts an aparát. */
  settings: AmpSettings;
  /** Id of the preset that produced `settings`, or null for "vlastní". */
  presetId: string | null;
  setSettings: (settings: AmpSettings, presetId?: string | null) => void;
  setPresetId: (id: string | null) => void;
}

/**
 * Shared aparát settings. Active state is intentionally NOT here — every
 * screen (full /amp, AmpWidget on /practice and /metal) owns its own
 * AmpChain instance because Next.js `output: 'export'` reloads the whole
 * document on every Link navigation, killing the AudioContext anyway.
 *
 * What this store DOES persist (and shares across screens):
 *  - settings (sliders, distortion type, cabinet)
 *  - presetId (so the active built-in / user preset stays selected)
 *
 * Sanitize the rehydrated payload so a corrupt or partial blob cannot
 * crash AmpChain.
 */
export const useAmpStore = create<AmpStoreState>()(
  persist(
    (set) => ({
      settings: DEFAULT_AMP_PRESET.settings,
      presetId: DEFAULT_AMP_PRESET.id,

      setSettings: (settings, presetId = null) => set({ settings, presetId }),
      setPresetId: (id) => set({ presetId: id }),
    }),
    {
      name: "rocker-amp",
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        settings: state.settings,
        presetId: state.presetId,
      }),
      merge: (persistedState, currentState) => {
        const persisted =
          persistedState && typeof persistedState === "object"
            ? (persistedState as Partial<{
                settings: unknown;
                presetId: unknown;
              }>)
            : {};
        return {
          ...currentState,
          settings: sanitizeAmpSettings(persisted.settings),
          presetId:
            typeof persisted.presetId === "string" ? persisted.presetId : null,
        };
      },
    },
  ),
);
