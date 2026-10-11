// Export trusted PWA content without rewriting the frozen W01 reference.
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const path = resolve(root, 'windows/content/catalog.json');
if (process.argv.includes('--check-frozen')) {
  // Windows CI needs no PWA development dependencies for the frozen export.
  const catalog = JSON.parse(readFileSync(path, 'utf8'));
  assert.equal(catalog.schemaVersion, 1);
  assert.deepEqual(catalog.groups, JSON.parse(readFileSync(resolve(root, 'docs/windows/baseline/fixtures/content.json'), 'utf8')));
  for (const [source, hash] of Object.entries(catalog.sourceSha256)) {
    assert(source.startsWith('src/') && !source.includes('..'));
    assert.equal(createHash('sha256').update(readFileSync(resolve(root, source))).digest('hex'), hash, source);
  }
  console.log('PASS: all 15 frozen content groups and source hashes.');
} else {
  const { referenceContent } = await import('./verify-windows-baseline.mjs');
  const groups = referenceContent();
  const sourceSha256 = Object.fromEntries(Object.values(groups).map(({ source }) =>
    [source, createHash('sha256').update(readFileSync(resolve(root, source))).digest('hex')]));
  const output = JSON.stringify({ schemaVersion: 1, sourceSha256, groups }, null, 2) + '\n';
  if (process.argv.includes('--check')) {
    assert.equal(readFileSync(path, 'utf8'), output, 'Native content differs from PWA sources; regenerate and review the export.');
    console.log('PASS: all 15 native content groups match their PWA sources and hashes.');
  } else {
    writeFileSync(path, output);
    console.log('Exported 15 versioned content groups.');
  }
}
