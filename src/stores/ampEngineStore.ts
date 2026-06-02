import { create } from "zustand";
import { ampEngine } from "@/audio/ampEngine";
import type { AmpEngineState } from "@/audio/ampEngine";

/**
 * Reactive mirror of the AmpEngine state for React components (layer 3).
 *
 * The engine (layer 5) is the single source of truth; this store only reflects
 * it. To change anything, call `ampEngine` methods — the change flows back here
 * through the subscription below. Same pattern as `audioStore`.
 */
export const useAmpEngineStore = create<AmpEngineState>(() =>
  ampEngine.getState(),
);

// Keep the store in sync with the engine.
ampEngine.subscribe((state) => {
  useAmpEngineStore.setState(state, true);
});
