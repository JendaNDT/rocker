"use client";

import { useEffect, useState } from "react";
import { AiSettings } from "@/components/AiSettings";
import { AudioInputSettings } from "@/components/AudioInputSettings";
import { DataBackup } from "@/components/DataBackup";
import { LayoutModeSwitcher } from "@/components/LayoutModeSwitcher";
import { PageContainer } from "@/components/PageContainer";
import { ThemePicker } from "@/components/ThemePicker";
import { ProfileCard } from "@/components/instrument/ProfileCard";
import { ToneAdvisor } from "@/components/instrument/ToneAdvisor";
import { ProgressDetail } from "@/components/profile/ProgressDetail";
import { TunerSettings } from "@/components/tuner/TunerSettings";
import { useResolvedLayoutMode } from "@/hooks/useResolvedLayoutMode";
import type { Notation } from "@/lib/pitch";
import { useSettingsStore } from "@/stores/settingsStore";

const SEGMENT_ACTIVE =
  "rounded-md bg-accent-soft px-3 py-1.5 text-sm text-accent";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

const NOTATION_OPTIONS: { value: Notation; label: string }[] = [
  { value: "english", label: "Mezinárodní (B)" },
  { value: "german", label: "Německá (H)" },
];

// Settings sections flow into two balanced columns on desktop. CSS multi-column
// keeps the original document order (so mobile is unchanged) and never splits a
// section across the column boundary (break-inside-avoid on each section).
const SETTINGS_COLUMNS = {
  desktop: "columns-2 gap-x-10",
  mobile: "columns-1",
  auto: "columns-1 lg:columns-2 lg:gap-x-10",
};

// Instrument card next to the tone advisor on desktop, stacked on mobile.
const INSTRUMENT_GRID = {
  desktop: "mt-3 grid grid-cols-2 items-start gap-6",
  mobile: "mt-3 space-y-4",
  auto: "mt-3 space-y-4 lg:grid lg:grid-cols-2 lg:items-start lg:gap-6 lg:space-y-0",
};

export default function ProfilePage() {
  const zenMode = useSettingsStore((state) => state.zenMode);
  const setZenMode = useSettingsStore((state) => state.setZenMode);
  const notation = useSettingsStore((state) => state.notation);
  const setNotation = useSettingsStore((state) => state.setNotation);
  const setOnboarded = useSettingsStore((state) => state.setOnboarded);
  const layoutMode = useResolvedLayoutMode();

  const [hydrated, setHydrated] = useState(false);
  useEffect(() => {
    setHydrated(true);
  }, []);

  // Before hydration the prerendered HTML uses the defaults.
  const zen = hydrated && zenMode;
  const activeNotation = hydrated ? notation : "english";

  return (
    <PageContainer>
      <h1 className="font-display text-3xl font-bold tracking-tight">Profil</h1>
      <p className="mt-1 text-dim">Nastavení aplikace.</p>

      <section className="mt-8">
        <h2 className="text-lg font-semibold">Kytara</h2>
        <p className="mt-1 text-sm text-dim">
          Digitální pas tvého nástroje a tónový poradce.
        </p>
        {hydrated ? (
          <div className={INSTRUMENT_GRID[layoutMode]}>
            <ProfileCard />
            <ToneAdvisor />
          </div>
        ) : (
          <p className="mt-3 text-sm text-muted">Načítám profil…</p>
        )}
      </section>

      <div className={SETTINGS_COLUMNS[layoutMode]}>
        <section className="mt-8 break-inside-avoid">
          <h2 className="text-lg font-semibold">Vzhled</h2>
          <p className="mt-1 text-sm text-dim">
            Vyber si styl, který ti sedí. Můžeš ho měnit kdykoliv — nastavení se
            uloží.
          </p>
          <div className="mt-3">
            <ThemePicker />
          </div>
        </section>

        <section className="mt-8 break-inside-avoid">
          <h2 className="text-lg font-semibold">Zobrazení</h2>
          <p className="mt-1 text-sm text-dim">
            Jak se má aplikace zobrazovat. „Automaticky" se přizpůsobí velikosti
            okna.
          </p>
          <div className="mt-3">
            <LayoutModeSwitcher />
          </div>
        </section>

        <section className="mt-8 break-inside-avoid">
          <h2 className="text-lg font-semibold">Zvuk a vstup</h2>
          <p className="mt-1 text-sm text-dim">
            Odkud aplikace poslouchá kytaru.
          </p>
          <div className="mt-3">
            <AudioInputSettings />
          </div>
        </section>

        <section className="mt-8 break-inside-avoid">
          <h2 className="text-lg font-semibold">Ladička</h2>
          <p className="mt-1 text-sm text-dim">
            Nastavení pro Ladičku — třeba hlasitost referenčního tónu.
          </p>
          <div className="mt-3">
            <TunerSettings />
          </div>
        </section>

        <section className="mt-8 break-inside-avoid">
          <h2 className="text-lg font-semibold">AI tutor</h2>
          <p className="mt-1 text-sm text-dim">
            Chat „zeptej se" na techniku, cvičení nebo zvuk. Volitelné — funguje
            na vlastní bezplatný klíč Google Gemini.
          </p>
          <div className="mt-3">
            <AiSettings />
          </div>
        </section>

        <section className="mt-8 break-inside-avoid">
          <h2 className="text-lg font-semibold">Notace</h2>
          <p className="mt-1 text-sm text-dim">
            Jak aplikace pojmenovává tóny. Německá notace používá „H" pro tón
            mezinárodně značený „B".
          </p>
          <div className="mt-3 inline-flex rounded-lg border border-line p-1">
            {NOTATION_OPTIONS.map((option) => (
              <button
                key={option.value}
                type="button"
                onClick={() => setNotation(option.value)}
                className={
                  activeNotation === option.value
                    ? SEGMENT_ACTIVE
                    : SEGMENT_IDLE
                }
              >
                {option.label}
              </button>
            ))}
          </div>
        </section>

        <section className="mt-8 break-inside-avoid">
          <h2 className="text-lg font-semibold">Zen režim</h2>
          <p className="mt-1 text-sm text-dim">
            Skryje gamifikaci — statistiky, streaky, denní plán i achievementy.
            Pokrok se měří dál, appka ho jen nezobrazuje.
          </p>
          <label className="mt-3 flex items-center gap-2 text-sm text-text">
            <input
              type="checkbox"
              checked={zen}
              onChange={(event) => setZenMode(event.target.checked)}
              className="h-4 w-4 accent-accent"
            />
            Zapnout Zen režim
          </label>
        </section>

        {!zen && (
          <section className="mt-8 break-inside-avoid">
            <h2 className="text-lg font-semibold">Pokrok</h2>
            <p className="mt-1 text-sm text-dim">
              Tvoje cvičební statistiky. Achievementy najdeš na úvodní
              obrazovce.
            </p>
            <div className="mt-3">
              <ProgressDetail />
            </div>
          </section>
        )}

        <section className="mt-8 break-inside-avoid">
          <h2 className="text-lg font-semibold">Záloha dat</h2>
          <p className="mt-1 text-sm text-dim">
            Ulož si nastavení a pokrok do souboru, nebo je obnov ze zálohy.
          </p>
          <div className="mt-3">
            <DataBackup />
          </div>
        </section>

        <section className="mt-8 break-inside-avoid">
          <h2 className="text-lg font-semibold">Úvod</h2>
          <p className="mt-1 text-sm text-dim">
            Úvodní průvodce nastavením aplikace. Můžeš ho projít znovu.
          </p>
          <button
            type="button"
            onClick={() => setOnboarded(false)}
            className="mt-3 rounded-lg border border-line bg-surface px-4 py-2 text-sm transition-colors hover:border-line-strong"
          >
            Projít úvod znovu
          </button>
        </section>
      </div>
    </PageContainer>
  );
}
