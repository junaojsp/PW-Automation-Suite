'use strict';
const fs=require('node:fs/promises');const path=require('node:path');const crypto=require('node:crypto');const pe=require('./pe-version');
const MAX=32*1024*1024;
const error=(message,status=409)=>Object.assign(new Error(message),{status});
async function binary(file,version){
 const stat=await fs.lstat(file);if(!stat.isFile()||stat.size<1024||stat.size>MAX)throw error('Arquivo indisponível ou tamanho inválido',400);
 const data=await fs.readFile(file);if(data.length!==stat.size)throw error('Arquivo alterado durante leitura');
 return {...pe.verify(data,version),sha256:crypto.createHash('sha256').update(data).digest('hex'),bytes:data.length};
}
function filename(row){pe.parts(row.version);if(row.filename!=='FarmZilla-'+row.version+'.exe')throw error('Nome do arquivo cadastrado inválido');return row.filename;}
async function validate(dir,row){
 const meta=await binary(path.join(dir,filename(row)),row.version);
 if(meta.sha256!==row.sha256.trim().toLowerCase()||meta.bytes!==Number(row.bytes))throw error('Hash ou tamanho do cadastro difere do arquivo',400);
 return meta;
}
async function lock(client){await client.query('LOCK TABLE releases IN SHARE ROW EXCLUSIVE MODE');await client.query('LOCK TABLE release_publication IN SHARE ROW EXCLUSIVE MODE');}
async function event(client,action,detail){await client.query('INSERT INTO audit_log(action,detail) VALUES($1,$2)',[action,JSON.stringify(detail)]);}
async function setPublication(client,row,meta,source,requireNewer){
 const q=await client.query('SELECT r.* FROM release_publication p JOIN releases r ON r.id=p.release_id WHERE p.id=1');const previous=q.rows[0]||null;
 if(requireNewer&&previous&&String(previous.id)!==String(row.id)&&pe.compare(row.version,previous.version)<=0)throw error('A nova publicação deve ter versão superior à publicada');
 await client.query('INSERT INTO release_publication(id,release_id,published_at) VALUES(1,$1,NOW()) ON CONFLICT(id) DO UPDATE SET release_id=EXCLUDED.release_id,published_at=NOW()',[row.id]);
 await event(client,'release.publish',{id:row.id,version:row.version,...meta,previousReleaseId:previous?.id||null,source});
}
async function register(pool,dir,temp,version,options={}){
 const meta=await binary(temp,version),v=pe.canonical(version);await fs.mkdir(dir,{recursive:true});
 const client=await pool.connect();let linked=false,commitSent=false,dest;
 try{
  await client.query('BEGIN');await lock(client);
  const rows=(await client.query('SELECT * FROM releases')).rows;
  const same=rows.filter(r=>pe.normalize(r.version)===pe.normalize(v));
  if(same.length){
   if(!options.reuse||same.length!==1||same[0].sha256.trim()!==meta.sha256||Number(same[0].bytes)!==meta.bytes)throw error('Esta versão já existe (0.51.31 e 0.51.31.0 são equivalentes). Use uma versão nova ou Corrigir arquivo');
   const row=same[0];await validate(dir,row);
   if(options.publish)await setPublication(client,row,meta,options.source||'ssh',true);
   commitSent=true;await client.query('COMMIT');return {...row,...meta,reused:true};
  }
  if(rows.some(r=>r.sha256.trim()===meta.sha256))throw error('Este executável já foi cadastrado com outro número de versão');
  const row=(await client.query('INSERT INTO releases(version,filename,sha256,bytes) VALUES($1,$2,$3,$4) RETURNING *',[v,'FarmZilla-'+v+'.exe',meta.sha256,meta.bytes])).rows[0];
  dest=path.join(dir,row.filename);await fs.link(temp,dest);linked=true;
  await event(client,'release.upload',{id:row.id,version:v,...meta,source:options.source||'panel'});
  if(options.publish)await setPublication(client,row,meta,options.source||'ssh',true);
  commitSent=true;await client.query('COMMIT');return {...row,...meta};
 }catch(err){await client.query('ROLLBACK').catch(()=>{});if(linked&&!commitSent)await fs.unlink(dest);throw err;}
 finally{client.release();}
}
async function publish(pool,dir,id){
 const client=await pool.connect();try{await client.query('BEGIN');await lock(client);
  const row=(await client.query('SELECT * FROM releases WHERE id=$1',[id])).rows[0];if(!row)throw error('Versão não encontrada',404);
  const same=(await client.query('SELECT version FROM releases')).rows.filter(r=>pe.normalize(r.version)===pe.normalize(row.version));
  if(same.length!==1)throw error('Há cadastros equivalentes desta versão. Publique uma versão nova e única');
  const meta=await validate(dir,row);await setPublication(client,row,meta,'panel',false);await client.query('COMMIT');return row;
 }catch(err){await client.query('ROLLBACK').catch(()=>{});throw err;}finally{client.release();}
}
async function repair(pool,dir,temp,id,expectedSha){
 if(!/^[a-f0-9]{64}$/i.test(expectedSha||''))throw error('Hash anterior obrigatório para corrigir');
 const client=await pool.connect();let dest,backup,replaced=false,commitSent=false;
 try{await client.query('BEGIN');await lock(client);
  const row=(await client.query('SELECT * FROM releases WHERE id=$1',[id])).rows[0];if(!row)throw error('Versão não encontrada',404);
  if(row.sha256.trim().toLowerCase()!==expectedSha.toLowerCase())throw error('Cadastro mudou. Atualize a página antes de corrigir');
  // Never replace an already valid release with another build of the same version.
  let valid=false;try{await validate(dir,row);valid=true;}catch(e){if(e.code&&!['ENOENT'].includes(e.code))throw e;}
  if(valid)throw error('Esta versão já é válida. Para alterar o programa, compile um número de versão superior');
  const meta=await binary(temp,row.version);dest=path.join(dir,filename(row));
  const history=path.join(dir,'.history');await fs.mkdir(history,{recursive:true,mode:0o700});
  const token=crypto.randomUUID();backup=path.join(history,row.filename+'.'+token+'.bak');
  let oldMissing=false;try{const st=await fs.lstat(dest);if(!st.isFile())throw error('Arquivo anterior não é regular');await fs.copyFile(dest,backup,require('node:fs').constants.COPYFILE_EXCL);}catch(e){if(e.code==='ENOENT')oldMissing=true;else throw e;}
  await fs.writeFile(path.join(history,token+'.json'),JSON.stringify({before:row,after:meta,backup:oldMissing?null:path.basename(backup)}),{flag:'wx',mode:0o600});
  const staged=path.join(dir,'.repair-'+token);try{await fs.copyFile(temp,staged,require('node:fs').constants.COPYFILE_EXCL);await fs.rename(staged,dest);replaced=true;}finally{await fs.rm(staged,{force:true});}
  await client.query('UPDATE releases SET sha256=$1,bytes=$2 WHERE id=$3',[meta.sha256,meta.bytes,id]);
  await event(client,'release.repair',{id,version:row.version,before:{sha256:row.sha256,bytes:row.bytes},after:meta,backup:oldMissing?null:path.basename(backup)});
  commitSent=true;await client.query('COMMIT');return {...row,...meta};
 }catch(err){await client.query('ROLLBACK').catch(()=>{});if(replaced&&!commitSent){try{await fs.rename(backup,dest);}catch(e){if(e.code==='ENOENT')await fs.rm(dest,{force:true});else throw e;}}throw err;}
 finally{client.release();}
}
module.exports={MAX,binary,validate,register,publish,repair};
