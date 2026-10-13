// SPDX-License-Identifier: MIT
// Validate actual desktop evidence. Construction coverage does not imply hardware QA.
import { readFile, stat } from 'node:fs/promises';
import path from 'node:path';

const [folder, expectedCommit] = process.argv.slice(2);
if (!folder || !/^[a-f0-9]{40}$/.test(expectedCommit ?? '')) throw new Error('Usage: node scripts/verify-windows-desktop-current.mjs REPORTS SOURCE_COMMIT');
const json = async name => JSON.parse((await readFile(path.join(folder, name), 'utf8')).replace(/^\uFEFF/, ''));
const reports = await Promise.all(['desktop-runtime.json', 'desktop-interactions.json', 'desktop-functional-coverage.json'].map(json));
const workbench = await json('desktop-workbench.json');
if (workbench.result !== 'passed' || workbench.sourceCommit !== expectedCommit || workbench.interfaceRevision !== 2 ||
    workbench.samples?.length !== 3 || !workbench.samples.every(sample => sample.documentHeightDip >= 300 && sample.transportHeightDip >= 32 && sample.transportHeightDip <= 62)) {
  throw new Error('The compact workbench requires fresh geometry checks at three window widths.');
}
for (const report of reports) {
  if (report.result !== 'passed' || report.sourceCommit !== expectedCommit || report.actualWindows !== true) throw new Error('Desktop report is stale, failed or not from Windows.');
  if (report.scenarios && (report.checks !== report.scenarios.length || new Set(report.scenarios).size !== report.checks)) throw new Error('Scenario counts must match actual unique assertions.');
}
const coverage = reports[2];
const expected = Array.from({ length: 32 }, (_, i) => String(i + 1).padStart(2, '0'));
if (coverage.viewCount !== 32 || coverage.views.length !== 32 || coverage.views.map(v => v.viewId).sort().join(',') !== expected.join(',')) throw new Error('All 32 functions require fresh desktop coverage.');
for (const view of coverage.views) {
  if (!view.rendered || view.widthDip <= 0 || view.heightDip <= 0 || view.themes?.join(',') !== 'dark,light') throw new Error(`Incomplete layout/theme coverage: ${view.viewId}`);
  for (const file of [view.file, view.lightFile]) {
    if (!/^desktop-screens\/view-\d{2}(?:-light)?\.png$/.test(file)) throw new Error('Invalid capture path.');
    const image = await readFile(path.join(folder, file));
    if (image.length < 32 || image.subarray(0, 8).toString('hex') !== '89504e470d0a1a0a' || image.readUInt32BE(16) <= 0 || image.readUInt32BE(20) <= 0) throw new Error(`Missing or invalid PNG: ${file}`);
  }
}
await stat(path.join(folder, 'desktop-gallery.html'));
console.log(`PASS: ${reports[0].checks + reports[1].checks} actual desktop assertions; all 32 functions in two themes. Hardware, physical drag and full installer success are separate evidence.`);
