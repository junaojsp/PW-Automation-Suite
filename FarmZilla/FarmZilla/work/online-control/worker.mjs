import {control,checkLease} from './control.mjs';
import keys from './keys.mjs';
const decode=s=>Uint8Array.from(atob(s.replace(/-/g,'+').replace(/_/g,'/')),c=>c.charCodeAt(0));
let updateKey;
export default {async fetch(request,env){
 const route=await control(request,env);if(route)return route;
 const fail=status=>Response.json({error:'Acesso não autorizado ou atualização indisponível'},{status,headers:{'Cache-Control':'private, no-store'}});
 if(request.method!=='GET')return fail(405);
 const auth=request.headers.get('Authorization');if(!auth?.startsWith('Bearer ')||!await checkLease(auth.slice(7),env,keys.license))return fail(401);
 try{
  const url=new URL(request.url),manifestResponse=await env.ASSETS.fetch(new Request(new URL('/update.json',url)));if(!manifestResponse.ok)throw Error();const bytes=await manifestResponse.arrayBuffer();if(bytes.byteLength>65536)throw Error();
  const envelope=JSON.parse(new TextDecoder().decode(bytes)),payload=decode(envelope.payload);if(!updateKey)updateKey=await crypto.subtle.importKey('jwk',keys.update,{name:'RSASSA-PKCS1-v1_5',hash:'SHA-256'},false,['verify']);if(!await crypto.subtle.verify('RSASSA-PKCS1-v1_5',updateKey,decode(envelope.signature),payload))throw Error();
  const m=JSON.parse(new TextDecoder().decode(payload)),path='/releases/v'+m.version+'/Bagaceira.exe';if(m.product!=='AvalonInterno'||m.architecture!=='x86'||!/^\d+\.\d+\.\d+$/.test(m.version)||m.url!==url.origin+path||!Number.isInteger(m.size)||m.size<1024||m.size>25*1024*1024||!/^[A-Fa-f0-9]{64}$/.test(m.sha256))throw Error();
  const headers={'Cache-Control':'private, no-store','X-Content-Type-Options':'nosniff'};
  if(url.pathname==='/update.json'&&!url.search)return new Response(bytes,{headers:{...headers,'Content-Type':'application/json'}});
  if(url.pathname!==path||url.search)return fail(404);
  const asset=await env.ASSETS.fetch(new Request(new URL(path,url)));if(!asset.ok)throw Error();const binary=await asset.arrayBuffer();if(binary.byteLength!==m.size)throw Error();const digest=await crypto.subtle.digest('SHA-256',binary);if(Array.from(new Uint8Array(digest),b=>b.toString(16).padStart(2,'0')).join('')!==m.sha256.toLowerCase())throw Error();
  return new Response(binary,{headers:{...headers,'Content-Type':'application/octet-stream','Content-Disposition':'attachment; filename="Bagaceira.exe"'}});
 }catch{return fail(503);}
}};
