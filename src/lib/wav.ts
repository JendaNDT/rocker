// WAV encoder — pure logic, no DOM or audio. Part of Bod 17 (multi-track
// export). Unit-tested in wav.test.ts.

/** Writes an ASCII string into a DataView at the given byte offset. */
function writeString(view: DataView, offset: number, text: string): void {
  for (let i = 0; i < text.length; i += 1) {
    view.setUint8(offset + i, text.charCodeAt(i));
  }
}

/**
 * Encodes raw PCM channel data into a 16-bit WAV file. Every channel array
 * is expected to have the same length (number of frames); the first channel
 * sets the frame count. Needs at least one channel.
 */
export function encodeWav(
  channels: Float32Array[],
  sampleRate: number,
): ArrayBuffer {
  const numChannels = Math.max(1, channels.length);
  const numFrames = channels[0]?.length ?? 0;
  const bytesPerSample = 2;
  const blockAlign = numChannels * bytesPerSample;
  const dataSize = numFrames * blockAlign;
  const buffer = new ArrayBuffer(44 + dataSize);
  const view = new DataView(buffer);

  // RIFF / WAVE header.
  writeString(view, 0, "RIFF");
  view.setUint32(4, 36 + dataSize, true);
  writeString(view, 8, "WAVE");
  // fmt chunk.
  writeString(view, 12, "fmt ");
  view.setUint32(16, 16, true);
  view.setUint16(20, 1, true); // PCM
  view.setUint16(22, numChannels, true);
  view.setUint32(24, sampleRate, true);
  view.setUint32(28, sampleRate * blockAlign, true); // byte rate
  view.setUint16(32, blockAlign, true);
  view.setUint16(34, 8 * bytesPerSample, true); // bits per sample
  // data chunk.
  writeString(view, 36, "data");
  view.setUint32(40, dataSize, true);

  // Interleaved 16-bit PCM samples.
  let offset = 44;
  for (let frame = 0; frame < numFrames; frame += 1) {
    for (let channel = 0; channel < numChannels; channel += 1) {
      const sample = channels[channel]?.[frame] ?? 0;
      const clamped = Math.max(-1, Math.min(1, sample));
      view.setInt16(offset, Math.round(clamped * 32767), true);
      offset += 2;
    }
  }
  return buffer;
}
