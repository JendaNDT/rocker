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
 * Shared aparát settings. Active state is intentionally NOT here — the
 * AmpChain lives in a single shared singleton (`src/audio/ampEngine.ts`)
 * hosted in `AppShell` via `AmpHost`, so the amp survives screen-to-screen
 * Link navigation (Next.js does client-side soft navigation in static
 * export — see ARCHITECTURE.md sections 1 and 2.5). The earlier comment
 * here claimed every screen owned its own AmpChain because navigation
 * tore the AudioContext down on every Link — that turned out to be
 * inaccurate and was corrected in 2026-06.
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
