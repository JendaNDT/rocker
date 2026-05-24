"use client";

import { useEffect, useState } from "react";
import { useSettingsStore } from "@/stores/settingsStore";
import type { LayoutMode } from "@/stores/settingsStore";

/**
 * Returns the layout mode to actually render with.
 *
 * Before hydration it always reports "auto", so the server-prerendered HTML
 * and the first client render agree (no hydration mismatch). "auto" is driven
 * purely by CSS media queries, so it stays correct on every viewport.
 *
 * After hydration the user's stored choice ("desktop" / "mobile") takes over.
 * A user who forced a mode that does not match their viewport may see a brief
 * flash of the "auto" layout on first load — this is expected and minor.
 */
export function useResolvedLayoutMode(): LayoutMode {
  const layoutMode = useSettingsStore((state) => state.layoutMode);
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    setHydrated(true);
  }, []);

  return hydrated ? layoutMode : "auto";
}
