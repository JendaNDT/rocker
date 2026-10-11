import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { pathToFileURL } from 'node:url';
const root=path.resolve(import.meta.dirname,'..');
const temporary=await fs.mkdtemp(path.join(os.tmpdir(),'rocker-migration-helper-'));
try {
  const {buildMigrationArchive}=await import(pathToFileURL(path.join(root,'src/lib/migrationArchive.ts')).href);
  const fixtures=path.join(root,'docs/windows/baseline/fixtures');
  const backup=JSON.parse(await fs.readFile(path.join(fixtures,'settings-v2.json'),'utf8'));
  const recordings=JSON.parse(await fs.readFile(path.join(fixtures,'recordings.json'),'utf8'));
  for(const recording of recordings)recording.blob=new Blob([await fs.readFile(path.join(fixtures,recording.file))],{type:recording.mimeType});
  const archive=Buffer.from(await (await buildMigrationArchive(backup,recordings)).arrayBuffer());
  const destination=path.join(root,'docs/windows/w12/fixtures/reference.rockermigration');
  if(process.argv.includes('--check')) {
    if(!archive.equals(await fs.readFile(destination)))throw new Error('Migration fixture differs from real PWA exporter.');
    console.log(`PASS: actual PWA export matches ${recordings.length} legacy recordings and eleven settings groups.`);
  }else{await fs.writeFile(destination,archive);console.log(`Wrote ${archive.length} bytes from real migration exporter.`);}
}finally{await fs.rm(temporary,{recursive:true,force:true});}
