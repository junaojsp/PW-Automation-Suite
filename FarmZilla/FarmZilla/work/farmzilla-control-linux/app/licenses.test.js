'use strict';
const {test}=require('node:test');const assert=require('node:assert/strict');const crypto=require('node:crypto');
const licenses=require('./licenses');

test('bot log ingest, toggle and admin read',{skip:!process.env.TEST_DATABASE_URL},async()=>{
 const {Pool}=require('pg');const base=new Pool({connectionString:process.env.TEST_DATABASE_URL});
 const schema='fzlog_'+crypto.randomBytes(6).toString('hex');await base.query('CREATE SCHEMA '+schema);
 const pool=new Pool({connectionString:process.env.TEST_DATABASE_URL,options:'-c search_path='+schema,max:4});
 try{
  await licenses.init(pool);
  const created=await licenses.create(pool,'PC teste',new Date(Date.now()+86400000).toISOString(),null);
  const device='ab'.repeat(32);
  const first=await licenses.activate(pool,created.key,device,'0.51.35');
  assert.equal(first.ok,true);assert.equal(first.log,false);
  const off=await licenses.ingest(pool,created.key,device,'0.51.35',['22:00:00 Frost: CAPTURA_INICIO']);
  assert.equal(off.ok,false);assert.equal(off.status,403);assert.equal(off.log,false);
  await licenses.change(pool,created.id,'log-on');
  assert.equal((await licenses.activate(pool,created.key,device,'0.51.35')).log,true);
  assert.equal((await licenses.ingest(pool,created.key,device,'0.51.35',['22:00:00 Frost: CAPTURA_INICIO','22:00:01 AMOSTRA n=1'])).ok,true);
  const listed=await licenses.listLogs(pool,created.id,100);
  assert.equal(listed.label,'PC teste');
  assert.deepEqual(listed.lines.map(l=>l.line),['22:00:00 Frost: CAPTURA_INICIO','22:00:01 AMOSTRA n=1']);
  assert.equal(listed.lines[0].version,'0.51.35');
  assert.equal((await licenses.list(pool))[0].send_log,true);
  const denied=await licenses.ingest(pool,'WRONGKEY1',device,'0.51.35',['x']);
  assert.equal(denied.ok,false);assert.equal(denied.status,403);assert.equal(denied.reason,'license_unavailable');
  await assert.rejects(licenses.listLogs(pool,'999999'),/não encontrada/);
  const invalid=await licenses.ingest(pool,created.key,device,'0.51.35',Array(201).fill('a'));
  assert.equal(invalid.status,400);
  const long=await licenses.ingest(pool,created.key,device,'0.51.35',['x'.repeat(501)]);
  assert.equal(long.status,400);
  await licenses.change(pool,created.id,'log-off');
  assert.equal((await licenses.ingest(pool,created.key,device,'0.51.35',['depois'])).log,false);
  await licenses.remove(pool,created.id);
  await assert.rejects(licenses.listLogs(pool,created.id),/não encontrada/);
 }finally{await pool.end();await base.query('DROP SCHEMA '+schema+' CASCADE');await base.end();}
});
