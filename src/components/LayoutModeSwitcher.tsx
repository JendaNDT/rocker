"use client";

import { useEffect, useState } from "react";
import { useSettingsStore } from "@/stores/settingsStore";
import type { LayoutMode } from "@/stores/settingsStore";

const OPTIONS: { value: LayoutMode; label: string; hint: string }[] = [
  { value: "auto", label: "Automaticky", hint: "Řídí se velikostí okna." },
  { value: "desktop", label: "Počítač", hint: "Vždy boční lišta." },
  { value: "mobile", label: "Mobil", hint: "Vždy spodní lišta." },
];

/**
 * Lets the user pick how the app is laid out. Persisted via the settings store.
 * Before hydration it shows "Automaticky" as selected to match the prerendered
 * HTML; the real stored choice appears right after mount.
 */
export function LayoutModeSwitcher() {
  const layoutMode = useSettingsStore((state) => state.layoutMode);
  const setLayoutMode = useSettingsStore((state) => state.setLayoutMode);

  const [hydrated, setHydrated] = useState(false);
  useEffect(() => {
    setHydrated(true);
  }, []);

  const current: LayoutMode = hydrated ? layoutMode : "auto";

  return (
    <div className="grid gap-2 sm:grid-cols-3">
      {OPTIONS.map((option) => {
        const active = current === option.value;
        return (
          <button
            key={option.value}
            type="button"
            onClick={() => setLayoutMode(option.value)}
            aria-pressed={active}
            className={[
              "rounded-lg border p-3 text-left transition-colors",
              active
                ? "border-accent bg-surface"
                : "border-line bg-surface hover:border-line-strong",
            ].join(" ")}
          >
            <div
              className={
                active ? "font-semibold text-accent" : "font-semibold"
              }
            >
              {option.label}
            </div>
            <div className="mt-1 text-sm text-dim">{option.hint}</div>
          </button>
        );
      })}
    </div>
  );
}
