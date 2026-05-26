"use client";

import { useHydrated } from "./useHydrated";
import { useSettingsStore, type Theme } from "@/stores/settingsStore";

/**
 * Returns the active theme name once the client has hydrated, otherwise
 * null (during SSR and the first client render).
 *
 * Polymorphic widgets use this to render theme-specific variants
 * (e.g. brass VU dial vs. neon glow vs. paper ink marker). Returning null
 * before hydration keeps the first render matching the prerendered HTML.
 *
 * Same pattern as VintageFrame, generalised so multiple widgets can share
 * it without each one re-implementing the hydration gate.
 */
export function useActiveTheme(): Theme | null {
  const hydrated = useHydrated();
  const theme = useSettingsStore((s) => s.theme);
  return hydrated ? theme : null;
}
