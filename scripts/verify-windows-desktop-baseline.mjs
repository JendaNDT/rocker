// Validate the frozen D01 inventory; this does not execute or validate WinUI.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const directory = resolve(root, "docs/windows/desktop");
const read = name => JSON.parse(readFileSync(resolve(directory, name), "utf8"));
const inventory = read("inventory.json");
const syntax = read("source-inventory.json");
const manifest = read("source-manifest.json");
assert.equal(inventory.schemaVersion, 1);
assert.equal(manifest.sourceCommit, inventory.sourceCommit);
const paths = new Set();
const frozen = path => execFileSync("git", ["show", inventory.sourceCommit + ":" + path], { cwd: root, maxBuffer: 16 * 1024 * 1024 });
for (const entry of manifest.files) {
  assert(!paths.has(entry.path), "Duplicate manifest path: " + entry.path);
  paths.add(entry.path);
  assert.match(entry.sha256, /^[a-f0-9]{64}$/);
  assert.equal(createHash("sha256").update(frozen(entry.path)).digest("hex"), entry.sha256, "Frozen source hash: " + entry.path);
}
for (const entry of syntax.sources) {
  assert(paths.has(entry.path), "Syntax source missing from manifest: " + entry.path);
  assert.equal(manifest.files.find(p => p.path === entry.path).sha256, entry.sha256);
}
const views = [...frozen("windows/Rocker.Domain/Navigation.cs").toString().matchAll(/new\("(\d{2})","([^"]+)","([^"]+)"\)/g)]
  .map(m => ({ id: m[1], title: m[2] }));
assert.equal(views.length, 32);
assert.deepEqual(inventory.views.map(v => ({ id: v.id, title: v.title })), views);
const sourceMethods = new Set(syntax.mainWindowMethods.map(m => m.path + "#" + m.name));
for (const view of inventory.views) {
  assert(sourceMethods.has(view.source.path + "#" + view.source.method), "Unknown entry method: " + view.id);
  assert(view.targetSession && view.dataKinds.length && view.scenarios.length);
}
const kindsDeclaration = frozen("windows/Rocker.Domain/UserData.cs").toString().match(/Kinds \{ get; \}[^\n]+/);
assert(kindsDeclaration, "Missing frozen data kinds declaration");
const kinds = [...kindsDeclaration[0].matchAll(/"([^"]+)"/g)].map(m => m[1]);
assert.deepEqual(inventory.dataKinds, kinds);
for (const view of inventory.views) for (const kind of view.dataKinds)
  assert(kind === "runtime-only" || kinds.includes(kind), "Unknown data kind: " + kind);
assert.equal(syntax.mainWindowFields.length, inventory.sourceCounts.mainWindowFields);
assert.equal(syntax.mainWindowMethods.length, inventory.sourceCounts.mainWindowMethods);
assert.equal(syntax.namedXamlControls.length, inventory.sourceCounts.namedXamlControls);
assert.equal(syntax.sources.length, inventory.sourceCounts.applicationCSharpFiles);
assert.equal(inventory.audioHostsPerProcess, 1);
assert.equal(inventory.activeNativeMediaSlots, 1);
assert.equal(inventory.runtimeChanged, false);
assert.equal(inventory.manualAcceptance.newDesktopUiVerified, false);
assert.equal(inventory.manualAcceptance.actualLatencyVerified, false);
assert(inventory.navigationSideEffects.length >= 7);
assert(inventory.services.length >= 20);
console.log("PASS: frozen D01 source hashes, 32 view entry points, all data kinds, actual state fields and desktop boundaries. Windows runtime remains a separate check.");
