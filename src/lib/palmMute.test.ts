import { describe, expect, it } from "vitest";
import {
  MIN_CALIBRATION_SAMPLES,
  PALM_MUTE_DRILLS,
  calibrate,
  classifyHit,
  currentHitIndex,
  drillScore,
  expectedTechnique,
  getPalmMuteDrill,
  isDrillComplete,
  isOnset,
  logDrillHit,
  spectralCentroid,
  startPalmMuteDrill,
} from "./palmMute";

describe("spectralCentroid", () => {
  // 4 magnitude bins -> fftSize 8; at 8000 Hz each bin is 1000 Hz wide.
  it("returns the frequency of the only populated bin", () => {
    expect(spectralCentroid([0, 0, 1, 0], 8000)).toBe(2000);
  });

  it("is zero for silence", () => {
    expect(spectralCentroid([0, 0, 0, 0], 8000)).toBe(0);
  });

  it("is higher for a bright spectrum than a dark one", () => {
    const dark = spectralCentroid([4, 2, 1, 0], 8000);
    const bright = spectralCentroid([0, 1, 2, 4], 8000);
    expect(bright).toBeGreaterThan(dark);
  });
});

describe("isOnset", () => {
  it("is true on a rising edge through the threshold", () => {
    expect(isOnset(0.1, 0.5, 0.3)).toBe(true);
  });

  it("is false when the level was already above the threshold", () => {
    expect(isOnset(0.5, 0.6, 0.3)).toBe(false);
  });

  it("is false on a falling edge", () => {
    expect(isOnset(0.5, 0.1, 0.3)).toBe(false);
  });
});

describe("calibrate", () => {
  it("sets the threshold midway between open and muted means", () => {
    const calibration = calibrate([2400, 2500, 2300], [800, 900, 700]);
    expect(calibration).not.toBeNull();
    expect(calibration?.openMean).toBe(2400);
    expect(calibration?.mutedMean).toBe(800);
    expect(calibration?.threshold).toBe(1600);
  });

  it("returns null with too few samples", () => {
    expect(calibrate([2400, 2500], [800, 900, 700])).toBeNull();
  });

  it("returns null when muted is not darker than open", () => {
    expect(calibrate([700, 800, 900], [2300, 2400, 2500])).toBeNull();
  });

  it("requires at least MIN_CALIBRATION_SAMPLES of each kind", () => {
    const open = Array(MIN_CALIBRATION_SAMPLES).fill(2400);
    const muted = Array(MIN_CALIBRATION_SAMPLES).fill(800);
    expect(calibrate(open, muted)).not.toBeNull();
  });
});

describe("classifyHit", () => {
  const calibration = { openMean: 2400, mutedMean: 800, threshold: 1600 };

  it("classifies a dark hit as muted", () => {
    expect(classifyHit(1200, calibration)).toBe("muted");
  });

  it("classifies a bright hit as open", () => {
    expect(classifyHit(2000, calibration)).toBe("open");
  });
});

describe("palm mute drills", () => {
  it("has unique ids", () => {
    const ids = PALM_MUTE_DRILLS.map((drill) => drill.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("looks up a drill by id", () => {
    expect(getPalmMuteDrill("eight-muted")?.pattern).toHaveLength(8);
  });

  it("returns undefined for an unknown id", () => {
    expect(getPalmMuteDrill("nope")).toBeUndefined();
  });
});

describe("drill progress", () => {
  const drill = getPalmMuteDrill("alternating");
  if (!drill) throw new Error("missing fixture drill");

  it("starts empty, pointing at the first hit", () => {
    const state = startPalmMuteDrill(drill);
    expect(state.hits).toEqual([]);
    expect(currentHitIndex(state)).toBe(0);
    expect(expectedTechnique(state)).toBe("muted");
    expect(isDrillComplete(state)).toBe(false);
  });

  it("advances as hits are logged", () => {
    let state = startPalmMuteDrill(drill);
    state = logDrillHit(state, "muted");
    expect(currentHitIndex(state)).toBe(1);
    expect(expectedTechnique(state)).toBe("open");
  });

  it("completes after the whole pattern is logged", () => {
    let state = startPalmMuteDrill(drill);
    for (let i = 0; i < drill.pattern.length; i += 1) {
      state = logDrillHit(state, "muted");
    }
    expect(isDrillComplete(state)).toBe(true);
    expect(currentHitIndex(state)).toBe(-1);
    expect(expectedTechnique(state)).toBeNull();
  });

  it("ignores hits logged after completion", () => {
    let state = startPalmMuteDrill(drill);
    for (let i = 0; i < drill.pattern.length; i += 1) {
      state = logDrillHit(state, "muted");
    }
    const after = logDrillHit(state, "open");
    expect(after.hits).toHaveLength(drill.pattern.length);
  });
});

describe("drillScore", () => {
  it("counts only hits that matched the expected technique", () => {
    const drill = getPalmMuteDrill("alternating");
    if (!drill) throw new Error("missing fixture drill");
    let state = startPalmMuteDrill(drill);
    // Pattern is muted/open/...; logging all muted matches the four muted slots.
    for (let i = 0; i < drill.pattern.length; i += 1) {
      state = logDrillHit(state, "muted");
    }
    expect(drillScore(state)).toBe(4);
  });

  it("is a perfect score when every hit matches", () => {
    const drill = getPalmMuteDrill("eight-muted");
    if (!drill) throw new Error("missing fixture drill");
    let state = startPalmMuteDrill(drill);
    for (let i = 0; i < drill.pattern.length; i += 1) {
      state = logDrillHit(state, "muted");
    }
    expect(drillScore(state)).toBe(8);
  });
});
