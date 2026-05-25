// AudioEngine — architecture layer 5 (audio).
//
// Owns the single AudioContext and the microphone / audio-interface input.
// Knows nothing about React or the stores above it: UI and stores call these
// methods and subscribe to state changes. See ARCHITECTURE.md, section 3.

export type AudioStatus = "idle" | "starting" | "running" | "error";
export type MicPermission = "unknown" | "prompt" | "granted" | "denied";

export interface AudioInputDevice {
  deviceId: string;
  label: string;
}

export interface AudioEngineState {
  status: AudioStatus;
  permission: MicPermission;
  devices: AudioInputDevice[];
  inputDeviceId: string | null;
  latencyMs: number | null;
  error: string | null;
}

const INITIAL_STATE: AudioEngineState = {
  status: "idle",
  permission: "unknown",
  devices: [],
  inputDeviceId: null,
  latencyMs: null,
  error: null,
};

// Guitar input must stay raw. These voice-call DSP features would mangle the
// signal and ruin pitch detection, so they are explicitly disabled.
//
// channelCount: 1 forces mono capture. A two-input interface (e.g. M-Track
// Solo) otherwise yields a 2-channel stream with the guitar on a single
// channel; passed straight through, that plays out of only one speaker.
// A mono stream up-mixes to both channels on a stereo output.
const RAW_AUDIO_CONSTRAINTS: MediaTrackConstraints = {
  echoCancellation: false,
  noiseSuppression: false,
  autoGainControl: false,
  channelCount: 1,
};

type Listener = (state: AudioEngineState) => void;

class AudioEngine {
  private state: AudioEngineState = INITIAL_STATE;
  private listeners = new Set<Listener>();
  private context: AudioContext | null = null;
  private stream: MediaStream | null = null;
  private source: MediaStreamAudioSourceNode | null = null;
  private analyser: AnalyserNode | null = null;
  private splitter: ChannelSplitterNode | null = null;
  private monoMix: GainNode | null = null;

  getState(): AudioEngineState {
    return this.state;
  }

  /** Subscribe to state changes. Returns an unsubscribe function. */
  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  /** The analyser node of the live input — used by the level meter. */
  getAnalyser(): AnalyserNode | null {
    return this.analyser;
  }

  /**
   * The live input MediaStream, or null if the input is not running. Used by
   * the recorder (Bod 11) to capture takes from the selected device.
   */
  getInputStream(): MediaStream | null {
    return this.stream;
  }

  /**
   * The mono-summed live input node, or null if the input is not running.
   * Used by the amp / effects chain (Bod 16) to process the guitar signal.
   * Input channels are summed to mono here, so a two-channel interface (with
   * the guitar on a single channel) does not play from one speaker only.
   */
  getInputSource(): AudioNode | null {
    return this.monoMix;
  }

  /**
   * Returns the shared AudioContext for output use (metronome, playback),
   * creating and resuming it on demand. Does not open the microphone.
   */
  async getOutputContext(): Promise<AudioContext> {
    const context = this.getContext();
    if (context.state === "suspended") {
      await context.resume();
    }
    return context;
  }

  /**
   * Ensures the microphone input is running, requesting permission first if
   * needed. Returns whether the input is now running.
   */
  async ensureListening(): Promise<boolean> {
    if (this.state.permission !== "granted") {
      const result = await this.requestPermission();
      if (result !== "granted") return false;
    }
    await this.start();
    return this.state.status === "running";
  }

  private setState(patch: Partial<AudioEngineState>): void {
    this.state = { ...this.state, ...patch };
    for (const listener of this.listeners) {
      listener(this.state);
    }
  }

  private isSupported(): boolean {
    return (
      typeof navigator !== "undefined" &&
      !!navigator.mediaDevices &&
      typeof navigator.mediaDevices.getUserMedia === "function"
    );
  }

  /**
   * Triggers the browser microphone permission prompt. Device labels only
   * become available after permission is granted, so callers should request
   * permission before listing devices.
   */
  async requestPermission(): Promise<MicPermission> {
    if (!this.isSupported()) {
      this.setState({
        permission: "denied",
        error: "Tento prohlížeč nepodporuje přístup k mikrofonu.",
      });
      return "denied";
    }
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      // This request only unlocks the prompt and device labels — release it.
      stream.getTracks().forEach((track) => track.stop());
      this.setState({ permission: "granted", error: null });
      await this.refreshDevices();
      return "granted";
    } catch {
      this.setState({
        permission: "denied",
        error: "Přístup k mikrofonu byl zamítnut.",
      });
      return "denied";
    }
  }

  /** Lists audio input devices. Labels require granted permission. */
  async refreshDevices(): Promise<AudioInputDevice[]> {
    if (!this.isSupported()) return [];
    try {
      const all = await navigator.mediaDevices.enumerateDevices();
      const devices = all
        .filter((device) => device.kind === "audioinput")
        .map((device, index) => ({
          deviceId: device.deviceId,
          label: device.label || `Vstup ${index + 1}`,
        }));
      this.setState({ devices });
      return devices;
    } catch {
      return [];
    }
  }

  /** Starts capturing from the given (or the default) input device. */
  async start(deviceId?: string): Promise<void> {
    if (!this.isSupported()) {
      this.setState({
        status: "error",
        error: "Zvukový vstup není v tomto prohlížeči podporován.",
      });
      return;
    }
    await this.stop();
    this.setState({ status: "starting", error: null });
    try {
      const context = this.getContext();
      if (context.state === "suspended") {
        await context.resume();
      }

      const stream = await navigator.mediaDevices.getUserMedia({
        audio: deviceId
          ? { ...RAW_AUDIO_CONSTRAINTS, deviceId: { exact: deviceId } }
          : { ...RAW_AUDIO_CONSTRAINTS },
      });

      const source = context.createMediaStreamSource(stream);
      const analyser = context.createAnalyser();
      // Large window so low strings (E2 ~82 Hz) detect reliably for the tuner.
      analyser.fftSize = 8192;
      source.connect(analyser);

      // Sum the input channels into a true mono signal. A two-input interface
      // delivers a 2-channel stream with the guitar on a single channel;
      // passed straight through it plays from one speaker only. Splitting the
      // channels and summing them into one gain node yields mono, which the
      // stereo output then up-mixes to both speakers. The amp / effects chain
      // reads this node via getInputSource().
      const splitter = context.createChannelSplitter(2);
      const monoMix = context.createGain();
      source.connect(splitter);
      splitter.connect(monoMix, 0);
      splitter.connect(monoMix, 1);

      this.stream = stream;
      this.source = source;
      this.analyser = analyser;
      this.splitter = splitter;
      this.monoMix = monoMix;

      // Detect the device being unplugged mid-session.
      stream.getAudioTracks()[0]?.addEventListener("ended", () => {
        this.handleDeviceLost();
      });

      this.setState({
        status: "running",
        permission: "granted",
        inputDeviceId: deviceId ?? null,
        latencyMs: this.measureLatency(context),
        error: null,
      });
      await this.refreshDevices();
    } catch {
      this.setState({
        status: "error",
        error: "Nepodařilo se spustit zvukový vstup.",
      });
    }
  }

  /** Stops capturing but keeps the AudioContext alive for a fast restart. */
  async stop(): Promise<void> {
    this.source?.disconnect();
    this.analyser?.disconnect();
    this.splitter?.disconnect();
    this.monoMix?.disconnect();
    this.stream?.getTracks().forEach((track) => track.stop());
    this.source = null;
    this.analyser = null;
    this.splitter = null;
    this.monoMix = null;
    this.stream = null;
    if (this.state.status === "running" || this.state.status === "starting") {
      this.setState({ status: "idle" });
    }
  }

  private handleDeviceLost(): void {
    void this.stop();
    this.setState({
      status: "error",
      error: "Zvukové zařízení bylo odpojeno.",
    });
  }

  private getContext(): AudioContext {
    if (!this.context) {
      // Fall back to the prefixed constructor for older Safari.
      const Ctor =
        window.AudioContext ??
        (window as unknown as { webkitAudioContext?: typeof AudioContext })
          .webkitAudioContext;
      this.context = new Ctor();
    }
    return this.context;
  }

  /** Rough round-trip latency estimate in milliseconds. */
  private measureLatency(context: AudioContext): number {
    const base = context.baseLatency || 0;
    const output = context.outputLatency || 0;
    return Math.round((base + output) * 1000);
  }
}

/** Shared singleton instance. */
export const audioEngine = new AudioEngine();
