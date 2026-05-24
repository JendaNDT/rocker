// Waveform helpers — pure, no DOM. Unit-tested in waveform.test.ts.

/**
 * Downsamples mono channel data into `buckets` peak values (0..1). Each bucket
 * holds the maximum absolute amplitude of its slice — enough to draw a
 * waveform overview.
 */
export function computePeaks(
  channelData: Float32Array,
  buckets: number,
): number[] {
  if (buckets <= 0 || channelData.length === 0) return [];

  const sliceSize = Math.max(1, Math.floor(channelData.length / buckets));
  const peaks: number[] = [];

  for (let bucket = 0; bucket < buckets; bucket += 1) {
    const start = bucket * sliceSize;
    let max = 0;
    for (
      let index = start;
      index < start + sliceSize && index < channelData.length;
      index += 1
    ) {
      const value = Math.abs(channelData[index]);
      if (value > max) max = value;
    }
    peaks.push(max);
  }

  return peaks;
}
