# W25 — Native lessons and teaching previews

All 18 lessons now have native stage/search/completion filters and the three
original step kinds: text, individual-note feedback and chord practice. The step
player has previous/next buttons, access keys and Ctrl+Left / Ctrl+Right, persistent
positions, replay, idempotent completion and direct lesson links from the daily
plan. The original rule allowing manual Next is retained; it does not mark a note
as detected. Note feedback requires fresh native input analysis and preserves the
original octave-equivalent pitch-class check. Chord practice uses listening and
six-string diagrams, without claiming polyphonic microphone grading.

Note, chord and arpeggio previews are synthetic teaching tones on the existing
native input graph. They do not replace a backing clip, open a second device or
reset a running device epoch. The fixed 128-event phrase command queue and sample
clock schedule the notes in the callback, including optional beat count-in. Gain
ramps, bounded output, phrase cancellation and ownership-free reads are exercised
with actual PCM; there are no UI timers for note timing. The common raw-input
analyzer remains separate from output synthesis. Lesson chord diagrams/previews
are explicitly labelled for standard tuning. Output/device failures are reported;
reading and manual navigation remain available without audio.

Validation: [Linux checks](linux-validation.json), 17 native tests, four relevant
ASan/UBSan suites, frozen PWA domain references plus traversal of every lesson,
actual C# ↔ C++ polyphonic preview calls, and unchanged PWA type/test checks.
The WinUI smoke check constructs all three step kinds, reopens lesson progress and
requires completion stored exactly once. Windows build/form verification is
pending. Physical pitch checks, listening, keyboard and screen-reader acceptance
remain unperformed and deferred to the complete candidate. Later W32 integrates
the lesson results with full progress, achievements and practice time.
