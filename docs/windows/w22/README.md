# W22 — Native recording and library

ABI 12 adds an 88-byte cached recording snapshot and control-thread capture
commands. The callback selects dry mono, processed mono or post-mix stereo,
excludes the count-in, and transfers preallocated 256-frame packets through a
256-packet queue. The disk worker exclusively creates PCM24 WAV, checkpoints
its header and synchronizes once per recorded second, and drains the final
partial block on stop. A file is limited to 1 GiB. Overflow, invalid samples,
device/epoch changes and disk errors explicitly produce a failed/partial take.
PCM24 describes the output representation, not the precision of the ADC.

Capture metadata retains exact written frames, source bus, common-clock start,
epoch, initial DSP group delay and a flag if it changes. Recovery has no known
clock start or epoch and stores null for those values. None of these values is
a measured end-to-end guitar latency. YouTube audio never enters the graph.

The native Vintage recording controls are available in Live and the library.
The library supports rename, multiple selection, confirmed group deletion,
undo of the last deleted group, playback, marker editing/seek and comparison
of two takes at the same source-time position. Import identity, original
encoded files and markers remain intact. Deleting rows retains owned media
for project references; explicit disk cleanup is tracked in W41.

A durable capture sidecar is written before arming. Finalization repairs RIFF,
renames the file and commits its hash and metadata to SQLite before removing
the sidecar. Startup recovers aligned PCM beyond an old checkpoint or a rename
interrupted before SQLite commit. Malformed files are preserved and reported.
Backup, migration, device changes and close finish the take before moving data.
A recording close guard offers save or cancellation, never an unawaited discard.

Export offers original bytes or a source-time range as PCM24 WAV. The system
picker handles target selection/overwrite; conversion runs independently of
the audio control queue and opens no device. Read-ahead cache misses can wait
on this offline worker, never on the callback. Progress is throttled, export
can be cancelled, and only a completed temporary output replaces the target.
Large WebM/AAC still share the documented W21 fallback limit until that decoder
path is extended before the full candidate. WAV/MP3/Ogg use the bounded cache.

Control workers now preserve submission order: rapid play/pause or seek cannot
finish in reverse order. Handle leases and immediate stop reservations remain;
one failed command cannot prevent a later valid command.

## Validation

- Linux: 14/14 CTest; full 14/14 ASan/UBSan and relevant final tests after edits.
- Actual callback dry/processed/mix isolation, mono/stereo PCM24 and odd RIFF
  padding, exact 96,000-sample count-in, final packet, overflow, DSP metadata,
  global stop, cancelled queued start, independent decode and WAV range export.
- C# P/Invoke: size/token/sample count, actual fake callback capture and export,
  FIFO controls, queued failure recovery, cancellation and handle disposal.
- SQLite/files: 20 recording assertions, including a child process killed after
  writing uncheckpointed PCM, header/rename recovery, hash, marker round-trip,
  group undo and cancelled original export preserving an existing target.
- Windows build, WinUI form and reopened recording database: pending CI.
- Physical M-Track capture, loopback latency, manual file dialogs, sustained
  disk load and listening: deferred to the full-candidate acceptance.

This is an implementation milestone, not a complete app or a new user release.
