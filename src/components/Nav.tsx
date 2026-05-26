"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { SCREENS } from "@/lib/screens";
import type { LayoutMode } from "@/stores/settingsStore";

// Nav container — "auto" uses CSS breakpoints, the others are fixed.
const CONTAINER: Record<LayoutMode, string> = {
  auto: "fixed inset-x-0 bottom-0 z-10 flex overflow-x-auto border-t border-line bg-bg md:inset-y-0 md:right-auto md:left-0 md:w-56 md:flex-col md:overflow-x-visible md:overflow-y-auto md:border-t-0 md:border-r",
  desktop:
    "fixed inset-y-0 left-0 z-10 flex w-56 flex-col overflow-y-auto border-r border-line bg-bg",
  mobile:
    "fixed inset-x-0 bottom-0 z-10 flex overflow-x-auto border-t border-line bg-bg",
};

// "Rocker" wordmark — shown only when the side rail is visible.
const TITLE: Record<LayoutMode, string> = {
  auto: "hidden px-5 py-6 text-lg font-bold tracking-tight md:block",
  desktop: "px-5 py-6 text-lg font-bold tracking-tight",
  mobile: "hidden",
};

// Profile is reached through the permanent settings cog (SettingsButton) in
// the top-right corner, so it is left out of the nav to avoid a duplicate
// entry. It still appears in the home screen's "Vše v aplikaci" grid.
const NAV_SCREENS = SCREENS.filter((screen) => screen.href !== "/profile");

// Individual nav item.
const ITEM: Record<LayoutMode, string> = {
  auto: "flex shrink-0 items-center justify-center px-4 py-3 text-xs whitespace-nowrap transition-colors md:justify-start md:px-5 md:text-sm",
  desktop:
    "flex shrink-0 items-center justify-start px-5 py-3 text-sm whitespace-nowrap transition-colors",
  mobile:
    "flex shrink-0 items-center justify-center px-4 py-3 text-xs whitespace-nowrap transition-colors",
};

/**
 * App navigation. Renders as a side rail or a bottom bar depending on the
 * resolved layout mode. Active route is highlighted.
 */
export function Nav({ layout }: { layout: LayoutMode }) {
  const pathname = usePathname();

  return (
    <nav className={CONTAINER[layout]}>
      <div className={TITLE[layout]}>Rocker</div>
      {NAV_SCREENS.map((screen) => {
        const active = pathname === screen.href;
        return (
          <Link
            key={screen.href}
            href={screen.href}
            className={[
              ITEM[layout],
              active ? "text-accent" : "text-dim hover:text-text",
            ].join(" ")}
          >
            {screen.label}
          </Link>
        );
      })}
    </nav>
  );
}
