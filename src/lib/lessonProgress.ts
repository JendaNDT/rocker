// Pure validation for persisted lesson progress. Lives here, not in the store,
// so it can be unit-tested without the store's localStorage side effects.

/**
 * Coerces a rehydrated value into a clean list of lesson ids: an array of
 * unique, non-empty strings. A non-array, or non-string / empty entries, are
 * dropped — so corrupt or hand-edited localStorage (this key is also part of
 * the data backup) cannot crash the dashboard, daily plan or AI tutor that
 * read this list with .includes() / .length.
 */
export function sanitizeCompletedLessonIds(value: unknown): string[] {
  if (!Array.isArray(value)) return [];
  const seen = new Set<string>();
  for (const entry of value) {
    if (typeof entry === "string" && entry.length > 0) seen.add(entry);
  }
  return [...seen];
}
