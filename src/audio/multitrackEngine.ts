// Multi-track engine — architecture layer 5 (audio). Part of Bod 17.
//
// Plays a set of tracks together and records a new take over them (overdub):
// while recording, the existing tracks play so the player hears them in the
// headphones. Browser-only; one operation runs at a time, hence a singleton.
//
// Note: the recorded take is offset from the monitored tracks by the system's
// audio latency. For practice this is acceptable; tight sync is not promised.

import type { Track } from "@/lib/multitrack";
import { encodeWav } from "@/lib/wav";
import { audioEngine } from "./AudioEngine";
import { type RecordingResult, recorder } from "./recorder";

class MultitrackEngine {
  /** Decoded audio cached by track id, so a blob is decoded only once. */
  private buffers = new Map<string, AudioBuffer>();
  private sources: AudioBufferSourceNode[] = [];
  private recording = false;

  /** True while a recording or playback is in progress. */
  isBusy(): boolean {
    return this.recording || this.sources.length > 0;
  }

  /** Decodes a track's blob to an AudioBuffer, caching the result. */
  private async decode(
    context: AudioContext,
    track: Track,
  ): Promise<AudioBuffer> {
    const cached = this.buffers.get(track.id);
    if (cached) return cached;
    const arrayBuffer = await track.blob.arrayBuffer();
    const buffer = await context.decodeAudioData(arrayBuffer);
    this.buffers.set(track.id, buffer);
    return buffer;
  }

  /** Decodes and starts every track together. `onEnded` fires once the
   *  longest track finishes (unless playback was stopped first). */
  private async startPlayback(
    tracks: Track[],
    onEnded: () => void,
  ): Promise<void> {
    this.stopPlayback();
    if (tracks.length === 0) return;

    const context = await audioEngine.getOutputContext();
    const buffers = await Promise.all(
      tracks.map((track) => this.decode(context, track)),
    );

    // Small lead-in so every source starts perfectly together.
    const startAt = context.currentTime + 0.12;
    let longest = 0;
    for (let i = 1; i < buffers.length; i += 1) {
      if (buffers[i].duration > buffers[longest].duration) longest = i;
    }

    buffers.forEach((buffer, index) => {
      const source = context.createBufferSource();
      source.buffer = buffer;
      const gain = context.createGain();
      gain.gain.value = tracks[index].gain;
      source.connect(gain).connect(context.destination);
      if (index === longest) {
        source.onended = () => {
          if (this.sources.length > 0) {
            this.sources = [];
            onEnded();
          }
        };
      }
      source.start(startAt);
      this.sources.push(source);
    });
  }

  /** Stops any running playback. */
  stopPlayback(): void {
    for (const source of this.sources) {
      source.onended = null;
      try {
        source.stop();
      } catch {
        // Already stopped — ignore.
      }
    }
    this.sources = [];
  }

  /**
   * Plays the given tracks together. `onEnded` fires when playback finishes
   * on its own. Returns false if the engine is busy or there is nothing to
   * play.
   */
  async play(tracks: Track[], onEnded: () => void): Promise<boolean> {
    if (this.isBusy() || tracks.length === 0) return false;
    await this.startPlayback(tracks, onEnded);
    return this.sources.length > 0;
  }

  /**
   * Starts recording a new take while playing the given tracks for
   * monitoring (overdub). Returns false if the engine is busy or the input
   * could not be started.
   */
  async startRecording(monitorTracks: Track[]): Promise<boolean> {
    if (this.isBusy()) return false;

    const running = await audioEngine.ensureListening();
    if (!running) return false;
    const stream = audioEngine.getInputStream();
    if (!stream) return false;

    await this.startPlayback(monitorTracks, () => {});
    if (!recorder.start(stream)) {
      this.stopPlayback();
      return false;
    }
    this.recording = true;
    return true;
  }

  /** Stops recording and resolves with the captured take, or null. */
  async stopRecording(): Promise<RecordingResult | null> {
    if (!this.recording) return null;
    this.recording = false;
    this.stopPlayback();
    return recorder.stop();
  }

  /**
   * Mixes the given tracks down to a single 16-bit WAV blob, rendered
   * offline (faster than real time). Returns null if there is nothing to
   * mix. Track gain is applied; muted tracks should be excluded by the
   * caller.
   */
  async mixdown(tracks: Track[]): Promise<Blob | null> {
    if (tracks.length === 0) return null;

    const context = await audioEngine.getOutputContext();
    const buffers = await Promise.all(
      tracks.map((track) => this.decode(context, track)),
    );

    let frames = 0;
    for (const buffer of buffers) {
      frames = Math.max(frames, buffer.length);
    }
    if (frames === 0) return null;

    const sampleRate = buffers[0].sampleRate;
    const offline = new OfflineAudioContext(2, frames, sampleRate);
    buffers.forEach((buffer, index) => {
      const source = offline.createBufferSource();
      source.buffer = buffer;
      const gain = offline.createGain();
      gain.gain.value = tracks[index].gain;
      source.connect(gain).connect(offline.destination);
      source.start(0);
    });

    const rendered = await offline.startRendering();
    const channels: Float32Array[] = [];
    for (let channel = 0; channel < rendered.numberOfChannels; channel += 1) {
      channels.push(rendered.getChannelData(channel));
    }
    return new Blob([encodeWav(channels, rendered.sampleRate)], {
      type: "audio/wav",
    });
  }
}

/** Shared singleton instance. */
export const multitrackEngine = new MultitrackEngine();
