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
});
