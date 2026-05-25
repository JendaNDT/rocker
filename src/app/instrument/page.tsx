"use client";

import { ProfileCard } from "@/components/instrument/ProfileCard";
import { ToneAdvisor } from "@/components/instrument/ToneAdvisor";
import { PageContainer } from "@/components/PageContainer";
import { useResolvedLayoutMode } from "@/lib/layout";
import { useHydrated } from "@/lib/useHydrated";

// Instrument profile card next to the tone advisor on desktop, stacked on mobile.
const INSTRUMENT_GRID = {
  desktop: "mt-6 grid grid-cols-2 items-start gap-6",
  mobile: "mt-6 space-y-4",
  auto: "mt-6 space-y-4 lg:grid lg:grid-cols-2 lg:items-start lg:gap-6 lg:space-y-0",
};

export default function InstrumentPage() {
  const hydrated = useHydrated();
  const layoutMode = useResolvedLayoutMode();

  return (
    <PageContainer>
      <h1 className="text-3xl font-bold tracking-tight">Kytara</h1>
      <p className="mt-1 text-dim">
        Digitální pas tvého nástroje a tónový poradce.
      </p>

      {hydrated ? (
        <div className={INSTRUMENT_GRID[layoutMode]}>
          <ProfileCard />
          <ToneAdvisor />
        </div>
      ) : (
        <p className="mt-6 text-sm text-muted">Načítám profil…</p>
      )}
    </PageContainer>
  );
}
