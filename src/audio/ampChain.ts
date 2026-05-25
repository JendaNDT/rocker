// Guitar amp / effects chain — architecture layer 5 (audio). Part of Bod 16.
//
// Signal path:
//   input -> compressor -> preGain -> distortion -> 3-band EQ -> cabinet sim
//   -> output
// The compressor sits at the front (compressor-pedal position) and is
// transparent at amount 0. The cabinet output also feeds a feedback delay
// mixed back into the output. The output is wired straight to the speakers.
//
// The cabinet is modelled with fixed filters (high/low-pass + a presence
// peak) rather than an impulse response — no external IR file, no licensing,
// and enough to tame the harsh "fizz" of raw WaveShaper distortion.

import type { CabinetType, DistortionType } from "@/lib/ampPresets";

/** Builds a 2048-sample WaveShaper curve from a per-sample shaping function. */
function buildCurve(
  shape: (x: number) => number,
): Float32Array<ArrayBuffer> {
  const samples = 2048;
  const curve = new Float32Array(samples);
  for (let i = 0; i < samples; i += 1) {
    const x = (i * 2) / samples - 1;
    curve[i] = shape(x);
  }
  return curve;
}

/** Builds the medium-hard "distortion" curve. `k` is the amount (~1..~100). */
function makeDistortionCurve(k: number): Float32Array<ArrayBuffer> {
  const deg = Math.PI / 180;
  return buildCurve(
    (x) => ((3 + k) * x * 20 * deg) / (Math.PI + k * Math.abs(x)),
  );
}

// WaveShaper curves per distortion character: a soft, smooth overdrive; the
// existing medium "distortion"; and a hard, near-square fuzz. Built once.
const DISTORTION_CURVES: Record<DistortionType, Float32Array<ArrayBuffer>> = {
  overdrive: buildCurve((x) => Math.tanh(x * 2)),
  distortion: makeDistortionCurve(40),
  fuzz: buildCurve((x) => Math.tanh(x * 9)),
};

/** Filter settings of one cabinet simulation. */
interface CabinetSpec {
  /** High-pass cutoff in Hz — trims sub-bass rumble. */
  highpass: number;
  /** Low-pass cutoff in Hz — tames the harsh top "fizz". */
  lowpass: number;
  /** Presence peak centre frequency in Hz. */
  presenceFreq: number;
  /** Presence peak Q. */
  presenceQ: number;
  /** Presence peak gain in dB. */
  presenceGain: number;
}

// Cabinet voicings: a balanced modern cab, a darker/warmer vintage cab and
// a brighter open-sounding cab. "modern" matches the original fixed values.
const CABINETS: Record<CabinetType, CabinetSpec> = {
  modern: {
    highpass: 85,
    lowpass: 5500,
    presenceFreq: 3000,
    presenceQ: 0.7,
    presenceGain: 3,
  },
  vintage: {
    highpass: 90,
    lowpass: 4200,
    presenceFreq: 2400,
    presenceQ: 0.7,
    presenceGain: 1.5,
  },
  open: {
    highpass: 75,
    lowpass: 7000,
    presenceFreq: 3600,
    presenceQ: 0.8,
    presenceGain: 4.5,
  },
};

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

  private readonly compressor: DynamicsCompressorNode;
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

    // Compressor at the front (compressor-pedal position). Defaults are
    // transparent — setCompression(0) leaves the ratio at 1:1.
    this.compressor = context.createDynamicsCompressor();
    this.compressor.ratio.value = 1;
    this.compressor.threshold.value = 0;
    this.compressor.knee.value = 0;
    this.compressor.attack.value = 0.003;
    this.compressor.release.value = 0.25;

    this.preGain = context.createGain();
    this.preGain.gain.value = 6;

    this.shaper = context.createWaveShaper();
    this.shaper.oversample = "4x";
    this.shaper.curve = DISTORTION_CURVES.distortion;

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

    // Cabinet simulation — filters that shape the raw distortion into a
    // speaker-like tone. Their values are set by setCabinet() below.
    this.cabHighpass = context.createBiquadFilter();
    this.cabHighpass.type = "highpass";

    this.cabLowpass = context.createBiquadFilter();
    this.cabLowpass.type = "lowpass";

    this.cabPresence = context.createBiquadFilter();
    this.cabPresence.type = "peaking";

    this.setCabinet("modern");

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
    this.input.connect(this.compressor);
    this.compressor.connect(this.preGain);
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

  /** Compression amount — `amount` runs 0 (transparent) to 1 (heavy). */
  setCompression(amount: number): void {
    const a = clamp(amount, 0, 1);
    // At 0 the ratio is 1:1 (no compression); higher values squash harder.
    this.compressor.ratio.value = 1 + a * 11;
    this.compressor.threshold.value = a * -36;
    this.compressor.knee.value = a * 24;
  }

  /** Distortion amount — `amount` runs 0 (mild) to 1 (heavy). */
  setDrive(amount: number): void {
    this.preGain.gain.value = 1 + clamp(amount, 0, 1) * 24;
  }

  /** Distortion character — swaps the waveshaper curve. */
  setDistortionType(type: DistortionType): void {
    this.shaper.curve = DISTORTION_CURVES[type];
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

  /** Cabinet character — retunes the cabinet-simulation filters. */
  setCabinet(type: CabinetType): void {
    const cab = CABINETS[type];
    this.cabHighpass.frequency.value = cab.highpass;
    this.cabLowpass.frequency.value = cab.lowpass;
    this.cabPresence.frequency.value = cab.presenceFreq;
    this.cabPresence.Q.value = cab.presenceQ;
    this.cabPresence.gain.value = cab.presenceGain;
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
      this.compressor,
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
