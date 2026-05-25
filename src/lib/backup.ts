// Data backup — exports the app's persisted localStorage data to a file and
// restores it. These are the pure helpers; the browser-bound parts
// (localStorage access, file download/upload) live in the DataBackup
// component. Audio recordings (IndexedDB) are intentionally not included —
// the backup stays a small, fast text file.

/**
 * localStorage keys holding persisted app data, all written by Zustand
 * `persist`. Settings, progress, instrument profile, lesson progress,
 * YouTube loops, AI settings and chat history.
 */
export const BACKUP_KEYS: readonly string[] = [
  "rocker-settings",
  "rocker-progress",
  "rocker-instrument",
  "rocker-lesson-progress",
  "rocker-yt-loops",
  "rocker-ai",
  "rocker-chat",
];

/** Backup file format version. */
export const BACKUP_VERSION = 1;

/** A Rocker data backup. `data` maps each backed-up key to its raw value. */
export interface Backup {
  app: "rocker";
  kind: "settings-backup";
  version: number;
  exportedAt: string;
  data: Record<string, string>;
}

/** Builds a Backup object from a map of localStorage key → value. */
export function buildBackup(entries: Record<string, string>): Backup {
  return {
    app: "rocker",
    kind: "settings-backup",
    version: BACKUP_VERSION,
    exportedAt: new Date().toISOString(),
    data: entries,
  };
}

function isStringRecord(value: unknown): value is Record<string, string> {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return false;
  }
  return Object.values(value).every((item) => typeof item === "string");
}

/** True if the value is a structurally valid Rocker backup. */
export function isValidBackup(value: unknown): value is Backup {
  if (typeof value !== "object" || value === null) return false;
  const backup = value as Record<string, unknown>;
  return (
    backup.app === "rocker" &&
    backup.kind === "settings-backup" &&
    typeof backup.version === "number" &&
    typeof backup.exportedAt === "string" &&
    isStringRecord(backup.data)
  );
}

/**
 * Restricts a backup's data to keys the app actually owns, so restoring a
 * (possibly hand-edited) file can't inject arbitrary localStorage entries.
 */
export function knownBackupEntries(
  data: Record<string, string>,
): Record<string, string> {
  const result: Record<string, string> = {};
  for (const key of BACKUP_KEYS) {
    if (typeof data[key] === "string") result[key] = data[key];
  }
  return result;
}
