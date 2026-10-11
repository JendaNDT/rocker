// Portable PWA-to-Windows migration archive. No browser storage or UI access.
import type { Backup } from "./backup";
import type { Recording } from "./recordings";
export const MAX_MIGRATION_BYTES = 256 * 1024 * 1024;
interface ArchiveFile { path: string; bytes: number; sha256: string }
const CRC_TABLE = Uint32Array.from({ length: 256 }, (_, value) => {
  let crc = value; for (let bit = 0; bit < 8; bit++) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1)); return crc >>> 0;
});
export function crc32(bytes: Uint8Array): number {
  let crc = 0xffffffff;
  for (const byte of bytes) {
    crc = (crc >>> 8) ^ CRC_TABLE[(crc ^ byte) & 0xff];
  }
  return (crc ^ 0xffffffff) >>> 0;
}
function header(size: number): [ArrayBuffer, DataView] {
  const buffer = new ArrayBuffer(size); return [buffer, new DataView(buffer)];
}
/** Stored ZIP, bounded to 256 MiB: preserves original audio bytes, no codec conversion. */
export function storedZip(entries: readonly { path: string; bytes: Uint8Array }[]): Blob {
  if (entries.length > 10000) throw new Error("Příliš mnoho souborů pro převod.");
  const parts: BlobPart[] = [], central: BlobPart[] = [];
  const names = new Set<string>(); let offset = 0, centralSize = 0;
  for (const entry of entries) {
    if (!/^[a-zA-Z0-9._/-]+$/.test(entry.path) || entry.path.split("/").some(p => p === ".." || p === "." || !p) || names.has(entry.path.toLowerCase())) throw new Error("Neplatná nebo duplicitní cesta exportu.");
    names.add(entry.path.toLowerCase());
    const name = new TextEncoder().encode(entry.path), checksum = crc32(entry.bytes);
    const [local, h] = header(30);
    h.setUint32(0, 0x04034b50, true); h.setUint16(4, 20, true); h.setUint16(6, 0x0800, true);
    h.setUint16(12, 0x0021, true); h.setUint32(14, checksum, true); h.setUint32(18, entry.bytes.length, true); h.setUint32(22, entry.bytes.length, true); h.setUint16(26, name.length, true);
    parts.push(local, name.buffer, new Uint8Array(entry.bytes).buffer);
    const [directory, d] = header(46);
    d.setUint32(0, 0x02014b50, true); d.setUint16(4, 20, true); d.setUint16(6, 20, true); d.setUint16(8, 0x0800, true);
    d.setUint16(14, 0x0021, true); d.setUint32(16, checksum, true); d.setUint32(20, entry.bytes.length, true); d.setUint32(24, entry.bytes.length, true); d.setUint16(28, name.length, true); d.setUint32(42, offset, true);
    central.push(directory, name.buffer); centralSize += 46 + name.length;
    offset += 30 + name.length + entry.bytes.length;
    if (offset + centralSize > MAX_MIGRATION_BYTES) throw new Error("Export pro Windows přesahuje 256 MB. Nahrávky přenes po menších částech.");
  }
  const [end, e] = header(22);
  e.setUint32(0, 0x06054b50, true); e.setUint16(8, entries.length, true); e.setUint16(10, entries.length, true); e.setUint32(12, centralSize, true); e.setUint32(16, offset, true);
  return new Blob([...parts, ...central, end], { type: "application/zip" });
}
function withoutSecrets(value: unknown, secrets: readonly string[]): unknown {
  if (Array.isArray(value)) return value.map(item => withoutSecrets(item, secrets));
  if (value !== null && typeof value === "object") {
    const safe: Record<string, unknown> = {};
    for (const [key, item] of Object.entries(value)) {
      if (["apikey", "secret", "password", "authorization", "accesstoken"].includes(key.replace(/[_-]/g, "").toLowerCase())) continue;
      safe[key] = withoutSecrets(item, secrets);
    }
    return safe;
  }
  if (typeof value === "string") { for (const key of secrets) value = (value as string).split(key).join("[klíč odstraněn]"); }
  return value;
}
async function sha256(bytes: Uint8Array): Promise<string> {
  const hash = await crypto.subtle.digest("SHA-256", new Uint8Array(bytes).buffer);
  return Array.from(new Uint8Array(hash), byte => byte.toString(16).padStart(2, "0")).join("");
}
function extension(mime: string): string {
  const lower = mime.toLowerCase();
  return lower.includes("ogg") ? ".ogg" : lower.includes("mp4") || lower.includes("m4a") ? ".m4a" : lower.includes("mpeg") ? ".mp3" : lower.includes("wav") ? ".wav" : ".webm";
}
export async function buildMigrationArchive(backup: Backup, recordings: readonly Recording[]): Promise<Blob> {
  const data: Record<string, string> = {};
  const secrets: string[] = [];
  for (const raw of Object.values(backup.data)) {
    try { const parsed = JSON.parse(raw); const key = parsed?.state?.apiKey; if (typeof key === "string" && key) secrets.push(key); } catch { /* Reported by native importer, no secret is read from invalid JSON. */ }
  }
  for (const [key, raw] of Object.entries(backup.data)) {
    let safe: string;
    try { safe = JSON.stringify(withoutSecrets(JSON.parse(raw), secrets)); } catch { continue; }
    try { JSON.parse(safe); data[key] = safe; } catch { /* Invalid group is omitted. */ }
  }
  const entries = [{ path: "settings.json", bytes: new TextEncoder().encode(JSON.stringify({ ...backup, data })) }];
  let total = entries[0].bytes.length;
  const metadata = [], seen = new Set<string>();
  for (let index = 0; index < recordings.length; index++) {
    const recording = recordings[index];
    if (seen.has(recording.id) || !recording.id || !Number.isFinite(recording.createdAt) || !Number.isFinite(recording.durationSec) || recording.durationSec < 0 || recording.blob.size <= 0) throw new Error("Nahrávka má neplatná metadata.");
    seen.add(recording.id); total += recording.blob.size;
    if (total > MAX_MIGRATION_BYTES) throw new Error("Export pro Windows přesahuje 256 MB. Nahrávky přenes po menších částech.");
    const path = `audio/${String(index + 1).padStart(6, "0")}${extension(recording.mimeType)}`;
    entries.push({ path, bytes: new Uint8Array(await recording.blob.arrayBuffer()) });
    const { id, name, createdAt, durationSec, mimeType, markers } = recording;
    metadata.push({ id, name, createdAt, durationSec, mimeType, markers, file: path });
  }
  const files: ArchiveFile[] = [];
  for (const entry of entries) files.push({ path: entry.path, bytes: entry.bytes.length, sha256: await sha256(entry.bytes) });
  const manifest = new TextEncoder().encode(JSON.stringify({ app: "rocker", kind: "windows-migration", version: 1, exportedAt: backup.exportedAt, recordings: metadata, files }));
  return storedZip([{ path: "manifest.json", bytes: manifest }, ...entries]);
}
