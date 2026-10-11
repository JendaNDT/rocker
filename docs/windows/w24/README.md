# W24 — Native multi-track Studio

The Windows Studio has persistent projects, relative media with SHA-256 checks,
track gain, pan, mute/solo, sample offsets and source trimming, a zoomable timeline,
20-step edit undo, auto-save, portable project import/export and stereo PCM24
mixdown with range, progress, cancellation and atomic destination replacement.
The playback source and offline exporter use the same track mixer and envelopes.
Original media is retained. Gain/pan changes use a 10 ms ramp; tracks have a 5 ms
boundary envelope. Project sample rates are independent of source rates.

ABI 14 adds immutable project descriptors, source-format metadata, live mixer
commands, independent offline export and aligned recording observations. Decoders,
cache workers and ownership release stay off the callback. Mixer reads and command
consumption pass the callback allocation audit. Limits are 256 tracks, 8 GiB of
combined decoded cache and U32 timeline frames; later W40 verifies usable live
performance limits. The Studio timeline displays clip regions, not a fabricated
mixed waveform. The project's peak summary is an approximate envelope.

A new guitar track records the source coordinate of its first captured sample in
the callback. Count-in samples are excluded; resampling maps source coordinates
into the project rate. Seek, pause, loop, cache discontinuity or source replacement
ends an aligned capture explicitly and preserves its partial audio. Recording may
continue beyond the existing backing's end. The output backing, raw guitar capture,
metronome and monitor remain on one physical device and common sample clock.

Placement subtracts reported input + output + initial DSP delay once, plus the
user's manual correction. These values are estimates, not a measured round trip.
A negative compensated start becomes a non-destructive source trim. The manifest
retains delay provenance and continuity. Unknown driver delays remain unknown;
questionable placement is labelled for manual checking. A first track in an empty
project starts at zero. Mixed-bus capture is kept available in the recording
library but refused for Studio overdub to avoid duplicating the backing.

Project switches clear the previous playable source. Canceled queued sources are
rejected again at the callback boundary before swapping PCM. The coordinator
retains callback clip leases and releases expired ownership afterward. Canceled
loads preserve controls on the retained project.

Validation: [Linux checks](linux-validation.json), frozen domain references,
actual C# ↔ C++ mixer/export/alignment calls, 16 native tests, portable transfer to
another data root and rejection of tampered media. Windows build/form checks are
pending at this checkpoint. The WinUI smoke check requires two native track rows,
two timeline regions, reopened SQLite/manifest data and an exact 560-frame source.
Physical M-Track Solo overdub, delay accuracy, dialogs and listening remain
**unperformed**, deferred to the complete candidate. Capture crash recovery keeps
unfinished audio in the recording library; an uncommitted Studio attachment may
need to be attached manually after a crash.
