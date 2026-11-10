import {adminPage} from './admin-page.mjs';
import keys from './keys.mjs';
const enc=new TextEncoder();
export const b64=b=>btoa(String.fromCharCode(...new Uint8Array(b))).replace(/=/g,'').replace(/\+/g,'-').replace(/\//g,'_');
const unb64=s=>Uint8Array.from(atob(s.replace(/-/g,'+').replace(/_/g,'/')),c=>c.charCodeAt(0));
export const hash=async s=>Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',enc.encode(s))),b=>b.toString(16).padStart(2,'0')).join('');
const codeOk=c=>typeof c==='string'&&/^BGC2-[A-F0-9]{64}$/.test(c);
const json=(data,status=200)=>Response.json(data,{status,headers:{'Cache-Control':'private, no-store','X-Content-Type-Options':'nosniff'}});
async function body(request){if(Number(request.headers.get('Content-Length')||0)>16384)throw Error('body');const text=await request.text();if(text.length>16384)throw Error('body');return JSON.parse(text);}
export async function deviceCode(hardware,jwk){return 'BGC2-'+(await hash('Bagaceira:online:v2|'+hardware+'|'+jwk.n+'|'+jwk.e)).toUpperCase();}
async function admin(request,env,keyOnly=false){const v=request.headers.get('Authorization');if(!v?.startsWith('Bearer ')||v.length>1000)return false;const t=v.slice(7);
 if(t.startsWith('FZS.')){if(keyOnly)return false;const row=await env.DB.prepare('SELECT access_expires FROM admin_sessions WHERE access_hash=?').bind(await hash(t)).first();return !!row&&row.access_expires>Date.now();}
 if(!env.ADMIN_TOKEN_SHA256)return false;const a=await hash(t),b=env.ADMIN_TOKEN_SHA256.toLowerCase();if(a.length!==b.length)return false;let diff=0;for(let i=0;i<a.length;i++)diff|=a.charCodeAt(i)^b.charCodeAt(i);return diff===0;}
async function lease(env,device,now){const payload=enc.encode(JSON.stringify({product:'Bagaceira',device,issued:now,expires:now+86400000}));const key=await crypto.subtle.importKey('jwk',JSON.parse(env.LICENSE_LEASE_PRIVATE_JWK),{name:'RSASSA-PKCS1-v1_5',hash:'SHA-256'},false,['sign']);const sig=await crypto.subtle.sign('RSASSA-PKCS1-v1_5',key,payload);return 'BGL2.'+b64(payload)+'.'+b64(sig);}
// Tools a PC can be given (0.38.0): "controle" = Espelho (follow, attack, PT, buff, missions mirrored); "rota" = Mat Farm.
export const FEATURES=['controle','rota'];
const featureList=s=>String(s??'').split(',').filter(f=>FEATURES.includes(f));
const featuresOk=a=>Array.isArray(a)&&a.every(f=>FEATURES.includes(f))&&new Set(a).size===a.length;
// Lifetime license (0.38.0, owner request: lifetime sale): no expiry, so the PC keeps working offline; it carries the tools
// and whether the PC sends its log. Blocking or deleting still works: the client asks again every 30 min when online.
async function license(env,device,row,now){const payload=enc.encode(JSON.stringify({product:'Bagaceira',kind:'lifetime',device,issued:now,features:featureList(row.features),log:row.log_enabled===1}));const key=await crypto.subtle.importKey('jwk',JSON.parse(env.LICENSE_LEASE_PRIVATE_JWK),{name:'RSASSA-PKCS1-v1_5',hash:'SHA-256'},false,['sign']);const sig=await crypto.subtle.sign('RSASSA-PKCS1-v1_5',key,payload);return 'BGL3.'+b64(payload)+'.'+b64(sig);}
export async function checkLease(token,env,publicJwk,now=Date.now()){
 if(typeof token!=='string'||token.length>8192)return null;const parts=token.split('.');if(parts.length!==3||(parts[0]!=='BGL2'&&parts[0]!=='BGL3'))return null;
 try{const payload=unb64(parts[1]),key=await crypto.subtle.importKey('jwk',publicJwk,{name:'RSASSA-PKCS1-v1_5',hash:'SHA-256'},false,['verify']);if(!await crypto.subtle.verify('RSASSA-PKCS1-v1_5',key,unb64(parts[2]),payload))return null;const data=JSON.parse(new TextDecoder().decode(payload));if(data.product!=='Bagaceira'||!codeOk(data.device)||!Number.isSafeInteger(data.issued)||data.issued>now+300000)return null;
  if(parts[0]==='BGL3'?data.kind!=='lifetime'||!featuresOk(data.features):!Number.isSafeInteger(data.expires)||data.expires<=now||data.expires-data.issued!==86400000)return null;const row=await env.DB.prepare('SELECT status FROM devices WHERE code=?').bind(data.device).first();return row?.status==='allowed'?data:null;}catch{return null;}
}
// Remote log: allowed PCs send their FarmZilla log lines; kept 7 days and at most 5000 lines per PC.
const LOG_DAYS=7,LOG_KEEP=5000,LOG_BATCH=200,LOG_LINE=500,LOG_RATE=3000;
async function deviceLog(request,env,licenseKey,now){
 const auth=request.headers.get('Authorization');const lease=auth?.startsWith('Bearer ')?await checkLease(auth.slice(7),env,licenseKey,now):null;if(!lease)return json({error:'PC sem autorização'},401);
 const on=await env.DB.prepare('SELECT log_enabled FROM devices WHERE code=?').bind(lease.device).first();if(on?.log_enabled!==1)return json({error:'Registro desligado para este PC',log:false},403);
 const d=await body(request);if(!Array.isArray(d.lines)||d.lines.length<1||d.lines.length>LOG_BATCH||typeof d.version!=='string'||d.version.length>20||d.lines.some(l=>typeof l!=='string'||l.length>LOG_LINE))return json({error:'Registro inválido'},400);
 const recent=await env.DB.prepare('SELECT COUNT(*) AS n FROM logs WHERE device_code=? AND at>?').bind(lease.device,now-600000).first();if((recent?.n||0)+d.lines.length>LOG_RATE)return json({error:'Muitos registros em pouco tempo'},429);
 await env.DB.batch(d.lines.map(l=>env.DB.prepare('INSERT INTO logs(device_code,at,version,line) VALUES(?,?,?,?)').bind(lease.device,now,d.version,l)));
 await env.DB.batch([env.DB.prepare('DELETE FROM logs WHERE at<?').bind(now-LOG_DAYS*86400000),env.DB.prepare('DELETE FROM logs WHERE device_code=? AND id<=(SELECT id FROM logs WHERE device_code=? ORDER BY id DESC LIMIT 1 OFFSET ?)').bind(lease.device,lease.device,LOG_KEEP)]);
 return json({ok:true});
}
// Approximate location from Cloudflare (city, region, country); the IP itself is never stored.
const place=request=>{const cf=request.cf||{};const text=[cf.city,cf.region,cf.country].filter(v=>typeof v==='string'&&v).join(', ');return text?text.slice(0,100):null;};
// Remembered computers: the full admin key once creates an HttpOnly cookie; afterwards a PIN unlocks a 12 h access token
// on that browser only. Five wrong PINs forget the computer. The PIN is stored only hashed with the cookie's random id.
const SESSION_DAYS=90,ACCESS_HOURS=12,PIN_TRIES=5,COOKIE='fz_admin';
const sessionId=request=>(request.headers.get('Cookie')||'').match(/(?:^|;\s*)fz_admin=([A-Za-z0-9_-]{43})(?:;|$)/)?.[1]||null;
const cookie=(value,maxAge)=>COOKIE+'='+value+'; Path=/; Max-Age='+maxAge+'; HttpOnly; Secure; SameSite=Strict';
const withCookie=(data,status,setCookie)=>Response.json(data,{status,headers:{'Cache-Control':'private, no-store','X-Content-Type-Options':'nosniff',...(setCookie?{'Set-Cookie':setCookie}:{})}});
const pinHash=(id,pin)=>hash('FarmZilla-admin-pin|'+id+'|'+pin);
async function newAccess(env,idHash,now){const token='FZS.'+b64(crypto.getRandomValues(new Uint8Array(32)));await env.DB.prepare('UPDATE admin_sessions SET failed=0,access_hash=?,access_expires=? WHERE id_hash=?').bind(await hash(token),now+ACCESS_HOURS*3600000,idHash).run();return token;}
async function adminSession(request,env,path,now){
 const id=sessionId(request),idHash=id?await hash(id):null;
 const row=idHash?await env.DB.prepare('SELECT pin_hash,expires_at,failed FROM admin_sessions WHERE id_hash=?').bind(idHash).first():null;
 const valid=!!row&&row.expires_at>now;
 if(path==='/v1/admin-session/status'&&request.method==='GET')return withCookie({remembered:valid},200,id&&!valid?cookie('',0):null);
 if(path==='/v1/admin-session/enroll'&&request.method==='POST'){
  if(!await admin(request,env,true))return json({error:'Chave administrativa necessária'},401);
  const d=await body(request);if(typeof d.pin!=='string'||!/^\d{4,8}$/.test(d.pin))return json({error:'O PIN deve ter de 4 a 8 números'},400);
  const fresh=b64(crypto.getRandomValues(new Uint8Array(32))),freshHash=await hash(fresh);
  await env.DB.batch([env.DB.prepare('DELETE FROM admin_sessions WHERE expires_at<?').bind(now),env.DB.prepare('INSERT INTO admin_sessions(id_hash,pin_hash,created_at,expires_at) VALUES(?,?,?,?)').bind(freshHash,await pinHash(fresh,d.pin),now,now+SESSION_DAYS*86400000),env.DB.prepare('DELETE FROM admin_sessions WHERE id_hash NOT IN (SELECT id_hash FROM admin_sessions ORDER BY created_at DESC LIMIT 10)')]);
  return withCookie({token:await newAccess(env,freshHash,now)},200,cookie(fresh,SESSION_DAYS*86400));
 }
 if(path==='/v1/admin-session/unlock'&&request.method==='POST'){
  if(!valid)return withCookie({error:'Este computador não está lembrado; entre com a chave administrativa'},401,id?cookie('',0):null);
  const d=await body(request);
  if(typeof d.pin!=='string'||!/^\d{4,8}$/.test(d.pin)||await pinHash(id,d.pin)!==row.pin_hash){const failed=row.failed+1;
   if(failed>=PIN_TRIES){await env.DB.prepare('DELETE FROM admin_sessions WHERE id_hash=?').bind(idHash).run();return withCookie({error:'PIN errado 5 vezes; este computador foi esquecido. Entre com a chave administrativa.'},401,cookie('',0));}
   await env.DB.prepare('UPDATE admin_sessions SET failed=? WHERE id_hash=?').bind(failed,idHash).run();return json({error:'PIN incorreto ('+(PIN_TRIES-failed)+' tentativa(s) restante(s))'},401);}
  return json({token:await newAccess(env,idHash,now)});
 }
 if(path==='/v1/admin-session/forget'&&request.method==='POST'){if(idHash)await env.DB.prepare('DELETE FROM admin_sessions WHERE id_hash=?').bind(idHash).run();return withCookie({ok:true},200,cookie('',0));}
 return json({error:'Rota não encontrada'},404);
}
export async function control(request,env,licenseKey=keys.license){
 const url=new URL(request.url),path=url.pathname,now=Date.now();
 if(url.protocol!=='https:')return json({error:'HTTPS obrigatório'},400);
 if(path==='/admin'&&request.method==='GET')return new Response(adminPage,{headers:{'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store','Referrer-Policy':'no-referrer','X-Frame-Options':'DENY','X-Content-Type-Options':'nosniff'}});
 if(!path.startsWith('/v1/'))return null;
 if(!env.DB)return json({error:'Serviço ainda não configurado'},503);
 try{
  if(path.startsWith('/v1/admin-session/'))return await adminSession(request,env,path,now);
  if(path.startsWith('/v1/admin/')){
   if(!await admin(request,env))return json({error:'Acesso administrativo necessário'},401);
   if(path==='/v1/admin/devices'&&request.method==='GET'){const data=await env.DB.prepare("SELECT code,name,status,created_at,updated_at,last_seen,place,features,log_enabled,hardware<>'' AS registered FROM devices WHERE status<>'pending' ORDER BY updated_at DESC LIMIT 500").all();return json({devices:data.results.map(r=>({...r,features:featureList(r.features),log:r.log_enabled===1,registered:r.registered===1}))});}
   if(path==='/v1/admin/logs'&&request.method==='GET'){const device=url.searchParams.get('device'),limit=Math.min(Math.max(parseInt(url.searchParams.get('limit')||'1000',10)||1000,1),LOG_KEEP);if(!codeOk(device))return json({error:'Dados inválidos'},400);const data=await env.DB.prepare('SELECT at,version,line FROM logs WHERE device_code=? ORDER BY id DESC LIMIT ?').bind(device,limit).all();return json({lines:data.results.reverse()});}
   // Delete (0.38.0): any computer can be deleted; a deleted PC loses its license on its next check and only comes back if
   // its code is activated again. all=true clears the old waiting requests.
   if(path==='/v1/admin/remove'&&request.method==='POST'){const data=await body(request);const codes=data.all===true?(await env.DB.prepare("SELECT code FROM devices WHERE status='pending'").all()).results.map(r=>r.code):[data.device];if(!codes.length)return json({removed:0});if(codes.some(c=>!codeOk(c)))return json({error:'Dados inválidos'},400);
    let removed=0;for(const code of codes){const row=await env.DB.prepare('SELECT status FROM devices WHERE code=?').bind(code).first();if(!row)continue;await env.DB.batch([env.DB.prepare('DELETE FROM challenges WHERE device_code=?').bind(code),env.DB.prepare('DELETE FROM logs WHERE device_code=?').bind(code),env.DB.prepare('DELETE FROM devices WHERE code=?').bind(code),env.DB.prepare('INSERT INTO audit(device_code,action,at) VALUES(?,?,?)').bind(code,'removed',now)]);removed++;}
    return json({removed});}
   // Activation by code (0.38.0): the customer sends the code shown by FarmZilla; the owner pastes it here. The PC's key
   // is bound on its first check (the code is a hash of its hardware and key, so no other PC can take it).
   if(path==='/v1/admin/activate'&&request.method==='POST'){const data=await body(request);const code=typeof data.device==='string'?data.device.trim().toUpperCase():'';const features=data.features??FEATURES;if(!codeOk(code)||typeof (data.name??'')!=='string'||(data.name??'').length>100||!featuresOk(features))return json({error:'Código inválido. Cole o código completo (BGC2-…) que o FarmZilla mostra.'},400);
    const existed=!!await env.DB.prepare('SELECT code FROM devices WHERE code=?').bind(code).first();
    await env.DB.batch([env.DB.prepare("INSERT INTO devices(code,hardware,public_jwk,name,status,created_at,updated_at,features) VALUES(?,'','',?,'allowed',?,?,?) ON CONFLICT(code) DO UPDATE SET status='allowed',name=excluded.name,features=excluded.features,updated_at=excluded.updated_at").bind(code,data.name??'',now,now,features.join(',')),env.DB.prepare('INSERT INTO audit(device_code,action,at) VALUES(?,?,?)').bind(code,'activated',now)]);return json({ok:true,existed});}
   if(path==='/v1/admin/device'&&request.method==='POST'){const data=await body(request);if(!codeOk(data.device)||!['allowed','blocked'].includes(data.status)||typeof data.name!=='string'||data.name.length>100||(data.features!==undefined&&!featuresOk(data.features))||(data.log!==undefined&&typeof data.log!=='boolean'))return json({error:'Dados inválidos'},400);const row=await env.DB.prepare('SELECT features,log_enabled FROM devices WHERE code=?').bind(data.device).first();if(!row)return json({error:'Computador não encontrado; ative pelo código'},404);
    const features=data.features?data.features.join(','):row.features,log=data.log===undefined?row.log_enabled:data.log?1:0;await env.DB.batch([env.DB.prepare('UPDATE devices SET status=?,name=?,features=?,log_enabled=?,updated_at=? WHERE code=?').bind(data.status,data.name,features,log,now,data.device),env.DB.prepare('INSERT INTO audit(device_code,action,at) VALUES(?,?,?)').bind(data.device,data.status,now)]);return json({ok:true});}
   return json({error:'Rota não encontrada'},404);
  }
  if(path==='/v1/device/log'&&request.method==='POST')return await deviceLog(request,env,licenseKey,now);
  if(path==='/v1/device/register'&&request.method==='POST'){
   const d=await body(request),j=d.publicKey;
   if(!codeOk(d.device)||typeof d.hardware!=='string'||!/^BGH2-[A-F0-9]{64}$/.test(d.hardware)||!j||j.kty!=='RSA'||j.e!=='AQAB'||typeof j.n!=='string'||!/^[A-Za-z0-9_-]{342}$/.test(j.n)||unb64(j.n).length!==256||d.device!==await deviceCode(d.hardware,j))return json({error:'Identificação do PC inválida'},400);
   const jwk=JSON.stringify({kty:'RSA',n:j.n,e:j.e});
   // No waiting request is stored any more (0.38.0): an unknown code is only answered; the owner activates it by code.
   if(!await env.DB.prepare('SELECT code FROM devices WHERE code=?').bind(d.device).first())return json({error:'Computador ainda não ativado. Copie o código e envie ao administrador.',status:'pending'},403);
   await env.DB.prepare("UPDATE devices SET hardware=?,public_jwk=? WHERE code=? AND hardware=''").bind(d.hardware,jwk,d.device).run();
   const row=await env.DB.prepare('SELECT status,hardware,public_jwk FROM devices WHERE code=?').bind(d.device).first();
   if(row.hardware===d.hardware&&row.public_jwk===jwk){const where=place(request);if(where)await env.DB.prepare('UPDATE devices SET place=? WHERE code=?').bind(where,d.device).run();}
   if(row.hardware!==d.hardware||row.public_jwk!==jwk)return json({error:'Identificação divergente'},403);
   if(row.status!=='allowed')return json({error:row.status==='blocked'?'Computador bloqueado pelo administrador':'Aguardando liberação pelo administrador',status:row.status},403);
   await env.DB.prepare('DELETE FROM challenges WHERE expires_at<?').bind(now).run();
   const nonce=b64(crypto.getRandomValues(new Uint8Array(32)));
   // At most one outstanding challenge per device bounds repeated polling storage.
   await env.DB.batch([env.DB.prepare('DELETE FROM challenges WHERE device_code=?').bind(d.device),env.DB.prepare('INSERT INTO challenges(nonce,device_code,expires_at) VALUES(?,?,?)').bind(nonce,d.device,now+120000)]);
   return json({nonce});
  }
  if(path==='/v1/device/lease'&&request.method==='POST'){
   const d=await body(request);if(!codeOk(d.device)||typeof d.nonce!=='string'||!/^[A-Za-z0-9_-]{43}$/.test(d.nonce)||typeof d.signature!=='string'||d.signature.length>400)return json({error:'Pedido inválido'},400);
   const row=await env.DB.prepare('SELECT status,public_jwk FROM devices WHERE code=?').bind(d.device).first();if(!row||row.status!=='allowed')return json({error:'PC sem autorização',status:'blocked'},403);
   const key=await crypto.subtle.importKey('jwk',JSON.parse(row.public_jwk),{name:'RSASSA-PKCS1-v1_5',hash:'SHA-256'},false,['verify']);
   if(!await crypto.subtle.verify('RSASSA-PKCS1-v1_5',key,unb64(d.signature),enc.encode('Bagaceira-lease-v2\n'+d.device+'\n'+d.nonce)))return json({error:'Prova do computador inválida'},403);
   const challenge=await env.DB.prepare('DELETE FROM challenges WHERE nonce=? AND device_code=? AND expires_at>=? RETURNING nonce').bind(d.nonce,d.device,now).first();if(!challenge)return json({error:'Pedido expirado ou já utilizado'},403);
   await env.DB.prepare('UPDATE devices SET last_seen=? WHERE code=?').bind(now,d.device).run();
   // Check state again after consuming the challenge, including a concurrent administrator block.
   const state=await env.DB.prepare('SELECT status,features,log_enabled FROM devices WHERE code=?').bind(d.device).first();if(state?.status!=='allowed')return json({error:'PC bloqueado',status:'blocked'},403);
   // format 3 (FarmZilla 0.38.0+) also receives the lifetime license; older clients keep the 24 h lease only.
   return json(d.format===3?{lease:await lease(env,d.device,now),license:await license(env,d.device,state,now)}:{lease:await lease(env,d.device,now)});
  }
  return json({error:'Rota não encontrada'},404);
 }catch{return json({error:'Serviço temporariamente indisponível'},503);}
}
