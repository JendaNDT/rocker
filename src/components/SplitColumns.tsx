"use client";

import type { ReactNode } from "react";
import { useResolvedLayoutMode } from "@/hooks/useResolvedLayoutMode";

/**
 * Two-column page layout that collapses to a single stacked column on mobile
 * or a narrow viewport. Follows the same Auto / Desktop / Mobile switch as the
 * navigation (useResolvedLayoutMode): "auto" splits into two columns at the lg
 * breakpoint, "desktop" always splits, "mobile" always stacks.
 *
 * When stacked, the two slots simply follow each other — each slot's own
 * content carries its vertical spacing.
 */
export function SplitColumns({
  left,
  right,
}: {
  left: ReactNode;
  right: ReactNode;
}) {
  const mode = useResolvedLayoutMode();
  const grid =
    mode === "desktop"
      ? "grid grid-cols-2 items-start gap-x-10"
      : mode === "mobile"
        ? ""
        : "lg:grid lg:grid-cols-2 lg:items-start lg:gap-x-10";
  return (
    <div className={grid}>
      <div>{left}</div>
      <div>{right}</div>
    </div>
  );
}
