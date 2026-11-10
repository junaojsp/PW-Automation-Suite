'use strict';
const crypto = require('node:crypto');
const COOKIE='__Host-fz_session';
const HOURS=12;
const attempts=new Map();
const hash=s=>crypto.createHash('sha256').update(s).digest('hex');
const safe=(a,b)=>{const x=Buffer.from(a||''),y=Buffer.from(b||'');return x.length===y.length&&crypto.timingSafeEqual(x,y);};
const derive=(password,salt)=>crypto.scryptSync(password,salt,64,{N:16384,r:8,p:1}).toString('hex');
const validPass=p=>typeof p==='string'&&p.length>=16&&p.length<=128;
function json(res,code,data,headers={}){res.writeHead(code,{'Content-Type':'application/json; charset=utf-8','Cache-Control':'no-store','X-Frame-Options':'DENY',...headers});res.end(JSON.stringify(data));}
function cookie(value,maxAge){return COOKIE+'='+value+'; Path=/; Max-Age='+maxAge+'; HttpOnly; Secure; SameSite=Strict';}
function token(req){const m=(req.headers.cookie||'').match(/(?:^|;\s*)__Host-fz_session=([a-f0-9]{64})(?:;|$)/);return m?.[1]||null;}
async function body(req){let chunks=[],n=0;for await(const part of req){n+=part.length;if(n>4096)throw Object.assign(Error('Requisição muito grande'),{status:413});chunks.push(part);}try{return JSON.parse(Buffer.concat(chunks).toString('utf8'));}catch{throw Object.assign(Error('JSON inválido'),{status:400});}}
async function init(pool,user,password){
 await pool.query(`CREATE TABLE IF NOT EXISTS admin_credentials (username TEXT PRIMARY KEY, salt TEXT NOT NULL, password_hash TEXT NOT NULL, updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW())`);
 await pool.query(`CREATE TABLE IF NOT EXISTS admin_sessions (token_hash CHAR(64) PRIMARY KEY, username TEXT NOT NULL REFERENCES admin_credentials(username) ON DELETE CASCADE, csrf_hash CHAR(64) NOT NULL, expires_at TIMESTAMPTZ NOT NULL)`);
 await pool.query('CREATE INDEX IF NOT EXISTS admin_sessions_expire ON admin_sessions(expires_at)');
 const exists=await pool.query('SELECT username FROM admin_credentials LIMIT 1');
 if(!exists.rowCount){if(!validPass(password))throw Error('Senha de bootstrap precisa de pelo menos 16 caracteres');const salt=crypto.randomBytes(16).toString('hex');await pool.query('INSERT INTO admin_credentials(username,salt,password_hash) VALUES($1,$2,$3) ON CONFLICT DO NOTHING',[user,salt,derive(password,salt)]);}
}
async function session(pool,req){const raw=token(req);if(!raw)return null;const found=await pool.query('SELECT username,csrf_hash FROM admin_sessions WHERE token_hash=$1 AND expires_at>NOW()',[hash(raw)]);return found.rows[0]||null;}
async function createSession(pool,username){const t=crypto.randomBytes(32).toString('hex'),csrf=crypto.randomBytes(32).toString('hex');await pool.query('DELETE FROM admin_sessions WHERE expires_at < NOW()');await pool.query('INSERT INTO admin_sessions(token_hash,username,csrf_hash,expires_at) VALUES($1,$2,$3,NOW()+INTERVAL \'12 hours\')',[hash(t),username,hash(csrf)]);return {t,csrf};}
function originOk(req){const origin=req.headers.origin;const forwarded=req.headers['x-forwarded-host'];const host=(forwarded||req.headers.host||'').split(',')[0].trim();const scheme=req.headers['x-forwarded-proto']==='https'?'https':'http';return typeof origin==='string'&&(origin===scheme+'://'+host)&&!origin.includes('@');}
async function route(pool,req,res,pathname){
 if(pathname==='/api/auth/login'&&req.method==='POST'){
  if(!originOk(req))return json(res,403,{error:'Origem não autorizada'});
  const b=await body(req);if(typeof b.username!=='string'||typeof b.password!=='string')return json(res,400,{error:'Credenciais inválidas'});
  const key='login';const state=attempts.get(key)||{count:0,until:0};if(Date.now()>state.until){state.count=0;state.until=Date.now()+15*60000;}
  if(state.count>=10)return json(res,429,{error:'Muitas tentativas. Tente novamente mais tarde.'});
  const rows=await pool.query('SELECT username,salt,password_hash FROM admin_credentials WHERE username=$1',[b.username]);
  const row=rows.rows[0];const fallbackSalt='00'.repeat(16);
  const derived=derive(b.password.slice(0,128),row?.salt||fallbackSalt);
  if(!row||!safe(derived,row.password_hash)){state.count++;attempts.set(key,state);return json(res,401,{error:'Usuário ou senha inválidos'});}
  attempts.delete(key);const sess=await createSession(pool,row.username);
  return json(res,200,{authenticated:true,username:row.username,csrf:sess.csrf},{'Set-Cookie':cookie(sess.t,HOURS*3600)});
 }
 if(pathname==='/api/auth/session'&&req.method==='GET'){
  const s=await session(pool,req);if(!s)return json(res,401,{authenticated:false});
  // CSRF secret is returned only after authentication and tied to the session hash.
  const raw=token(req);const csrf=crypto.createHmac('sha256',raw).update('csrf').digest('hex');
  // Existing sessions contain a random csrf hash; use a deterministic value instead on new sessions.
  return json(res,200,{authenticated:true,username:s.username,csrf});
 }
 if(pathname==='/api/auth/logout'&&req.method==='POST'){
  const s=await session(pool,req);if(s&&!await authorized(pool,req,s))return json(res,403,{error:'Requisição não autorizada'});
  const raw=token(req);if(raw)await pool.query('DELETE FROM admin_sessions WHERE token_hash=$1',[hash(raw)]);
  return json(res,200,{ok:true},{'Set-Cookie':cookie('',0)});
 }
 if(pathname==='/api/auth/password'&&req.method==='POST'){
  const s=await session(pool,req);if(!s)return json(res,401,{error:'Sessão expirada'});
  if(!await authorized(pool,req,s))return json(res,403,{error:'Requisição não autorizada'});
  const b=await body(req);
  if(!validPass(b.newPassword)||b.newPassword===b.currentPassword)return json(res,400,{error:'Nova senha precisa ter 16 a 128 caracteres e ser diferente'});
  const old=await pool.query('SELECT salt,password_hash FROM admin_credentials WHERE username=$1',[s.username]);
  if(!old.rowCount||!safe(derive(String(b.currentPassword||''),old.rows[0].salt),old.rows[0].password_hash))return json(res,403,{error:'Senha atual incorreta'});
  const salt=crypto.randomBytes(16).toString('hex');
  const client=await pool.connect();try{await client.query('BEGIN');await client.query('UPDATE admin_credentials SET salt=$1,password_hash=$2,updated_at=NOW() WHERE username=$3',[salt,derive(b.newPassword,salt),s.username]);await client.query('DELETE FROM admin_sessions WHERE username=$1',[s.username]);await client.query('INSERT INTO audit_log(action,detail) VALUES($1,$2)',['admin.password.changed',JSON.stringify({username:s.username})]);await client.query('COMMIT');}catch(e){await client.query('ROLLBACK');throw e;}finally{client.release();}
  return json(res,200,{ok:true,relogin:true},{'Set-Cookie':cookie('',0)});
 }
 return false;
}
async function authorized(pool,req,s){if(!originOk(req))return false;const csrf=crypto.createHmac('sha256',token(req)||'').update('csrf').digest('hex');return safe(req.headers['x-fz-csrf'],csrf);}
module.exports={init,route,session,authorized};
