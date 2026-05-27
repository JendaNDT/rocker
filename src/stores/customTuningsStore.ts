// Custom guitar tunings — user-defined alternates to the factory list in
// src/audio/tunings.ts. Saved in localStorage so they survive across
// sessions; surfaced in the Guided tuner select and resolved by
// resolveTuning everywhere else (Library, Games, Metal, Tutor).

import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { TuningString } from "@/audio/tunings";
import { midiToLabel } from "@/lib/pitch";

/**
 * Defensive cap on the number of saved custom tunings. The list is one of
 * the things that get hand-edited in localStorage during debugging, so an
 * upper bound keeps the select from blowing up.
 */
export const MAX_CUSTOM_TUNINGS = 20;
/** Lowest MIDI note a custom string can be tuned to — roughly E1. */
export const MIN_TUNING_MIDI = 24;
/** Highest MIDI note a custom string can be tuned to — roughly C6. */
export const MAX_TUNING_MIDI = 84;
/** Every tuning has six strings. */
export const STRINGS_PER_TUNING = 6;
/** Prefix used so custom ids can never collide with factory ones. */
const CUSTOM_PREFIX = "custom-";

export interface CustomTuning {
  id: string;
  label: string;
  strings: TuningString[];
}

/** Generates an id like "custom-…". Uses crypto.randomUUID when available. */
function newCustomTuningId(): string {
  if (
    typeof globalThis.crypto !== "undefined" &&
    typeof globalThis.crypto.randomUUID === "function"
  ) {
    return `${CUSTOM_PREFIX}${globalThis.crypto.randomUUID()}`;
  }
  // Fallback for environments without crypto.randomUUID — combines time
  // with a small random suffix. Good enough for a local list of 20 items.
  return `${CUSTOM_PREFIX}${Date.now()}-${Math.floor(Math.random() * 1e9).toString(36)}`;
}

/** Whether an id was minted by us (i.e. lives in this store). */
export function isCustomTuningId(id: string): boolean {
  return typeof id === "string" && id.startsWith(CUSTOM_PREFIX);
}

/**
 * Validates raw input that might come from the editor or persisted storage.
 * Returns the strings cast to TuningString[] when every entry has a valid
 * MIDI value in range and the array is the correct length; otherwise null.
 *
 * NOTE: we do NOT require strict low→high monotonic ordering. Exotic
 * tunings (Nashville, custom slack-key variants) can break the rule, and
 * a future editor extension may rely on this allowance.
 */
function sanitizeStrings(input: unknown): TuningString[] | null {
  if (!Array.isArray(input)) return null;
  if (input.length !== STRINGS_PER_TUNING) return null;
  const out: TuningString[] = [];
  for (const entry of input) {
    if (typeof entry !== "object" || entry === null) return null;
    const e = entry as Record<string, unknown>;
    if (typeof e.midi !== "number") return null;
    if (!Number.isInteger(e.midi)) return null;
    if (e.midi < MIN_TUNING_MIDI || e.midi > MAX_TUNING_MIDI) return null;
    const note = typeof e.note === "string" ? e.note : midiToLabel(e.midi);
    out.push({ note, midi: e.midi });
  }
  return out;
}

/** Sanitizes one CustomTuning candidate; returns null when it can't be used. */
function sanitizeOne(input: unknown): CustomTuning | null {
  if (typeof input !== "object" || input === null) return null;
  const entry = input as Record<string, unknown>;
  if (typeof entry.id !== "string") return null;
  if (!isCustomTuningId(entry.id)) return null;
  const label =
    typeof entry.label === "string" ? entry.label.trim() : "";
  if (label.length === 0) return null;
  const strings = sanitizeStrings(entry.strings);
  if (!strings) return null;
  return { id: entry.id, label, strings };
}

/**
 * Sanitizes a raw list of custom tunings from storage. Drops anything
 * invalid, dedupes ids, and enforces the MAX_CUSTOM_TUNINGS cap.
 */
function sanitizeList(input: unknown): CustomTuning[] {
  if (!Array.isArray(input)) return [];
  const out: CustomTuning[] = [];
  const seen = new Set<string>();
  for (const entry of input) {
    const sanitized = sanitizeOne(entry);
    if (!sanitized) continue;
    if (seen.has(sanitized.id)) continue;
    seen.add(sanitized.id);
    out.push(sanitized);
    if (out.length >= MAX_CUSTOM_TUNINGS) break;
  }
  return out;
}

interface CustomTuningsState {
  tunings: CustomTuning[];
  /**
   * Adds a new custom tuning. Trims the label; refuses empty labels and
   * over-cap additions. Returns the new id on success, null otherwise.
   */
  addTuning: (label: string, strings: TuningString[]) => string | null;
  /** Removes a tuning by id. Returns true when something was removed. */
  removeTuning: (id: string) => boolean;
}

export const useCustomTuningsStore = create<CustomTuningsState>()(
  persist(
    (set, get) => ({
      tunings: [],
      addTuning: (label, strings) => {
        const trimmed = label.trim();
        if (trimmed.length === 0) return null;
        const validStrings = sanitizeStrings(strings);
        if (!validStrings) return null;
        if (get().tunings.length >= MAX_CUSTOM_TUNINGS) return null;
        const id = newCustomTuningId();
        set((state) => ({
          tunings: [
            ...state.tunings,
            { id, label: trimmed, strings: validStrings },
          ],
        }));
        return id;
      },
      removeTuning: (id) => {
        const before = get().tunings.length;
        set((state) => ({
          tunings: state.tunings.filter((tuning) => tuning.id !== id),
        }));
        return get().tunings.length !== before;
      },
    }),
    {
      name: "rocker-custom-tunings",
      merge: (persisted, current) => {
        const safe = { ...current };
        if (typeof persisted === "object" && persisted !== null) {
          const stored = persisted as Record<string, unknown>;
          safe.tunings = sanitizeList(stored.tunings);
        }
        return safe;
      },
    },
  ),
);

// Exported for tests so they can exercise the sanitizer directly without
// touching localStorage. Internal helpers are deliberately not exported.
export const _internal = {
  sanitizeList,
  sanitizeOne,
  sanitizeStrings,
};
