// Export trusted PWA content without rewriting the frozen W01 reference.
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const path = resolve(root, 'windows/content/catalog.json');
const runtimePath = resolve(root, 'windows/content/runtime-catalog.json');
const replacementPath = resolve(root, 'windows/content/riff-replacements.json');
function runtimeCatalog(catalog) {
  const data = JSON.parse(readFileSync(replacementPath, 'utf8'));
  assert.equal(data.schemaVersion, 1);
  assert.equal(data.replacements.length, 2);
  const runtime = structuredClone(catalog);
  runtime.legacyRiffAliases = {};
  for (const { legacyId, riff } of data.replacements) {
    const index = runtime.groups.riffs.items.findIndex(item => item.id === legacyId);
    assert(index >= 0 && runtime.groups.riffs.items[index].artist);
    assert(!riff.artist && !runtime.groups.riffs.items.some(item => item.id === riff.id));
    runtime.groups.riffs.items[index] = riff;
    runtime.legacyRiffAliases[legacyId] = riff.id;
  }
  assert(!runtime.groups.riffs.items.some(item => item.artist));
  runtime.replacementsSha256 = createHash('sha256').update(readFileSync(replacementPath, 'utf8').replace(/\r\n/g, '\n')).digest('hex');
  return JSON.stringify(runtime, null, 2) + '\n';
}
// Git can check text out as CRLF on Windows. Hash the canonical LF source,
// preserving every other byte so the check still detects real content drift.
const sourceHash = source => createHash('sha256')
  .update(readFileSync(resolve(root, source), 'utf8').replace(/\r\n/g, '\n')).digest('hex');
if (process.argv.includes('--check-frozen')) {
  // Windows CI needs no PWA development dependencies for the frozen export.
  const catalog = JSON.parse(readFileSync(path, 'utf8'));
  assert.equal(catalog.schemaVersion, 1);
  assert.deepEqual(catalog.groups, JSON.parse(readFileSync(resolve(root, 'docs/windows/baseline/fixtures/content.json'), 'utf8')));
  for (const [source, hash] of Object.entries(catalog.sourceSha256)) {
    assert(source.startsWith('src/') && !source.includes('..'));
    assert.equal(sourceHash(source), hash, source);
  }
  assert.equal(readFileSync(runtimePath, 'utf8').replace(/\r\n/g, '\n'), runtimeCatalog(catalog), 'Runtime content or replacements differ.');
  console.log('PASS: all 15 frozen content groups and source hashes.');
} else {
  const { referenceContent } = await import('./verify-windows-baseline.mjs');
  const groups = referenceContent();
  const sourceSha256 = Object.fromEntries(Object.values(groups).map(({ source }) =>
    [source, sourceHash(source)]));
  const output = JSON.stringify({ schemaVersion: 1, sourceSha256, groups }, null, 2) + '\n';
  if (process.argv.includes('--check')) {
    assert.equal(readFileSync(path, 'utf8').replace(/\r\n/g, '\n'), output, 'Native content differs from PWA sources; regenerate and review the export.');
    assert.equal(readFileSync(runtimePath, 'utf8').replace(/\r\n/g, '\n'), runtimeCatalog(JSON.parse(output)));
    console.log('PASS: all 15 native content groups match their PWA sources and hashes.');
  } else {
    writeFileSync(path, output);
    writeFileSync(runtimePath, runtimeCatalog(JSON.parse(output)));
    console.log('Exported 15 versioned content groups.');
  }
}
