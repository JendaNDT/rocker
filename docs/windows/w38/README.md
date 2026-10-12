# W38 — interruption and bounded media recovery

Long WebM/Opus and Windows AAC are decoded into a temporary PCM spool, then the existing disk cache and read-ahead source. Neither path accumulates the complete decoded file in RAM. Encoded inputs remain bounded to 1 GiB; decoded/stretched caches have an 8 GiB limit each and U32 frame timelines. Temporary files are removed when cancelled or released. A 400-second authored silent file exceeds the previous 64 MiB decoded limit. Windows AAC validation is required separately.

Cancellation restores the waveform belonging to the surviving clip. Song loading also restores the previous native source if cancellation races publication; restored playback stays paused. File/device/backup/project operations have mutual guards. Named-pipe failures are observed without closing the existing workspace.

Synthetic WM_POWERBROADCAST suspend/resume events now pause the explicit practice session, release keep-awake, cancel online/loading/export work, stop sources and finish/preserve the current take. Resume never enables monitoring or starts audio. Physical device unplug, physical sleep, full/removable disks and listening are deferred to W44.

## Recovery evidence

| Failure | Automated coverage | Recoverable state / action |
|---|---|---|
| Device start failure, late start/stop, callback fault | audio_contract, audio_diagnostics, recording_contract | Released device, silent monitoring; select/reopen device. Partial take explicitly retained. |
| Missing/changed media | storage checks, project_mixer, native song form | Stored references preserved; locate song, reimport missing project medium. |
| Writer error/queue overflow, rate change, interrupted capture | recording_writer, recording_contract, infrastructure recording checks incl. child termination | No overwritten unread blocks; checkpoint WAV recovered as partial. |
| Failed/cancelled export/import | storage/recording/backup/migration checks | Atomic destination preserves last valid data; cancel or choose another destination. |
| Process killed during autosave/restore | infrastructure child-termination/rollback checks | WAL/quick-check and rollback journal recover; last valid project kept. |
| HTTP error/quota/timeout/incomplete stream | 28 mocked AI transport checks | No incomplete reply persisted; lessons and audio remain local. |
| Sleep / resume | power-recovery.json on Windows | Synthetic messages only; stopped audio and monitoring, session paused. |
| Long containers / mid-decode cancellation | container_webm_stream, container_aac_stream | Bounded spool/cache, no cancelled source published. |

The checks use injected faults and synthetic/local data. They do not demonstrate physical disk-full/unplug permissions on the brother's PC or a successful online-provider request.
