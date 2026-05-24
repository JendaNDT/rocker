import { create } from "zustand";
import { metronome } from "@/audio/metronome";
import type { MetronomeState } from "@/audio/metronome";

/**
 * Reactive mirror of the Metronome state for React components.
 * The metronome (architecture layer 5) is the source of truth; this store
 * only reflects it. Call `metronome` methods to make changes.
 */
export const useMetronomeStore = create<MetronomeState>(() =>
  metronome.getState(),
);

metronome.subscribe((state) => {
  useMetronomeStore.setState(state, true);
});
