import { describe, expect, it } from "vitest";
import {
  BACKUP_KEYS,
  BACKUP_VERSION,
  buildBackup,
  isValidBackup,
  knownBackupEntries,
} from "./backup";

describe("buildBackup", () => {
  it("zabalí data se správnou hlavičkou", () => {
    const backup = buildBackup({ "rocker-settings": "{}" });
    expect(backup.app).toBe("rocker");
    expect(backup.kind).toBe("settings-backup");
    expect(backup.version).toBe(BACKUP_VERSION);
    expect(typeof backup.exportedAt).toBe("string");
    expect(backup.data).toEqual({ "rocker-settings": "{}" });
  });
});

describe("isValidBackup", () => {
  it("přijme platnou zálohu", () => {
    expect(isValidBackup(buildBackup({ "rocker-settings": "{}" }))).toBe(true);
  });

  it("odmítne null a ne-objekt", () => {
    expect(isValidBackup(null)).toBe(false);
    expect(isValidBackup("záloha")).toBe(false);
    expect(isValidBackup(42)).toBe(false);
  });

  it("odmítne objekt cizí aplikace", () => {
    expect(
      isValidBackup({
        app: "neco-jineho",
        kind: "settings-backup",
        version: 1,
        exportedAt: "2026-05-25",
        data: {},
      }),
    ).toBe(false);
  });

  it("odmítne data s nestringovou hodnotou", () => {
    expect(
      isValidBackup({
        app: "rocker",
        kind: "settings-backup",
        version: 1,
        exportedAt: "2026-05-25",
        data: { "rocker-settings": 123 },
      }),
    ).toBe(false);
  });

  it("odmítne chybějící pole", () => {
    expect(isValidBackup({ app: "rocker", kind: "settings-backup" })).toBe(
      false,
    );
  });
});

describe("knownBackupEntries", () => {
  it("zachová známé klíče", () => {
    const entries = knownBackupEntries({
      "rocker-settings": "a",
      "rocker-progress": "b",
    });
    expect(entries).toEqual({
      "rocker-settings": "a",
      "rocker-progress": "b",
    });
  });

  it("zahodí neznámé klíče (nelze vložit cizí localStorage)", () => {
    const entries = knownBackupEntries({
      "rocker-settings": "a",
      "evil-key": "x",
    });
    expect(entries["rocker-settings"]).toBe("a");
    expect("evil-key" in entries).toBe(false);
  });

  it("BACKUP_KEYS obsahuje očekávané klíče", () => {
    expect(BACKUP_KEYS).toContain("rocker-settings");
    expect(BACKUP_KEYS).toContain("rocker-progress");
  });

  it("BACKUP_KEYS zahrnuje všech 11 perzistovaných stores", () => {
    // Defensive lock — if someone removes a key in the future, this test
    // will flag it. Mirror of the persist `name` in each Zustand store.
    expect([...BACKUP_KEYS].sort()).toEqual(
      [
        "rocker-ai",
        "rocker-amp",
        "rocker-amp-presets",
        "rocker-chat",
        "rocker-custom-tunings",
        "rocker-instrument",
        "rocker-lesson-progress",
        "rocker-metronome",
        "rocker-progress",
        "rocker-settings",
        "rocker-yt-loops",
      ].sort(),
    );
  });

  it("zahrne i čtyři klíče přidané ve verzi 2 (amp, amp presety, metronom, vlastní ladění)", () => {
    const entries = knownBackupEntries({
      "rocker-amp": "{\"presetId\":\"clean\"}",
      "rocker-amp-presets": "[]",
      "rocker-metronome": "{\"bpm\":120}",
      "rocker-custom-tunings": "[]",
    });
    expect(entries).toEqual({
      "rocker-amp": "{\"presetId\":\"clean\"}",
      "rocker-amp-presets": "[]",
      "rocker-metronome": "{\"bpm\":120}",
      "rocker-custom-tunings": "[]",
    });
  });
});

describe("backward compatibility", () => {
  it("BACKUP_VERSION je 2 — informativně po přidání 4 stores", () => {
    expect(BACKUP_VERSION).toBe(2);
  });

  it("starší záloha (version 1) bez nových klíčů projde isValidBackup", () => {
    const oldBackup = {
      app: "rocker",
      kind: "settings-backup",
      version: 1,
      exportedAt: "2026-05-25T10:00:00Z",
      data: {
        "rocker-settings": "{}",
        "rocker-progress": "{}",
      },
    };
    expect(isValidBackup(oldBackup)).toBe(true);
  });

  it("restore starší zálohy (version 1) projde tiše — chybějící klíče se přeskočí", () => {
    // The v1 file lacked rocker-amp etc. — restore must not throw or
    // synthesize empty values; the user's existing stores stay untouched.
    const oldData = {
      "rocker-settings": "{\"theme\":\"vintage\"}",
      "rocker-progress": "{}",
    };
    const restored = knownBackupEntries(oldData);
    expect(restored).toEqual(oldData);
    expect("rocker-amp" in restored).toBe(false);
    expect("rocker-amp-presets" in restored).toBe(false);
    expect("rocker-metronome" in restored).toBe(false);
    expect("rocker-custom-tunings" in restored).toBe(false);
  });

  it("kruh export → import zachová všech 11 klíčů (round-trip pro v2)", () => {
    const entries: Record<string, string> = {};
    for (const key of BACKUP_KEYS) {
      entries[key] = `value-for-${key}`;
    }
    const backup = buildBackup(entries);
    expect(isValidBackup(backup)).toBe(true);
    expect(knownBackupEntries(backup.data)).toEqual(entries);
  });
});
