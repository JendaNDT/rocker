// Bridge between the factory tunings (src/audio/tunings.ts) and the
// user-defined ones (src/stores/customTuningsStore.ts).
//
// Every place that used to call `getTuning(id)` from `@/audio/tunings`
// should now go through `resolveTuning` so a custom id (`custom-…`) is
// looked up in the custom store rather than silently falling back to the
// first factory tuning. The Tuner UI builds its select from
// `resolveAllTunings()` so factory and custom appear together in a
// stable order.

import {
  DEFAULT_TUNING_ID,
  TUNINGS,
  getTuning,
  type Tuning,
} from "@/audio/tunings";
import { isCustomTuningId } from "@/stores/customTuningsStore";
import type { CustomTuning } from "@/stores/customTuningsStore";

/**
 * Resolves a tuning by id, falling back to the default ("standard") when
 * the id matches neither a factory nor a custom entry. Stable: never
 * throws, always returns a usable Tuning. Custom tunings are returned
 * untouched (same shape as the factory ones).
 */
export function resolveTuning(
  id: string,
  customs: readonly CustomTuning[],
): Tuning {
  if (isCustomTuningId(id)) {
    const found = customs.find((tuning) => tuning.id === id);
    if (found) return found;
    // Custom id pointing at a deleted tuning — fall through to default.
  } else {
    const found = TUNINGS.find((tuning) => tuning.id === id);
    if (found) return found;
  }
  return getTuning(DEFAULT_TUNING_ID);
}

/**
 * Returns every tuning the app knows about — factory first, then the
 * user's custom ones. Used for select boxes in the Tuner; consumers that
 * just need to look up one id should call resolveTuning instead.
 */
export function resolveAllTunings(
  customs: readonly CustomTuning[],
): Tuning[] {
  return [...TUNINGS, ...customs];
}
