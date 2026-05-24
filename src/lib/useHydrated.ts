"use client";

import { useEffect, useState } from "react";

/**
 * Returns false during server prerender and the first client render, then
 * true once mounted. Use to gate values that come from persisted storage so
 * the first render matches the prerendered HTML (no hydration mismatch).
 */
export function useHydrated(): boolean {
  const [hydrated, setHydrated] = useState(false);
  useEffect(() => {
    setHydrated(true);
  }, []);
  return hydrated;
}
