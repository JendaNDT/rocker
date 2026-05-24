import { CentsBar } from "@/components/tuner/CentsBar";
import type { TunerReading } from "@/components/tuner/useTuner";
import { type Notation, pitchClassName } from "@/lib/pitch";

// Within this many cents the note counts as in tune.
const IN_TUNE_CENTS = 5;

/**
 * Big note read-out with a cents deviation bar. Chromatic tuner display.
 */
export function TunerDisplay({
  reading,
  notation,
}: {
  reading: TunerReading | null;
  notation: Notation;
}) {
  if (!reading) {
    return (
      <div className="flex h-64 items-center justify-center rounded-lg border border-neutral-800 bg-neutral-900">
        <p className="text-neutral-500">Zahraj tón…</p>
      </div>
    );
  }

  const { note, frequency } = reading;
  const cents = note.cents;
  const inTune = Math.abs(cents) <= IN_TUNE_CENTS;
  const accent = inTune ? "text-emerald-400" : "text-amber-500";

  return (
    <div className="rounded-lg border border-neutral-800 bg-neutral-900 p-6">
      <div className="flex items-end justify-center gap-1">
        <span className={`text-7xl font-bold ${accent}`}>
          {pitchClassName(note.midi, notation)}
        </span>
        <span className="mb-2 text-2xl text-neutral-500">{note.octave}</span>
      </div>

      <div className="mt-6">
        <CentsBar cents={cents} inTune={inTune} />
      </div>

      <div className="mt-5 flex items-center justify-between text-sm">
        <span className="text-neutral-500">♭ níž</span>
        <span className={`font-semibold ${accent}`}>
          {inTune ? "Naladěno" : `${cents > 0 ? "+" : ""}${cents} centů`}
        </span>
        <span className="text-neutral-500">výš ♯</span>
      </div>

      <p className="mt-3 text-center text-xs text-neutral-600">
        {frequency.toFixed(1)} Hz
      </p>
    </div>
  );
}
