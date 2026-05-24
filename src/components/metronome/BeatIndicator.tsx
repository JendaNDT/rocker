/**
 * Row of dots, one per beat in the bar. The sounding beat lights up;
 * the first beat (accent) carries a ring.
 */
export function BeatIndicator({
  beatsPerBar,
  currentBeat,
}: {
  beatsPerBar: number;
  currentBeat: number;
}) {
  return (
    <div className="flex justify-center gap-3">
      {Array.from({ length: beatsPerBar }, (_, index) => {
        const active = index === currentBeat;
        const isAccent = index === 0;
        const color = active ? "scale-125 bg-amber-400" : "bg-neutral-700";
        const ring = isAccent ? " ring-2 ring-amber-500/50" : "";
        return (
          <div
            key={index}
            className={`h-5 w-5 rounded-full transition-all duration-75 ${color}${ring}`}
          />
        );
      })}
    </div>
  );
}
