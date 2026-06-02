import { create } from "zustand";
import { persist } from "zustand/middleware";
import { sanitizeSavedLoops } from "@/lib/ytLoops";

export interface SavedLoop {
  id: string;
  videoId: string;
  label: string;
  start: number;
  end: number;
  rate: number;
}

interface YtLoopState {
  loops: SavedLoop[];
  addLoop: (loop: Omit<SavedLoop, "id">) => void;
  removeLoop: (id: string) => void;
}

/**
 * Saved YouTube practice loops, persisted in localStorage under
 * "rocker-yt-loops". This is small JSON data, so localStorage is enough —
 * IndexedDB is reserved for large blobs (recordings, uploaded audio).
 */
export const useYtLoopStore = create<YtLoopState>()(
  persist(
    (set) => ({
      loops: [],
      addLoop: (loop) =>
        set((state) => ({
          loops: [...state.loops, { ...loop, id: crypto.randomUUID() }],
        })),
      removeLoop: (id) =>
        set((state) => ({
          loops: state.loops.filter((loop) => loop.id !== id),
        })),
    }),
    {
      name: "rocker-yt-loops",
      // Defensive: corrupt or hand-edited data must not crash the YouTube
      // player (loops.filter). Drop any malformed loop on rehydrate.
      merge: (persistedState, currentState) => {
        const persisted =
          persistedState && typeof persistedState === "object"
            ? (persistedState as { loops?: unknown })
            : {};
        return {
          ...currentState,
          loops: sanitizeSavedLoops(persisted.loops),
        };
      },
    },
  ),
);
