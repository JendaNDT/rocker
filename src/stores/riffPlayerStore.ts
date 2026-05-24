import { create } from "zustand";
import { riffPlayer } from "@/audio/riffPlayer";
import type { RiffPlayerState } from "@/audio/riffPlayer";

/**
 * Reactive mirror of the RiffPlayer state for React components.
 * The riff player (architecture layer 5) is the source of truth; this store
 * only reflects it. Call `riffPlayer` methods to make changes.
 */
export const useRiffPlayerStore = create<RiffPlayerState>(() =>
  riffPlayer.getState(),
);

riffPlayer.subscribe((state) => {
  useRiffPlayerStore.setState(state, true);
});
