// Pure shape + identity helpers for user-defined tunings — split out of
// customTuningsStore so the lib layer (e.g. resolveTuning) can import the
// type and id check without reaching up into the stores layer. The store
// re-exports these for backward compatibility with existing call sites.

import type { TuningString } from "@/audio/tunings";

/** Prefix used so custom ids can never collide with factory ones. */
export const CUSTOM_TUNING_PREFIX = "custom-";

export interface CustomTuning {
  id: string;
  label: string;
  strings: TuningString[];
}

/** Whether an id was minted by the custom tunings store. */
export function isCustomTuningId(id: string): boolean {
  return typeof id === "string" && id.startsWith(CUSTOM_TUNING_PREFIX);
}
