'use strict';
const crypto = require('node:crypto');
const KEY_RE = /^[A-Z0-9]{8,128}$/;
const DEVICE_RE = /^[a-f0-9]{64}$/;
const digest = value => crypto.createHash('sha256').update(value).digest('hex');

async function init(pool) {
  await pool.query(`CREATE TABLE IF NOT EXISTS bot_licenses (
    id BIGSERIAL PRIMARY KEY,
    key_hash CHAR(64) UNIQUE NOT NULL,
    label VARCHAR(120) NOT NULL,
    status VARCHAR(16) NOT NULL DEFAULT 'active' CHECK (status IN ('active','suspended','revoked')),
    device_hash CHAR(64),
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen_at TIMESTAMPTZ,
    client_version VARCHAR(40)
  )`);
  await pool.query('CREATE INDEX IF NOT EXISTS bot_licenses_device ON bot_licenses(device_hash)');
  await pool.query('ALTER TABLE bot_licenses ADD COLUMN IF NOT EXISTS send_log BOOLEAN NOT NULL DEFAULT FALSE');
  await pool.query(`CREATE TABLE IF NOT EXISTS bot_logs (
    id BIGSERIAL PRIMARY KEY,
    license_id BIGINT NOT NULL REFERENCES bot_licenses(id) ON DELETE CASCADE,
    at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    version VARCHAR(40) NOT NULL,
    line VARCHAR(500) NOT NULL
  )`);
  await pool.query('CREATE INDEX IF NOT EXISTS bot_logs_license_id ON bot_logs(license_id, id DESC)');
}

function newKey() {
  return 'FZ' + crypto.randomBytes(24).toString('hex').toUpperCase();
}

async function create(pool,label,expiresAt,deviceHash) {
  if(deviceHash!=null && (typeof deviceHash!=='string' || !DEVICE_RE.test(deviceHash))) throw Object.assign(Error('Código do computador inválido'),{status:400});
  if(typeof label !== 'string' || !label.trim() || label.length > 120) throw Object.assign(Error('Descrição inválida'),{status:400});
  const date = new Date(expiresAt);
  if(!Number.isFinite(date.getTime()) || date <= new Date()) throw Object.assign(Error('Validade inválida'),{status:400});
  const key = newKey();
  const result = await pool.query('INSERT INTO bot_licenses(key_hash,label,expires_at,device_hash) VALUES($1,$2,$3,$4) RETURNING id,label,status,expires_at,created_at',[digest(key),label.trim(),date,deviceHash||null]);
  return {...result.rows[0],key};
}

async function list(pool) {
  const r=await pool.query('SELECT id,label,status,device_hash IS NOT NULL AS linked,expires_at,created_at,updated_at,last_seen_at,client_version,send_log FROM bot_licenses ORDER BY id DESC LIMIT 500');
  return r.rows;
}

async function remove(pool,id) {
  return pool.query('DELETE FROM bot_licenses WHERE id=$1 RETURNING id,label,status',[id]);
}

async function change(pool,id,action,expiresAt) {
  if(!['active','suspended','revoked','release-device','renew','log-on','log-off'].includes(action)) throw Object.assign(Error('Ação inválida'),{status:400});
  if(action==='log-on'||action==='log-off') return pool.query('UPDATE bot_licenses SET send_log=$1,updated_at=NOW() WHERE id=$2 RETURNING id,status,expires_at,send_log',[action==='log-on',id]);
  if(action==='renew') {
    const date=new Date(expiresAt);
    if(!Number.isFinite(date.getTime()) || date <= new Date()) throw Object.assign(Error('Validade inválida'),{status:400});
    return pool.query('UPDATE bot_licenses SET expires_at=$1,updated_at=NOW() WHERE id=$2 RETURNING id,status,expires_at',[date,id]);
  }
  if(action==='release-device') return pool.query('UPDATE bot_licenses SET device_hash=NULL,last_seen_at=NULL,client_version=NULL,updated_at=NOW() WHERE id=$1 RETURNING id,status,expires_at',[id]);
  return pool.query('UPDATE bot_licenses SET status=$1,updated_at=NOW() WHERE id=$2 RETURNING id,status,expires_at',[action,id]);
}

// Atomic compare-and-bind: a license can be claimed by exactly one device.
async function activate(pool,key,deviceHash,version) {
  if(typeof key!=='string'||!KEY_RE.test(key)||typeof deviceHash!=='string'||!DEVICE_RE.test(deviceHash)||
     typeof version!=='string'||version.length>40) return {ok:false,reason:'invalid_request'};
  const result=await pool.query(`
    UPDATE bot_licenses
       SET device_hash=COALESCE(device_hash,$2),
           last_seen_at=NOW(),client_version=$3,updated_at=NOW()
     WHERE key_hash=$1 AND status='active' AND expires_at>NOW()
       AND (device_hash IS NULL OR device_hash=$2)
     RETURNING id,expires_at,send_log`,[digest(key),deviceHash,version]);
  if(result.rowCount)return {ok:true,expiresAt:result.rows[0].expires_at,log:!!result.rows[0].send_log};
  // Do not reveal whether the key exists, is expired, suspended, or bound to another PC.
  return {ok:false,reason:'license_unavailable'};
}

const LOG_KEEP=5000,LOG_BATCH=200,LOG_LINE=500,LOG_RATE=3000;

async function ingest(pool,key,deviceHash,version,lines){
  if(typeof key!=='string'||!KEY_RE.test(key)||typeof deviceHash!=='string'||!DEVICE_RE.test(deviceHash)||
     typeof version!=='string'||!version||version.length>40||
     !Array.isArray(lines)||lines.length<1||lines.length>LOG_BATCH||
     lines.some(l=>typeof l!=='string'||l.length>LOG_LINE))
    return {ok:false,status:400,error:'Registro inválido'};
  const client=await pool.connect();
  try{
    await client.query('BEGIN');
    const license=await client.query(`
      SELECT id,send_log FROM bot_licenses
       WHERE key_hash=$1 AND status='active' AND expires_at>NOW()
         AND device_hash=$2
       FOR UPDATE`,[digest(key),deviceHash]);
    if(!license.rowCount){await client.query('ROLLBACK');return {ok:false,status:403,reason:'license_unavailable'};}
    if(!license.rows[0].send_log){await client.query('ROLLBACK');return {ok:false,status:403,error:'Registro desligado para este PC',log:false};}
    const id=license.rows[0].id;
    await client.query('UPDATE bot_licenses SET last_seen_at=NOW(),client_version=$2,updated_at=NOW() WHERE id=$1',[id,version]);
    const recent=await client.query("SELECT COUNT(*)::int AS n FROM bot_logs WHERE license_id=$1 AND at>NOW()-INTERVAL '10 minutes'",[id]);
    if((recent.rows[0].n||0)+lines.length>LOG_RATE){await client.query('ROLLBACK');return {ok:false,status:429,error:'Muitos registros em pouco tempo'};}
    const params=[];const tuples=lines.map((line,i)=>{params.push(id,version,line);const n=i*3;return `($${n+1},$${n+2},$${n+3})`;});
    await client.query('INSERT INTO bot_logs(license_id,version,line) VALUES '+tuples.join(','),params);
    await client.query("DELETE FROM bot_logs WHERE at<NOW()-INTERVAL '7 days'");
    await client.query('DELETE FROM bot_logs WHERE license_id=$1 AND id<=(SELECT id FROM bot_logs WHERE license_id=$1 ORDER BY id DESC OFFSET $2 LIMIT 1)',[id,LOG_KEEP]);
    await client.query('COMMIT');
    return {ok:true};
  }catch(e){await client.query('ROLLBACK');throw e;}
  finally{client.release();}
}

async function listLogs(pool,id,limit){
  const n=Math.min(Math.max(Number(limit)||1000,1),LOG_KEEP);
  if(!/^[0-9]+$/.test(String(id))) throw Object.assign(Error('Licença inválida'),{status:400});
  const found=await pool.query('SELECT id,label FROM bot_licenses WHERE id=$1',[id]);
  if(!found.rowCount) throw Object.assign(Error('Licença não encontrada'),{status:404});
  const rows=await pool.query('SELECT (EXTRACT(EPOCH FROM at)*1000)::bigint AS at,version,line FROM bot_logs WHERE license_id=$1 ORDER BY id DESC LIMIT $2',[id,n]);
  return {label:found.rows[0].label,lines:rows.rows.reverse()};
}

module.exports={init,create,list,change,remove,activate,ingest,listLogs,KEY_RE,DEVICE_RE,digest};
