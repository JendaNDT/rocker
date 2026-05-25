// Tests for the IndexedDB recordings layer. Uses fake-indexeddb to provide an
// in-memory IndexedDB so the layer can be exercised without a browser.

import "fake-indexeddb/auto";
import { beforeEach, describe, expect, it } from "vitest";
import type { Marker, Recording } from "./recordings";
import {
  deleteRecording,
  getRecording,
  listRecordings,
  saveRecording,
  updateRecording,
} from "./recordingsDb";

/** Builds a valid Recording; pass overrides for the fields a test cares about. */
function makeRecording(overrides: Partial<Recording> = {}): Recording {
  return {
    id: "rec-default",
    name: "Pokus",
    createdAt: 1000,
    durationSec: 10,
    mimeType: "audio/webm",
    markers: [],
    blob: new Blob(["audio"], { type: "audio/webm" }),
    ...overrides,
  };
}

// Each test starts from an empty store.
beforeEach(async () => {
  const all = await listRecordings();
  for (const recording of all) await deleteRecording(recording.id);
});

describe("recordingsDb", () => {
  it("saveRecording a getRecording — roundtrip", async () => {
    await saveRecording(
      makeRecording({ id: "r1", name: "Můj pokus", durationSec: 42 }),
    );
    const got = await getRecording("r1");
    expect(got?.name).toBe("Můj pokus");
    expect(got?.durationSec).toBe(42);
    expect(got?.blob).toBeInstanceOf(Blob);
  });

  it("getRecording vrátí undefined pro neznámé id", async () => {
    expect(await getRecording("neexistuje")).toBeUndefined();
  });

  it("listRecordings vrací nahrávky od nejnovější", async () => {
    await saveRecording(makeRecording({ id: "old", createdAt: 1000 }));
    await saveRecording(makeRecording({ id: "new", createdAt: 5000 }));
    const list = await listRecordings();
    expect(list.map((recording) => recording.id)).toEqual(["new", "old"]);
  });

  it("deleteRecording odstraní nahrávku", async () => {
    await saveRecording(makeRecording({ id: "d1" }));
    await deleteRecording("d1");
    expect(await getRecording("d1")).toBeUndefined();
  });

  it("saveRecording uloží markery seřazené podle času", async () => {
    await saveRecording(
      makeRecording({
        id: "s1",
        markers: [
          { id: "y", timeSec: 9, label: "y" },
          { id: "x", timeSec: 3, label: "x" },
        ],
      }),
    );
    const got = await getRecording("s1");
    expect(got?.markers.map((marker) => marker.id)).toEqual(["x", "y"]);
  });

  it("updateRecording změní název", async () => {
    await saveRecording(makeRecording({ id: "u1", name: "Staré" }));
    await updateRecording("u1", { name: "Nové" });
    expect((await getRecording("u1"))?.name).toBe("Nové");
  });

  it("updateRecording seřadí markery z patche", async () => {
    await saveRecording(makeRecording({ id: "u2" }));
    await updateRecording("u2", {
      markers: [
        { id: "b", timeSec: 5, label: "pět" },
        { id: "a", timeSec: 1, label: "jedna" },
      ],
    });
    const got = await getRecording("u2");
    expect(got?.markers.map((marker) => marker.id)).toEqual(["a", "b"]);
  });

  it("updateRecording zahodí vadný marker z patche", async () => {
    await saveRecording(makeRecording({ id: "u3" }));
    await updateRecording("u3", {
      markers: [
        { id: "ok", timeSec: 2, label: "ok" },
        { id: "bad", timeSec: -1, label: "záporný čas" },
      ] as Marker[],
    });
    const got = await getRecording("u3");
    expect(got?.markers.map((marker) => marker.id)).toEqual(["ok"]);
  });

  it("updateRecording je no-op pro neznámé id", async () => {
    await expect(
      updateRecording("neexistuje", { name: "x" }),
    ).resolves.toBeUndefined();
  });
});
