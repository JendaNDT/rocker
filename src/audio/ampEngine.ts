// AmpEngine — architecture layer 5 (audio).
//
// Owns a single, long-lived AmpChain so the aparát keeps playing across screen
// changes. The chain used to be component-scoped (useLocalAmpChain), which tore
// it down on every navigation. Hosting it in a module-level singleton — exactly
// like AudioEngine — lets it survive client-side navigation (which preserves the
// JS heap; a full reload, e.g. right after a deploy, still resets everything,
// which the service worker change addresses separately).
//
// Settings are pushed in from the shared ampStore via the AmpHost mounted in
// AppShell. A take recorded here captures the post-effects stream.

import type { AmpSettings } from "@/lib/ampPresets";
import { AmpChain } from "./ampChain";
import { audioEngine } from "./AudioEngine";

export type AmpStatus = "idle" | "starting" | "running" | "error";

export interface AmpEngineState {
  status: AmpStatus;
  /** True while the chain is wired to the speakers. */
  active: boolean;
  /** User-facing error from the last failed start, or null. */
  error: string | null;
}

/** Pushes every parameter of a settings object onto the chain. */
export function applyAmpSettings(chain: AmpChain, settings: AmpSettings): void {
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

const INITIAL_STATE: AmpEngineState = {
  status: "idle",
  active: false,
  error: null,
};

type Listener = (state: AmpEngineState) => void;

class AmpEngine {
  private state: AmpEngineState = INITIAL_STATE;
  private listeners = new Set<Listener>();
  private chain: AmpChain | null = null;
  private settings: AmpSettings | null = null;

  getState(): AmpEngineState {
    return this.state;
  }

  /** Subscribe to state changes. Returns an unsubscribe function. */
  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  /** The post-effects analyser for meters / scope, or null when not running. */
  getOutputAnalyser(): AnalyserNode | null {
    return this.chain?.outputAnalyser ?? null;
  }

  /** The post-effects MediaStream for recording, or null when not running. */
  getRecordingStream(): MediaStream | null {
    return this.chain?.recordingStream ?? null;
  }

  /**
   * Stores the current tone settings and applies them live if the chain is
   * already running. Called by the AmpHost whenever the shared store changes,
   * so a tweak on any screen reaches the single running chain.
   */
  setSettings(settings: AmpSettings): void {
    this.settings = settings;
    if (this.chain) applyAmpSettings(this.chain, settings);
  }

  /**
   * Spins the chain up: ensures the input is listening, builds the chain, wires
   * the mono input into it and the output to the speakers. No-op if already
   * running or starting.
   */
  async start(): Promise<void> {
    if (this.state.status === "starting" || this.state.active) return;
    this.setState({ status: "starting", error: null });

    const running = await audioEngine.ensureListening();
    if (!running) {
      this.setState({
        status: "error",
        active: false,
        error: "Nepodařilo se spustit zvukový vstup. Povol mikrofon v Profilu.",
      });
      return;
    }

    const source = audioEngine.getInputSource();
    if (!source) {
      this.setState({
        status: "error",
        active: false,
        error: "Zvukový vstup není dostupný.",
      });
      return;
    }

    const context = await audioEngine.getOutputContext();
    const chain = new AmpChain(context);
    if (this.settings) applyAmpSettings(chain, this.settings);
    source.connect(chain.input);
    this.chain = chain;
    this.setState({ status: "running", active: true, error: null });
  }

  /** Tears the chain down and disconnects it from the input. */
  async stop(): Promise<void> {
    if (this.chain) {
      audioEngine.getInputSource()?.disconnect(this.chain.input);
      this.chain.dispose();
      this.chain = null;
    }
    this.setState({ status: "idle", active: false, error: null });
  }

  private setState(patch: Partial<AmpEngineState>): void {
    this.state = { ...this.state, ...patch };
    for (const listener of this.listeners) {
      listener(this.state);
    }
  }
}

/** Shared singleton instance. */
export const ampEngine = new AmpEngine();
