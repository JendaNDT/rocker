"use client";

import { useActiveTheme } from "@/lib/useActiveTheme";

/**
 * Cents-deviation indicator. Marker position maps cents -50..+50 to 0..100%.
 *
 * Polymorphic: each theme has its own visual incarnation. Pre-hydration we
 * render the neutral default so the first paint matches the prerendered HTML.
 *
 * - studio  : flat dark strip with a brass-chrome needle and tick scale
 * - vintage : warm paper dial with an ink-style marker and inked labels
 * - glassy  : translucent track with a glowing neon orb and halo
 * - default : original simple pill bar (also the fallback before hydration)
 */
export function CentsBar({
  cents,
  inTune,
}: {
  cents: number;
  inTune: boolean;
}) {
  const theme = useActiveTheme();
  const markerPct = Math.max(0, Math.min(100, 50 + cents));

  if (theme === "studio") {
    return <StudioVariant markerPct={markerPct} inTune={inTune} />;
  }
  if (theme === "vintage") {
    return <VintageVariant markerPct={markerPct} inTune={inTune} />;
  }
  if (theme === "glassy") {
    return <GlassyVariant markerPct={markerPct} inTune={inTune} />;
  }
  return <DefaultVariant markerPct={markerPct} inTune={inTune} />;
}

/** Original render — kept as a safe fallback that matches the SSR output. */
function DefaultVariant({
  markerPct,
  inTune,
}: {
  markerPct: number;
  inTune: boolean;
}) {
  const markerColor = inTune ? "bg-success" : "bg-accent";
  return (
    <div className="relative h-4 rounded-full bg-bg">
      <div className="absolute top-0 left-1/2 h-full w-0.5 -translate-x-1/2 bg-surface-3" />
      <div
        className={`absolute top-1/2 h-6 w-6 -translate-x-1/2 -translate-y-1/2 rounded-full ${markerColor}`}
        style={{ left: `${markerPct}%` }}
      />
    </div>
  );
}

/**
 * STUDIO — precise console feel. Flat dark strip with thin tick marks every
 * 25 cents, brushed-chrome / brass needle gliding over the track. In-tune
 * flips the needle to a warm green.
 */
function StudioVariant({
  markerPct,
  inTune,
}: {
  markerPct: number;
  inTune: boolean;
}) {
  const ticks = [0, 25, 50, 75, 100];
  const needleColor = inTune
    ? "linear-gradient(180deg, #b5e58a 0%, #5d8a3e 100%)"
    : "linear-gradient(180deg, #f5e0a6 0%, #c0a070 35%, #8a6f3a 100%)";
  return (
    <div
      className="relative h-8 overflow-hidden rounded-md border border-line-strong"
      style={{
        background:
          "linear-gradient(180deg, rgba(0,0,0,0.45), rgba(0,0,0,0.25))",
        boxShadow:
          "inset 0 1px 2px rgba(0,0,0,0.6), inset 0 -1px 0 rgba(255,255,255,0.05)",
      }}
    >
      {ticks.map((pct) => (
        <span
          key={pct}
          aria-hidden="true"
          className="absolute top-1/2 h-2 w-px -translate-y-1/2 bg-dim opacity-60"
          style={{ left: `${pct}%` }}
        />
      ))}
      {/* Centre mark — sturdier than the sibling ticks. */}
      <span
        aria-hidden="true"
        className="absolute top-1/2 h-4 w-px -translate-x-1/2 -translate-y-1/2 bg-text opacity-80"
        style={{ left: "50%" }}
      />
      {/* Brass-chrome needle. */}
      <span
        aria-hidden="true"
        className="absolute top-1/2 h-6 w-1 -translate-x-1/2 -translate-y-1/2 rounded-sm"
        style={{
          left: `${markerPct}%`,
          background: needleColor,
          boxShadow:
            "0 0 0 1px rgba(0,0,0,0.5), 0 0 6px rgba(255,220,160,0.35)",
          transition: "left 80ms linear",
        }}
      />
    </div>
  );
}

/**
 * VINTAGE — warm paper dial with hand-painted tick labels and an ink-style
 * marker that lands on the scale. In-tune marker glows softly green.
 */
function VintageVariant({
  markerPct,
  inTune,
}: {
  markerPct: number;
  inTune: boolean;
}) {
  const labels: { pct: number; text: string }[] = [
    { pct: 0, text: "-50" },
    { pct: 25, text: "-25" },
    { pct: 50, text: "0" },
    { pct: 75, text: "+25" },
    { pct: 100, text: "+50" },
  ];
  const markerBg = inTune ? "var(--success)" : "var(--accent)";
  return (
    <div
      className="relative h-10 overflow-hidden rounded-sm border"
      style={{
        background: "var(--surface-2)",
        backgroundImage: "var(--paper-grain)",
        borderColor: "var(--line-strong)",
        boxShadow:
          "inset 0 1px 0 rgba(255,250,232,0.5), inset 0 0 12px rgba(80,60,30,0.18)",
      }}
    >
      {labels.map((tick) => (
        <span
          key={tick.pct}
          aria-hidden="true"
          className="absolute top-1 -translate-x-1/2 font-mono text-[10px]"
          style={{ left: `${tick.pct}%`, color: "var(--dim)" }}
        >
          {tick.text}
        </span>
      ))}
      {/* Ink centre mark. */}
      <span
        aria-hidden="true"
        className="absolute bottom-1 left-1/2 h-4 w-px -translate-x-1/2"
        style={{ background: "var(--text)", opacity: 0.7 }}
      />
      {/* Ink marker dot — slightly raised so the user can spot it. */}
      <span
        aria-hidden="true"
        className="absolute bottom-2 h-3 w-3 -translate-x-1/2 rounded-full"
        style={{
          left: `${markerPct}%`,
          background: markerBg,
          boxShadow: inTune
            ? "0 0 6px rgba(126,181,103,0.7), 0 0 2px rgba(126,181,103,0.9)"
            : "0 0 0 1px rgba(60,40,12,0.4)",
          transition: "left 120ms ease-out",
        }}
      />
    </div>
  );
}

/**
 * GLASSY — translucent track with a soft neon orb and halo blur. In-tune
 * the halo shifts to the success colour and intensifies.
 */
function GlassyVariant({
  markerPct,
  inTune,
}: {
  markerPct: number;
  inTune: boolean;
}) {
  const orbCore = inTune ? "var(--success)" : "var(--accent)";
  const orbHalo = inTune
    ? "rgba(94,230,161,0.55)"
    : "rgba(255,77,138,0.55)";
  return (
    <div
      className="relative h-6 overflow-visible rounded-full"
      style={{
        background:
          "linear-gradient(90deg, rgba(255,255,255,0.10), rgba(255,255,255,0.18), rgba(255,255,255,0.10))",
        backdropFilter: "blur(8px)",
        WebkitBackdropFilter: "blur(8px)",
        border: "1px solid var(--line)",
        boxShadow: "inset 0 1px 0 rgba(255,255,255,0.4)",
      }}
    >
      {/* Centre mark — cyan tick. */}
      <span
        aria-hidden="true"
        className="absolute top-0 left-1/2 h-full w-0.5 -translate-x-1/2"
        style={{
          background: "var(--accent-2, #39d4ff)",
          boxShadow: "0 0 6px var(--accent-2, #39d4ff)",
          opacity: 0.6,
        }}
      />
      {/* Glowing orb. */}
      <span
        aria-hidden="true"
        className="absolute top-1/2 h-5 w-5 -translate-x-1/2 -translate-y-1/2 rounded-full"
        style={{
          left: `${markerPct}%`,
          background: orbCore,
          boxShadow: `0 0 18px 4px ${orbHalo}, 0 0 4px ${orbCore}`,
          transition: "left 120ms ease-out, box-shadow 200ms ease",
        }}
      />
    </div>
  );
}
