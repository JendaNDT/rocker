# W26 — Native surgical practice

View 09 is now the native self-paced single-note trainer: exercise picker,
current target, error counts per position, run → automatically selected worst
three-note window → drill → completion, with three clean repetitions. The
original SurgicalEngine and all 37 frozen transition states remain unchanged.
Users can pause, restart, listen to the phrase or drill, record through the common
recording service, and open view 07 for waveform/A-B training without note grading.
Only a fresh selected guitar input feeds the trainer. Missing/stale data or a
changed device epoch pauses assessment while preserving the current attempt.
Navigation preserves the attempt in memory and pauses assessment; persistence of
completed lessons remains separate. An unfinished surgical attempt is not restored
after process termination in this implementation.

SurgicalNoteGate uses input-frame timing, stable pitch changes, genuine silent
releases and later native onsets. A held note feeds once; a delayed report of that
same initial onset cannot feed again. Repeated same-pitch attacks wait for a fresh
analysis window. The native onset detector retains the original 320 ms refractory
period and centroid sampling; this is a slow single-note practice mode, not an
accurate grader for fast guitar passages. Its analysis window does not delay the
live monitoring/amp path.

Validation: [Linux checks](linux-validation.json), existing frozen domain states,
repeat-note/silence/epoch gate cases and actual prepared PCM through the fake device
callback, native off-thread analyzer, CLR gate and SurgicalEngine. The prepared
sequence accepts exactly E2, E2, F#2 and completes; the held sections do not
repeatedly advance. The native test PCM injection symbol exists only in the test
DLL, never the production audio library. WinUI smoke exercises native run, drill
and completed feedback; [Windows run 38179590084](windows-validation.json) passed the build, all 17 native suites and both actual WinUI instances. Physical guitar,
keyboard/dialog and listening checks remain unperformed until the full candidate.
