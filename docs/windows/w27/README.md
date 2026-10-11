# W27 — Native rhythm trainers and palm mute

Views 10–12 now have native exercise selection, current power chord diagrams,
gallop cell position, whole-round progress, count-in, 5% acceleration, listening,
recording and a link to the shared amplifier. Musical accompaniment and round
boundaries run in the existing device callback. Their timing is independent of
UI refresh and callback size; acceleration preserves oscillator pitch. Tempo
records use the last *completed* round BPM and original exercise IDs. This is
paced practice, not automatic chord or timing accuracy grading. Unsupported
fingering for a selected tuning blocks start with an explanation.

The bounded rhythm command installs the round and accompaniment together.
Reserved cancellation rejects stale queued starts and fades accompaniment. The
trainer owns tempo while active; conflicting external metronome/tempo controls
are rejected. Recording can join without resetting the trainer clock. Its optional
count-in waits on the existing beat grid rather than installing another clock.
Native ABI is 15; old DLLs are rejected before use.

Palm mute collects at least three actual onset-body centroids per technique.
Calibration stores device, tuning, instrument and pickup context, with adjustable
threshold sensitivity. Changing context interrupts assessment and requires new
samples. Overlapping samples produce a warning. Results show expected/measured
technique and distance from the calibration boundary, **not** a statistical
confidence percentage. Missing/stale input or epoch changes pause measurement;
no input never invents a result. The original 320 ms onset refractory interval is
retained, so fast passages require listening rather than this slow hit classifier.
Calibration is persistent; a partly completed attempt stays only in memory.

Validation: [Linux checks](linux-validation.json), 18 native suites, four relevant
ASan/UBSan suites, real CLR/C++ rhythm scheduling and actual prepared PCM through
the native off-thread analyzer into calibration/classification. The PCM signals
are contrasting 2400 Hz / 600 Hz synthetic tones, **not evidence of accurate real
guitar palm-mute recognition**. WinUI smoke constructs all three views and uses
explicitly simulated observations to check rendered hit results and reopened
settings/tempo records. Windows build/form verification is pending. Physical
M-Track Solo, listening, pickup/gain calibration and keyboard/manual checks remain
unperformed until the complete candidate.
