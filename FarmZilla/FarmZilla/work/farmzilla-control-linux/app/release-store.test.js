'use strict';
const {test}=require('node:test');const assert=require('node:assert/strict');const fs=require('node:fs/promises');const os=require('node:os');const path=require('node:path');const crypto=require('node:crypto');
const pe=require('./pe-version'),store=require('./release-store');
// Minimal PE fixture, with a real resource directory, not a raw signature match.
function fixture(version='0.51.32.0'){
 const b=Buffer.alloc(2048);b.writeUInt16LE(0x5a4d,0);b.writeUInt32LE(128,60);b.writeUInt32LE(0x4550,128);b.writeUInt16LE(0x14c,132);b.writeUInt16LE(1,134);b.writeUInt16LE(224,148);b.writeUInt16LE(2,150);
 const opt=152;b.writeUInt16LE(0x10b,opt);b.writeUInt32LE(16,opt+92);b.writeUInt32LE(0x1000,opt+112);b.writeUInt32LE(512,opt+116);
 const section=opt+224;b.writeUInt32LE(512,section+8);b.writeUInt32LE(0x1000,section+12);b.writeUInt32LE(512,section+16);b.writeUInt32LE(512,section+20);
 const root=512;
 for(const [dir,id,target] of [[0,16,0x80000018],[24,1,0x80000030],[48,0x409,72]]){b.writeUInt16LE(1,root+dir+14);b.writeUInt32LE(id,root+dir+16);b.writeUInt32LE(target,root+dir+20);}
 b.writeUInt32LE(0x1060,root+72);b.writeUInt32LE(92,root+76);
 const v=root+96;b.writeUInt16LE(92,v);b.writeUInt16LE(52,v+2);Buffer.from('VS_VERSION_INFO\0','utf16le').copy(b,v+6);
 const f=v+40;b.writeUInt32LE(0xfeef04bd,f);b.writeUInt32LE(0x10000,f+4);const p=pe.parts(version),ms=((p[0]*65536)+p[1])>>>0,ls=((p[2]*65536)+p[3])>>>0;
 b.writeUInt32LE(ms,f+8);b.writeUInt32LE(ls,f+12);b.writeUInt32LE(ms,f+16);b.writeUInt32LE(ls,f+20);return b;
}
test('version equivalence, ordering and bounds',()=>{assert.equal(pe.normalize('0.51.31'),pe.normalize('0.51.31.0'));assert.equal(pe.canonical('0.51.32.0'),'0.51.32');assert.equal(pe.compare('0.51.32','0.51.31.0'),1);assert.throws(()=>pe.normalize('0.51.65536'));assert.throws(()=>pe.normalize('../1.2.3'));});
test('PE resource version and expected version',()=>{const b=fixture();assert.equal(pe.verify(b,'0.51.32').fileVersion,'0.51.32.0');assert.throws(()=>pe.verify(b,'0.51.33'),/difere/);assert.throws(()=>pe.inspect(b.subarray(0,700)));const bad=Buffer.from(b);bad.writeUInt16LE(0x8664,132);assert.throws(()=>pe.inspect(bad),/x86/);const fake=Buffer.alloc(2048);fake.write('MZ');Buffer.from('VS_VERSION_INFO\0','utf16le').copy(fake,500);assert.throws(()=>pe.inspect(fake));});
test('malformed resource pointers, loops, language conflict and DLL rejected',()=>{
 for(const mutate of [b=>b.writeUInt32LE(0xffffffff,512+20),b=>b.writeUInt32LE(0x80000000,512+68),b=>b.writeUInt32LE(0xfffffffe,512+72),b=>b.writeUInt16LE(0x2002,150),b=>b.writeUInt32LE(123,512+96+40+16)]){const b=fixture();mutate(b);assert.throws(()=>pe.inspect(b));}
});
test('real Windows artifact parses correctly',{skip:!process.env.FZ_TEST_EXE},async()=>{const b=await fs.readFile(process.env.FZ_TEST_EXE);const m=pe.inspect(b);assert.equal(m.architecture,'x86');assert.equal(m.fileVersion,m.productVersion);assert.equal(m.fileVersion,process.env.FZ_TEST_VERSION||'0.51.32.0');});
test('PostgreSQL release transaction and repair',{skip:!process.env.TEST_DATABASE_URL},async t=>{
 const {Pool}=require('pg');const base=new Pool({connectionString:process.env.TEST_DATABASE_URL});const schema='fztest_'+crypto.randomBytes(6).toString('hex');await base.query('CREATE SCHEMA '+schema);
 const pool=new Pool({connectionString:process.env.TEST_DATABASE_URL,options:'-c search_path='+schema,max:4});const dir=await fs.mkdtemp(path.join(os.tmpdir(),'fz-release-'));
 try{
  await pool.query(await fs.readFile(path.join(__dirname,'../sql/init.sql'),'utf8'));
  await pool.query('CREATE TABLE release_publication (id INTEGER PRIMARY KEY CHECK(id=1),release_id BIGINT NOT NULL REFERENCES releases(id),published_at TIMESTAMPTZ NOT NULL DEFAULT NOW())');
  const put=async(v,n)=>{const file=path.join(dir,n);await fs.writeFile(file,fixture(v));return file;};
  const temp=await put('0.51.32','incoming');
  const row=await store.register(pool,dir,temp,'0.51.32.0',{publish:true,reuse:true,source:'test'});
  assert.equal(row.version,'0.51.32');assert.equal((await pool.query('SELECT release_id FROM release_publication')).rows[0].release_id,row.id);await store.validate(dir,row);
  await assert.rejects(store.register(pool,dir,temp,'0.51.32.0'),/já existe/);
  const changed=Buffer.from(await fs.readFile(temp));changed[1500]=1;const other=path.join(dir,'other');await fs.writeFile(other,changed);
  await assert.rejects(store.register(pool,dir,other,'0.51.32',{reuse:true,publish:true}),/já existe/);
  const reused=await store.register(pool,dir,temp,'0.51.32',{reuse:true,publish:true});assert.equal(reused.reused,true);
  await assert.rejects(store.repair(pool,dir,other,row.id,row.sha256),/já é válida/);
  await assert.rejects(store.register(pool,dir,temp,'0.51.33'),/difere/);
  const older=await put('0.51.31','older');await assert.rejects(store.register(pool,dir,older,'0.51.31',{publish:true}),/superior/);assert.equal((await pool.query('SELECT count(*)::int AS n FROM releases')).rows[0].n,1);await assert.rejects(fs.stat(path.join(dir,'FarmZilla-0.51.31.exe')));
  const invalid=await put('0.51.30','FarmZilla-0.51.29.exe');const invalidHash=crypto.createHash('sha256').update(await fs.readFile(invalid)).digest('hex');
  const wrong=(await pool.query("INSERT INTO releases(version,filename,sha256,bytes) VALUES('0.51.29','FarmZilla-0.51.29.exe',$1,2048) RETURNING *",[invalidHash])).rows[0];
  await assert.rejects(store.publish(pool,dir,wrong.id),/difere/);
  const correct=await put('0.51.29','correct');const repaired=await store.repair(pool,dir,correct,wrong.id,wrong.sha256);assert.equal(repaired.fileVersion,'0.51.29.0');await store.validate(dir,repaired);
  assert.equal((await pool.query('SELECT release_id FROM release_publication')).rows[0].release_id,row.id);
  assert((await fs.readdir(path.join(dir,'.history'))).some(n=>n.endsWith('.bak')));
  const before=await fs.readFile(path.join(dir,'FarmZilla-0.51.29.exe'));const history=(await fs.readdir(path.join(dir,'.history'))).find(n=>n.endsWith('.bak'));assert.deepEqual(await fs.readFile(path.join(dir,'.history',history)),fixture('0.51.30'));
  // SQL failure after linking must remove only the uncommitted new file.
  await pool.query("CREATE FUNCTION block_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.action='release.upload' THEN RAISE EXCEPTION 'simulated audit failure'; END IF; RETURN NEW; END $$");await pool.query('CREATE TRIGGER block_audit BEFORE INSERT ON audit_log FOR EACH ROW EXECUTE FUNCTION block_audit()');
  const next=await put('0.51.33','next');await assert.rejects(store.register(pool,dir,next,'0.51.33',{publish:true}),/simulated/);await assert.rejects(fs.stat(path.join(dir,'FarmZilla-0.51.33.exe')));assert.equal((await pool.query('SELECT release_id FROM release_publication')).rows[0].release_id,row.id);
  await pool.query('DROP TRIGGER block_audit ON audit_log');
  const race=await Promise.allSettled([store.register(pool,dir,next,'0.51.33'),store.register(pool,dir,next,'0.51.33.0')]);assert.equal(race.filter(r=>r.status==='fulfilled').length,1);
  // Valid binary may not be republished through historical semantic aliases.
  await pool.query('INSERT INTO releases(version,filename,sha256,bytes) VALUES($1,$2,$3,$4)',['0.51.32.0','FarmZilla-0.51.32.0.exe',row.sha256,row.bytes]);await assert.rejects(store.publish(pool,dir,row.id),/equivalentes/);
  assert((await pool.query("SELECT count(*)::int AS n FROM audit_log WHERE action='release.repair'")).rows[0].n===1);
 }finally{await pool.end();await base.query('DROP SCHEMA '+schema+' CASCADE');await base.end();await fs.rm(dir,{recursive:true,force:true});}
});
