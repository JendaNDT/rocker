// Guitar amp / effects chain — architecture layer 5 (audio). Part of Bod 16.
//
// Signal path:
//   input -> preGain -> distortion -> 3-band EQ -> cabinet sim -> output
// The cabinet output also feeds a feedback delay that is mixed back into the
// output. The output is wired straight to the speakers.
//
// The cabinet is modelled with fixed filters (high/low-pass + a presence
// peak) rather than an impulse response — no external IR file, no licensing,
// and enough to tame the harsh "fizz" of raw WaveShaper distortion.

/** Builds a WaveShaper distortion curve. `k` is the amount (~1 to ~100). */
function makeDistortionCurve(k: number): Float32Array<ArrayBuffer> {
  const samples = 2048;
  const curve = new Float32Array(samples);
  const deg = Math.PI / 180;
  for (let i = 0; i < samples; i += 1) {
    const x = (i * 2) / samples - 1;
    curve[i] = ((3 + k) * x * 20 * deg) / (Math.PI + k * Math.abs(x));
  }
  return curve;
}

/** Clamps a value into the [min, max] range. */
function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

/**
 * A guitar effects chain: drive, three-band EQ, a filter-based cabinet
 * simulation and a feedback delay. Connect a source to {@link input}; the
 * chain's output is wired straight to the context's speakers.
 */
export class AmpChain {
  /** Entry node — connect the guitar source here. */
  readonly input: GainNode;

  private readonly preGain: GainNode;
  private readonly shaper: WaveShaperNode;
  private readonly bass: BiquadFilterNode;
  private readonly mid: BiquadFilterNode;
  private readonly treble: BiquadFilterNode;
  private readonly cabHighpass: BiquadFilterNode;
  private readonly cabLowpass: BiquadFilterNode;
  private readonly cabPresence: BiquadFilterNode;
  private readonly delay: DelayNode;
  private readonly delayFeedback: GainNode;
  private readonly delayWet: GainNode;
  private readonly output: GainNode;

  constructor(context: BaseAudioContext) {
    this.input = context.createGain();

    this.preGain = context.createGain();
    this.preGain.gain.value = 6;

    this.shaper = context.createWaveShaper();
    this.shaper.oversample = "4x";
    this.shaper.curve = makeDistortionCurve(40);

    // Three-band tone stack.
    this.bass = context.createBiquadFilter();
    this.bass.type = "lowshelf";
    this.bass.frequency.value = 120;

    this.mid = context.createBiquadFilter();
    this.mid.type = "peaking";
    this.mid.frequency.value = 800;
    this.mid.Q.value = 1;

    this.treble = context.createBiquadFilter();
    this.treble.type = "highshelf";
    this.treble.frequency.value = 3500;

    // Cabinet simulation — fixed filters that shape the raw distortion into
    // a speaker-like tone.
    this.cabHighpass = context.createBiquadFilter();
    this.cabHighpass.type = "highpass";
    this.cabHighpass.frequency.value = 85;

    this.cabLowpass = context.createBiquadFilter();
    this.cabLowpass.type = "lowpass";
    this.cabLowpass.frequency.value = 5500;

    this.cabPresence = context.createBiquadFilter();
    this.cabPresence.type = "peaking";
    this.cabPresence.frequency.value = 3000;
    this.cabPresence.Q.value = 0.7;
    this.cabPresence.gain.value = 3;

    // Feedback delay.
    this.delay = context.createDelay(1);
    this.delay.delayTime.value = 0.3;
    this.delayFeedback = context.createGain();
    this.delayFeedback.gain.value = 0.35;
    this.delayWet = context.createGain();
    this.delayWet.gain.value = 0;

    this.output = context.createGain();
    this.output.gain.value = 0.5;

    // Main chain.
    this.input.connect(this.preGain);
    this.preGain.connect(this.shaper);
    this.shaper.connect(this.bass);
    this.bass.connect(this.mid);
    this.mid.connect(this.treble);
    this.treble.connect(this.cabHighpass);
    this.cabHighpass.connect(this.cabLowpass);
    this.cabLowpass.connect(this.cabPresence);

    // Dry path to the output.
    this.cabPresence.connect(this.output);

    // Delay send with a feedback loop, returned wet to the output.
    this.cabPresence.connect(this.delay);
    this.delay.connect(this.delayFeedback);
    this.delayFeedback.connect(this.delay);
    this.delay.connect(this.delayWet);
    this.delayWet.connect(this.output);

    this.output.connect(context.destination);
  }

  /** Distortion amount — `amount` runs 0 (mild) to 1 (heavy). */
  setDrive(amount: number): void {
    this.preGain.gain.value = 1 + clamp(amount, 0, 1) * 24;
  }

  /** Bass EQ — `amount` runs -1 (cut) to 1 (boost). */
  setBass(amount: number): void {
    this.bass.gain.value = clamp(amount, -1, 1) * 12;
  }

  /** Mid EQ — `amount` runs -1 (cut) to 1 (boost). */
  setMid(amount: number): void {
    this.mid.gain.value = clamp(amount, -1, 1) * 12;
  }

  /** Treble EQ — `amount` runs -1 (cut) to 1 (boost). */
  setTreble(amount: number): void {
    this.treble.gain.value = clamp(amount, -1, 1) * 12;
  }

  /** Delay mix — `amount` runs 0 (dry) to 1 (full echo). */
  setDelayMix(amount: number): void {
    this.delayWet.gain.value = clamp(amount, 0, 1) * 0.6;
  }

  /** Output level — `value` runs 0 to 1. */
  setLevel(value: number): void {
    this.output.gain.value = clamp(value, 0, 1);
  }

  /** Disconnects every node so the chain can be discarded. */
  dispose(): void {
    const nodes = [
      this.input,
      this.preGain,
      this.shaper,
      this.bass,
      this.mid,
      this.treble,
      this.cabHighpass,
      this.cabLowpass,
      this.cabPresence,
      this.delay,
      this.delayFeedback,
      this.delayWet,
      this.output,
    ];
    for (const node of nodes) {
      node.disconnect();
    }
  }
}
