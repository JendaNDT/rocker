// Generate W06 comparisons from trusted PWA code, without changing W01.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const path = resolve(root, 'docs/windows/w06/domain-reference.json');
const hash = name => createHash('sha256').update(readFileSync(resolve(root, name), 'utf8').replace(/\r\n/g, '\n')).digest('hex');
if (process.argv.includes('--check-source')) {
  const value = JSON.parse(readFileSync(path, 'utf8'));
  assert.equal(value.schemaVersion, 1);
  for (const [source, expected] of Object.entries(value.sourceSha256)) {
    assert(source.startsWith('src/') && !source.includes('..'));
    assert.equal(hash(source), expected, source);
  }
  console.log('PASS: W06 domain reference source hashes.');
} else {
  const { loadSource, json } = await import('./verify-windows-baseline.mjs');
  const sources = ['powerChordTrainer', 'gallopTrainer', 'palmMute', 'powerChords', 'fretboardGame', 'earTraining', 'scales', 'progress', 'lessonProgress', 'lessonEngine', 'breakReminder', 'achievements', 'tempo', 'lessons', 'metalExercises', 'metronomeConfig', 'math', 'metronomePresets', 'ampPresets'].map(name => `src/lib/${name}.ts`);
  const cases = [];
  const capture = (source, fn, args) => cases.push({ source, function: fn, args: json(args), expected: json(loadSource(source)[fn](...args)) });
  const exercises = loadSource('src/lib/metalExercises.ts');
  const rhythm = [];
  for (const exercise of exercises.POWER_CHORD_EXERCISES) for (const speedUp of [false, true]) {
    const engine = loadSource('src/lib/powerChordTrainer.ts');
    let state = engine.startPowerChordTrainer(exercise, speedUp); const snapshots = [];
    for (let i = 0; i <= engine.roundBeats(exercise) * 3; ++i) {
      snapshots.push(json({ state, active: engine.activeChordId(state), next: engine.nextChordId(state), progress: engine.roundProgress(state) }));
      state = engine.advanceBeat(state);
    }
    rhythm.push({ kind: 'power', exercise, speedUp, snapshots });
  }
  for (const exercise of exercises.GALLOP_EXERCISES) for (const speedUp of [false, true]) {
    const engine = loadSource('src/lib/gallopTrainer.ts');
    let state = engine.startGallopTrainer(exercise, speedUp); const snapshots = [];
    for (let i = 0; i <= 40; ++i) { snapshots.push(json({ state, progress: engine.roundProgress(state) })); state = engine.advanceCell(state); }
    rhythm.push({ kind: 'gallop', exercise, speedUp, snapshots });
    for (const bpm of [40,90,120,240]) {
      capture('src/lib/gallopTrainer.ts', 'cellOnsetsSeconds', [exercise.cell, bpm]);
      capture('src/lib/gallopTrainer.ts', 'cellDurationSeconds', [exercise.cell, bpm]);
    }
  }
  const palm = loadSource('src/lib/palmMute.ts'); const palmTraces = [];
  for (const drill of palm.PALM_MUTE_DRILLS) for (const clean of [false,true]) {
    const hits = [...drill.pattern.map((hit,i) => !clean && i % 3 === 0 ? hit === 'muted' ? 'open' : 'muted' : hit), 'open'];
    let state = palm.startPalmMuteDrill(drill); const snapshots = [];
    for (let i = 0; i <= hits.length; ++i) {
      snapshots.push(json({ state, complete: palm.isDrillComplete(state), expected: palm.expectedTechnique(state), score: palm.drillScore(state) }));
      if (i < hits.length) state = palm.logDrillHit(state, hits[i]);
    }
    palmTraces.push({ drill, hits, snapshots });
  }
  for (const samples of [[[2200,2400,2600],[400,600,800]], [[1,2],[0,0]], [[100,100,100],[200,200,200]], [[0,0,0],[0,0,0]]]) capture('src/lib/palmMute.ts', 'calibrate', samples);
  const calibration = palm.calibrate([2200,2400,2600],[400,600,800]);
  for (const centroid of [0,1499,1500,3000]) capture('src/lib/palmMute.ts', 'classifyHit', [centroid, calibration]);
  for (const tuning of [[40,45,50,55,59,64], [38,45,50,55,59,64], [36,43,48,53,57,62]])
    for (const chord of loadSource('src/lib/powerChords.ts').POWER_CHORDS) {
      capture('src/lib/powerChords.ts', 'getPowerChord', [chord.id,tuning]);
      capture('src/lib/powerChords.ts', 'powerChordMidiNotes', [loadSource('src/lib/powerChords.ts').getPowerChord(chord.id,tuning)]);
    }
  for (const open of [38,40,45,64]) for (const fret of [0,1,12,24]) capture('src/lib/fretboardGame.ts', 'noteAtFret', [open,fret]);
  for (const scale of loadSource('src/lib/scales.ts').SCALES) for (const pitch of [0,1,3,7,11]) capture('src/lib/scales.ts', 'scaleOffset', [scale,4,pitch]);
  // Random suppliers cannot be serialized; store the injected random value.
  for (const random of [0,0.25,0.99,1]) for (const previous of [null,0,3,11]) {
    cases.push({ source:'src/lib/fretboardGame.ts', function:'pickTarget', args:[previous,random], expected:loadSource('src/lib/fretboardGame.ts').pickTarget(previous,()=>random) });
    cases.push({ source:'src/lib/earTraining.ts', function:'pickIndex', args:[8,previous,random], expected:loadSource('src/lib/earTraining.ts').pickIndex(8,previous,()=>random) });
  }
  for (const random of [0,0.25,0.99,1]) cases.push({ source:'src/lib/earTraining.ts', function:'pickRoot', args:[random], expected:loadSource('src/lib/earTraining.ts').pickRoot(()=>random) });
  for (const value of [null,{},[],{dailyMinutes:{'2026-10-09':3,bad:-2,string:'1',zero:0},bpmRecords:{x:120,y:null},unlockedAchievements:['first-practice','first-practice',12,''] }]) capture('src/lib/progress.ts','sanitizeProgress',[value]);
  for (const value of [null,{},[],['drzeni','drzeni',12,'','other']]) capture('src/lib/lessonProgress.ts','sanitizeCompletedLessonIds',[value]);
  const stages = loadSource('src/lib/lessons.ts').STAGES;
  const allIds = stages.flatMap(stage=>stage.lessons.map(lesson=>lesson.id));
  for (const ids of [[],[allIds[0]],allIds.slice(0,4),allIds]) {
    capture('src/lib/lessonEngine.ts','overallCompletion',[stages,ids]);
    capture('src/lib/lessonEngine.ts','nextIncompleteLesson',[stages,ids]);
    for(const stage of stages) {capture('src/lib/lessonEngine.ts','stageCompletion',[stage,ids]); capture('src/lib/lessonEngine.ts','isStageComplete',[stage,ids]);}
  }
  for (const days of [{},{'2026-10-09':2,'2026-10-10':0},{'2025-12-31':3,'2026-01-01':4,'2026-01-02':5,'2026-03-01':2}]) {
    capture('src/lib/progress.ts','longestStreak',[days]); capture('src/lib/progress.ts','totalPracticeDays',[days]);
  }
  const achievements = loadSource('src/lib/achievements.ts');
  for (const ids of [[],allIds.slice(0,4),allIds]) {
    const progress = {dailyMinutes:{'2026-10-07':200,'2026-10-08':200,'2026-10-09':200},bpmRecords:{a:120},unlockedAchievements:[]};
    const context = achievements.buildAchievementContext(progress,ids);
    cases.push({source:'src/lib/achievements.ts',function:'evaluateAchievements',args:[progress,ids],expected:achievements.evaluateAchievements(context)});
  }
  const breaks = loadSource('src/lib/breakReminder.ts'); let state = breaks.initialBreakState;
  const breakTicks = [[20*60*1000,true],[25*60*1000,true],[1000,true],[4*60*1000,false],[60*1000,false],[1000,true],[0,true],[-1,false]];
  const breakStates = [];
  for (const [delta, practicing] of breakTicks) { const result = breaks.advanceBreak(state,delta,practicing); breakStates.push(json(result)); state=result.state; }
  for (const config of [null,{},[],{bpm:999,beatsPerBar:3.6,subdivision:7,accentPattern:['muted','bad'],sound:'bad',volume:2,ramp:{enabled:true,everyBars:0,stepBpm:50,targetBpm:10},gap:{enabled:true,audibleBars:2,silentBars:1},polyrhythm:{enabled:true,ticksPerBeat:8,volume:-1},swing:0.9,countInBars:4,soundPattern:['wood',null,'bad'],subdivisionSound:'beep'}, ...loadSource('src/lib/metronomePresets.ts').FACTORY_PRESETS]) capture('src/lib/metronomeConfig.ts','sanitizeMetronomeConfig',[config]);
  for (const bar of [-1,0,1,2,3,4,5,6]) for (const enabled of [false,true]) capture('src/lib/metronomeConfig.ts','isSilentBar',[bar,{enabled,audibleBars:2,silentBars:1}]);
  for (const bpm of [40,100,159,160,240]) capture('src/lib/metronomeConfig.ts','nextRampBpm',[bpm,{enabled:true,everyBars:4,stepBpm:5,targetBpm:160}]);
  for (const accent of ['normal','accent','muted']) capture('src/lib/metronomeConfig.ts','cycleAccent',[accent]);
  for (const value of [null,{},[],{compression:-1,drive:3,bass:-2,mid:0.3,treble:4,cabinet:'bad',distortion:'fuzz',level:'1'},...loadSource('src/lib/ampPresets.ts').AMP_PRESETS.map(p=>p.settings)]) capture('src/lib/ampPresets.ts','sanitizeAmpSettings',[value]);
  for (const value of [null,[],[{}, {id:'u1',name:' Test ',settings:{drive:4}},{id:'',name:'Invalid'},{id:'u2',name:' ',settings:null}]]) capture('src/lib/ampPresets.ts','sanitizeUserPresets',[value]);
  const output = JSON.stringify({schemaVersion:1,sourceSha256:Object.fromEntries(sources.map(source=>[source,hash(source)])),cases,rhythm,palm:palmTraces,breaks:{ticks:breakTicks,states:breakStates}},null,2)+'\n';
  if(process.argv.includes('--check')) assert.equal(readFileSync(path,'utf8').replace(/\r\n/g,'\n'),output,'W06 reference changed; review the original logic before regenerating.');
  else writeFileSync(path,output);
  console.log(`PASS: ${cases.length} W06 cases, ${rhythm.length} rhythm traces, ${palmTraces.length} palm-mute traces.`);
}
