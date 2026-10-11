# W29 — Native notation workspace

View 20 retains an imported score separately from its paged native Skia bitmap.
The WinUI controls select any track, 50–200% zoom, a bar number and follow mode.
Playback can use the shown track or all tracks. Selected and actually played
bars have separate borders. A readable bar description exposes notes/rests and
string/fret positions without requiring pointer interaction with the bitmap.
Opening an invalid file preserves the last valid score. No web notation engine
or second audio device is created.

The MIDI tick lookup expands repeats and score jumps. Its complete tempo map
converts ticks to original source seconds; the transport's stretched position
is multiplied by the chosen speed before locating the played bar. Bar A/B
chooses a specific repeated traversal, rejects a non-contiguous repeat jump,
and is reapplied after speed changes. Track changes retain the source position
and playback state. Page/zoom changes do not replace the playing MIDI.

The common C++ MIDI renderer now follows RPN pitch-wheel sensitivity, bends,
channel volume/expression and deterministic percussion. It retains a procedural
string reference timbre; this is a listening/practice aid, not a complete GM
soundfont or a realistic model of every instrument. It uses the existing master
media gain and audio callback. Fonts remain the pinned Bravura/Noto assets with
their existing license notices; no downloaded soundfont or network is required.
Limits are 4 MiB input, 16 tracks, 128 bars, 10,000 written notes, 100,000 MIDI
events, 12 bars per page and 12 million image pixels. The native synthesis limit
is five minutes / 5,000 expanded notes; larger audio is rejected explicitly while
the score can remain visible.

The actual authored file matrix is GP3, GP4, GP5, GPX/BCFS, GP7 ZIP, plain and
compressed MusicXML. Native renderer checks cover structure, every displayed
bar's hit geometry, non-blank PNG decoding and MIDI events; a three-track score
checks percussion, tempo changes, repeats, selected-track audio and later pages.
Actual generated MIDI from every fixture is prepared through the C++ library
via CLR/PInvoke. C++ tests measure continuous bend frequency, release, muted
channels and deterministic drum output.

Linux validation: 82 notation assertions, 19/19 native suites and the changed
MIDI/transport suites under ASAN/UBSAN passed. Actual MIDI synthesis via PInvoke
passed. Windows build and native form smoke validation are pending. Physical
listening, keyboard and screen-reader acceptance are deferred to the complete
candidate; automated synthesis does not imply verified device output.
