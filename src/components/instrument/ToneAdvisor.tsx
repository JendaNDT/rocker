"use client";

import { useState } from "react";
import {
  GAIN_LABELS,
  POSITION_LABELS,
  STYLES,
  recommendTone,
  type ToneStyle,
} from "@/lib/instrument";
import { useInstrumentStore } from "@/stores/instrumentStore";

const STYLE_ACTIVE =
  "rounded-lg border border-amber-600 bg-amber-500/15 px-3 py-2 text-sm text-amber-400";
const STYLE_IDLE =
  "rounded-lg border border-neutral-800 bg-neutral-950 px-3 py-2 text-sm text-neutral-300 transition-colors hover:border-amber-600";

/** One labelled value in the advice panel. */
function AdviceRow({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-xs font-medium text-neutral-500">{label}</div>
      <div className="mt-0.5 text-sm text-neutral-100">{value}</div>
    </div>
  );
}

/**
 * Tone advisor (Bod 21): the user picks a style, the advisor suggests pickup,
 * gain and EQ — adapted to the instrument profile. Advisory only; the app
 * never changes anything itself, the user dials it in by hand.
 */
export function ToneAdvisor() {
  const profile = useInstrumentStore((state) => state.profile);
  const [style, setStyle] = useState<ToneStyle | null>(null);
  const advice = style ? recommendTone(profile, style) : null;

  return (
    <div className="rounded-lg border border-neutral-800 bg-neutral-900 p-4">
      <h2 className="text-lg font-semibold">Tónový poradce</h2>
      <p className="mt-1 text-sm text-neutral-400">
        Vyber styl a poradím snímač, gain a EQ podle tvého profilu. Je to jen
        rada — nastavení si na aparátu otočíš sám.
      </p>

      <div className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-3">
        {STYLES.map((option) => (
          <button
            key={option.id}
            type="button"
            onClick={() => setStyle(option.id)}
            className={style === option.id ? STYLE_ACTIVE : STYLE_IDLE}
          >
            {option.label}
          </button>
        ))}
      </div>

      {advice && (
        <div className="mt-4 rounded-lg border border-neutral-800 bg-neutral-950 p-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <AdviceRow
              label="Snímač"
              value={POSITION_LABELS[advice.pickup]}
            />
            <AdviceRow label="Gain" value={GAIN_LABELS[advice.gain]} />
          </div>
          <div className="mt-3">
            <AdviceRow label="EQ" value={advice.eq} />
          </div>
          <p className="mt-3 border-t border-neutral-800 pt-3 text-sm text-amber-200/80">
            {advice.note}
          </p>
        </div>
      )}
    </div>
  );
}
