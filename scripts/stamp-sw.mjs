// Stamps a unique version into the built service worker.
//
// Next.js static export copies public/sw.js verbatim, so without this every
// deploy would ship a byte-identical sw.js and the browser would never detect
// a new version. Run after `next build` (see package.json "build" script).
//
// Failure is non-fatal: a missing stamp only disables the update prompt, it
// must never break a deploy.

import { readFileSync, writeFileSync } from "node:fs";

const SW_PATH = "out/sw.js";
const PLACEHOLDER = 'const VERSION = "DEV";';

const stamp = new Date().toISOString().replace(/[^0-9]/g, "").slice(0, 14);

try {
  const source = readFileSync(SW_PATH, "utf8");
  if (!source.includes(PLACEHOLDER)) {
    console.warn(`stamp-sw: placeholder not found in ${SW_PATH} — skipped.`);
    process.exit(0);
  }
  writeFileSync(
    SW_PATH,
    source.replace(PLACEHOLDER, `const VERSION = "${stamp}";`),
  );
  console.log(`stamp-sw: ${SW_PATH} stamped with version ${stamp}`);
} catch (error) {
  console.warn(`stamp-sw: could not stamp ${SW_PATH} — ${error.message}`);
  process.exit(0);
}
