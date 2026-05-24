"use client";

import { playChord } from "@/audio/chordPlayer";
import { chordMidiNotes } from "@/lib/chords";
import type { Chord } from "@/lib/chords";
import { type Notation, localizeChordName } from "@/lib/pitch";
import { ChordDiagram } from "@/components/library/ChordDiagram";

/**
 * A chord tile — shows the diagram and plays a strummed preview when clicked.
 */
export function ChordCard({
  chord,
  mirror,
  notation,
}: {
  chord: Chord;
  mirror: boolean;
  notation: Notation;
}) {
  return (
    <button
      type="button"
      onClick={() => void playChord(chordMidiNotes(chord))}
      className="rounded-lg border border-neutral-800 bg-neutral-900 p-3 transition-colors hover:border-amber-600"
    >
      <div className="mx-auto max-w-[128px]">
        <ChordDiagram chord={chord} mirror={mirror} />
      </div>
      <div className="mt-2 text-center text-sm font-semibold">
        {localizeChordName(chord.name, notation)}
      </div>
      <div className="text-center text-xs text-neutral-500">
        Klepni a poslechni
      </div>
    </button>
  );
}
