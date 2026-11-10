export const adminPage=`<!doctype html><html lang="pt-BR"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>FarmZilla · Painel</title>
<style>
:root{--bg:#0b0c10;--panel:#15161c;--panel2:#1c1e25;--panel3:#23252e;--line:#2a2c36;--line2:#363946;--text:#eeeef3;--muted:#9a9cab;--faint:#6e7080;--accent:#f16476;--accent2:#c9475a;--ok:#3ecf8e;--warn:#f0b54a;--bad:#f16476;--info:#7aa7ff;--radius:14px}
*{box-sizing:border-box}[hidden]{display:none!important}
body{margin:0;background:radial-gradient(1100px 520px at 8% -12%,#2c1219 0,transparent 62%),radial-gradient(900px 480px at 110% 0,#141a2c 0,transparent 60%),var(--bg);color:var(--text);font:14px/1.45 "Segoe UI",system-ui,sans-serif;min-height:100vh}
button,input{font:inherit;color:inherit}button{cursor:pointer}
.wrap{max-width:1180px;margin:0 auto;padding:26px 16px 70px}
header{display:flex;align-items:center;gap:12px;margin-bottom:22px}
.badge-fz{width:40px;height:38px;border-radius:11px;display:grid;place-items:center;font-weight:800;color:#23090f;background:linear-gradient(180deg,#ff8a98,#c9475a);box-shadow:0 4px 14px rgba(241,100,118,.38),inset 0 1px 0 rgba(255,255,255,.4)}
header h1{margin:0;font-size:21px;letter-spacing:.2px}header h1 span{color:var(--accent)}header .sub{color:var(--muted);font-size:13px}.spacer{flex:1}
.btn{display:inline-flex;align-items:center;gap:6px;border:1px solid var(--line2);background:linear-gradient(180deg,#262832,#1c1d24);border-radius:10px;padding:8px 14px;box-shadow:inset 0 1px 0 rgba(255,255,255,.05),0 2px 6px rgba(0,0,0,.3);transition:.15s;white-space:nowrap}
.btn:hover{border-color:#4a4d5c;background:linear-gradient(180deg,#2c2e39,#20222a)}.btn:disabled{opacity:.5;cursor:default}
.btn.primary{border-color:var(--accent2);background:linear-gradient(180deg,#f57a8a,#d9536a);color:#23090f;font-weight:700}.btn.primary:hover{background:linear-gradient(180deg,#f88796,#e05d73)}
.btn.ok{color:var(--ok);border-color:#235f45}.btn.bad{color:var(--bad);border-color:#5d2731}.btn.ghost{background:none;box-shadow:none}.btn.sm{padding:5px 10px;font-size:13px;border-radius:8px}
.card{background:linear-gradient(180deg,var(--panel2),var(--panel));border:1px solid var(--line);border-radius:var(--radius);box-shadow:0 10px 30px rgba(0,0,0,.35),inset 0 1px 0 rgba(255,255,255,.04)}
.auth{max-width:430px;margin:12vh auto 0;padding:28px}.auth h2{margin:0 0 6px;font-size:18px}.auth p{margin:0 0 18px;color:var(--muted)}.auth .check{display:flex;align-items:center;gap:8px;margin:14px 0 0;color:var(--muted);cursor:pointer}.auth .links{display:flex;justify-content:space-between;margin-top:16px;font-size:13px}.link{border:0;background:none;color:var(--muted);padding:0;text-decoration:underline}.link:hover{color:var(--text)}#pin,#newpin{letter-spacing:6px;font-size:18px;text-align:center}
.field{width:100%;background:#0e0f13;border:1px solid var(--line);border-radius:10px;padding:10px 12px;outline:none;transition:.15s}.field:focus{border-color:var(--accent);box-shadow:0 0 0 3px rgba(241,100,118,.15)}
.row{display:flex;gap:10px}.row .field{flex:1}
.msg{min-height:22px;color:var(--muted);margin:10px 0 0!important}.msg.err{color:var(--bad)!important}
.stats{display:grid;grid-template-columns:repeat(5,1fr);gap:12px;margin-bottom:16px}
.stat{padding:15px 16px;position:relative;overflow:hidden}.stat b{display:block;font-size:26px;line-height:1.1}.stat span{color:var(--muted);font-size:11.5px;text-transform:uppercase;letter-spacing:.7px}
.stat::after{content:"";position:absolute;right:-18px;top:-18px;width:70px;height:70px;border-radius:50%;background:currentColor;opacity:.06}
.stat.ok{color:var(--ok)}.stat.bad{color:var(--bad)}.stat.info{color:var(--info)}.stat.acc{color:var(--accent)}.stat span{color:var(--muted)}
.activate{padding:18px;margin-bottom:16px;display:grid;grid-template-columns:1fr auto;gap:14px;align-items:end}
.activate h2{margin:0 0 2px;font-size:16px}.activate p{margin:0 0 12px;color:var(--muted);font-size:13px}
.activate .inputs{display:grid;grid-template-columns:2.2fr 1fr;gap:10px}.activate .code-in{font:13px ui-monospace,Consolas,monospace;letter-spacing:.3px}
.tools{display:flex;gap:8px;flex-wrap:wrap;margin-top:10px}
.chip{display:inline-flex;align-items:center;gap:7px;padding:5px 11px;border-radius:999px;border:1px solid var(--line2);background:#101116;color:var(--muted);font-size:13px;user-select:none;transition:.15s}
.chip input{display:none}.chip .box{width:14px;height:14px;border-radius:4px;border:1px solid #4a4d5c;display:grid;place-items:center;font-size:10px}
.chip.on{color:var(--text);border-color:#5d2731;background:rgba(241,100,118,.1)}.chip.on .box{background:var(--accent);border-color:var(--accent);color:#23090f}.chip.on .box::after{content:"✓";font-weight:800}
.chip.busy{opacity:.55;pointer-events:none}button.chip{cursor:pointer}
.toolbar{display:flex;gap:10px;align-items:center;flex-wrap:wrap;padding:12px;margin-bottom:12px}
.tabs{display:flex;background:#0e0f13;border:1px solid var(--line);border-radius:11px;padding:3px}
.tabs button{border:0;background:none;padding:6px 13px;border-radius:8px;color:var(--muted)}.tabs button.on{background:var(--panel3);color:var(--text);box-shadow:0 1px 3px rgba(0,0,0,.4)}
.toolbar .field{max-width:300px;padding:8px 12px}
.list{display:grid;gap:10px}
.pc{padding:14px 16px;display:grid;grid-template-columns:minmax(220px,1.4fr) minmax(150px,1fr) minmax(200px,1.2fr) auto;gap:14px;align-items:center}
.pc.blocked{opacity:.78}
.pc .name{background:transparent;border:1px solid transparent;border-radius:8px;padding:4px 8px;margin-left:-9px;width:100%;max-width:260px;font-weight:650;font-size:15px}
.pc .name:hover{border-color:var(--line)}.pc .name:focus{border-color:var(--accent);background:#0e0f13;outline:none}.pc .name::placeholder{color:var(--faint);font-weight:400;font-style:italic}
.code{display:flex;align-items:center;gap:6px;font:12px ui-monospace,Consolas,monospace;color:var(--muted);margin-top:3px}
.copy{border:0;background:none;color:var(--muted);padding:2px 5px;border-radius:5px;font-size:12px}.copy:hover{color:var(--text);background:var(--panel3)}
.pill{display:inline-flex;align-items:center;gap:6px;padding:3px 10px;border-radius:999px;font-size:12px;font-weight:650;border:1px solid}
.pill.allowed{color:var(--ok);border-color:#235f45;background:rgba(62,207,142,.08)}.pill.blocked{color:var(--bad);border-color:#5d2731;background:rgba(241,100,118,.08)}.pill.new{color:var(--warn);border-color:#5f4a1f;background:rgba(240,181,74,.08)}
.meta{display:grid;gap:4px;font-size:13px}.meta .seen{display:flex;align-items:center;gap:8px;white-space:nowrap}
.dot{width:8px;height:8px;border-radius:50%;background:#4a4c58;flex:none}.dot.on{background:var(--ok);box-shadow:0 0 0 3px rgba(62,207,142,.18)}
.muted{color:var(--muted)}.faint{color:var(--faint);font-size:12px}
.pc .tools{margin:0}
.actions{display:flex;gap:6px;justify-content:flex-end;flex-wrap:wrap}
.empty{padding:44px;text-align:center;color:var(--muted)}
.overlay{position:fixed;inset:0;background:rgba(5,6,8,.72);backdrop-filter:blur(3px);display:flex;align-items:center;justify-content:center;padding:16px;z-index:10}
.modal{width:min(1000px,100%);height:min(80vh,820px);display:flex;flex-direction:column}
.modal-head{display:flex;align-items:center;gap:10px;padding:14px 16px;border-bottom:1px solid var(--line);flex-wrap:wrap}.modal-head h2{margin:0;font-size:16px;flex:1;min-width:200px}
.modal-head label{display:flex;align-items:center;gap:6px;color:var(--muted)}.modal-head .field{max-width:220px;padding:6px 10px}
#log{flex:1;overflow:auto;padding:10px 0;font:12.5px/1.55 ui-monospace,Consolas,monospace;background:#0a0b0e;border-radius:0 0 var(--radius) var(--radius)}
#log div{padding:1px 16px;white-space:pre-wrap;overflow-wrap:anywhere}#log div:hover{background:rgba(255,255,255,.03)}#log .t{color:var(--faint)}#log .err{color:#ff9aa7}#log .info{color:var(--muted)}
.toast{position:fixed;bottom:18px;left:50%;transform:translateX(-50%);background:var(--panel3);border:1px solid var(--line2);padding:10px 16px;border-radius:10px;box-shadow:0 8px 24px rgba(0,0,0,.45);z-index:20;max-width:calc(100% - 32px)}
@media(max-width:980px){.pc{grid-template-columns:1fr 1fr}.actions{justify-content:flex-start}}
@media(max-width:720px){.stats{grid-template-columns:repeat(2,1fr)}.activate{grid-template-columns:1fr}.activate .inputs{grid-template-columns:1fr}.pc{grid-template-columns:1fr}.toolbar .field{max-width:none;flex:1 1 100%}.row{flex-wrap:wrap}}
</style>
<div class="wrap">
<header><div class="badge-fz">FZ</div><div><h1>Farm<span>Zilla</span> · Painel</h1><div class="sub">Ativação vitalícia por código, ferramentas e registros</div></div><div class="spacer"></div><button id="logout" class="btn" hidden>Sair</button></header>
<form id="pinform" class="card auth" hidden><h2>Entrar com PIN</h2><p>Este computador está lembrado. Digite o seu PIN.</p><div class="row"><input id="pin" class="field" type="password" inputmode="numeric" pattern="[0-9]{4,8}" maxlength="8" autocomplete="off" placeholder="••••" required><button class="btn primary">Entrar</button></div><p id="pinmessage" class="msg"></p><div class="links"><button type="button" id="usekey" class="link">Usar chave administrativa</button><button type="button" id="forget" class="link">Esquecer este computador</button></div></form>
<form id="login" class="card auth" hidden><h2>Entrar</h2><p>Use a chave administrativa. Ela não fica salva neste navegador.</p><div class="row"><input id="secret" class="field" type="password" autocomplete="off" placeholder="Chave administrativa" required><button class="btn primary">Conectar</button></div><label class="check"><input id="remember" type="checkbox"> Lembrar este computador e entrar com PIN</label><div id="pinrow" class="row" style="margin-top:10px" hidden><input id="newpin" class="field" type="password" inputmode="numeric" pattern="[0-9]{4,8}" maxlength="8" autocomplete="new-password" placeholder="Crie um PIN (4 a 8 números)"></div><p id="message" class="msg"></p></form>
<section id="panel" hidden>
<div class="stats"><div class="card stat acc"><b id="sTotal">0</b><span>Computadores</span></div><div class="card stat ok"><b id="sAllowed">0</b><span>Liberados</span></div><div class="card stat bad"><b id="sBlocked">0</b><span>Bloqueados</span></div><div class="card stat info"><b id="sRota">0</b><span>Com Mat Farm</span></div><div class="card stat ok"><b id="sOnline">0</b><span>Online agora</span></div></div>
<form id="activate" class="card activate"><div><h2>Ativar computador</h2><p>Cole o código que o cliente copiou no FarmZilla (BGC2-…). A liberação é vitalícia; escolha as ferramentas dele.</p>
<div class="inputs"><input id="newcode" class="field code-in" placeholder="BGC2-…" autocomplete="off" spellcheck="false" required><input id="newname" class="field" maxlength="100" placeholder="Nome do cliente"></div>
<div class="tools" id="newtools"></div></div><button class="btn primary" id="activatebtn">Ativar</button></form>
<div class="card toolbar"><div class="tabs" id="tabs"><button data-f="all" class="on">Todos</button><button data-f="allowed">Liberados</button><button data-f="blocked">Bloqueados</button></div><input id="search" class="field" placeholder="Buscar nome, código ou local"><div class="spacer"></div><span id="updated" class="muted"></span><button id="refresh" class="btn">Atualizar</button></div>
<div id="rows" class="list"></div><div id="empty" class="card empty" hidden>Nenhum computador neste filtro.</div>
</section></div>
<div id="logview" class="overlay" hidden><div class="card modal"><div class="modal-head"><h2 id="logtitle"></h2><input id="logsearch" class="field" placeholder="Filtrar texto"><label><input id="onlyerr" type="checkbox"> Só falhas</label><button id="logreload" class="btn sm">Atualizar registro</button><button id="logclose" class="btn sm">Fechar</button></div><div id="log"></div></div></div>
<script type="module">
let key='',devices=[],filter='all';const $=id=>document.getElementById(id);
const el=(tag,cls,text)=>{const e=document.createElement(tag);if(cls)e.className=cls;if(text!=null)e.textContent=text;return e;};
const LABEL={allowed:'Liberado',blocked:'Bloqueado'},ORDER={allowed:0,blocked:1},ONLINE=40*60000;
const TOOLS=[['controle','Espelho','Seguir, ataque, PT, bufar, teleporte e missões espelhados nos personagens'],['rota','Mat Farm','Rotas de coleta e farm automático de materiais']];
function toast(text){const t=el('div','toast',text);document.body.append(t);setTimeout(()=>t.remove(),2800);}
function ago(ms){if(!ms)return'nunca';const m=Math.round((Date.now()-ms)/60000);if(m<1)return'agora';if(m<60)return'há '+m+' min';const h=Math.round(m/60);if(h<24)return'há '+h+' h';return new Date(ms).toLocaleDateString('pt-BR');}
function shortCode(c){return c.slice(0,9)+'…'+c.slice(-6);}
async function api(path,body){const r=await fetch(path,{method:body?'POST':'GET',headers:{Authorization:'Bearer '+key,'Content-Type':'application/json'},body:body?JSON.stringify(body):undefined,cache:'no-store'});const d=await r.json();if(!r.ok)throw Error(d.error||'Não foi possível concluir');return d;}
function chip(label,title,on,onclick){const c=el(onclick?'button':'label','chip'+(on?' on':''));if(onclick)c.type='button';c.title=title;c.append(el('span','box'),el('span','',label));c.onclick=onclick;return c;}
// Activation form: tools chosen before activating (both on by default).
const chosen=new Set(TOOLS.map(t=>t[0]));
function renderNewTools(){$('newtools').replaceChildren(...TOOLS.map(([id,label,title])=>chip(label,title,chosen.has(id),()=>{chosen.has(id)?chosen.delete(id):chosen.add(id);renderNewTools();})));}
renderNewTools();
$('newcode').oninput=()=>{const v=$('newcode').value.replace(/\\s+/g,'').toUpperCase();if(v!==$('newcode').value)$('newcode').value=v;};
$('activate').onsubmit=async e=>{e.preventDefault();const device=$('newcode').value.trim().toUpperCase();if(!/^BGC2-[A-F0-9]{64}$/.test(device)){toast('Código incompleto: deve começar com BGC2- e ter 64 caracteres depois do traço.');return;}
 $('activatebtn').disabled=true;try{const r=await api('/v1/admin/activate',{device,name:$('newname').value.trim(),features:[...chosen]});toast(r.existed?'Computador reativado':'Computador ativado. Ele já pode abrir o FarmZilla.');$('newcode').value='';$('newname').value='';await refresh();}catch(err){toast(err.message);}finally{$('activatebtn').disabled=false;}};
async function save(d,changes){await api('/v1/admin/device',{device:d.code,status:d.status,name:d.name,features:d.features,log:d.log,...changes});}
function render(){const q=$('search').value.trim().toLowerCase();const now=Date.now();
 $('sTotal').textContent=devices.length;$('sAllowed').textContent=devices.filter(d=>d.status==='allowed').length;$('sBlocked').textContent=devices.filter(d=>d.status==='blocked').length;$('sRota').textContent=devices.filter(d=>d.status==='allowed'&&d.features.includes('rota')).length;$('sOnline').textContent=devices.filter(d=>d.last_seen&&now-d.last_seen<ONLINE).length;
 const list=devices.filter(d=>(filter==='all'||d.status===filter)&&(!q||(d.name||'').toLowerCase().includes(q)||d.code.toLowerCase().includes(q)||(d.place||'').toLowerCase().includes(q))).sort((a,b)=>ORDER[a.status]-ORDER[b.status]||(b.last_seen||b.created_at)-(a.last_seen||a.created_at));
 $('rows').replaceChildren();$('empty').hidden=list.length>0;
 for(const d of list){const card=el('div','card pc '+d.status);
  const who=el('div');const name=el('input','name');name.value=d.name;name.maxLength=100;name.placeholder='Sem nome';name.title='Clique para dar um nome';
  name.onkeydown=e=>{if(e.key==='Enter')name.blur();};name.onblur=async()=>{if(name.value===d.name)return;try{await save(d,{name:name.value});d.name=name.value;toast('Nome salvo');}catch(e){toast(e.message);name.value=d.name;}};
  const code=el('div','code',shortCode(d.code));code.title=d.code;const copy=el('button','copy','Copiar');copy.onclick=async()=>{try{await navigator.clipboard.writeText(d.code);toast('Código copiado');}catch{toast(d.code);}};code.append(copy);who.append(name,code);
  const meta=el('div','meta');const pills=el('div');pills.append(el('span','pill '+d.status,LABEL[d.status]));if(!d.registered){const p=el('span','pill new','Ainda não abriu');p.style.marginLeft='6px';p.title='Ativado pelo código; o PC ainda não abriu o FarmZilla.';pills.append(p);}meta.append(pills);
  const seen=el('div','seen');seen.append(el('span','dot'+(d.last_seen&&now-d.last_seen<ONLINE?' on':'')),el('span',d.last_seen?'':'muted',ago(d.last_seen)));seen.title=d.last_seen?'Última consulta: '+new Date(d.last_seen).toLocaleString('pt-BR'):'Ainda não consultou';meta.append(seen,el('div','faint',d.place||'Local desconhecido'));
  const tools=el('div','tools');
  for(const [id,label,title] of TOOLS){const c=chip(label,title+' — clique para liberar ou tirar',d.features.includes(id),async()=>{const next=d.features.includes(id)?d.features.filter(f=>f!==id):[...d.features,id];c.classList.add('busy');try{await save(d,{features:next});d.features=next;toast((d.name||shortCode(d.code))+': '+label+(next.includes(id)?' liberada':' retirada')+'. Vale quando o FarmZilla dele consultar (até 30 min).');render();}catch(e){toast(e.message);c.classList.remove('busy');}});tools.append(c);}
  const logChip=chip('Enviar registro','Liga ou desliga o envio do registro deste PC (desligado por padrão)',d.log,async()=>{logChip.classList.add('busy');try{await save(d,{log:!d.log});d.log=!d.log;toast('Registro '+(d.log?'ligado':'desligado')+' para '+(d.name||shortCode(d.code)));render();}catch(e){toast(e.message);logChip.classList.remove('busy');}});tools.append(logChip);
  const bar=el('div','actions');
  const button=(text,cls,fn)=>{const b=el('button','btn sm '+cls,text);b.onclick=async()=>{b.disabled=true;try{await fn();}catch(e){toast(e.message);b.disabled=false;}};bar.append(b);};
  button('Ver registro','ghost',()=>showLog(d.code,d.name||shortCode(d.code)));
  if(d.status==='blocked')button('Liberar','ok',async()=>{await save(d,{status:'allowed'});toast((d.name||shortCode(d.code))+': liberado');await refresh();});
  else button('Bloquear','bad',async()=>{await save(d,{status:'blocked'});toast((d.name||shortCode(d.code))+': bloqueado. O FarmZilla dele para na próxima consulta (até 30 min, quando online).');await refresh();});
  button('Deletar','bad',async()=>{if(!confirm('Deletar a liberação de '+(d.name||shortCode(d.code))+'?\\nO FarmZilla dele deixa de funcionar na próxima consulta e o registro dele é apagado. Para voltar, ative o código de novo.'))return;await api('/v1/admin/remove',{device:d.code});toast('Liberação deletada');await refresh();});
  card.append(who,meta,tools,bar);$('rows').append(card);}
}
async function refresh(){try{const data=await api('/v1/admin/devices');devices=data.devices;$('updated').textContent='Atualizado '+new Date().toLocaleTimeString('pt-BR',{hour:'2-digit',minute:'2-digit'});$('login').hidden=$('pinform').hidden=true;$('panel').hidden=false;$('logout').hidden=false;render();}catch(e){key='';devices=[];$('panel').hidden=true;$('logout').hidden=true;await entry(e.message);}}
// Entry screen: PIN when this browser is remembered (HttpOnly cookie), otherwise the administrative key.
async function entry(text,forceKey){let remembered=false;if(!forceKey){try{remembered=(await (await fetch('/v1/admin-session/status',{cache:'no-store'})).json()).remembered;}catch{}}$('pinform').hidden=!remembered;$('login').hidden=remembered;const m=remembered?$('pinmessage'):$('message');m.textContent=text||'';m.className='msg'+(text?' err':'');(remembered?$('pin'):$('secret')).focus();}
async function post(path,body){const r=await fetch(path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body),cache:'no-store'});const d=await r.json();if(!r.ok)throw Error(d.error||'Não foi possível concluir');return d;}
let logDevice='',logName='',logLines=[];const fail=/não|falh|erro|inválid|indisponível|recus|bloquead|cancelad|sem |expir|Diagnóstico/i;
function renderLog(){const q=$('logsearch').value.trim().toLowerCase();const lines=logLines.filter(l=>(!$('onlyerr').checked||fail.test(l.line))&&(!q||l.line.toLowerCase().includes(q)));$('log').replaceChildren();
 if(!logLines.length){$('log').append(el('div','info','Nenhuma linha recebida deste PC. O registro só é enviado com "Enviar registro" ligado (FarmZilla 0.38.0 ou mais novo).'));return;}
 if(!lines.length){$('log').append(el('div','info','Nenhuma linha com esse filtro.'));return;}
 for(const l of lines){const row=el('div',fail.test(l.line)?'err':'');row.append(el('span','t',new Date(l.at).toLocaleDateString('pt-BR',{day:'2-digit',month:'2-digit'})+' v'+l.version+'  '),document.createTextNode(l.line));$('log').append(row);}
 $('log').scrollTop=$('log').scrollHeight;}
async function showLog(code,label){logDevice=code;logName=label;$('logview').hidden=false;$('logtitle').textContent='Registro · '+label;$('log').replaceChildren(el('div','info','Carregando…'));try{logLines=(await api('/v1/admin/logs?device='+encodeURIComponent(code)+'&limit=2000')).lines;renderLog();}catch(e){$('log').replaceChildren(el('div','err',e.message));}}
$('logreload').onclick=()=>showLog(logDevice,logName);$('onlyerr').onchange=renderLog;$('logsearch').oninput=renderLog;$('logclose').onclick=()=>{$('logview').hidden=true;};$('logview').onclick=e=>{if(e.target===$('logview'))$('logview').hidden=true;};document.addEventListener('keydown',e=>{if(e.key==='Escape')$('logview').hidden=true;});
$('tabs').onclick=e=>{const b=e.target.closest('button');if(!b)return;filter=b.dataset.f;for(const x of $('tabs').children)x.classList.toggle('on',x===b);render();};$('search').oninput=render;
$('remember').onchange=()=>{$('pinrow').hidden=!$('remember').checked;$('newpin').required=$('remember').checked;if($('remember').checked)$('newpin').focus();};
$('login').onsubmit=async e=>{e.preventDefault();const secret=$('secret').value;$('message').textContent='Conectando…';$('message').className='msg';
 if($('remember').checked){const pin=$('newpin').value;if(!/^\\d{4,8}$/.test(pin)){$('message').textContent='O PIN deve ter de 4 a 8 números';$('message').className='msg err';return;}try{const r=await fetch('/v1/admin-session/enroll',{method:'POST',headers:{Authorization:'Bearer '+secret,'Content-Type':'application/json'},body:JSON.stringify({pin}),cache:'no-store'});const d=await r.json();if(!r.ok)throw Error(d.error||'Não foi possível concluir');key=d.token;toast('Computador lembrado. Da próxima vez, use só o PIN.');}catch(err){$('message').textContent=err.message;$('message').className='msg err';return;}}
 else key=secret;$('secret').value='';$('newpin').value='';$('remember').checked=false;$('pinrow').hidden=true;await refresh();};
$('pinform').onsubmit=async e=>{e.preventDefault();const pin=$('pin').value;$('pin').value='';$('pinmessage').textContent='Entrando…';$('pinmessage').className='msg';try{key=(await post('/v1/admin-session/unlock',{pin})).token;await refresh();}catch(err){await entry(err.message);}};
$('usekey').onclick=()=>entry('',true);$('forget').onclick=async()=>{try{await post('/v1/admin-session/forget',{});}catch{}await entry('Este computador foi esquecido.');};
$('refresh').onclick=refresh;
$('logout').onclick=()=>{key='';devices=[];logLines=[];$('rows').replaceChildren();$('log').replaceChildren();$('logview').hidden=true;$('panel').hidden=true;$('logout').hidden=true;entry('').then(()=>{$('message').textContent=$('pinmessage').textContent='Desconectado';});};
entry('');
setInterval(()=>{if(key&&!$('panel').hidden&&!document.activeElement?.matches('input'))refresh();},60000);
</script></html>`;
