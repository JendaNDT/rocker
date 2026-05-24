// IndexedDB layer for recordings (Bod 11) — architecture layer 7.
// Browser-only. The database is opened lazily on first call, never at import
// time, so the static export / SSR build is unaffected.

import { type DBSchema, type IDBPDatabase, openDB } from "idb";
import {
  type Recording,
  type RecordingMeta,
  sanitizeRecording,
  toMeta,
} from "./recordings";

const DB_NAME = "rocker";
const DB_VERSION = 1;
const STORE = "recordings";

interface RockerDB extends DBSchema {
  recordings: {
    key: string;
    value: Recording;
  };
}

let dbPromise: Promise<IDBPDatabase<RockerDB>> | null = null;

function getDb(): Promise<IDBPDatabase<RockerDB>> {
  if (!dbPromise) {
    dbPromise = openDB<RockerDB>(DB_NAME, DB_VERSION, {
      upgrade(db) {
        if (!db.objectStoreNames.contains(STORE)) {
          db.createObjectStore(STORE, { keyPath: "id" });
        }
      },
    });
  }
  return dbPromise;
}

/** Stores a recording, overwriting any existing one with the same id. */
export async function saveRecording(recording: Recording): Promise<void> {
  const db = await getDb();
  await db.put(STORE, recording);
}

/** Metadata for every stored recording, newest first. Corrupt rows dropped. */
export async function listRecordings(): Promise<RecordingMeta[]> {
  const db = await getDb();
  const all = await db.getAll(STORE);
  return all
    .map(sanitizeRecording)
    .filter((recording): recording is Recording => recording !== null)
    .map(toMeta)
    .sort((a, b) => b.createdAt - a.createdAt);
}

/** A single recording with its audio blob, or undefined if missing/corrupt. */
export async function getRecording(
  id: string,
): Promise<Recording | undefined> {
  const db = await getDb();
  return sanitizeRecording(await db.get(STORE, id)) ?? undefined;
}

/** Removes a recording. */
export async function deleteRecording(id: string): Promise<void> {
  const db = await getDb();
  await db.delete(STORE, id);
}

/**
 * Applies a metadata patch (name and/or markers) to a stored recording,
 * keeping its audio blob. A no-op if the recording no longer exists.
 */
export async function updateRecording(
  id: string,
  patch: Partial<Pick<RecordingMeta, "name" | "markers">>,
): Promise<void> {
  const db = await getDb();
  const existing = sanitizeRecording(await db.get(STORE, id));
  if (!existing) return;
  await db.put(STORE, { ...existing, ...patch });
}
