// Loop layer mixing — pure logic, no DOM or audio. Part of Bod 17 (loop
// pedal). Unit-tested in loopMix.test.ts.

/**
 * Mixes an overlay layer into a base loop, sample by sample. The result keeps
 * the base's length: a shorter overlay is padded with silence, a longer one
 * is trimmed. Summed samples are clamped to the -1..1 range. A mono overlay
 * is applied to every base channel.
 */
export function mixLoopLayer(
  base: Float32Array[],
  overlay: Float32Array[],
): Float32Array<ArrayBuffer>[] {
  return base.map((baseChannel, channel) => {
    const overlayChannel = overlay[channel] ?? overlay[0];
    const out = new Float32Array(baseChannel.length);
    for (let i = 0; i < baseChannel.length; i += 1) {
      const sum = baseChannel[i] + (overlayChannel?.[i] ?? 0);
      out[i] = Math.max(-1, Math.min(1, sum));
    }
    return out;
  });
}
