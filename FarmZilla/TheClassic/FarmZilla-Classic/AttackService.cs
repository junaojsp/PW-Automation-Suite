using System;using System.Collections.Generic;using System.Linq;
sealed class AttackResult {public bool Blocked;public uint Combo;public string Message;}
interface IAttackRuntime {
 uint Target(CharacterRef c);bool ValidTarget(CharacterRef c,uint target);bool Running(CharacterRef c,uint combo,uint target);AttackResult Start(CharacterRef c,uint target);AttackResult Stop(CharacterRef c,uint combo,uint target);AttackResult Release(CharacterRef c);int Step(CharacterRef c,uint combo);
 AttackResult StartSupport(CharacterRef c,CharacterRef leader);bool RunningSupport(CharacterRef c,uint combo);AttackResult StopSupport(CharacterRef c);AttackResult ReleaseSupport(CharacterRef c);
}
sealed class NativeAttackRuntime:IAttackRuntime {
 static CharacterMemory Open(CharacterRef c){var s=new CharacterMemory(c.Pid);if(s.Id!=c.Id){s.Dispose();throw new Exception("Personagem mudou; atualize a lista.");}return s;}
 public uint Target(CharacterRef c){using(var s=Open(c))return s.SelectedTarget;}
 static AttackResult Use(CharacterRef c,Action<AttackBridge,AttackResult> action){var result=new AttackResult();AttackBridge b=null;try{b=new AttackBridge(c.Pid);if(b.CharacterId!=c.Id)throw new Exception("Personagem mudou.");action(b,result);}catch(Exception e){result.Message=c.Name+": "+e.Message;}finally{if(b!=null){try{b.Dispose();}catch(Exception e){result.Message=c.Name+": "+e.Message;}result.Blocked=!b.RestoreSucceeded;if(result.Blocked)result.Message=c.Name+": ponte de ataque sem restauração; reinicie esse cliente.";}}return result;}
 public bool ValidTarget(CharacterRef c,uint target){if(!AttackController.MonsterId(target))return false;var result=Use(c,(b,r)=>{b.Command(0,target);r.Combo=b.LastResult==1?1u:0u;});if(result.Blocked)throw new AttackBlocked(c.Pid,result.Message);if(result.Message!=null)throw new Exception(result.Message);return result.Combo==1;}
 public bool Running(CharacterRef c,uint combo,uint target){using(var s=Open(c))return s.OwnedAttackRunning(combo,target);}
 // Current step of the running macro (combo+0x20, observed advancing 1..5 as skills are used on the live client).
 // Support (sacerdote em "Sup"): target the leader (the principal) and run macro 2 on it; same stop/ESC flow as the attackers.
 public AttackResult StartSupport(CharacterRef c,CharacterRef leader){var cancel=CancelFollowing(c);if(cancel!=null)return cancel;bool macroRunning;using(var s=Open(c)){if(!s.MacroConfigured(2))return new AttackResult{Message=c.Name+": cadastre a macro 2 (terceiro espaço da janela R) para o suporte."};macroRunning=s.AnyMacroRunning();}
  return Use(c,(b,r)=>{if(macroRunning&&!b.StopMacro(-1)&&b.Read(b.Host+ClassicLayout.HostMacro)!=0)throw new Exception("Macro em execução não parou.");if(!b.StartMacroOn(leader.Id,2))throw new Exception("Macro 2 não iniciou no líder.");if(b.Read(b.Host+ClassicLayout.HostTarget)!=leader.Id)throw new Exception(leader.Name+" não ficou selecionado para o suporte.");r.Combo=b.Combo;r.Message=c.Name+": suporte em "+leader.Name+" com a macro 2.";});}
 public bool RunningSupport(CharacterRef c,uint combo){using(var s=Open(c))return s.OwnedMacroRunning(combo,2);}
 public AttackResult StopSupport(CharacterRef c){return Use(c,(b,r)=>{if(b.StopMacro(2))r.Message=c.Name+": suporte encerrado.";});}
 public AttackResult ReleaseSupport(CharacterRef c){var result=new AttackResult();TeleportBridge b=null;try{b=new TeleportBridge(c.Pid);if(b.CharacterId!=c.Id)throw new Exception("Personagem mudou.");b.SendControl(new byte[]{0x2A,0});b.SendControl(new byte[]{0x08,0});}catch(Exception e){result.Message=c.Name+": "+e.Message;}finally{if(b!=null){try{b.Dispose();}catch(Exception e){result.Message=c.Name+": "+e.Message;}result.Blocked=!b.RestoreSucceeded;if(result.Blocked)result.Message=c.Name+": ponte sem restauração; reinicie esse cliente.";}}return result;}
 public int Step(CharacterRef c,uint combo){using(var s=Open(c))return s.RunningMacro()==combo?(int)s.U(combo+ClassicLayout.ComboStep):-1;}
 // ESC on the secondary: only when it still has a monster selected; confirmed by reading the selection afterwards.
 public AttackResult Release(CharacterRef c){using(var s=Open(c))if(!AttackController.MonsterId(s.SelectedTarget))return new AttackResult();
  // 0.19.20 used the selection call with id 0, which the client ignores ("alvo não foi limpo"); 0.19.21 sends the real ESC commands.
  var result=new AttackResult();TeleportBridge b=null;try{b=new TeleportBridge(c.Pid);if(b.CharacterId!=c.Id)throw new Exception("Personagem mudou.");b.SendControl(new byte[]{0x2A,0});b.SendControl(new byte[]{0x08,0});}catch(Exception e){result.Message=c.Name+": "+e.Message;}finally{if(b!=null){try{b.Dispose();}catch(Exception e){result.Message=c.Name+": "+e.Message;}result.Blocked=!b.RestoreSucceeded;if(result.Blocked)result.Message=c.Name+": ponte sem restauração; reinicie esse cliente.";}}
  if(result.Message!=null)return result;for(int i=0;i<10;i++){using(var s=Open(c))if(!AttackController.MonsterId(s.SelectedTarget))return result;System.Threading.Thread.Sleep(100);}result.Message=c.Name+": ESC enviado, mas o alvo continua selecionado.";return result;}
 // A secondary that is following keeps walking and the new macro never lands (seen after ESC → follow → new target).
 // Cancel its current action first (2A 00, the same command the client sends on ESC); following resumes when the attack ends.
 static AttackResult CancelFollowing(CharacterRef c){bool following;try{using(var st=new State(c.Pid))following=st.Following&&st.Id==c.Id;}catch{return null;}if(!following)return null;
  var result=new AttackResult();TeleportBridge b=null;try{b=new TeleportBridge(c.Pid);if(b.CharacterId!=c.Id)throw new Exception("Personagem mudou.");b.SendControl(new byte[]{0x2A,0});}catch(Exception e){result.Message=c.Name+": "+e.Message;}finally{if(b!=null){try{b.Dispose();}catch(Exception e){result.Message=c.Name+": "+e.Message;}result.Blocked=!b.RestoreSucceeded;if(result.Blocked)result.Message=c.Name+": ponte sem restauração; reinicie esse cliente.";}}
  // 0.51.14 (registros 08–09/11: TG → ataque ~1,9 s): espera o seguir soltar no máximo 0,3 s (antes 1 s); a macro começa logo depois.
  if(result.Message!=null)return result;for(int i=0;i<15;i++){try{using(var st=new State(c.Pid))if(!st.Following)return null;}catch{return null;}System.Threading.Thread.Sleep(20);}return null;}
 public AttackResult Start(CharacterRef c,uint target){var cancel=CancelFollowing(c);if(cancel!=null)return cancel;bool macroRunning;using(var s=Open(c)){if(!s.AttackConfigured())return new AttackResult{Message=c.Name+": cadastre habilidades na macro 0."};macroRunning=s.AnyMacroRunning();}
  // The new command wins: a running macro (buff or the player's own) is stopped so the attack never stays locked out.
  return Use(c,(b,r)=>{if(macroRunning&&!b.StopMacro(-1)&&b.Read(b.Host+ClassicLayout.HostMacro)!=0)throw new Exception("Macro em execução não parou.");b.Command(0,target);if(b.LastResult!=1)throw new Exception("Alvo indisponível ou não atacável.");b.Command(1,target);if(b.LastResult!=1||b.Read(b.Host+ClassicLayout.HostTarget)!=target)throw new Exception("Alvo não confirmado.");b.Command(2,target);if(b.LastResult!=1||b.Combo==0)throw new Exception("Macro 0 não iniciou.");r.Combo=b.Combo;r.Message=c.Name+(macroRunning?": macro anterior interrompida; atacando o alvo do principal com a macro 0.":": atacando o alvo do principal com a macro 0.");});}
 public AttackResult Stop(CharacterRef c,uint combo,uint target){return Use(c,(b,r)=>{b.Command(3,target,combo);r.Message=b.LastResult==1?c.Name+": ataque encerrado.":null;});}
}
sealed class AttackBlocked:Exception {public readonly int Pid;public AttackBlocked(int pid,string message):base(message){Pid=pid;}}
sealed class AttackController {
 sealed class Session {public CharacterRef Character;public uint Target,Combo,SupportOn;public bool Completed,Support;public DateTime RetryAt;public int Step=-1;public DateTime StepSince;}
 // A macro that stays on the same step (a skill it cannot use: cooldown, range, condition) is restarted after this many seconds.
 public static double StallSeconds=5;
 // 0.51.8 (registro Esdras 08/11 18:18: CheirosaEP reiniciada a cada 5,5 s sempre no passo 2 — uma habilidade longa/recarga da
 // macro dela nunca terminava e ela parecia não atacar): stalling again at the same step waits longer each time (5→10→15→20 s);
 // reaching a later step or the macro ending by itself clears it.
 public const double StallMaxSeconds=20;readonly Dictionary<int,int[]> stallRepeat=new Dictionary<int,int[]>();
 double StallLimit(int pid,int step){int[] r;if(!stallRepeat.TryGetValue(pid,out r)||r[0]!=step)return StallSeconds;return Math.Min(StallMaxSeconds,StallSeconds*(1+r[1]));}
 // Automatic mode keeps combing the principal's target: a finished macro 0 is started again while the same monster stays selected;
 // a failed start waits 3 s. Selecting yourself (or nothing) stops the attack and following resumes.
 readonly IAttackRuntime runtime;readonly Dictionary<int,Session> sessions=new Dictionary<int,Session>();readonly HashSet<int> blocked;readonly Action<string> note;uint manualTarget,lastTarget;DateTime nextProbe;bool targetValid;
 public AttackController(IAttackRuntime runtime,HashSet<int> blocked,Action<string> note){this.runtime=runtime;this.blocked=blocked;this.note=note;}
 public uint ReadTarget(CharacterRef primary){return runtime.Target(primary);}
 public bool Active{get{return sessions.Count>0;}}public uint LastTarget{get{return lastTarget;}}
 Tuple<bool,Exception> ProbeTarget(CharacterRef primary,uint target){try{return Tuple.Create(runtime.ValidTarget(primary,target),(Exception)null);}catch(Exception e){return Tuple.Create(false,e);}}
 bool ApplyProbe(Tuple<bool,Exception> r){var b=r.Item2 as AttackBlocked;if(b!=null){blocked.Add(b.Pid);note(b.Message);}else if(r.Item2!=null)note("Ataque: "+r.Item2.Message);return r.Item1;}
 static CharacterRef SupportFor(CharacterRef supporter,CharacterRef primary,CharacterRef chosen){return chosen==null||chosen.Pid==supporter.Pid?primary:chosen;}
 public static bool MonsterId(uint target){return target!=0&&(target&0xC0000000)==0x80000000;}
 // Also engaged while waiting to retry on the same target, so following does not pull the secondary away mid-combo.
 // 0.47.7 (owner: crowd control made them run back to the leader): a secondary stays "in combat" while the principal keeps a
 // monster TG — whatever happened to its macro (stunned, slept, failed start). Read from the UI thread, so it is a snapshot.
 volatile HashSet<int> engaged=new HashSet<int>();void Snapshot(){engaged=new HashSet<int>(sessions.Keys);}
 public bool Engaged(int pid){return engaged.Contains(pid);}
 // Set when the principal dropped the monster (selected itself/nothing): the UI sends everyone to follow at once.
 volatile bool released;public bool TakeReleased(){bool r=released;released=false;return r;}
 public void StopAll(){StopAll(false);}
 // release: the principal dropped its target (ESC or selected itself) — besides stopping macro 0, clear each secondary's target so
 // the basic attack stops too and following can bring them back.
 // 0.47.7 (owner: "esquece o ESC", follow at full speed): every macro is stopped at the same time and nobody gets ESC;
 // following is sent right after by the UI (TakeReleased).
 public void StopAll(bool release){var list=sessions.Values.ToArray();sessions.Clear();manualTarget=lastTarget=0;nextProbe=DateTime.MinValue;targetValid=false;
  // 0.50.1 (log 06/11 21:54:02: macros stopped, follow sent 0,2 s later and refused by all 5 — still finishing the attack;
  // only Limeira followed, 6 s later): on release each account also gets the cancel-action/clear-target commands right after
  // its stop, all accounts at the same time, so the follow sent next is accepted.
  // 0.51.11 (owner: EP kept healing/supporting with no monster TG): a supporter always gets the stop of macro 2, even when the
  // session looked finished (the game may still be repeating it).
  var jobs=list.Where(x=>!blocked.Contains(x.Character.Pid)&&(release||x.Support||x.Combo!=0&&!x.Completed)).Select(x=>System.Threading.Tasks.Task.Run(()=>{
   var results=new List<AttackResult>();try{
    if(x.Support)results.Add(runtime.StopSupport(x.Character));else if(x.Combo!=0&&!x.Completed)results.Add(runtime.Stop(x.Character,x.Combo,x.Target));
    if(release)results.Add(x.Support?runtime.ReleaseSupport(x.Character):runtime.Release(x.Character));
    return Tuple.Create(x,results,(Exception)null);}catch(Exception e){return Tuple.Create(x,results,e);}})).ToArray();
  if(jobs.Length>0)System.Threading.Tasks.Task.WaitAll(jobs);
  foreach(var j in jobs){var t=j.Result;if(t.Item3!=null){blocked.Add(t.Item1.Character.Pid);note(t.Item1.Character.Name+": falha encerrando ataque; reinicie esse cliente. "+t.Item3.Message);continue;}foreach(var r in t.Item2){if(r.Blocked)blocked.Add(t.Item1.Character.Pid);if(r.Message!=null)note(r.Message);}}
  if(release&&list.Length>0)released=true;Snapshot();}
 void StopLeaderOnly(CharacterRef leader,bool requested){Session own;if(sessions.TryGetValue(leader.Pid,out own)){Stop(own);sessions.Remove(leader.Pid);}if(requested)note("Ataque: líder não consegue atacar o alvo; secundários mantidos.");}
 void Stop(Session s){if(blocked.Contains(s.Character.Pid)||!s.Support&&(s.Combo==0||s.Completed))return;try{var result=s.Support?runtime.StopSupport(s.Character):runtime.Stop(s.Character,s.Combo,s.Target);if(result.Blocked)blocked.Add(s.Character.Pid);if(result.Message!=null)note(result.Message);}catch(Exception e){blocked.Add(s.Character.Pid);note(s.Character.Name+": falha encerrando ataque; reinicie esse cliente. "+e.Message);}}
 public void Tick(CharacterRef primary,CharacterRef[] selected,bool enabled,bool automatic,bool request){Tick(primary,selected,enabled,automatic,request,new HashSet<int>());}
 // support: pids that heal/buff the leader with macro 2 while the others attack the monster.
 public void Tick(CharacterRef primary,CharacterRef[] selected,bool enabled,bool automatic,bool request,HashSet<int> support){Tick(primary,selected,enabled,automatic,request,support,null);}
 // supportOn (0.46.8, owner): who receives macro 2 from the supporters; null or the supporter itself = the leader (principal).
 public void Tick(CharacterRef primary,CharacterRef[] selected,bool enabled,bool automatic,bool request,HashSet<int> support,CharacterRef supportOn){try{TickCore(primary,selected,enabled,automatic,request,support,supportOn);}finally{Snapshot();}}
 void TickCore(CharacterRef primary,CharacterRef[] selected,bool enabled,bool automatic,bool request,HashSet<int> support,CharacterRef supportOn){
  if(!enabled||primary==null||blocked.Contains(primary.Pid)){StopAll();return;}uint target;try{target=runtime.Target(primary);}catch{StopAll();return;}
  if(!MonsterId(target)){StopAll(true);return;}if(target!=lastTarget){manualTarget=0;nextProbe=DateTime.MinValue;targetValid=false;lastTarget=target;}
  if(request&&!automatic)manualTarget=target;
  bool wanted=automatic||manualTarget==target;
  foreach(var pair in sessions.ToArray()){var ps=pair.Value;bool sup=support.Contains(ps.Character.Pid);bool still=selected.Any(c=>c.Key==ps.Character.Key);if(ps.Support&&sup&&ps.SupportOn!=SupportFor(ps.Character,primary,supportOn).Id){Stop(ps);sessions.Remove(pair.Key);continue;}if(ps.Support&&sup&&wanted&&ps.Target!=target){ps.Target=target;continue;}
   // New monster for an attacker: no separate stop round — the new start interrupts the running macro itself (faster switch).
   if(wanted&&still&&!ps.Support&&!sup&&ps.Target!=target){sessions.Remove(pair.Key);continue;}
   if(!wanted||ps.Support!=sup||ps.Target!=target||!still){Stop(ps);sessions.Remove(pair.Key);}}
  if(!wanted||selected.Length==0)return;
  // 0.46.7: on a new target the attackable check runs together with the starts (no wait before the first skill).
  bool fresh=nextProbe==DateTime.MinValue;
  if(!fresh&&DateTime.UtcNow>=nextProbe){nextProbe=DateTime.UtcNow.AddSeconds(1);targetValid=ApplyProbe(ProbeTarget(primary,target));}
  // The leader supplies the monster target, but his temporary inability to attack must not stop the other clients.
  // Each secondary validates attackability independently in NativeAttackRuntime.Start.
  if(!fresh&&!targetValid)StopLeaderOnly(primary,request);
  var pending=new List<Tuple<CharacterRef,bool,bool>>();
  foreach(var c in selected){if(blocked.Contains(c.Pid)||(c.Pid==primary.Pid&&!fresh&&!targetValid))continue;Session s;bool again=false;if(sessions.TryGetValue(c.Pid,out s)){if(!s.Completed){try{if(!(s.Support?runtime.RunningSupport(c,s.Combo):runtime.Running(c,s.Combo,s.Target))){s.Completed=true;stallRepeat.Remove(c.Pid);}else{int step=runtime.Step(c,s.Combo);int[] sr;if(stallRepeat.TryGetValue(c.Pid,out sr)&&step>sr[0])stallRepeat.Remove(c.Pid);if(step!=s.Step){s.Step=step;s.StepSince=DateTime.UtcNow;}else{double limit=StallLimit(c.Pid,step);if((DateTime.UtcNow-s.StepSince).TotalSeconds>=limit){note(c.Name+": macro travada no passo "+step+" por "+limit.ToString("0")+" s; reiniciada.");if(stallRepeat.TryGetValue(c.Pid,out sr)&&sr[0]==step)sr[1]++;else stallRepeat[c.Pid]=new[]{step,1};Stop(s);s.Completed=true;}}}}catch{Stop(s);s.Completed=true;}}if(!s.Completed)continue;if(automatic){if(DateTime.UtcNow<s.RetryAt)continue;again=true;}else if(!request)continue;sessions.Remove(c.Pid);}
   pending.Add(Tuple.Create(c,again,support.Contains(c.Pid)&&c.Pid!=primary.Pid));
  }
  if(pending.Count==0){if(fresh){nextProbe=DateTime.UtcNow.AddSeconds(1);targetValid=ApplyProbe(ProbeTarget(primary,target));}return;}
  // Recheck the principal right before sending, avoiding stale target commands.
  if(runtime.Target(primary)!=target)return;
  // Each character is its own game process: send all starts at once instead of one after another. The principal's own start
  // waits for the check on its process (both use its bridge); the secondaries do not wait.
  var probe=fresh?System.Threading.Tasks.Task.Run(()=>ProbeTarget(primary,target)):null;
  Func<Tuple<CharacterRef,bool,bool>,Tuple<AttackResult,Exception>> start=p=>{try{return Tuple.Create(p.Item3?runtime.StartSupport(p.Item1,SupportFor(p.Item1,primary,supportOn)):runtime.Start(p.Item1,target),(Exception)null);}catch(Exception e){return Tuple.Create((AttackResult)null,e);}};
  var took=new double[pending.Count];var jobs=pending.Select((p,k)=>System.Threading.Tasks.Task.Run(()=>{var sw=System.Diagnostics.Stopwatch.StartNew();try{if(probe!=null&&p.Item1.Pid==primary.Pid&&!probe.Result.Item1)return Tuple.Create(new AttackResult(),(Exception)null);return start(p);}finally{took[k]=sw.Elapsed.TotalSeconds;}})).ToArray();
  System.Threading.Tasks.Task.WaitAll(jobs);
  // 0.51.14: time of each start on a new TG in the log, to see who is slow.
  if(fresh&&pending.Count>0&&took.Max()>=0.5)note("Ataque: TG novo → início "+string.Join(", ",pending.Select((p,k)=>p.Item1.Name+" "+took[k].ToString("0.00")+" s").ToArray())+".");
  if(probe!=null){nextProbe=DateTime.UtcNow.AddSeconds(1);targetValid=ApplyProbe(probe.Result);}
  for(int i=0;i<pending.Count;i++){var c=pending[i].Item1;bool again=pending[i].Item2,supporting=pending[i].Item3;var result=jobs[i].Result.Item1;var error=jobs[i].Result.Item2;
   // Repeated starts on the same target stay quiet in the log unless something goes wrong.
   if(c.Pid==primary.Pid&&probe!=null&&!targetValid)continue;
   if(error==null){if(result.Blocked)blocked.Add(c.Pid);if(result.Message!=null&&(!again||result.Combo==0||result.Blocked))note(result.Message);sessions[c.Pid]=new Session{Character=c,Target=target,Combo=result.Combo,Completed=result.Combo==0,Support=supporting,SupportOn=supporting?SupportFor(c,primary,supportOn).Id:0,RetryAt=result.Combo==0?DateTime.UtcNow.AddSeconds(3):DateTime.MinValue};}
   else{if(!again)note(c.Name+": "+error.Message);sessions[c.Pid]=new Session{Character=c,Target=target,Completed=true,Support=supporting,SupportOn=supporting?SupportFor(c,primary,supportOn).Id:0,RetryAt=DateTime.UtcNow.AddSeconds(3)};}}
  if(probe!=null&&!targetValid)StopLeaderOnly(primary,request);
 }
}
