"use client";

import { useEffect } from "react";
import {
  useSettingsStore,
  type Theme,
  type ColorMode,
} from "@/stores/settingsStore";

const VALID_THEMES: readonly Theme[] = ["studio", "vintage", "glassy"];
const VALID_MODES: readonly ColorMode[] = ["light", "dark", "auto"];

/**
 * Mounts under <AppShell> and keeps the <html> data-theme / data-mode
 * attributes in sync with the settings store. "auto" mode follows the
 * OS preference via prefers-color-scheme.
 *
 * Defensive: if the store ever holds an invalid value (shouldn't happen,
 * but persistance lives on the user's device), fall back to "studio"/"dark".
 */
export function ThemeProvider() {
  const theme = useSettingsStore((s) => s.theme);
  const colorMode = useSettingsStore((s) => s.colorMode);

  useEffect(() => {
    const html = document.documentElement;
    const safeTheme = VALID_THEMES.includes(theme) ? theme : "studio";
    html.setAttribute("data-theme", safeTheme);
  }, [theme]);

  useEffect(() => {
    const html = document.documentElement;
    const safeMode = VALID_MODES.includes(colorMode) ? colorMode : "dark";

    const apply = (resolved: "light" | "dark") => {
      html.setAttribute("data-mode", resolved);
    };

    if (safeMode === "auto") {
      const mql = window.matchMedia("(prefers-color-scheme: dark)");
      apply(mql.matches ? "dark" : "light");
      const onChange = (e: MediaQueryListEvent) =>
        apply(e.matches ? "dark" : "light");
      mql.addEventListener("change", onChange);
      return () => mql.removeEventListener("change", onChange);
    }
    apply(safeMode);
  }, [colorMode]);

  return null;
}
