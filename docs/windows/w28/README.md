# W28 — Native libraries and music games

Views 17–19 now provide the complete runtime library: 26 chords with category/search,
fingering, barre, muted/open strings, mirrored diagrams and synthetic chord or
arpeggio listening; six scales with root, selected tuning, position 0–24, root
marking, note selection and ascending/descending listening; and 14 original riffs
with search/difficulty, native six-string timed tablature, technique annotations,
count-in, fixed-tempo loop, playback position, recording and direct practice links.
The two historical riff IDs resolve to licensed replacement patterns.

Chord diagrams explicitly use standard tuning; playing them under another tuning
requires switching the setting. Scale positions use the selected real tuning.
Riffs retain their declared tuning and block incompatible practice with a clear
explanation and a setting button. Changing the setting does not physically tune
the guitar. Only monophonic, non-overlapping riffs can open the single-note
SurgicalEngine drill; chord passages cannot be falsely graded by the YIN detector.
Riff accompaniment uses the existing native rhythm actor/clock. Palm-muted preview
notes are quieter/shorter; bend, slide and legato are player annotations, not a
claim that synthetic playback reproduces those techniques.

Views 21–22 provide the original fretboard and ear games. The fretboard game uses
standard tuning regardless of the application setting, conceals note names,
retains correct/wrong score and streak rules, and adds an optional no-point hint.
The common native fretboard has one roving Tab stop, arrow navigation, Enter
selection, actual fret headings, screen-reader names and left-handed mirroring.
The ear game retains all eight intervals and four chord qualities, random root
C3–B3, no immediate repeat, replay, answer feedback and optional no-point reveal.
Users can hear simultaneous or sequential tones. No microphone is required; an
unavailable output leaves the answer controls disabled. Leaving the view cancels
its preview. Scores and unfinished game attempts stay in memory, as in the PWA;
practice-time/progress integration follows W32.

Validation: [Linux domain checks](linux-validation.json) preserve the frozen PWA
rules, every runtime riff's event bounds/tuning, monophonic drill filtering, all
question types and no-point hint rules. WinUI smoke requires all 26 diagrams in
both orientations, 78 interactive cells in a shifted position, all 14 complete
native tabs, legacy alias resolution and simulated native game answers. Windows
build/form validation is pending. No physical keyboard, screen reader, guitar or
listening acceptance has been performed. Shared native audio remains covered by
W25/W27 callback/CLR checks; these UI checks do not imply real audio playback.

Windows run 38181594614 passed build/native/CLR checks but stopped in the prior
W27 UI assertion: adding the riff trainer made the shared editor dictionary have
three entries, while that check required exactly two. It now requires the two
specific power/gallop editor keys and their diagrams. Corrected Windows UI
validation is pending; the failed run is not counted as passed.

Corrected run 38182383504 still failed the same W27 form check: the shared
round-progress dictionary also gained the riff entry. Its assertion now checks
the specific power/gallop progress controls and records keys/control success
in the report. Both dictionaries are scoped by trainer identity. The failure
occurred before library/game validation, so those Windows checks remain pending.
