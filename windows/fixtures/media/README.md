# Stereo reference recordings

Authored synthetic W01 `input-2-only.wav` (48 kHz, two channels, 220 Hz only
on channel 2) transcoded with FFmpeg 7.1.5-0+deb13u1. No personal recordings
or commercial song content. The project MIT fixture license applies.

```sh
ffmpeg -i docs/windows/baseline/fixtures/audio/input-2-only.wav -c:a libopus -b:a 128k windows/fixtures/media/input-2-only.webm
ffmpeg -i docs/windows/baseline/fixtures/audio/input-2-only.wav -c:a aac -b:a 128k windows/fixtures/media/input-2-only.m4a
```

SHA-256:

- WebM: `91b6997e3815a80642a6e7f4cb5ebd5f73c973633356b0ecb5450d9403ed67e0`
- M4A: `0bc94befbac41c83ec2a16ea74152a4d254ddb914f6d9979197004b3d9b7c9a7`

Encoded binaries are retained to make CI independent of FFmpeg. Codec
padding and lossy reconstruction are expected; tests inspect actual decoded
frames, pitch, and channel isolation, not encoded-byte identity.

`a4-streaming.webm` uses the authored mono W01 `a4-440.wav` and FFmpeg's
live muxing mode: unknown Segment size (`01ffffffffffffff`) and no declared
duration, instead of requiring a finalized seekable file. It is synthetic,
not an actual browser capture; it checks the streaming container shape.

```sh
ffmpeg -i docs/windows/baseline/fixtures/audio/a4-440.wav -c:a libopus -b:a 96k -live 1 windows/fixtures/media/a4-streaming.webm
```

SHA-256: `df582eacf607a8d1e2b4c939dd63996649665a9062f45946ea601c27fc16e62f`.
