// Amp engine singleton — keeps the AmpChain alive across navigation.
//
// Pattern mirrors src/audio/metronome.ts: a single instance lives outside
// React so the audio graph survives unmounting the /amp page. Components
// only set parameters and ask for AudioNode references when they need them
// (e.g. the post-effects analyser for the VU meter or the recording stream
// for capturing takes).
//
// State (active / starting / error / current settings) is owned by
// useAmpStore on top of this engine — see src/stores/ampStore.ts. The
// engine itself only cares about whether the chain is wired up.

import { AmpChain } from "./ampChain";
import { audioEngine } from "./AudioEngine";
import {
  DEFAULT_AMP_PRESET,
  type AmpSettings,
  type CabinetType,
  type DistortionType,
} from "@/lib/ampPresets";

/** Pushes every parameter of a settings object onto the chain. */
function applyToChain(chain: AmpChain, settings: AmpSettings): void {
  chain.setCompression(settings.compression);
  chain.setDrive(settings.drive);
  chain.setDistortionType(settings.distortion);
  chain.setBass(settings.bass);
  chain.setMid(settings.mid);
  chain.setTreble(settings.treble);
  chain.setCabinet(settings.cabinet);
  chain.setDelayMix(settings.delay);
  chain.setReverbMix(settings.reverb);
  chain.setChorusMix(settings.chorus);
  chain.setLevel(settings.level);
}

export type StartResult = { ok: true } | { ok: false; error: string };

class AmpEngine {
  private chain: AmpChain | null = null;
  private settings: AmpSettings = DEFAULT_AMP_PRESET.settings;

  /** Whether the chain is wired up and audio is flowing. */
  isActive(): boolean {
    return this.chain !== null;
  }

  /** The settings currently applied to the chain (and used when starting). */
  getSettings(): AmpSettings {
    return this.settings;
  }

  /**
   * Spin up the chain if it isn't running. Returns a success result, or a
   * failure with a user-facing message when input permission is missing or
   * the audio context cannot be acquired.
   */
  async start(): Promise<StartResult> {
    if (this.chain) return { ok: true };
    const running = await audioEngine.ensureListening();
    if (!running) {
      return {
        ok: false,
        error:
          "Nepodařilo se spustit zvukový vstup. Povol mikrofon v Profilu.",
      };
    }
    const source = audioEngine.getInputSource();
    if (!source) {
      return { ok: false, error: "Zvukový vstup není dostupný." };
    }
    const context = await audioEngine.getOutputContext();
    const chain = new AmpChain(context);
    applyToChain(chain, this.settings);
    source.connect(chain.input);
    this.chain = chain;
    return { ok: true };
  }

  /** Tear the chain down. Safe to call when already stopped. */
  stop(): void {
    const chain = this.chain;
    if (!chain) return;
    audioEngine.getInputSource()?.disconnect(chain.input);
    chain.dispose();
    this.chain = null;
  }

  /** Replace all parameters in one go and apply to the live chain if active. */
  setSettings(settings: AmpSettings): void {
    this.settings = settings;
    if (this.chain) applyToChain(this.chain, settings);
  }

  /** Convenience: change distortion character while keeping the rest. */
  setDistortion(type: DistortionType): void {
    this.settings = { ...this.settings, distortion: type };
    if (this.chain) this.chain.setDistortionType(type);
  }

  /** Convenience: change cabinet voicing while keeping the rest. */
  setCabinet(type: CabinetType): void {
    this.settings = { ...this.settings, cabinet: type };
    if (this.chain) this.chain.setCabinet(type);
  }

  /** Post-effects analyser, or null when the chain is not running. */
  getOutputAnalyser(): AnalyserNode | null {
    return this.chain?.outputAnalyser ?? null;
  }

  /** MediaStream of the post-effects output for MediaRecorder capture. */
  getRecordingStream(): MediaStream | null {
    return this.chain?.recordingStream ?? null;
  }
}

export const ampEngine = new AmpEngine();
