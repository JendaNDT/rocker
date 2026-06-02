// Loop pedal engine — architecture layer 5 (audio). Part of Bod 17.
//
// Records a base loop, plays it back seamlessly (an AudioBufferSourceNode
// with loop=true loops sample-accurately) and overdubs further layers mixed
// into the loop. Browser-only; one loop at a time, hence a singleton.
//
// Note: overdub layers are offset by the system's audio latency, so layering
// is "close enough" for practice rather than pedal-tight.

import { mixLoopLayer } from "@/lib/loopMix";
import { audioEngine } from "./AudioEngine";
import { recorder } from "./recorder";

/** The loop pedal's lifecycle state. */
export type LoopState =
  | "idle"
  | "recordingBase"
  | "looping"
  | "recordingOverdub";

class LoopPedal {
  private loopBuffer: AudioBuffer | null = null;
  private source: AudioBufferSourceNode | null = null;
  private state: LoopState = "idle";
  private layers = 0;

  getState(): LoopState {
    return this.state;
  }

  /** Number of recorded layers in the current loop. */
  getLayerCount(): number {
    return this.layers;
  }

  /** Ensures the input is running and returns its stream, or null. */
  private async input(): Promise<MediaStream | null> {
    const running = await audioEngine.ensureListening();
    if (!running) return null;
    return audioEngine.getRecordingStream();
  }

  /** Starts recording the base loop. */
  async startBase(): Promise<boolean> {
    if (this.state !== "idle") return false;
    const stream = await this.input();
    if (!stream || !recorder.start(stream)) return false;
    this.state = "recordingBase";
    return true;
  }

  /** Stops the base recording and starts the loop playing. */
  async stopBase(): Promise<void> {
    if (this.state !== "recordingBase") return;
    const result = await recorder.stop();
    this.state = "idle";
    if (!result) return;
    const context = await audioEngine.getOutputContext();
    this.loopBuffer = await context.decodeAudioData(
      await result.blob.arrayBuffer(),
    );
    this.layers = 1;
    this.startLoop(context);
  }

  /** Starts recording an overdub layer over the running loop. */
  async startOverdub(): Promise<boolean> {
    if (this.state !== "looping" || !this.loopBuffer) return false;
    const stream = await this.input();
    if (!stream || !recorder.start(stream)) return false;
    this.state = "recordingOverdub";
    return true;
  }

  /** Stops the overdub and mixes the layer into the loop. */
  async stopOverdub(): Promise<void> {
    if (this.state !== "recordingOverdub" || !this.loopBuffer) return;
    const result = await recorder.stop();
    this.state = "looping";
    if (!result) return;
    const context = await audioEngine.getOutputContext();
    const overlay = await context.decodeAudioData(
      await result.blob.arrayBuffer(),
    );
    this.loopBuffer = this.combine(context, this.loopBuffer, overlay);
    this.layers += 1;
    this.startLoop(context);
  }

  /** Stops everything and clears the loop. */
  clear(): void {
    this.stopSource();
    this.loopBuffer = null;
    this.layers = 0;
    this.state = "idle";
  }

  /** (Re)starts looping playback of the current loop buffer. */
  private startLoop(context: AudioContext): void {
    this.stopSource();
    if (!this.loopBuffer) return;
    const source = context.createBufferSource();
    source.buffer = this.loopBuffer;
    source.loop = true;
    source.connect(context.destination);
    source.start();
    this.source = source;
    this.state = "looping";
  }

  /** Stops and releases the looping source node. */
  private stopSource(): void {
    if (this.source) {
      try {
        this.source.stop();
      } catch {
        // Already stopped — ignore.
      }
      this.source.disconnect();
      this.source = null;
    }
  }

  /** Mixes an overlay buffer into the base, keeping the base's length. */
  private combine(
    context: AudioContext,
    base: AudioBuffer,
    overlay: AudioBuffer,
  ): AudioBuffer {
    const baseChannels: Float32Array[] = [];
    for (let ch = 0; ch < base.numberOfChannels; ch += 1) {
      baseChannels.push(base.getChannelData(ch));
    }
    const overlayChannels: Float32Array[] = [];
    for (let ch = 0; ch < overlay.numberOfChannels; ch += 1) {
      overlayChannels.push(overlay.getChannelData(ch));
    }
    const mixed = mixLoopLayer(baseChannels, overlayChannels);
    const out = context.createBuffer(mixed.length, base.length, base.sampleRate);
    mixed.forEach((channel, ch) => out.copyToChannel(channel, ch));
    return out;
  }
}

/** Shared singleton instance. */
export const loopPedal = new LoopPedal();
