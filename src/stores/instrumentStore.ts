import { create } from "zustand";
import { persist } from "zustand/middleware";
import {
  DEFAULT_PROFILE,
  sanitizeProfile,
  type InstrumentProfile,
} from "@/lib/instrument";

interface InstrumentState {
  /** The instrument's "digital passport". */
  profile: InstrumentProfile;
  /** Replaces the whole profile. */
  setProfile: (profile: InstrumentProfile) => void;
  /** Patches some fields of the profile. */
  updateProfile: (patch: Partial<InstrumentProfile>) => void;
  /** Restores the default profile. */
  reset: () => void;
}

/**
 * The instrument profile, persisted in localStorage under "rocker-instrument".
 * Small JSON data, so localStorage is enough. On rehydration the stored value
 * passes through sanitizeProfile so corrupt data can never crash the app.
 */
export const useInstrumentStore = create<InstrumentState>()(
  persist(
    (set) => ({
      profile: DEFAULT_PROFILE,
      setProfile: (profile) => set({ profile }),
      updateProfile: (patch) =>
        set((state) => ({ profile: { ...state.profile, ...patch } })),
      reset: () => set({ profile: { ...DEFAULT_PROFILE } }),
    }),
    {
      name: "rocker-instrument",
      merge: (persisted, current) => {
        const stored = persisted as { profile?: unknown } | undefined;
        return { ...current, profile: sanitizeProfile(stored?.profile) };
      },
    },
  ),
);
