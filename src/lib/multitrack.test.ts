import { describe, expect, it } from "vitest";
import {
  type Track,
  addTrack,
  audibleTracks,
  emptyProject,
  nextTrackName,
  removeTrack,
  renameTrack,
  sanitizeProject,
  sanitizeTrack,
  setTrackGain,
  setTrackMuted,
} from "./multitrack";

/** Builds a Track for tests, with optional field overrides. */
function track(id: string, over: Partial<Track> = {}): Track {
  return {
    id,
    name: `Stopa ${id}`,
    blob: new Blob(["audio"], { type: "audio/webm" }),
    mimeType: "audio/webm",
    durationSec: 4,
    gain: 1,
    muted: false,
    ...over,
  };
}

describe("multitrack project helpers", () => {
  it("adds and removes tracks immutably", () => {
    const project = emptyProject();
    const withOne = addTrack(project, track("a"));
    expect(project.tracks).toHaveLength(0);
    expect(withOne.tracks).toHaveLength(1);

    const withTwo = addTrack(withOne, track("b"));
    expect(removeTrack(withTwo, "a").tracks.map((t) => t.id)).toEqual(["b"]);
  });

  it("names the next track by count", () => {
    let project = emptyProject();
    expect(nextTrackName(project)).toBe("Stopa 1");
    project = addTrack(project, track("a"));
    expect(nextTrackName(project)).toBe("Stopa 2");
  });

  it("mutes, renames and sets gain on one track only", () => {
    const project = addTrack(addTrack(emptyProject(), track("a")), track("b"));

    expect(setTrackMuted(project, "a", true).tracks[0].muted).toBe(true);
    expect(setTrackMuted(project, "a", true).tracks[1].muted).toBe(false);
    expect(renameTrack(project, "b", "Sólo").tracks[1].name).toBe("Sólo");
  });

  it("clamps track gain to 0..1", () => {
    const project = addTrack(emptyProject(), track("a"));
    expect(setTrackGain(project, "a", 1.7).tracks[0].gain).toBe(1);
    expect(setTrackGain(project, "a", -0.5).tracks[0].gain).toBe(0);
    expect(setTrackGain(project, "a", 0.4).tracks[0].gain).toBeCloseTo(0.4);
  });

  it("lists only non-muted, audible tracks", () => {
    const project = {
      tracks: [
        track("a"),
        track("b", { muted: true }),
        track("c", { gain: 0 }),
      ],
    };
    expect(audibleTracks(project).map((t) => t.id)).toEqual(["a"]);
  });
});

describe("multitrack type guards", () => {
  it("accepts a well-formed track", () => {
    expect(sanitizeTrack(track("a"))?.id).toBe("a");
  });

  it("rejects a track with no audio blob or bad fields", () => {
    expect(sanitizeTrack({ ...track("a"), blob: "nope" })).toBeNull();
    expect(sanitizeTrack({ ...track("a"), durationSec: -1 })).toBeNull();
    expect(sanitizeTrack(null)).toBeNull();
  });

  it("defaults a missing gain to 1 and clamps a bad one", () => {
    const noGain = sanitizeTrack({ ...track("a"), gain: undefined });
    expect(noGain?.gain).toBe(1);
    const bigGain = sanitizeTrack({ ...track("a"), gain: 9 });
    expect(bigGain?.gain).toBe(1);
  });

  it("sanitizes a project, dropping malformed tracks", () => {
    const project = sanitizeProject({
      tracks: [track("a"), { id: "broken" }, track("b")],
    });
    expect(project.tracks.map((t) => t.id)).toEqual(["a", "b"]);
    expect(sanitizeProject("garbage").tracks).toEqual([]);
  });
});
