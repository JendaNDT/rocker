import { describe, expect, it } from "vitest";
import { buildMigrationArchive, crc32, MAX_MIGRATION_BYTES, storedZip } from "./migrationArchive";
import type { Backup } from "./backup";
import type { Recording } from "./recordings";
const backup: Backup = { app: "rocker", kind: "settings-backup", version: 2, exportedAt: "2026-10-10T00:00:00.000Z", data: {} };
const recording: Recording = { id: "original-id", name: "Kytara", createdAt: 1791590402000, durationSec: 1, mimeType: "audio/ogg;codecs=opus", markers: [{ id: "mark", timeSec: 0.25, label: "Začátek" }], blob: new Blob([new Uint8Array([1,2,3])]) };
// Read stored entries independently; the Windows checks additionally consume the real generated archive using System.IO.Compression.
async function entries(blob: Blob): Promise<Map<string, Uint8Array>> {
  const bytes = new Uint8Array(await blob.arrayBuffer()), view = new DataView(bytes.buffer), output = new Map<string, Uint8Array>(); let cursor = 0;
  while (view.getUint32(cursor, true) === 0x04034b50) {
    const size = view.getUint32(cursor+18, true), nameLength = view.getUint16(cursor+26, true), extraLength = view.getUint16(cursor+28, true);
    const name = new TextDecoder().decode(bytes.slice(cursor+30, cursor+30+nameLength)), start = cursor+30+nameLength+extraLength;
    output.set(name, bytes.slice(start, start+size)); cursor = start+size;
  }
  return output;
}
describe("Windows migration archive", () => {
  it("uses the standard ZIP CRC-32 vector", () => expect(crc32(new TextEncoder().encode("123456789"))).toBe(0xcbf43926));
  it("preserves original recording bytes, metadata and markers", async () => {
    const files = await entries(await buildMigrationArchive(backup, [recording]));
    expect(files.get("audio/000001.ogg")).toEqual(new Uint8Array([1,2,3]));
    const manifest = JSON.parse(new TextDecoder().decode(files.get("manifest.json")));
    expect(manifest.recordings[0]).toEqual({ id: recording.id, name: recording.name, createdAt: recording.createdAt, durationSec: 1, mimeType: recording.mimeType, markers: recording.markers, file: "audio/000001.ogg" });
    expect(manifest.files.find((f: { path: string }) => f.path === "audio/000001.ogg").sha256).toBe("039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81");
  });
  it("removes structured secrets and redacts a known key even with JSON punctuation", async () => {
    const secret = 'KEY"\\VALUE';
    const source = { ...backup, data: { "rocker-ai": JSON.stringify({ state: { apiKey: secret, model: "gemini-2.5-flash" } }), "rocker-chat": JSON.stringify({ state: { messages: [{ role: "user", text: `My ${secret}` }] } }) } };
    const files = await entries(await buildMigrationArchive(source, []));
    const settings = JSON.parse(new TextDecoder().decode(files.get("settings.json")));
    expect(JSON.parse(settings.data["rocker-ai"]).state).toEqual({ model: "gemini-2.5-flash" });
    expect(JSON.parse(settings.data["rocker-chat"]).state.messages[0].text).toBe("My [klíč odstraněn]");
    expect(source.data["rocker-ai"]).toContain("apiKey");
  });
  it("rejects duplicate recording IDs", async () => await expect(buildMigrationArchive(backup, [recording, recording])).rejects.toThrow("metadata"));
  it("rejects traversal and case-insensitive duplicate paths", () => {
    expect(() => storedZip([{ path: "../escape", bytes: new Uint8Array([1]) }])).toThrow("cesta");
    expect(() => storedZip([{ path: "audio/A.wav", bytes: new Uint8Array([1]) }, { path: "audio/a.wav", bytes: new Uint8Array([1]) }])).toThrow("cesta");
  });
  it("refuses excessive input before allocating its audio buffer", async () => {
    const oversized = { ...recording, blob: { size: MAX_MIGRATION_BYTES+1, arrayBuffer: () => { throw new Error("must not allocate"); } } as unknown as Blob };
    await expect(buildMigrationArchive(backup, [oversized])).rejects.toThrow("256 MB");
  });
});
