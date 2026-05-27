import { create } from "zustand";
import { persist } from "zustand/middleware";
import {
  DEFAULT_REFERENCE_TONE_VOLUME,
  clampReferenceVolume,
} from "@/audio/referenceTone";
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
  /**
   * Noise gate (0..1, same scale as the input level meter). Pitch detection
   * ignores input quieter than this, filtering ambient room noise. 0 = off.
   */
  noiseGate: number;
  setNoiseGate: (noiseGate: number) => void;
  /**
   * Master volume (0..1) for the in-tuner reference tone. The Guided tuner
   * plays this when the user holds the "Přehrát" button next to a string.
   */
  referenceToneVolume: number;
  setReferenceToneVolume: (volume: number) => void;
  /**
   * Play a short chime when the Guided tuner detects a string has settled
   * in the in-tune zone. Off by default — the user opts in.
   */
  tunerConfirmSound: boolean;
  setTunerConfirmSound: (enabled: boolean) => void;
  /**
   * Fire a haptic pulse when the Guided tuner detects a string has settled
   * in the in-tune zone. Off by default. Silently a no-op on devices that
   * do not support navigator.vibrate (iOS Safari).
   */
  tunerConfirmHaptic: boolean;
  setTunerConfirmHaptic: (enabled: boolean) => void;
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
      noiseGate: 0.06,
      setNoiseGate: (noiseGate) => set({ noiseGate }),
      referenceToneVolume: DEFAULT_REFERENCE_TONE_VOLUME,
      setReferenceToneVolume: (volume) =>
        set({ referenceToneVolume: clampReferenceVolume(volume) }),
      tunerConfirmSound: false,
      setTunerConfirmSound: (enabled) => set({ tunerConfirmSound: enabled }),
      tunerConfirmHaptic: false,
      setTunerConfirmHaptic: (enabled) =>
        set({ tunerConfirmHaptic: enabled }),
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
          if (
            typeof stored.noiseGate === "number" &&
            Number.isFinite(stored.noiseGate) &&
            stored.noiseGate >= 0 &&
            stored.noiseGate <= 1
          ) {
            safe.noiseGate = stored.noiseGate;
          }
          if (stored.referenceToneVolume !== undefined) {
            // clampReferenceVolume falls back to the default for garbage,
            // so we can pass it through unconditionally.
            safe.referenceToneVolume = clampReferenceVolume(
              stored.referenceToneVolume,
            );
          }
          if (typeof stored.tunerConfirmSound === "boolean") {
            safe.tunerConfirmSound = stored.tunerConfirmSound;
          }
          if (typeof stored.tunerConfirmHaptic === "boolean") {
            safe.tunerConfirmHaptic = stored.tunerConfirmHaptic;
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
