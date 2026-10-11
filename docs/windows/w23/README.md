# W23 — Layered native looper

ABI 13 exposes a 120-byte cached looper snapshot. The looper shares the input
callback, amplifier and sample clock with playback, metronome and recording.
Its 64 MiB buffer is allocated on the coordinator when first used, before a
queued pointer handoff. The callback never allocates, resizes, saves or frees
layers. Eight mono layers share an exact integer length, up to 2,097,152 frames
(43.69 seconds at 48 kHz; the maximum time depends on the device rate).

The first loop may start freely or on a bar boundary, with zero, one or two
count-in bars. An aligned end waits for a whole-bar boundary. Overdubs always
start at position zero and record exactly one loop; no accumulating per-block
rounding occurs. The selected dry/processed input is captured independently
of monitoring. Layer volume, a 5 ms seam envelope and a playback start ramp
control level and boundaries. End-to-end listening remains unperformed.

Overdub placement subtracts its delay exactly once modulo the loop length.
The UI can use reported input/output delay plus processed-input DSP delay, or
a manual sample offset. Driver values are estimates. Saved provenance retains
partial status and the supplied delay; the app has not measured hardware
roundtrip. Existing completed layers survive device loss, memory limit and
sample-rate changes. A partial first capture is marked; an interrupted overdub
is discarded without changing completed layers. Save waits for an active
recorded overdub to finish, rather than silently discarding it.

The native Vintage page has capture, free/aligned end, count-in, overdub, undo,
pause/resume, confirmed clear, per-layer levels and explicit session saving.
Audio continues across navigation. Close guards protect unsaved sessions.
Session saving pauses writes, exports individual PCM24 layers, verifies owned
files, attaches relative media and atomically commits a `.rockerproj` plus
SQLite index. Later saves retain layer identities and names. Sessions reopen
paused, retain gain/rate/partial provenance and can also be opened in Studio.
Rate conversion is never implied: choose the recorded rate before playback.
A mono mix can also be saved in the recording library.

Loop material is deliberately volatile until **Save session**. This is shown
by the unsaved close guard; unlike disk recording, it is not advertised as
surviving a process crash before saving. Completed saved sessions reuse the
project hash and previous-manifest recovery mechanism from W09.

## Validation

- Linux: 15/15 CTest, including 20,000 wraps with varying blocks, delay-aligned
  impulse overdub, undo, missing input, bounded overflow and device changes.
- Real fake-device callback integration: 800-frame base and overdub, shared
  device start, independent WAV layer decode and paused restoration.
- Allocation audit covers first capture, playback, compensated overdub and undo.
- Relevant ASan/UBSan, C# ABI/capture/layer-export/restore, full frozen domain
  parity and 22 recording/project persistence assertions.
- Windows build, both WinUI runs and layer/session round-trip: [passed](windows-validation.json).
- Physical M-Track playback, timing/listening, manual save/close dialogs and
  sustained full-memory load: deferred to the complete candidate acceptance.

The UI smoke test loads two own synthetic layers without opening an audio
device and reopens their saved project. It clears the test session before
closing; it does not pretend to validate physical overdubbing.
