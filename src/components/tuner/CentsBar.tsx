/**
 * Horizontal cents-deviation bar with a centre mark. Shared by the chromatic
 * and guided tuner displays. Marker position maps cents -50..+50 to 0..100%.
 */
export function CentsBar({
  cents,
  inTune,
}: {
  cents: number;
  inTune: boolean;
}) {
  const markerPct = Math.max(0, Math.min(100, 50 + cents));
  const markerColor = inTune ? "bg-emerald-400" : "bg-amber-500";

  return (
    <div className="relative h-4 rounded-full bg-neutral-950">
      {/* Centre mark — perfectly in tune. */}
      <div className="absolute top-0 left-1/2 h-full w-0.5 -translate-x-1/2 bg-neutral-600" />
      {/* Deviation marker. */}
      <div
        className={`absolute top-1/2 h-6 w-6 -translate-x-1/2 -translate-y-1/2 rounded-full ${markerColor}`}
        style={{ left: `${markerPct}%` }}
      />
    </div>
  );
}
