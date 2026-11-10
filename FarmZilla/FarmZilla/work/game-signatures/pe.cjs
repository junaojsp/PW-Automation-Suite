// Leitura somente de PE x86: seções mapeadas por RVA, relocações e RTTI. Não grava nada no exe.
const fs=require('fs');
function load(file){
 const b=fs.readFileSync(file),pe=b.readUInt32LE(0x3c),n=b.readUInt16LE(pe+6),opt=pe+24,tab=opt+b.readUInt16LE(pe+20);
 const imageBase=b.readUInt32LE(opt+28),sizeOfImage=b.readUInt32LE(opt+56);
 const img=Buffer.alloc(sizeOfImage);b.copy(img,0,0,Math.min(b.length,0x1000));
 const sections=[];
 for(let i=0;i<n;i++){const s=tab+40*i;const name=b.toString('latin1',s,s+8).replace(/\0.*$/,'');const vs=b.readUInt32LE(s+8),rva=b.readUInt32LE(s+12),raw=b.readUInt32LE(s+16),off=b.readUInt32LE(s+20),ch=b.readUInt32LE(s+36);
  sections.push({name,rva,vs,raw,off,ch});b.copy(img,rva,off,off+Math.min(raw,vs));}
 const relocDir=opt+96+5*8,relRva=b.readUInt32LE(relocDir),relSize=b.readUInt32LE(relocDir+4);
 const relocs=new Set();
 for(let p=relRva;p<relRva+relSize;){const page=img.readUInt32LE(p),size=img.readUInt32LE(p+4);if(!size)break;for(let q=p+8;q<p+size;q+=2){const e=img.readUInt16LE(q);if((e>>12)===3)relocs.add(page+(e&0xfff));}p+=size;}
 const sec=name=>sections.find(s=>s.name===name);
 return {file,raw:b,img,imageBase,sizeOfImage,sections,relocs,sec};
}
function rttiName(P,vtRva){const col=P.img.readUInt32LE(vtRva-4)-P.imageBase;if(col<0||col>=P.sizeOfImage-24)return null;const td=P.img.readUInt32LE(col+12)-P.imageBase;if(td<0||td>=P.sizeOfImage-16)return null;const s=P.img.toString('latin1',td+8,td+8+96);return {name:s.split('\0')[0],offset:P.img.readUInt32LE(col+4),sig:P.img.readUInt32LE(col)};}
module.exports={load,rttiName};
