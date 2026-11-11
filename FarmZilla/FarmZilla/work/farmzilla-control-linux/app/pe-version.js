'use strict';
// Parse RT_VERSION through the PE resource tree, never by searching arbitrary bytes.
const fail=message=>Object.assign(new Error(message),{status:400});
function parts(version){
 if(typeof version!=='string'||!/^\d{1,5}\.\d{1,5}\.\d{1,5}(?:\.\d{1,5})?$/.test(version))throw fail('Versão inválida');
 const p=version.split('.').map(Number);if(p.some(n=>n>65535))throw fail('Componente de versão maior que 65535');
 while(p.length<4)p.push(0);return p;
}
const normalize=v=>parts(v).join('.');
const canonical=v=>{const p=parts(v);return p.slice(0,p[3]===0?3:4).join('.');};
function compare(a,b){const x=parts(a),y=parts(b);for(let i=0;i<4;i++)if(x[i]!==y[i])return x[i]>y[i]?1:-1;return 0;}
function inspect(b){
 const need=(offset,size,end=b.length)=>{if(!Number.isSafeInteger(offset)||offset<0||size<0||offset+size>end||end>b.length)throw fail('Executável PE truncado ou inválido');};
 const u16=o=>{need(o,2);return b.readUInt16LE(o);};const u32=o=>{need(o,4);return b.readUInt32LE(o);};
 need(0,64);if(u16(0)!==0x5a4d)throw fail('Arquivo não é um PE Windows');
 const pe=u32(60);need(pe,24);if(u32(pe)!==0x4550||u16(pe+4)!==0x14c)throw fail('Executável deve ser Windows x86');
 const count=u16(pe+6),opt=pe+24,optSize=u16(pe+20);need(opt,optSize);
 if(count<1||count>96||optSize<120||u16(opt)!==0x10b||(u16(pe+22)&0x2000)||!(u16(pe+22)&2))throw fail('PE32 executável inválido');
 if(u32(opt+92)<3)throw fail('PE sem diretório de recursos');
 const sections=opt+optSize;need(sections,count*40);
 function rva(address,size){
  for(let i=0;i<count;i++){const s=sections+i*40,va=u32(s+12),rawSize=u32(s+16),raw=u32(s+20);
   if(address>=va&&address-va<rawSize&&size<=rawSize-(address-va)){const o=raw+address-va;need(o,size);return o;}
  }throw fail('Recurso aponta para RVA inválido');
 }
 const rootRva=u32(opt+112),rootSize=u32(opt+116);if(rootSize<16||rootSize>32*1024*1024)throw fail('Recursos de versão ausentes ou inválidos');
 const root=rva(rootRva,rootSize),end=root+rootSize;
 const at=(relative,size)=>{if(relative&0x80000000)throw fail('Deslocamento de recurso inválido');const o=root+relative;need(o,size,end);return o;};
 function entries(relative){const o=at(relative,16),n=u16(o+12)+u16(o+14);if(n>256)throw fail('Árvore de recursos excessiva');need(o+16,n*8,end);return Array.from({length:n},(_,i)=>({id:u32(o+16+i*8),target:u32(o+20+i*8)}));}
 const values=[];
 function visit(relative,depth){for(const item of entries(relative)){
  if(item.target&0x80000000){if(depth>=2)throw fail('Árvore RT_VERSION inválida');visit(item.target&0x7fffffff,depth+1);}
  else{if(depth!==2||values.length>=64)throw fail('Folha RT_VERSION inválida');const d=at(item.target,16),size=u32(d+4),o=rva(u32(d),size);need(o,size);if(size<6)throw fail('Recurso de versão incompleto');
   const length=u16(o),valueLength=u16(o+2);if(length>size||length<6||valueLength!==52||u16(o+4)!==0)throw fail('VS_VERSION_INFO inválido');
   const key=Buffer.from('VS_VERSION_INFO\0','utf16le');need(o+6,key.length,o+length);if(!b.subarray(o+6,o+6+key.length).equals(key))throw fail('Chave VS_VERSION_INFO ausente');
   const fixed=o+((6+key.length+3)&~3);need(fixed,52,o+length);if(u32(fixed)!==0xfeef04bd||u32(fixed+4)!==0x10000)throw fail('Versão fixa inválida');
   const version=p=>[u32(p)>>>16,u32(p)&65535,u32(p+4)>>>16,u32(p+4)&65535].join('.');
   const fileVersion=version(fixed+8),productVersion=version(fixed+16);
   // .NET FileVersionInfo reads StringFileInfo as well as the fixed numeric fields.
   // Both representations must agree, or Windows can reject a server-approved file.
   function block(offset,limit){
    need(offset,6,limit);const length=u16(offset),chars=u16(offset+2),type=u16(offset+4);
    if(length<8||offset+length>limit)throw fail('Bloco de versão inválido');
    let cursor=offset+6;while(cursor+2<=offset+length&&u16(cursor)!==0)cursor+=2;
    if(cursor+2>offset+length)throw fail('Nome de bloco sem terminador');
    const key=b.subarray(offset+6,cursor).toString('utf16le');const value=offset+((cursor+2-offset+3)&~3),bytes=chars*(type===1?2:1);
    need(value,bytes,offset+length);return {key,type,chars,value,children:(value+bytes+3)&~3,end:offset+length};
   }
   function children(start,limit){const result=[];for(let cursor=start;cursor<limit;){
    if(limit-cursor<6){if(b.subarray(cursor,limit).some(v=>v!==0))throw fail('Padding inválido');break;}
    const child=block(cursor,limit);result.push(child);cursor=(child.end+3)&~3;
   }return result;}
   const strings=[];
   for(const info of children((fixed+52+3)&~3,o+length))if(info.key==='StringFileInfo'){
    for(const table of children(info.children,info.end)){
     if(!/^[a-f0-9]{8}$/i.test(table.key))throw fail('Tabela de idioma inválida');
     const fields={};for(const field of children(table.children,table.end))if(['FileVersion','ProductVersion'].includes(field.key)){
      if(field.type!==1||!field.chars||fields[field.key])throw fail('Texto de versão inválido');
      const text=b.subarray(field.value,field.value+field.chars*2).toString('utf16le').replace(/\0+$/,'').trim();
      fields[field.key]=normalize(text);
     }
     if(fields.FileVersion!==fileVersion||fields.ProductVersion!==productVersion)throw fail('Texto de versão diverge da versão numérica');
     strings.push(fields);
    }
   }
   if(!strings.length)throw fail('Textos FileVersion e ProductVersion ausentes');
   values.push({fileVersion,productVersion});
  }
 }}
 const types=entries(0).filter(e=>e.id===16);if(types.length!==1||!(types[0].target&0x80000000))throw fail('RT_VERSION ausente');
 visit(types[0].target&0x7fffffff,1);
 if(!values.length||values.some(v=>v.fileVersion!==values[0].fileVersion||v.productVersion!==v.fileVersion))throw fail('Versões internas divergentes');
 return {...values[0],architecture:'x86'};
}
function verify(b,version){const meta=inspect(b);if(normalize(version)!==meta.fileVersion)throw fail('Versão informada '+version+' difere da versão interna '+meta.fileVersion);return meta;}
module.exports={parts,normalize,canonical,compare,inspect,verify};
