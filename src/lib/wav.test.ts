import { describe, expect, it } from "vitest";
import { encodeWav } from "./wav";

/** Reads a 4-char ASCII tag from a WAV buffer at the given offset. */
function tag(buffer: ArrayBuffer, offset: number): string {
  const bytes = new Uint8Array(buffer, offset, 4);
  return String.fromCharCode(...bytes);
}

describe("encodeWav", () => {
  it("writes a valid header and the right size for one channel", () => {
    const buffer = encodeWav([new Float32Array([0, 1, -1, 0.5])], 44100);
    // 44-byte header + 4 frames * 1 channel * 2 bytes.
    expect(buffer.byteLength).toBe(44 + 8);
    expect(tag(buffer, 0)).toBe("RIFF");
    expect(tag(buffer, 8)).toBe("WAVE");
    expect(tag(buffer, 36)).toBe("data");

    const view = new DataView(buffer);
    expect(view.getUint16(22, true)).toBe(1); // channels
    expect(view.getUint32(24, true)).toBe(44100); // sample rate
    expect(view.getUint16(34, true)).toBe(16); // bits per sample
  });

  it("converts float samples to clamped 16-bit PCM", () => {
    const buffer = encodeWav([new Float32Array([0, 1, -1, 2])], 44100);
    const view = new DataView(buffer);
    expect(view.getInt16(44, true)).toBe(0); // 0.0
    expect(view.getInt16(46, true)).toBe(32767); // 1.0
    expect(view.getInt16(48, true)).toBe(-32767); // -1.0
    expect(view.getInt16(50, true)).toBe(32767); // 2.0 clamped to 1.0
  });

  it("interleaves two channels", () => {
    const left = new Float32Array([1, 0]);
    const right = new Float32Array([-1, 0]);
    const buffer = encodeWav([left, right], 48000);
    // 2 frames * 2 channels * 2 bytes.
    expect(buffer.byteLength).toBe(44 + 8);
    const view = new DataView(buffer);
    expect(view.getInt16(44, true)).toBe(32767); // frame 0, left
    expect(view.getInt16(46, true)).toBe(-32767); // frame 0, right
    expect(view.getUint16(22, true)).toBe(2);
  });
});
