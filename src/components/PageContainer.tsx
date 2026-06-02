"use client";

import type { ReactNode } from "react";
import { useResolvedLayoutMode } from "@/hooks/useResolvedLayoutMode";

/**
 * Page-level width container. Follows the same Auto / Desktop / Mobile switch
 * as the navigation (useResolvedLayoutMode).
 *
 * - "wide" (default): narrow on mobile, wider on desktop so two-column page
 *   layouts have room to breathe ("auto" widens responsively at lg).
 * - "focused": a moderate centred column on every viewport — for focused
 *   single-task screens where full width would only stretch the content.
 */
export function PageContainer({
  children,
  size = "wide",
}: {
  children: ReactNode;
  size?: "wide" | "focused";
}) {
  const mode = useResolvedLayoutMode();
  let width: string;
  if (size === "focused") {
    width = "max-w-3xl";
  } else {
    width =
      mode === "desktop"
        ? "max-w-5xl"
        : mode === "mobile"
          ? "max-w-xl"
          : "max-w-xl lg:max-w-5xl";
  }
  return <div className={`mx-auto ${width} p-6`}>{children}</div>;
}
