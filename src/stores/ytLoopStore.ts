import { create } from "zustand";
import { persist } from "zustand/middleware";

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
    { name: "rocker-yt-loops" },
  ),
);
