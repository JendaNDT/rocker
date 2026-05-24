import { create } from "zustand";
import {
  type Marker,
  type Recording,
  type RecordingMeta,
  toMeta,
} from "@/lib/recordings";
import {
  deleteRecording,
  listRecordings,
  saveRecording,
  updateRecording,
} from "@/lib/recordingsDb";

interface RecordingsState {
  /** Recording metadata, newest first. Mirror of the IndexedDB store. */
  recordings: RecordingMeta[];
  /** True once the initial load from IndexedDB has completed. */
  loaded: boolean;
  /** False when the recordings storage (IndexedDB) is unavailable. */
  storageAvailable: boolean;
  /** Loads the recording list from IndexedDB into memory. */
  load: () => Promise<void>;
  /** Persists a new recording and prepends it to the list. */
  addRecording: (recording: Recording) => Promise<void>;
  /** Deletes a recording from storage and the list. */
  removeRecording: (id: string) => Promise<void>;
  /** Renames a recording. */
  renameRecording: (id: string, name: string) => Promise<void>;
  /** Replaces the markers of a recording. */
  setMarkers: (id: string, markers: Marker[]) => Promise<void>;
}

/**
 * In-memory mirror of the IndexedDB recordings store (Bod 11). It is not a
 * Zustand persist store — IndexedDB is the source of truth and this store
 * just reflects it. Audio blobs are never held here; the UI fetches a single
 * blob on demand via getRecording().
 *
 * Every storage call is guarded: if IndexedDB is unavailable (e.g. a strict
 * private-browsing mode) the failure is caught, `storageAvailable` flips to
 * false and the UI can tell the user — the app never crashes on it.
 */
export const useRecordingsStore = create<RecordingsState>()((set) => ({
  recordings: [],
  loaded: false,
  storageAvailable: true,

  load: async () => {
    try {
      const recordings = await listRecordings();
      set({ recordings, loaded: true });
    } catch {
      // Finish loading anyway so the UI does not hang waiting for storage.
      set({ loaded: true, storageAvailable: false });
    }
  },

  addRecording: async (recording) => {
    try {
      await saveRecording(recording);
      set((state) => ({
        recordings: [toMeta(recording), ...state.recordings],
      }));
    } catch {
      set({ storageAvailable: false });
    }
  },

  removeRecording: async (id) => {
    try {
      await deleteRecording(id);
      set((state) => ({
        recordings: state.recordings.filter((item) => item.id !== id),
      }));
    } catch {
      set({ storageAvailable: false });
    }
  },

  renameRecording: async (id, name) => {
    try {
      await updateRecording(id, { name });
      set((state) => ({
        recordings: state.recordings.map((item) =>
          item.id === id ? { ...item, name } : item,
        ),
      }));
    } catch {
      set({ storageAvailable: false });
    }
  },

  setMarkers: async (id, markers) => {
    try {
      await updateRecording(id, { markers });
      set((state) => ({
        recordings: state.recordings.map((item) =>
          item.id === id ? { ...item, markers } : item,
        ),
      }));
    } catch {
      set({ storageAvailable: false });
    }
  },
}));
