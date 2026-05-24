import { describe, expect, it } from "vitest";
import { mixLoopLayer } from "./loopMix";

describe("mixLoopLayer", () => {
  it("sums samples and clamps to the -1..1 range", () => {
    const mixed = mixLoopLayer(
      [new Float32Array([0, 0.5, -0.8])],
      [new Float32Array([0.5, 0.6, -0.8])],
    );
    expect(Array.from(mixed[0])).toEqual([0.5, 1, -1]);
  });

  it("pads a shorter overlay with silence, keeping the base length", () => {
    const mixed = mixLoopLayer(
      [new Float32Array([0, 0, 0, 0])],
      [new Float32Array([1, 1])],
    );
    expect(Array.from(mixed[0])).toEqual([1, 1, 0, 0]);
  });

  it("trims a longer overlay to the base length", () => {
    const mixed = mixLoopLayer(
      [new Float32Array([0, 0])],
      [new Float32Array([1, 1, 1, 1])],
    );
    expect(mixed[0]).toHaveLength(2);
  });

  it("applies a mono overlay to every base channel", () => {
    const mixed = mixLoopLayer(
      [new Float32Array([0, 0]), new Float32Array([0, 0])],
      [new Float32Array([0.5, 0.5])],
    );
    expect(Array.from(mixed[0])).toEqual([0.5, 0.5]);
    expect(Array.from(mixed[1])).toEqual([0.5, 0.5]);
  });
});
