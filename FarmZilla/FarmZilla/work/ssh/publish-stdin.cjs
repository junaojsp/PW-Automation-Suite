'use strict';
// Runs inside the existing web container. Only release tables and files are touched.
const path=require('node:path');const fs=require('node:fs/promises');
const {Pool}=require('/srv/app/node_modules/pg');
const store=require('/srv/app/release-store');
async function main(){
 const name=process.env.FZ_PUBLISH_FILE,version=process.env.FZ_PUBLISH_VERSION;
 if(!/^\.ssh-upload-[A-Za-z0-9]+\.exe$/.test(name||''))throw Error('Arquivo de staging inválido');
 const dir=process.env.RELEASE_DIR||'/releases',file=path.join(dir,name);
 const pool=new Pool({connectionString:process.env.DATABASE_URL,max:1});
 const owned=path.join(dir,'.publish-'+require('node:crypto').randomUUID()+'.exe');
 try{
  // SSH staging belongs to root; copy as the container user before creating hard links.
  await fs.copyFile(file,owned,require('node:fs').constants.COPYFILE_EXCL);
  const meta=await store.binary(owned,version);
  if(meta.sha256!==process.env.FZ_PUBLISH_SHA||meta.bytes!==Number(process.env.FZ_PUBLISH_BYTES))throw Error('Integridade do staging divergente');
  const row=await store.register(pool,dir,owned,version,{publish:true,reuse:true,source:'github-ssh'});
  // Re-read the publication and the disk after commit; never report only a successful SQL write.
  const q=await pool.query('SELECT r.* FROM release_publication p JOIN releases r ON r.id=p.release_id WHERE p.id=1');
  if(String(q.rows[0]?.id)!==String(row.id))throw Error('A publicação mudou após o commit');
  const verified=await store.validate(dir,q.rows[0]);
  console.log(JSON.stringify({published:true,id:row.id,version:row.version,...verified,reused:!!row.reused}));
 }finally{await fs.rm(owned,{force:true});await pool.end();}
}
main().catch(e=>{console.error('Publicação recusada:',e.message);process.exitCode=1;});
