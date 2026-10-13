// SPDX-License-Identifier: MIT
import { readFile, writeFile, copyFile, mkdir } from 'node:fs/promises';
import path from 'node:path';

const [reports, output, expectedCommit, runId] = process.argv.slice(2);
if (!reports || !output || !/^[a-f0-9]{40}$/.test(expectedCommit ?? '') || !/^\d+$/.test(runId ?? '')) throw new Error('Usage: REPORTS OUTPUT SOURCE_COMMIT RUN_ID');
const json = async name => JSON.parse((await readFile(path.join(reports, name), 'utf8')).replace(/^\uFEFF/, ''));
const candidate = await json('candidate-validation.json');
const coverage = await json('desktop-functional-coverage.json');
if (candidate.result !== 'passed' || candidate.sourceCommit !== expectedCommit || !candidate.windowsRun.endsWith('/' + runId) || coverage.sourceCommit !== expectedCommit || coverage.viewCount !== 32) throw new Error('Only a complete, matching Windows candidate can supply the final gallery.');
await mkdir(path.join(output, 'screens'), { recursive: true });
const views = [];
for (const view of coverage.views) {
  for (const file of [view.file, view.lightFile]) {
    if (!/^desktop-screens\/view-\d{2}(?:-light)?\.png$/.test(file)) throw new Error('Invalid image path.');
    await copyFile(path.join(reports, file), path.join(output, 'screens', path.basename(file)));
  }
  views.push({ id: view.viewId, title: view.Title, destination: view.destination });
}
await writeFile(path.join(output, 'gallery-data.json'), JSON.stringify({ sourceCommit: expectedCommit, runId, views }, null, 2) + '\n');
await copyFile(path.join(reports, 'candidate-validation.json'), path.join(output, 'candidate-validation.json'));
const encode = s => s.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;');
const cards = views.map(v => `<article data-title="${encode(v.title.toLowerCase())}"><h2>${v.id} · ${encode(v.title)}</h2><p>${encode(v.destination)}</p><button class="capture" data-id="${v.id}" data-title="${encode(v.title)}"><img src="screens/view-${v.id}.png" alt="${encode(v.title)} ve Windows" loading="lazy"></button></article>`).join('\n');
await writeFile(path.join(output, 'gallery.html'), `<!doctype html><html lang="cs"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Rocker · skutečný Windows desktop</title><link rel="stylesheet" href="gallery.css"><body><header><p class="eyebrow">ROCKER / WINDOWS</p><h1>Pracovní plocha, dokumenty a nástroje</h1><p>Všech 32 funkcí přestavěného programu. Obsah skutečných WinUI oken a dialogů, zachycený v izolovaném kontrolním profilu; systémové rámy nejsou součástí snímků UI stromu. Ukázkové nahrávky jsou syntetické, poslech a odezva zvukovky zůstávají ruční přejímkou.</p><p><a href="https://github.com/JendaNDT/rocker/actions/runs/${runId}">Úspěšný Windows běh ${runId}</a> · revize <code>${expectedCommit}</code></p><nav><label>Hledat <input id="search" type="search" placeholder="Název pohledu"></label><label>Vzhled <select id="theme"><option value="dark">Tmavý Vintage</option><option value="light">Světlý Vintage</option></select></label><span id="count">32 pohledů</span></nav></header><main>${cards}</main><dialog id="preview"><div class="viewer"><button id="close">Zavřít · Esc</button><h2 id="title"></h2><img id="image" alt=""><div><button id="previous">Předchozí</button><button id="next">Další</button><a id="original">Otevřít celý snímek</a></div></div></dialog><script src="gallery.js"></script></body></html>`);
console.log('Exported 64 current Windows captures with complete candidate provenance.');
