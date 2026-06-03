import { create } from "zustand";
import { audioEngine } from "@/audio/audioEngine";
import type { AudioEngineState } from "@/audio/audioEngine";

/**
 * Reactive mirror of the AudioEngine state for React components.
 *
 * The engine (architecture layer 5) is the single source of truth; this store
 * (layer 3) only reflects it. To change anything, call `audioEngine` methods
 * directly — the change flows back here through the subscription below.
 */
export const useAudioStore = create<AudioEngineState>(() =>
  audioEngine.getState(),
);

// Keep the store in sync with the engine.
audioEngine.subscribe((state) => {
  useAudioStore.setState(state, true);
});
