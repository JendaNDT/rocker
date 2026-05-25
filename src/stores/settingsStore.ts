import { create } from "zustand";
import { persist } from "zustand/middleware";
import { DEFAULT_TUNING_ID, TUNINGS } from "@/audio/tunings";
import type { Notation } from "@/lib/pitch";

/**
 * How the app chrome (navigation) is laid out.
 * - "auto"    — follows the window size (CSS media queries)
 * - "desktop" — always the side rail, regardless of window size
 * - "mobile"  — always the bottom bar, regardless of window size
 */
export type LayoutMode = "auto" | "desktop" | "mobile";

export type Handedness = "right" | "left";

/**
 * Visual theme — affects palette, typography and decorations.
 * - "studio"  — dark studio look, brass accents, serif headlines
 * - "vintage" — pedal-like, brushed metal, screws, knobs
 * - "glassy"  — frosted glass over gradients, vibrant accent
 */
export type Theme = "studio" | "vintage" | "glassy";

/**
 * Light / dark colour mode.
 * - "light" — světlý
 * - "dark"  — tmavý
 * - "auto"  — řídí se systémovým nastavením OS (prefers-color-scheme)
 */
export type ColorMode = "light" | "dark" | "auto";

interface SettingsState {
  layoutMode: LayoutMode;
  setLayoutMode: (mode: LayoutMode) => void;
  /** Visual theme. */
  theme: Theme;
  setTheme: (theme: Theme) => void;
  /** Light/dark mode (auto = OS). */
  colorMode: ColorMode;
  setColorMode: (mode: ColorMode) => void;
  /** Selected guitar tuning id (see src/audio/tunings.ts). */
  tuningId: string;
  setTuningId: (id: string) => void;
  /** Playing hand — flips chord diagrams for left-handed players. */
  handedness: Handedness;
  setHandedness: (handedness: Handedness) => void;
  /** Hides gamification (dashboard, achievements) for a calmer, focused app. */
  zenMode: boolean;
  setZenMode: (zenMode: boolean) => void;
  /** Note naming: international ("english", B) or German/Czech ("german", H). */
  notation: Notation;
  setNotation: (notation: Notation) => void;
  /** Whether the first-run onboarding has been completed or skipped. */
  onboarded: boolean;
  setOnboarded: (onboarded: boolean) => void;
}

/**
 * Persistent app settings. Stored in localStorage under "rocker-settings".
 */
export const useSettingsStore = create<SettingsState>()(
  persist(
    (set) => ({
      layoutMode: "auto",
      setLayoutMode: (mode) => set({ layoutMode: mode }),
      theme: "studio",
      setTheme: (theme) => set({ theme }),
      colorMode: "dark", // historická default — appka byla dosud jen dark
      setColorMode: (colorMode) => set({ colorMode }),
      tuningId: DEFAULT_TUNING_ID,
      setTuningId: (id) => set({ tuningId: id }),
      handedness: "right",
      setHandedness: (handedness) => set({ handedness }),
      zenMode: false,
      setZenMode: (zenMode) => set({ zenMode }),
      notation: "english",
      setNotation: (notation) => set({ notation }),
      onboarded: false,
      setOnboarded: (onboarded) => set({ onboarded }),
    }),
    {
      name: "rocker-settings",
      // Stored data can be corrupt or written by an older app version. Each
      // field is validated here; anything invalid falls back to a safe
      // default so the app never starts in a broken state (see AGENTS.md).
      merge: (persisted, current) => {
        const safe = { ...current };
        if (typeof persisted === "object" && persisted !== null) {
          const stored = persisted as Record<string, unknown>;
          if (
            stored.layoutMode === "auto" ||
            stored.layoutMode === "desktop" ||
            stored.layoutMode === "mobile"
          ) {
            safe.layoutMode = stored.layoutMode;
          }
          if (
            stored.theme === "studio" ||
            stored.theme === "vintage" ||
            stored.theme === "glassy"
          ) {
            safe.theme = stored.theme;
          }
          if (
            stored.colorMode === "light" ||
            stored.colorMode === "dark" ||
            stored.colorMode === "auto"
          ) {
            safe.colorMode = stored.colorMode;
          }
          if (
            typeof stored.tuningId === "string" &&
            TUNINGS.some((tuning) => tuning.id === stored.tuningId)
          ) {
            safe.tuningId = stored.tuningId;
          }
          if (stored.handedness === "right" || stored.handedness === "left") {
            safe.handedness = stored.handedness;
          }
          if (stored.notation === "english" || stored.notation === "german") {
            safe.notation = stored.notation;
          }
          if (typeof stored.zenMode === "boolean") {
            safe.zenMode = stored.zenMode;
          }
          if (typeof stored.onboarded === "boolean") {
            safe.onboarded = stored.onboarded;
          }
        }
        return safe;
      },
    },
  ),
);
