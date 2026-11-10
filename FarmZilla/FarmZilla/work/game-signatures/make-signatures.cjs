// FarmZilla 0.50.0 — gera assinaturas curtas de bytes (com curingas) a partir do ElementClient.exe validado.
// Só leitura do exe. Saída: signatures.json e o arquivo C# GameSignatures.cs (tabela embutida no cliente).
// Uso: node make-signatures.cjs <ElementClient.exe> <saida.json> <GameSignatures.cs>
// Requer iced-x86 (pasta work/package do workspace original ou variável ICED).
const fs=require('fs'),path=require('path');
const {load,rttiName}=require('./pe.cjs');
const iced=require(process.env.ICED||'C:/Users/nyeds/Documents/Codex/2026-10-26/que/work/package');
const {Decoder,DecoderOptions}=iced;
const [exe,outJson,outCs]=process.argv.slice(2);
const P=load(exe),T=P.sec('.text'),TEXT_END=T.rva+T.vs;
const crypto=require('crypto');const sha=crypto.createHash('sha256').update(P.raw).digest('hex');

// Alvos: nome lógico -> RVA atual (perfil 34fca82c…).
const functions={
 FollowPost:0x44E8F0, AttackSelect:0x3FA3C0, AttackNormal:0x400050, MacroStart:0x3FD040, MacroStop:0x3FD130,
 SendData:0x514240, NpcClose:0x6FAAF0, NpcCancel:0x222BC0,
 TraceMatter:0x3FA070, TaskLookup:0x586B00, HintCommand:0x2B5680, TaskConfirmRefresh:0x2B5240, TaskConfirmCommand:0x2B50D0,
 AwardSelect:0x109050, AwardConfirm:0x108DF0, KeyMove:0x3D73C0, AutoPolicyGet:0x313620, AutoPolicyStop:0x3136E0,
 AutoPolicyChange:0x313790, Navigate:0x4DFF80
};
const globals={Root:0x9B870C, RotaRoot:0x116E55C, TeleportTable:0x9C1900, TeleportCount:0x9C190C, AutoKillName:0x866FD8, EmptyString:0x11BA970};
const vtables={FollowWork:0x8E78F0, ComboSkill:0x8E5688, DlgNpc:0x8DCD88, DlgTask:0x8E19F8, DlgTaskConfirm:0x8E1A94, DlgTaskList:0x8E1B30,
 Team:0x8EC3B8, TeamMember:0x8EC3B0, DlgMessageBox:0x8DC644, TaskInterface:0x8F3278, ItemTransmitScroll:0x8E852C, ItemTaskDice:0x8E9984};
// Deslocamentos de estruturas conferidos dentro de métodos do personagem (CECHostPlayer): se o jogo mudar o campo, o perfil é recusado.
const guards=[
 {name:'HostMacro',value:0x1D3C,inside:['MacroStart','MacroStop','AttackSelect','AttackNormal','TraceMatter','KeyMove']},
 {name:'HostTarget',value:0x5FC,inside:['AttackSelect','AttackNormal','MacroStart','TraceMatter','KeyMove']},
 {name:'HostVertical',value:0xB78,inside:['KeyMove','TraceMatter','MacroStart','AttackSelect','AttackNormal']},
 {name:'HostId',value:0x4F0,inside:['AttackSelect','AttackNormal','MacroStart','MacroStop','TraceMatter','KeyMove','SendData']},
 {name:'HostWork',value:0x1D1C,inside:['TraceMatter','KeyMove','AttackSelect','AttackNormal','MacroStart','MacroStop']},
 {name:'HostTasks',value:0x1D24,inside:['TaskLookup','TraceMatter','AttackSelect','MacroStart','KeyMove']},
];

function decodeAt(rva,max){const out=[];const d=new Decoder(32,P.img.subarray(rva,Math.min(rva+max,TEXT_END)),DecoderOptions.None);d.ip=BigInt(P.imageBase+rva);
 while(d.canDecode){const i=d.decode();if(i.isInvalid){i.free();break;}const co=d.getConstantOffsets(i);
  out.push({rva:Number(i.ip)-P.imageBase,len:i.length,rel32:(i.isCallNear||i.isJmpNear||i.isJccNear)&&i.length>=5,disp:co.hasDisplacement?{o:co.displacementOffset,s:co.displacementSize}:null,
   value:Number(i.memoryDisplacement&0xffffffffn),ret:i.flowControl===iced.FlowControl.Return});co.free();i.free();}
 d.free();return out;}
// Máscara: true = byte fixo. Curinga em relocações (endereços absolutos) e em destinos relativos de call/jmp/jcc.
function maskFor(ins){const m=[];for(const i of ins){for(let k=0;k<i.len;k++)m.push(true);}
 const start=ins[0].rva;for(const i of ins){if(i.rel32){for(let k=i.len-4;k<i.len;k++)m[i.rva-start+k]=false;}}
 for(let r=start-3;r<start+m.length;r++)if(P.relocs.has(r))for(let k=0;k<4;k++)if(r+k-start>=0&&r+k-start<m.length)m[r+k-start]=false;
 return m;}
function patternText(rva,m){return m.map((f,k)=>f?P.img[rva+k].toString(16).toUpperCase().padStart(2,'0'):'??').join(' ');}
// Todas as posições do .text que casam o padrão (para uniqueness).
function matches(rva,m,limit){const fixed=[];for(let k=0;k<m.length;k++)if(m[k])fixed.push(k);
 // âncora: primeiro trecho fixo de até 4 bytes
 let a=fixed[0];const bytes=P.img;const first=bytes[rva+a];const hits=[];
 for(let p=T.rva;p+m.length<=TEXT_END;p++){if(bytes[p+a]!==first)continue;let ok=true;for(const k of fixed){if(bytes[p+k]!==bytes[rva+k]){ok=false;break;}}if(ok){hits.push(p);if(hits.length>limit)break;}}
 return hits;}
function fixedCount(m){return m.filter(x=>x).length;}
// Menor prefixo (em instruções inteiras) único no .text, com pelo menos 12 bytes fixos.
function unique(rva,maxBytes){const ins=decodeAt(rva,maxBytes+16);let take=[];
 for(const i of ins){take.push(i);const m=maskFor(take);if(m.length>maxBytes)return null;if(fixedCount(m)<12)continue;
  const h=matches(rva,m,1);if(h.length===1&&h[0]===rva)return {rva,mask:m,len:m.length};if(i.ret)return null;}
 return null;}
// Instrução que contém o dword em q (xref de endereço absoluto)
// Fronteira da instrução que contém q: decodifica a partir de vários pontos anteriores (o x86 se ressincroniza) e vota.
function instrAt(q){const votes=new Map();for(let st=q-48;st<=q-16;st++){if(st<T.rva)continue;const ins=decodeAt(st,64);for(const i of ins){if(i.rva<=q&&i.rva+i.len>=q+4){votes.set(i.rva,(votes.get(i.rva)||0)+1);break;}if(i.rva>q)break;}}
 let best=null,n=0;for(const [k,v] of votes)if(v>n){n=v;best=k;}if(best==null||n<24)return null;
 const d=new Decoder(32,P.img.subarray(best,best+16),DecoderOptions.None);const x=d.decode();const co=d.getConstantOffsets(x);const ok=(co.hasDisplacement&&co.displacementSize===4&&best+co.displacementOffset===q)||(co.hasImmediate&&co.immediateSize===4&&best+co.immediateOffset===q);co.free();x.free();d.free();return ok?best:null;}

const result={sha256:P.raw.length&&sha,imageBase:P.imageBase,sizeOfImage:P.sizeOfImage,text:{rva:T.rva,size:T.vs},targets:[],guards:[]};
// Funções: assinatura do início + (opcional) chamada única.
const callers={};for(const k in functions)callers[functions[k]]=[];
for(let p=T.rva;p+5<=TEXT_END;p++){if(P.img[p]!==0xE8)continue;const t=p+5+P.img.readInt32LE(p+1);if(callers[t])callers[t].push(p);}
for(const [name,rva] of Object.entries(functions)){
 const sigs=[];const u=unique(rva,64);if(u)sigs.push({kind:'fn',pattern:patternText(rva,u.mask),offset:0});
 // chamada: padrão iniciando na própria instrução call (E8 rel32 curinga) seguido de instruções até ficar único
 let best=null;for(const c of callers[rva].slice(0,400)){const ins=decodeAt(c,80);if(!ins.length||ins[0].rva!==c)continue;let take=[];
  for(const i of ins){take.push(i);const m=maskFor(take);if(m.length>48)break;if(fixedCount(m)<10)continue;const h=matches(c,m,1);if(h.length===1&&h[0]===c){if(!best||m.length<best.m.length)best={c,m};break;}}
  if(best&&best.m.length<=16)break;}
 if(best)sigs.push({kind:'rel',pattern:patternText(best.c,best.m),offset:1});
 const pins=decodeAt(rva,32);let pt=[];for(const i of pins){pt.push(i);if(maskFor(pt).length>=6)break;}const prologue=patternText(rva,maskFor(pt).slice(0,6));
 result.targets.push({name,group:'function',rva,prologue,signatures:sigs});
}
// Globais: duas referências independentes (em funções diferentes, se possível).
for(const [name,rva] of Object.entries(globals)){const want=P.imageBase+rva,xs=[];for(const q of P.relocs){if(q<T.rva||q>=TEXT_END)continue;if(P.img.readUInt32LE(q)===want)xs.push(q);}
 xs.sort((a,b)=>a-b);const found=[];
 for(const q of xs){const s=instrAt(q);if(s==null)continue;const u=unique(s,40);if(!u)continue;found.push({s,q,len:u.len,mask:u.mask});if(found.length>=60)break;}
 found.sort((a,b)=>a.len-b.len);const sigs=[];
 for(const f of found){if(sigs.some(x=>Math.abs(x.at-f.s)<0x400))continue;sigs.push({kind:'abs',pattern:patternText(f.s,f.mask),offset:f.q-f.s,at:f.s});if(sigs.length===2)break;}
 result.targets.push({name,group:'global',rva,signatures:sigs.map(({at,...x})=>x)});}
for(const [name,rva] of Object.entries(vtables)){const t=rttiName(P,rva);result.targets.push({name,group:'vtable',rva,signatures:[{kind:'rtti',type:t.name,offset:t.offset}]});}
// Guardas de deslocamento
const fnByName={};for(const t of result.targets)if(t.group==='function')fnByName[t.name]=t.rva;
for(const g of guards){let made=null;
 for(const fname of g.inside){const f=fnByName[fname];const ins=decodeAt(f,0x800);
  for(let n=0;n<ins.length&&!made;n++){const i=ins[n];if(!i.disp||i.value!==g.value||i.disp.s!==4)continue;
   let take=[];for(let k=n;k<ins.length;k++){take.push(ins[k]);const m=maskFor(take);const o=i.disp.o;for(let z=0;z<4;z++)m[o+z]=false;if(m.length>40)break;if(fixedCount(m)<10)continue;const h=matches(i.rva,m,1);if(h.length===1&&h[0]===i.rva){made={name:g.name,value:g.value,function:fname,pattern:patternText(i.rva,m),offset:o,within:i.rva-f+0x200>0x800?i.rva-f+0x200:0x800};break;}}}
  if(made)break;}
 result.guards.push(made||{name:g.name,value:g.value,missing:true});}

fs.writeFileSync(outJson,JSON.stringify(result,null,1));
// C#
let cs='// Gerado por work/game-signatures/make-signatures.cjs a partir do ElementClient.exe '+sha+'. Não editar à mão.\n// Só padrões curtos de bytes com curingas (??); nenhum código do jogo é distribuído.\npublic static partial class GameSignatures {\n public static readonly GameSignatures.Target[] Targets=new GameSignatures.Target[]{\n';
for(const t of result.targets){cs+='  new GameSignatures.Target("'+t.name+'",0x'+t.rva.toString(16).toUpperCase()+'u,new GameSignatures.Sig[]{'+t.signatures.map(s=>s.kind==='rtti'?'GameSignatures.Sig.Rtti("'+s.type+'",'+s.offset+')':'GameSignatures.Sig.'+({fn:'Fn',abs:'Abs',rel:'Rel'}[s.kind])+'("'+s.pattern+'",'+s.offset+')').join(',')+'}'+(t.prologue?',"'+t.prologue+'"':'')+'),\n';}
cs+=' };\n public static readonly GameSignatures.Guard[] Guards=new GameSignatures.Guard[]{\n';
for(const g of result.guards)if(!g.missing)cs+='  new GameSignatures.Guard("'+g.name+'",0x'+g.value.toString(16).toUpperCase()+'u,"'+g.function+'","'+g.pattern+'",'+g.offset+',0x'+g.within.toString(16).toUpperCase()+'u),\n';
cs+=' };\n}\n';fs.writeFileSync(outCs,cs);
for(const t of result.targets)console.log(t.group.padEnd(8),t.name.padEnd(20),t.rva.toString(16).padStart(7),t.signatures.map(s=>s.kind+(s.pattern?'['+s.pattern.split(' ').length+']':'')).join(' '));
for(const g of result.guards)console.log('guard',g.name,g.missing?'MISSING':g.function+' '+g.pattern);
