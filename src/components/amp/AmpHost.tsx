"use client";

import { useEffect } from "react";
import { ampEngine } from "@/audio/ampEngine";
import { useAmpStore } from "@/stores/ampStore";

/**
 * Mounts under <AppShell> and keeps the shared AmpEngine in sync with the
 * persisted amp settings. Because the engine is a module-level singleton and
 * AppShell survives client-side navigation, the aparát keeps playing when the
 * user moves between screens. Renders nothing — pattern mirrors ThemeProvider.
 *
 * setSettings stores the values on the engine even when the chain is not
 * running, so a later start() picks up the latest tone. When the chain is
 * live, the change is applied to it immediately.
 */
export function AmpHost() {
  const settings = useAmpStore((s) => s.settings);

  useEffect(() => {
    ampEngine.setSettings(settings);
  }, [settings]);

  return null;
}
