// Frozen W01 reference checks. This script never rewrites the baseline.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync, existsSync, readdirSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, resolve, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { compileFunction } from "node:vm";
import ts from "typescript";

export const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
export const baseline = resolve(root, "docs/windows/baseline");
const modules = new Map();
export const json = (value) => JSON.parse(JSON.stringify(value));
export const readJson = (name) => JSON.parse(readFileSync(resolve(baseline, name), "utf8"));

// Execute only trusted repository TS, with the same installed dependencies.
// Type-only imports are erased; no application source or build output is edited.
export function loadSource(name) {
  const filename = resolve(root, name);
  assert(filename.startsWith(resolve(root, "src") + "/"));
  if (modules.has(filename)) return modules.get(filename).exports;
  const module = { exports: {} };
  modules.set(filename, module);
  const output = ts.transpileModule(readFileSync(filename, "utf8"), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
    fileName: filename,
  }).outputText;
  const require = (specifier) => {
    if (specifier.startsWith("@/") || specifier.startsWith(".")) {
      const target = specifier.startsWith("@/")
        ? resolve(root, "src", specifier.slice(2))
        : resolve(dirname(filename), specifier);
      return loadSource(relative(root, target.endsWith(".ts") ? target : target + ".ts"));
    }
    return createRequire(filename)(specifier);
  };
  compileFunction(output, ["exports", "require", "module"], { filename })(module.exports, require, module);
  return module.exports;
}

export function referenceContent() {
  const definitions = {
    stages: ["src/lib/lessons.ts", "STAGES"],
    chords: ["src/lib/chords.ts", "CHORDS"],
    scales: ["src/lib/scales.ts", "SCALES"],
    riffs: ["src/lib/metalRiffs.ts", "METAL_RIFFS"],
    achievements: ["src/lib/achievements.ts", "ACHIEVEMENTS"],
    tunings: ["src/audio/tunings.ts", "TUNINGS"],
    ampPresets: ["src/lib/ampPresets.ts", "AMP_PRESETS"],
    metronomePresets: ["src/lib/metronomePresets.ts", "FACTORY_PRESETS"],
    timeSignatures: ["src/lib/metronomeConfig.ts", "TIME_SIGNATURES"],
    clickSounds: ["src/lib/metronomeConfig.ts", "CLICK_SOUNDS"],
    surgicalExercises: ["src/lib/exercises.ts", "EXERCISES"],
    powerChordExercises: ["src/lib/metalExercises.ts", "POWER_CHORD_EXERCISES"],
    gallopExercises: ["src/lib/metalExercises.ts", "GALLOP_EXERCISES"],
    palmMuteDrills: ["src/lib/palmMute.ts", "PALM_MUTE_DRILLS"],
    screens: ["src/lib/screens.ts", "SCREENS"],
  };
  return json(Object.fromEntries(Object.entries(definitions).map(([key, [source, symbol]]) => {
    const value = loadSource(source)[symbol];
    assert(Array.isArray(value), symbol);
    return [key, { source, symbol, items: value }];
  })));
}

export function referenceCases() {
  const cases = [];
  function capture(id, source, name, args) {
    cases.push({ id, source, function: name, args, expected: json(loadSource(source)[name](...args)) });
  }
  for (const midi of [24, 36, 40, 45, 50, 55, 59, 64, 69, 84]) {
    capture(`pitch-${midi}`, "src/lib/pitch.ts", "midiToFrequency", [midi]);
  }
  for (const frequency of [0, -1, 82.4068892282175, 110, 440, 445]) {
    capture(`frequency-${frequency}`, "src/lib/pitch.ts", "frequencyToNote", [frequency]);
  }
  for (const notation of ["english", "german"]) {
    capture(`notation-${notation}`, "src/lib/pitch.ts", "midiToLabelIn", [59, notation]);
  }
  for (const bpm of [12, 40, 119.6, 240, 999]) capture(`bpm-${bpm}`, "src/lib/tempo.ts", "clampBpm", [bpm]);
  capture("tap-120", "src/lib/tempo.ts", "bpmFromTaps", [[0, 500, 1000, 1500]]);
  capture("tap-empty", "src/lib/tempo.ts", "bpmFromTaps", [[]]);
  for (const tick of [0, 1, 2, 3]) capture(`swing-${tick}`, "src/lib/metronomeConfig.ts", "swingedTickDuration", [tick, 0.25, 2, 2 / 3]);
  for (const route of ["/live", "/recorder", "/studio", "/practice/", "/notation"]) {
    capture(`route-${route}`, "src/lib/practiceTime.ts", "isPracticeRoute", [route]);
  }
  for (const chord of loadSource("src/lib/chords.ts").CHORDS) capture(`chord-${chord.id}`, "src/lib/chords.ts", "chordMidiNotes", [chord]);
  const days = { "2026-10-07": 15, "2026-10-08": 20, "2026-10-09": 10 };
  capture("streak-yesterday", "src/lib/progress.ts", "currentStreak", [days, "2026-10-10"]);
  capture("streak-broken", "src/lib/progress.ts", "currentStreak", [days, "2026-10-11"]);
  capture("minutes", "src/lib/progress.ts", "totalMinutes", [days]);
  capture("daily-plan-new", "src/lib/dailyPlan.ts", "generateDailyPlan", [{ completedLessonIds: [], dayKey: "2026-10-10" }]);
  capture("daily-plan-progress", "src/lib/dailyPlan.ts", "generateDailyPlan", [{ completedLessonIds: ["drzeni", "prazdne-struny"], dayKey: "2026-10-10" }]);
  capture("recording-name", "src/lib/recordings.ts", "recordingFileName", ["Můj take / A:B", "audio/webm;codecs=opus"]);
  capture("markers-sort", "src/lib/recordings.ts", "sortMarkers", [[{id:"b",timeSec:1,label:"Konec"},{id:"a",timeSec:0.25,label:"Začátek"}]]);
  capture("chat-validation", "src/lib/ai/types.ts", "sanitizeChatHistory", [[{role:"user",text:"Zkušební dotaz"},{role:"model",text:"Zkušební odpověď"},{role:"invalid",text:"bad"}]]);
  capture("gallop-onsets", "src/lib/gallopTrainer.ts", "cellOnsetsSeconds", [[0.5,0.25,0.25],120]);
  return cases;
}

export function surgicalTraces() {
  const { startSurgical, feedNote } = loadSource("src/lib/surgicalEngine.ts");
  const exercise = loadSource("src/lib/exercises.ts").EXERCISES[0];
  const sequences = [
    {id:"clean",notes:exercise.notes},
    {id:"pitch-class-octave",notes:exercise.notes.map(note=>note+12)},
    {id:"mistake-then-three-drills",notes:[exercise.notes[0]+1,...exercise.notes,...Array(3).fill(exercise.notes.slice(0,3)).flat()]},
  ];
  return sequences.map(sequence => {
    let state = startSurgical(exercise);
    const states = [json(state)];
    for (const note of sequence.notes) { state=feedNote(state,note); states.push(json(state)); }
    return { ...sequence, exerciseId:exercise.id, states };
  });
}

export const stores = [
  ["rocker-settings","settingsStore","useSettingsStore"],
  ["rocker-progress","progressStore","useProgressStore"],
  ["rocker-instrument","instrumentStore","useInstrumentStore"],
  ["rocker-lesson-progress","lessonProgressStore","useLessonProgressStore"],
  ["rocker-yt-loops","ytLoopStore","useYtLoopStore"],
  ["rocker-ai","aiStore","useAiStore"],
  ["rocker-chat","chatStore","useChatStore"],
  ["rocker-amp","ampStore","useAmpStore"],
  ["rocker-amp-presets","ampPresetsStore","useAmpPresetsStore"],
  ["rocker-metronome","metronomeSettingsStore","useMetronomeSettingsStore"],
  ["rocker-custom-tunings","customTuningsStore","useCustomTuningsStore"],
];

export async function checkBaseline() {
  const manifest = readJson("manifest.json");
  for (const [name, expected] of Object.entries(manifest.sourceSha256)) {
    assert.equal(createHash("sha256").update(readFileSync(resolve(root,name))).digest("hex"),expected,`Source drift: ${name}`);
  }
  const content=referenceContent();
  assert.deepEqual(content,readJson("fixtures/content.json"));
  assert.deepEqual(referenceCases(),readJson("fixtures/domain-cases.json"));
  assert.deepEqual(surgicalTraces(),readJson("fixtures/surgical-traces.json"));
  const chordIds=new Set(content.chords.items.map(item=>item.id));
  for (const group of Object.values(content)) {
    const ids=group.items.map(item=>item.id ?? item.href);
    assert.equal(new Set(ids).size,ids.length,`Duplicate ID in ${group.source}`);
  }
  for (const stage of content.stages.items) for (const lesson of stage.lessons) {
    for (const step of lesson.steps) if (step.kind==="chord") assert(chordIds.has(step.chordId));
  }
  const inventory=readJson("inventory.json");
  assert.deepEqual(inventory.views.map(view=>view.id),Array.from({length:32},(_,i)=>String(i+1).padStart(2,"0")));
  const roadmap=readFileSync(resolve(root,"WINDOWS_ROADMAP.md"),"utf8");
  for (const view of inventory.views) {
    assert(view.current.length && view.acceptance.length && view.sources.length);
    for (const name of [...view.sources,...view.tests]) assert(existsSync(resolve(root,name)),name);
    for (const step of view.steps) assert(roadmap.includes(`#### ${step} —`),step);
  }
  const backup=readJson("fixtures/settings-v2.json");
  const {isValidBackup,BACKUP_KEYS}=loadSource("src/lib/backup.ts");
  assert(isValidBackup(backup));
  assert.deepEqual(Object.keys(backup.data).sort(),[...BACKUP_KEYS].sort());
  const memory=new Map(Object.entries(backup.data));
  const previousStorage=Object.getOwnPropertyDescriptor(globalThis,"localStorage");
  const previousWindow=Object.getOwnPropertyDescriptor(globalThis,"window");
  Object.defineProperty(globalThis,"localStorage",{configurable:true,value:{
    getItem:key=>memory.get(key)??null,setItem:(key,value)=>memory.set(key,value),removeItem:key=>memory.delete(key),
  }});
  Object.defineProperty(globalThis,"window",{configurable:true,value:{localStorage:globalThis.localStorage}});
  try {
    // Some domain helpers import stores transitively. Reload them after the
    // in-memory browser storage exists so persist can hydrate the fixture.
    modules.clear();
    for (const [key,file,symbol] of stores) {
      const store=loadSource(`src/stores/${file}.ts`)[symbol];
      await store.persist.rehydrate();
      assert.equal(store.persist.getOptions().name,key);
      const state=store.getState();
      const persisted=json(store.persist.getOptions().partialize?.(state) ?? state);
      assert.deepEqual(persisted,JSON.parse(backup.data[key]).state,`Fixture rejected by real store: ${key}`);
    }
    assert.equal(JSON.parse(backup.data["rocker-ai"]).state.apiKey,"");
  } finally {
    if(previousStorage) Object.defineProperty(globalThis,"localStorage",previousStorage);
    else delete globalThis.localStorage;
    if(previousWindow) Object.defineProperty(globalThis,"window",previousWindow);
    else delete globalThis.window;
  }
  for (const name of ["settings-v1.json","settings-partial.json"]) assert(isValidBackup(readJson(`fixtures/${name}`)));
  assert(!isValidBackup(readJson("fixtures/settings-invalid.json")));
  const corrupt=readJson("fixtures/settings-corrupt-value.json");
  assert(isValidBackup(corrupt));
  assert.throws(()=>JSON.parse(corrupt.data["rocker-progress"]));
  const media=readJson("fixtures/media.json");
  for (const item of media.files) {
    const data=readFileSync(resolve(baseline,"fixtures",item.path));
    assert.equal(createHash("sha256").update(data).digest("hex"),item.sha256,item.path);
    if (item.path.endsWith(".wav")) {
      assert.equal(data.toString("ascii",0,4),"RIFF");
      assert.equal(data.readUInt16LE(20),1);
      assert.equal(data.readUInt16LE(22),item.channels);
      assert.equal(data.readUInt32LE(24),item.sampleRate);
      assert.equal((data.length-44)/(item.channels*2),item.frames);
      const sample=(frame,channel=0)=>data.readInt16LE(44+2*(frame*item.channels+channel));
      if(item.path.includes("silence")) for(let frame=0;frame<item.frames;frame++) assert.equal(sample(frame),0);
      if(item.expected.silentChannel!==undefined) {
        let peak=0;
        for(let frame=0;frame<item.frames;frame++) {
          assert.equal(sample(frame,item.expected.silentChannel),0);
          peak=Math.max(peak,Math.abs(sample(frame,item.expected.activeChannel)));
        }
        assert(peak>6000 && peak<7000);
      }
      if(item.expected.impulseFrames) {
        const actual=[];
        for(let frame=0;frame<item.frames;frame++) if(sample(frame)!==0) actual.push(frame);
        assert.deepEqual(actual,item.expected.impulseFrames);
      }
    }
  }
  const alphaTab=createRequire(import.meta.url)("@coderline/alphatab");
  const score=alphaTab.importer.ScoreLoader.loadScoreFromBytes(
    new Uint8Array(readFileSync(resolve(baseline,"fixtures/notation-scale.musicxml"))),new alphaTab.Settings(),
  );
  assert.equal(score.tracks.length,1);
  assert.equal(score.masterBars.length,1);
  const notes=score.tracks.flatMap(track=>track.staves.flatMap(staff=>staff.bars.flatMap(bar=>bar.voices.flatMap(voice=>voice.beats.flatMap(beat=>beat.notes)))));
  assert.equal(notes.length,4);
  const {sanitizeRecording}=loadSource("src/lib/recordings.ts");
  for(const item of readJson("fixtures/recordings.json")) {
    const {file,...meta}=item;
    const blob=new Blob([readFileSync(resolve(baseline,"fixtures",file))],{type:meta.mimeType});
    const safe=sanitizeRecording({...meta,blob});
    assert(safe,meta.id);
    assert.deepEqual(json({...safe,blob:undefined}),meta);
  }
  assert.equal(readdirSync(resolve(baseline,"fixtures/audio")).length,media.files.filter(file=>file.path.startsWith("audio/")).length);
  console.log(JSON.stringify({result:"PASS",views:32,stores:stores.length,domainCases:referenceCases().length,surgicalTraces:surgicalTraces().length,mediaFiles:media.files.length,sourceFiles:Object.keys(manifest.sourceSha256).length}));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  checkBaseline().catch(error=>{console.error(error);process.exitCode=1;});
}
