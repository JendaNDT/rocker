"use client";

import { useState } from "react";
import {
  BACKUP_KEYS,
  buildBackup,
  isValidBackup,
  knownBackupEntries,
} from "@/lib/backup";
import { downloadBlob } from "@/lib/download";

const SECONDARY =
  "rounded-lg border border-line bg-bg px-4 py-2 text-sm transition-colors hover:border-line-strong";

/**
 * Data backup: downloads the app's persisted settings and progress as a JSON
 * file and restores it. Lives on the Profile screen. Audio recordings are
 * not part of the backup.
 */
export function DataBackup() {
  const [error, setError] = useState<string | null>(null);

  function downloadBackup() {
    setError(null);
    const entries: Record<string, string> = {};
    for (const key of BACKUP_KEYS) {
      const value = localStorage.getItem(key);
      if (value !== null) entries[key] = value;
    }
    const json = JSON.stringify(buildBackup(entries), null, 2);
    const blob = new Blob([json], { type: "application/json" });
    downloadBlob(
      blob,
      `rocker-zaloha-${new Date().toISOString().slice(0, 10)}.json`,
    );
  }

  async function restoreFromFile(file: File) {
    setError(null);
    let parsed: unknown;
    try {
      parsed = JSON.parse(await file.text());
    } catch {
      setError("Soubor se nepodařilo přečíst — není to platná záloha.");
      return;
    }
    if (!isValidBackup(parsed)) {
      setError("Tohle není platná záloha Rockeru.");
      return;
    }
    // Restore overlays the backup onto current state: keys present in the
    // file get written, keys missing from the file keep whatever the user
    // has now. Older v1 backups (made before amp/amp-presets/metronome/
    // custom-tunings were included) therefore do not wipe those stores.
    const entries = knownBackupEntries(parsed.data);
    // isValidBackup is happy with `data: {}` (every value-of-empty test is
    // vacuously true). Tell the user explicitly so a silent reload doesn't
    // feel like the restore broke.
    if (Object.keys(entries).length === 0) {
      setError(
        "Záloha je prázdná — neobsahuje žádná známá nastavení. Nic se neobnovilo.",
      );
      return;
    }
    const confirmed = window.confirm(
      "Obnova přepíše nastavení a pokrok v aplikaci tím, co je v záloze. " +
        "Nahrávky zůstanou nedotčené. Pokračovat?",
    );
    if (!confirmed) return;
    for (const [key, value] of Object.entries(entries)) {
      localStorage.setItem(key, value);
    }
    window.location.reload();
  }

  function handleFile(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = ""; // allow picking the same file again
    if (file) void restoreFromFile(file);
  }

  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <p className="text-sm text-text">
        Stáhni si zálohu svého nastavení a pokroku, nebo ji obnov ze souboru.
        Hodí se před vyčištěním prohlížeče nebo při přechodu na jiné zařízení.
        Záloha neobsahuje zvukové nahrávky.
      </p>
      <div className="mt-4 flex flex-wrap gap-3">
        <button type="button" onClick={downloadBackup} className={SECONDARY}>
          Zálohovat
        </button>
        <label className={`cursor-pointer ${SECONDARY}`}>
          Obnovit ze zálohy
          <input
            type="file"
            accept="application/json,.json"
            onChange={handleFile}
            className="hidden"
          />
        </label>
      </div>
      {error && <p className="mt-3 text-sm text-danger">{error}</p>}
      <p className="mt-3 text-xs text-muted">
        Soubor zálohy obsahuje i tvůj AI klíč — ber ho jako osobní a nesdílej
        ho.
      </p>
    </div>
  );
}
