'use strict';
const http = require('node:http');
const fs = require('node:fs');
const fsp = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const { pipeline } = require('node:stream/promises');
const { Transform } = require('node:stream');
const { Pool } = require('pg');
const auth = require('./auth');
const licenses = require('./licenses');

const PORT = Number(process.env.PORT || 3000);
const ADMIN_USER = process.env.ADMIN_USER;
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD;
const RELEASE_DIR = process.env.RELEASE_DIR || '/releases';
if (!ADMIN_USER || !ADMIN_PASSWORD || ADMIN_PASSWORD.length < 8 || !process.env.DATABASE_URL) {
  throw Error('Defina ADMIN_USER, ADMIN_PASSWORD (min. 8 caracteres) e DATABASE_URL');
}
const pool = new Pool({ connectionString: process.env.DATABASE_URL, max: 6 });
const MAX_JSON = 8192;
const MAX_RELEASE = 32 * 1024 * 1024;
const versionPattern = /^[0-9]{1,4}\.[0-9]{1,4}\.[0-9]{1,5}(?:\.[0-9]{1,5})?$/;
const safeName = v => typeof v === 'string' && v.trim().length >= 1 && v.trim().length <= 120;
function response(res, code, data, extra={}) {
  res.writeHead(code, {'Content-Type':'application/json; charset=utf-8','Cache-Control':'no-store','X-Content-Type-Options':'nosniff','X-Frame-Options':'DENY',...extra});
  res.end(JSON.stringify(data));
}
async function readJson(req) {
  const chunks=[];let total=0;
  for await (const chunk of req) { total+=chunk.length; if(total>MAX_JSON) throw Object.assign(Error('JSON excedeu limite'),{status:413});chunks.push(chunk);}
  try {return JSON.parse(Buffer.concat(chunks).toString('utf8'));}
  catch {throw Object.assign(Error('JSON inválido'),{status:400});}
}
async function audit(action,detail) {await pool.query('INSERT INTO audit_log(action,detail) VALUES ($1,$2)',[action,JSON.stringify(detail)]);}
function notFound(res) {response(res,404,{error:'Rota não encontrada'});}
async function handle(req,res) {
  const url = new URL(req.url, 'http://localhost');
  const route = url.pathname;
  if(route==='/healthz') {
    try {await pool.query('SELECT 1');return response(res,200,{status:'ok'});}
    catch {return response(res,503,{status:'database_unavailable'});}
  }
  if(route==='/api/bot/activate' && req.method==='POST') {
    const body=await readJson(req);
    const result=await licenses.activate(pool,body.key,body.deviceHash,body.version);
    return response(res,result.ok?200:403,result);
  }
  if(route.startsWith('/api/auth/')) {
    const result=await auth.route(pool,req,res,route);
    if(result===false)return notFound(res);
    return;
  }
  const active=await auth.session(pool,req);
  if(route==='/' && req.method==='GET') {
    const html = await fsp.readFile(path.join(__dirname,'index.html'));
    res.writeHead(200,{'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store','Content-Security-Policy':"default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; object-src 'none'; base-uri 'none'; form-action 'self'",'X-Content-Type-Options':'nosniff','X-Frame-Options':'DENY'});
    return res.end(html);
  }
  if(!active)return response(res,401,{error:'Faça login no FarmZilla Control'});
  if(!['GET','HEAD','OPTIONS'].includes(req.method)&&!await auth.authorized(pool,req,active))return response(res,403,{error:'Sessão ou origem inválida'});
  if(route==='/api/overview' && req.method==='GET') {
    const [devices,releases] = await Promise.all([pool.query("SELECT count(*)::int AS total,count(*) FILTER (WHERE status='blocked')::int AS blocked FROM devices"),pool.query('SELECT count(*)::int AS total FROM releases')]);
    return response(res,200,{devices:devices.rows[0],releases:releases.rows[0]});
  }
  if(route==='/api/devices' && req.method==='GET') {
    const rows=await pool.query('SELECT id,name,note,status,created_at,updated_at FROM devices ORDER BY id DESC LIMIT 500');
    return response(res,200,{devices:rows.rows});
  }
  if(route==='/api/devices' && req.method==='POST') {
    const body=await readJson(req);
    if(!safeName(body.name)||typeof body.note!=='string'||body.note.length>1000) return response(res,400,{error:'Nome e observação inválidos'});
    const q=await pool.query('INSERT INTO devices(name,note) VALUES ($1,$2) RETURNING id,name,note,status',[body.name.trim(),body.note]);
    await audit('device.create',{id:q.rows[0].id});
    return response(res,201,{device:q.rows[0]});
  }
  const match=route.match(/^\/api\/devices\/([0-9]+)$/);
  if(match && req.method==='PATCH') {
    const body=await readJson(req);
    if(!['registered','blocked'].includes(body.status)) return response(res,400,{error:'Status inválido'});
    const q=await pool.query('UPDATE devices SET status=$1,updated_at=NOW() WHERE id=$2 RETURNING id,name,status',[body.status,match[1]]);
    if(!q.rowCount)return notFound(res);
    await audit('device.status',{id:q.rows[0].id,status:body.status});
    return response(res,200,{device:q.rows[0]});
  }
  if(route==='/api/licenses' && req.method==='GET') {
    return response(res,200,{licenses:await licenses.list(pool)});
  }
  if(route==='/api/licenses' && req.method==='POST') {
    const b=await readJson(req);
    const license=await licenses.create(pool,b.label,b.expiresAt);
    await audit('license.create',{id:license.id});
    return response(res,201,{license});
  }
  const licenseMatch=route.match(new RegExp('^/api/licenses/([0-9]+)$'));
  if(licenseMatch && req.method==='PATCH') {
    const b=await readJson(req);
    const q=await licenses.change(pool,licenseMatch[1],b.action,b.expiresAt);
    if(!q.rowCount)return notFound(res);
    await audit('license.change',{id:q.rows[0].id,action:b.action});
    return response(res,200,{license:q.rows[0]});
  }
  if(route==='/api/releases' && req.method==='GET') {
    const q=await pool.query('SELECT id,version,filename,sha256,bytes,created_at FROM releases ORDER BY id DESC LIMIT 100');
    return response(res,200,{releases:q.rows});
  }
  if(route==='/api/releases' && req.method==='POST') {
    const version=url.searchParams.get('version')||'';
    if(!versionPattern.test(version)) return response(res,400,{error:'Versão inválida (ex.: 0.51.20)'});
    if(req.headers['content-type']!=='application/octet-stream') return response(res,415,{error:'Envie arquivo como application/octet-stream'});
    const length=Number(req.headers['content-length']);
    if(!Number.isSafeInteger(length)||length<1024||length>MAX_RELEASE) return response(res,413,{error:'Arquivo deve ter entre 1 KB e 32 MB'});
    await fsp.mkdir(RELEASE_DIR,{recursive:true});
    const temp=path.join(RELEASE_DIR,'.upload-'+crypto.randomUUID());
    const hash=crypto.createHash('sha256');let received=0;
    const meter=new Transform({transform(chunk,enc,cb) {received+=chunk.length;if(received>MAX_RELEASE)cb(Error('Arquivo excedeu limite'));else {hash.update(chunk);cb(null,chunk);}}});
    try {
      await pipeline(req,meter,fs.createWriteStream(temp,{flags:'wx',mode:0o600}));
      if(received!==length)throw Object.assign(Error('Tamanho do arquivo não confere'),{status:400});
      // Windows PE MZ signature. This is a file-type check, not signature verification.
      const handle=await fsp.open(temp,'r');const header=Buffer.alloc(2);
      try{await handle.read(header,0,2,0);}finally{await handle.close();}
      if(header.toString()!=='MZ')throw Object.assign(Error('Arquivo não parece ser um executável Windows'),{status:400});
      const filename='FarmZilla-'+version+'.exe';
      const dest=path.join(RELEASE_DIR,filename);
      if(fs.existsSync(dest))throw Object.assign(Error('Essa versão já possui arquivo'),{status:409});
      const digest=hash.digest('hex');
      const client=await pool.connect();
      let linked=false;
      try {
        await client.query('BEGIN');
        await client.query('INSERT INTO releases(version,filename,sha256,bytes) VALUES ($1,$2,$3,$4)',[version,filename,digest,received]);
        await fsp.link(temp,dest);linked=true;
        await client.query('INSERT INTO audit_log(action,detail) VALUES ($1,$2)',['release.upload',JSON.stringify({version,sha256:digest,bytes:received})]);
        await client.query('COMMIT');
      } catch(err) {await client.query('ROLLBACK');if(linked)await fsp.rm(dest,{force:true});throw err;}
      finally{client.release();}
      return response(res,201,{release:{version,sha256:digest,bytes:received}});
    } finally {await fsp.rm(temp,{force:true});}
  }
  if(route==='/api/audit' && req.method==='GET') {
    const q=await pool.query('SELECT action,detail,created_at FROM audit_log ORDER BY id DESC LIMIT 100');
    return response(res,200,{events:q.rows});
  }
  return notFound(res);
}
const server=http.createServer((req,res)=>{handle(req,res).catch(err=>{
  console.error(new Date().toISOString(),err.code || err.message);
  if(res.headersSent){res.destroy();return;}
  response(res,err.status|| (err.code==='23505'?409:500),{error:err.status?err.message:err.code==='23505'?'Registro duplicado':'Falha interna'});
});});
server.headersTimeout=15000;server.requestTimeout=120000;
auth.init(pool,ADMIN_USER,ADMIN_PASSWORD).then(()=>licenses.init(pool)).then(()=>server.listen(PORT,'0.0.0.0',()=>console.log('FarmZilla Control ouvindo porta '+PORT))).catch(error=>{console.error('Falha inicializando autenticação:',error.message);process.exitCode=1;pool.end();});
process.on('SIGTERM',()=>server.close(()=>pool.end()));
