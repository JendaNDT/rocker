"use client";

import type { CSSProperties } from "react";
import { useHydrated } from "@/lib/useHydrated";
import { useSettingsStore } from "@/stores/settingsStore";

/**
 * Mosazné cvočky (upholstery tacks) ve 4 rozích viewportu — vykreslí se
 * jen ve Vintage tématu. Fixed overlay, nezasahuje do kliků.
 */
export function VintageFrame() {
  // Gate on hydration: the persisted theme is only known on the client, so
  // rendering the studs before hydration would mismatch the prerendered HTML.
  const hydrated = useHydrated();
  const theme = useSettingsStore((s) => s.theme);
  if (!hydrated || theme !== "vintage") return null;

  const stud: CSSProperties = {
    position: "fixed",
    width: 12,
    height: 12,
    borderRadius: "50%",
    zIndex: 5,
    pointerEvents: "none",
    background:
      "radial-gradient(circle at 32% 28%, #f5e0a6 0%, #c0a070 25%, #8a6f3a 65%, #4a3815 100%)",
    boxShadow:
      "inset 0 -1px 2px rgba(60,40,12,0.5), " +
      "inset 0 1px 1px rgba(255,250,200,0.45), " +
      "0 2px 3px rgba(60,40,12,0.45), " +
      "0 0 0 0.5px #4a3815",
  };
  return (
    <>
      <span style={{ ...stud, top: 12, left: 12 }} />
      <span style={{ ...stud, top: 12, right: 12 }} />
      <span style={{ ...stud, bottom: 12, left: 12 }} />
      <span style={{ ...stud, bottom: 12, right: 12 }} />
    </>
  );
}
