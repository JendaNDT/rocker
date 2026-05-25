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
        const color = active ? "scale-125 bg-accent" : "bg-surface-3";
        const ring = isAccent ? " ring-2 ring-accent/50" : "";
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
