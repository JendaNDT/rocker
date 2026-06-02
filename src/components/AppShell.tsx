"use client";

import { AchievementWatcher } from "@/components/AchievementWatcher";
import { AmpHost } from "@/components/amp/AmpHost";
import { BreakReminder } from "@/components/BreakReminder";
import { Nav } from "@/components/Nav";
import { Onboarding } from "@/components/Onboarding";
import { PracticeTimeTracker } from "@/components/PracticeTimeTracker";
import { SettingsButton } from "@/components/SettingsButton";
import { ThemeProvider } from "@/components/ThemeProvider";
import { VintageFrame } from "@/components/VintageFrame";
import { useResolvedLayoutMode } from "@/lib/layout";
import type { LayoutMode } from "@/stores/settingsStore";

// Content padding leaves room for the nav (bottom bar or side rail).
const MAIN: Record<LayoutMode, string> = {
  auto: "min-h-screen pb-24 md:pb-0 md:pl-56",
  desktop: "min-h-screen pl-56",
  mobile: "min-h-screen pb-24",
};

/**
 * Client-side app frame: navigation, the permanent settings button, and the
 * padded content area. Reads the resolved layout mode so forced desktop/mobile
 * works on any screen size.
 */
export function AppShell({ children }: { children: React.ReactNode }) {
  const layout = useResolvedLayoutMode();

  return (
    <>
      <ThemeProvider />
      <AmpHost />
      <VintageFrame />
      <Nav layout={layout} />
      <SettingsButton />
      <PracticeTimeTracker />
      <AchievementWatcher />
      <BreakReminder />
      <Onboarding />
      <main className={MAIN[layout]}>{children}</main>
    </>
  );
}
