"use client";

import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import { ampEngine } from "@/audio/ampEngine";
import {
  DEFAULT_AMP_PRESET,
  type AmpSettings,
  type CabinetType,
  type DistortionType,
  sanitizeAmpSettings,
} from "@/lib/ampPresets";

interface AmpStoreState {
  /** Whether the chain is wired up and processing audio. */
  active: boolean;
  /** True between requesting input permission and the chain coming up. */
  starting: boolean;
  /** Current parameter values applied to the chain. */
  settings: AmpSettings;
  /** Id of the preset that produced `settings`, or null for "vlastní". */
  presetId: string | null;
  /** User-facing error from the last failed start, or null. */
  error: string | null;

  start: () => Promise<void>;
  stop: () => Promise<void>;
  setSettings: (settings: AmpSettings, presetId?: string | null) => void;
  setDistortion: (type: DistortionType) => void;
  setCabinet: (type: CabinetType) => void;
  setPresetId: (id: string | null) => void;
  clearError: () => void;
}

/**
 * Mirror of the AmpEngine state for React. The engine owns the audio graph
 * and survives navigation; this store is the React-side handle on it. Only
 * `settings` and `presetId` are persisted — `active` always starts false on
 * a fresh load so a previous-session aparát doesn't suddenly start hissing
 * before the user opts back in.
 */
// TEMP debug — remove once the navigation reset bug is found.
function dbg(tag: string): void {
  if (typeof window === "undefined") return;
  // eslint-disable-next-line no-console
  console.log(`[ampStore] ${tag} @ ${location.pathname}`, new Error().stack);
}

export const useAmpStore = create<AmpStoreState>()(
  persist(
    (set, get) => ({
      active: false,
      starting: false,
      settings: DEFAULT_AMP_PRESET.settings,
      presetId: DEFAULT_AMP_PRESET.id,
      error: null,

      start: async () => {
        if (get().active || get().starting) return;
        dbg("start: enter");
        set({ starting: true, error: null });
        const result = await ampEngine.start();
        if (result.ok) {
          dbg("start: ok -> active=true");
          set({ active: true, starting: false });
        } else {
          dbg(`start: fail -> ${result.error}`);
          set({ active: false, starting: false, error: result.error });
        }
      },

      stop: async () => {
        dbg("stop");
        ampEngine.stop();
        set({ active: false });
      },

      setSettings: (settings, presetId = null) => {
        ampEngine.setSettings(settings);
        set({ settings, presetId });
      },

      setDistortion: (type) => {
        ampEngine.setDistortion(type);
        set((state) => ({
          settings: { ...state.settings, distortion: type },
          presetId: null,
        }));
      },

      setCabinet: (type) => {
        ampEngine.setCabinet(type);
        set((state) => ({
          settings: { ...state.settings, cabinet: type },
          presetId: null,
        }));
      },

      setPresetId: (id) => set({ presetId: id }),

      clearError: () => set({ error: null }),
    }),
    {
      name: "rocker-amp",
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        settings: state.settings,
        presetId: state.presetId,
      }),
      // Sanitize the rehydrated payload — a corrupt or partial settings blob
      // would otherwise crash AmpChain. On a clean restore, push the values
      // into the engine so the next start() picks them up.
      merge: (persistedState, currentState) => {
        dbg(`merge: currentState.active=${currentState.active}`);
        const persisted =
          persistedState && typeof persistedState === "object"
            ? (persistedState as Partial<{
                settings: unknown;
                presetId: unknown;
              }>)
            : {};
        const safeSettings = sanitizeAmpSettings(persisted.settings);
        const safePresetId =
          typeof persisted.presetId === "string" ? persisted.presetId : null;
        ampEngine.setSettings(safeSettings);
        return {
          ...currentState,
          settings: safeSettings,
          presetId: safePresetId,
        };
      },
    },
  ),
);
