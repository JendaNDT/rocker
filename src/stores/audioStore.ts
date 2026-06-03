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

// Keep the store in sync with the engine. The subscribe() call runs at
// module import time, including during the static prerender. That's safe
// here because audioEngine itself does NOT touch window / navigator
// inside its subscribe machinery (it just registers a listener in a
// Set). Anything that would need browser APIs sits behind a method call
// triggered by a user gesture, never at module top level — so this
// import does not break Next.js static export.
