'use strict';
const fs=require('node:fs/promises');const crypto=require('node:crypto');const path=require('node:path');
const pe=require('../farmzilla-control-linux/app/pe-version');
async function output(values){for(const [k,v] of Object.entries(values))await fs.appendFile(process.env.GITHUB_OUTPUT,k+'='+v+'\n');}
async function main(){
 const source=await fs.readFile(path.join(__dirname,'../../outputs/FarmZilla-0.51.20/ReleaseInfo.cs'),'utf8');
 const version=source.match(/public const string Version="([0-9.]+)";/)?.[1];pe.parts(version);
 if(process.argv[2]==='run'){
  const id=process.env.FZ_BUILD_RUN_ID;if(!/^\d+$/.test(id||''))throw Error('Número da compilação inválido');
  const response=await fetch('https://api.github.com/repos/'+process.env.GITHUB_REPOSITORY+'/actions/runs/'+id,{headers:{Authorization:'Bearer '+process.env.GITHUB_TOKEN,Accept:'application/vnd.github+json'}});
  if(!response.ok)throw Error('Não foi possível consultar a compilação: HTTP '+response.status);
  const run=await response.json();
  if(run.repository?.full_name!==process.env.GITHUB_REPOSITORY||run.path!=='.github/workflows/farmzilla-build.yml'||run.head_branch!=='main'||run.head_sha!==process.env.GITHUB_SHA||run.status!=='completed'||run.conclusion!=='success'||!['push','workflow_dispatch'].includes(run.event))throw Error('Use uma compilação concluída e aprovada da mesma revisão da branch main');
  await output({version,artifact:'FarmZilla-'+version+'-PUBLICAR'});
 }else if(process.argv[2]==='file'){
  const b=await fs.readFile(process.argv[3]);const meta=pe.verify(b,version);
  const sha=crypto.createHash('sha256').update(b).digest('hex');await output({version,sha,bytes:b.length});
  console.log(JSON.stringify({version,...meta,sha256:sha,bytes:b.length}));
 }else throw Error('Operação inválida');
}
main().catch(e=>{console.error(e.message);process.exitCode=1;});
