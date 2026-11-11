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
  const r=await pool.query('SELECT id,label,status,device_hash IS NOT NULL AS linked,expires_at,created_at,updated_at,last_seen_at,client_version FROM bot_licenses ORDER BY id DESC LIMIT 500');
  return r.rows;
}

async function remove(pool,id) {
  return pool.query('DELETE FROM bot_licenses WHERE id=$1 RETURNING id,label,status',[id]);
}

async function change(pool,id,action,expiresAt) {
  if(!['active','suspended','revoked','release-device','renew'].includes(action)) throw Object.assign(Error('Ação inválida'),{status:400});
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
     RETURNING id,expires_at`,[digest(key),deviceHash,version]);
  if(result.rowCount)return {ok:true,expiresAt:result.rows[0].expires_at};
  // Do not reveal whether the key exists, is expired, suspended, or bound to another PC.
  return {ok:false,reason:'license_unavailable'};
}

module.exports={init,create,list,change,remove,activate,KEY_RE,DEVICE_RE,digest};
