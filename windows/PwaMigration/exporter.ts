// SPDX-License-Identifier: MIT
// Runs only as an explicit bookmark in the user's existing Rocker browser origin.
import { BACKUP_KEYS, buildBackup } from '../../src/lib/backup';
import { buildMigrationArchive } from '../../src/lib/migrationArchive';
import { sanitizeRecording, type Recording } from '../../src/lib/recordings';
import { downloadBlob } from '../../src/lib/download';
async function exportExistingProfile() {
  const entries: Record<string,string>={};
  for(const key of BACKUP_KEYS){const value=localStorage.getItem(key);if(value!==null)entries[key]=value;}
  if(Object.keys(entries).length===0)throw new Error('Tato stránka nemá uložená data Rockeru. Otevři původní Rocker ve stejném prohlížeči a profilu.');
  const request=indexedDB.open('rocker',1);
  const db=await new Promise<IDBDatabase>((resolve,reject)=>{request.onupgradeneeded=()=>{if(!request.result.objectStoreNames.contains('recordings'))request.result.createObjectStore('recordings',{keyPath:'id'});};request.onsuccess=()=>resolve(request.result);request.onerror=()=>reject(request.error);request.onblocked=()=>reject(new Error('Úložiště je obsazené. Zavři ostatní karty Rockeru a zkus to znovu.'));});
  let values: unknown[];
  try {if(!db.objectStoreNames.contains('recordings'))throw new Error('Úložiště nahrávek nemá podporovaný formát.');const read=db.transaction('recordings','readonly').objectStore('recordings').getAll();values=await new Promise<unknown[]>((resolve,reject)=>{read.onsuccess=()=>resolve(read.result);read.onerror=()=>reject(read.error);});}finally{db.close();}
  const recordings:Recording[]=[];let rejected=0;
  for(const raw of values){const safe=sanitizeRecording(raw);if(safe)recordings.push(safe);else rejected++;}
  if(!confirm(`Exportovat ${Object.keys(entries).length} skupin nastavení a ${recordings.length} nahrávek pro Windows?${rejected?` ${rejected} poškozených nahrávek nelze převzít.`:''} Původní data zůstanou zachovaná. AI klíč se nepřenese.`))return;
  const archive=await buildMigrationArchive(buildBackup(entries),recordings);downloadBlob(archive,'rocker-pro-windows-'+new Date().toISOString().slice(0,10)+'.rockermigration');
}
void exportExistingProfile().catch(error=>alert(error instanceof Error?error.message:'Export Rockeru se nepodařil. Původní data zůstávají.'));
