"use client";

import Link from "next/link";
import { Dashboard } from "@/components/dashboard/Dashboard";
import { PageContainer } from "@/components/PageContainer";
import { useResolvedLayoutMode } from "@/hooks/useResolvedLayoutMode";
import { SCREENS } from "@/lib/screens";

// "Vše v aplikaci" tiles, following the Auto / Desktop / Mobile switch.
const SECTION_GRID = {
  desktop: "mt-3 grid grid-cols-3 gap-3",
  mobile: "mt-3 grid grid-cols-1 gap-3",
  auto: "mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3",
};

export default function HomePage() {
  const layoutMode = useResolvedLayoutMode();
  const sections = SCREENS.filter((screen) => screen.href !== "/");

  return (
    <PageContainer>
      <h1 className="text-3xl font-bold tracking-tight">Rocker</h1>
      <p className="mt-1 text-dim">
        Kytarový asistent — tvůj denní přehled.
      </p>

      <div className="mt-6">
        <Dashboard />
      </div>

      <h2 className="mt-8 text-lg font-semibold">Vše v aplikaci</h2>
      <div className={SECTION_GRID[layoutMode]}>
        {sections.map((screen) => (
          <Link
            key={screen.href}
            href={screen.href}
            className="rounded-lg border border-line bg-surface p-4 transition-colors hover:border-accent"
          >
            <div className="font-semibold">{screen.label}</div>
            <div className="mt-1 text-sm text-dim">{screen.description}</div>
          </Link>
        ))}
      </div>
    </PageContainer>
  );
}
