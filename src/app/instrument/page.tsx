"use client";

import { ProfileCard } from "@/components/instrument/ProfileCard";
import { ToneAdvisor } from "@/components/instrument/ToneAdvisor";
import { useHydrated } from "@/lib/useHydrated";

export default function InstrumentPage() {
  const hydrated = useHydrated();

  return (
    <div className="mx-auto max-w-3xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">Kytara</h1>
      <p className="mt-1 text-neutral-400">
        Digitální pas tvého nástroje a tónový poradce.
      </p>

      {hydrated ? (
        <div className="mt-6 space-y-4">
          <ProfileCard />
          <ToneAdvisor />
        </div>
      ) : (
        <p className="mt-6 text-sm text-neutral-500">Načítám profil…</p>
      )}
    </div>
  );
}
