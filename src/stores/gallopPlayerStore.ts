import { create } from "zustand";
import { gallopPlayer } from "@/audio/gallopPlayer";
import type { GallopPlayerState } from "@/audio/gallopPlayer";

/**
 * Reactive mirror of the GallopPlayer state for React components.
 * The gallop player (architecture layer 5) is the source of truth; this store
 * only reflects it. Call `gallopPlayer` methods to make changes.
 */
export const useGallopPlayerStore = create<GallopPlayerState>(() =>
  gallopPlayer.getState(),
);

gallopPlayer.subscribe((state) => {
  useGallopPlayerStore.setState(state, true);
});
