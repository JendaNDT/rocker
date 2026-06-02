// Pure validation for persisted YouTube practice loops. Lives here, not in the
// store, so it can be unit-tested without the store's localStorage side
// effects.

import type { SavedLoop } from "@/stores/ytLoopStore";

/** True if a rehydrated value is a structurally valid SavedLoop. */
function isValidLoop(value: unknown): value is SavedLoop {
  if (!value || typeof value !== "object") return false;
  const loop = value as Record<string, unknown>;
  return (
    typeof loop.id === "string" &&
    loop.id.length > 0 &&
    typeof loop.videoId === "string" &&
    loop.videoId.length > 0 &&
    typeof loop.label === "string" &&
    typeof loop.start === "number" &&
    Number.isFinite(loop.start) &&
    typeof loop.end === "number" &&
    Number.isFinite(loop.end) &&
    typeof loop.rate === "number" &&
    Number.isFinite(loop.rate)
  );
}

/**
 * Coerces a rehydrated value into a clean array of saved loops, dropping any
 * malformed entries. A non-array becomes []. Protects the YouTube player, which
 * calls loops.filter(), from crashing on corrupt or hand-edited localStorage.
 */
export function sanitizeSavedLoops(value: unknown): SavedLoop[] {
  if (!Array.isArray(value)) return [];
  return value.filter(isValidLoop);
}
