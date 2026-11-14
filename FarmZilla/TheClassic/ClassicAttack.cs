using System;
using FarmZilla.TheClassic;

// Classic copy of NativeAttackRuntime (AttackService.cs). The shared AttackController drives it unchanged;
// only the host fields (macro +0x1418, target +0x59C) and the bridges are Classic.
sealed class ClassicAttackRuntime:IAttackRuntime {
 static CharacterMemory Open(CharacterRef c){var s=new CharacterMemory(c.Pid);if(s.Id!=c.Id){s.Dispose();throw new Exception("Personagem mudou; atualize a lista.");}return s;}
 public uint Target(CharacterRef c){using(var s=Open(c))return s.SelectedTarget;}
 static AttackResult Use(CharacterRef c,Action<AttackBridge,AttackResult> action){var result=new AttackResult();AttackBridge b=null;try{b=new AttackBridge(c.Pid);if(b.CharacterId!=c.Id)throw new Exception("Personagem mudou.");action(b,result);}catch(Exception e){result.Message=c.Name+": "+e.Message;}finally{if(b!=null){try{b.Dispose();}catch(Exception e){result.Message=c.Name+": "+e.Message;}result.Blocked=!b.RestoreSucceeded;if(result.Blocked)result.Message=c.Name+": ponte de ataque sem restauração; reinicie esse cliente.";}}return result;}
 static AttackResult Control(CharacterRef c,params byte[][] packets){var result=new AttackResult();TeleportBridge b=null;try{b=new TeleportBridge(c.Pid);if(b.CharacterId!=c.Id)throw new Exception("Personagem mudou.");foreach(var p in packets)b.SendControl(p);}catch(Exception e){result.Message=c.Name+": "+e.Message;}finally{if(b!=null){try{b.Dispose();}catch(Exception e){result.Message=c.Name+": "+e.Message;}result.Blocked=!b.RestoreSucceeded;if(result.Blocked)result.Message=c.Name+": ponte sem restauração; reinicie esse cliente.";}}return result;}
 static readonly byte[] CancelAction={0x2A,0},Unselect={0x08,0};
 public bool ValidTarget(CharacterRef c,uint target){if(!AttackController.MonsterId(target))return false;var result=Use(c,(b,r)=>{b.Command(0,target);r.Combo=b.LastResult==1?1u:0u;});if(result.Blocked)throw new AttackBlocked(c.Pid,result.Message);if(result.Message!=null)throw new Exception(result.Message);return result.Combo==1;}
 public bool Running(CharacterRef c,uint combo,uint target){using(var s=Open(c))return s.OwnedAttackRunning(combo,target);}
 public AttackResult StartSupport(CharacterRef c,CharacterRef leader){var cancel=CancelFollowing(c);if(cancel!=null)return cancel;bool macroRunning;using(var s=Open(c)){if(!s.MacroConfigured(2))return new AttackResult{Message=c.Name+": cadastre a macro 2 (terceiro espaço da janela R) para o suporte."};macroRunning=s.AnyMacroRunning();}
  return Use(c,(b,r)=>{if(macroRunning&&!b.StopMacro(-1)&&b.Read(b.Host+AttackProfile.HostMacro)!=0)throw new Exception("Macro em execução não parou.");if(!b.StartMacroOn(leader.Id,2))throw new Exception("Macro 2 não iniciou no líder.");if(b.Read(b.Host+AttackProfile.HostTarget)!=leader.Id)throw new Exception(leader.Name+" não ficou selecionado para o suporte.");r.Combo=b.Combo;r.Message=c.Name+": suporte em "+leader.Name+" com a macro 2.";});}
 public bool RunningSupport(CharacterRef c,uint combo){using(var s=Open(c))return s.OwnedMacroRunning(combo,2);}
 public AttackResult StopSupport(CharacterRef c){return Use(c,(b,r)=>{if(b.StopMacro(2))r.Message=c.Name+": suporte encerrado.";});}
 public AttackResult ReleaseSupport(CharacterRef c){return Control(c,CancelAction,Unselect);}
 public int Step(CharacterRef c,uint combo){using(var s=Open(c))return s.RunningMacro()==combo?(int)s.U(combo+0x20):-1;}
 public AttackResult Release(CharacterRef c){using(var s=Open(c))if(!AttackController.MonsterId(s.SelectedTarget))return new AttackResult();
  var result=Control(c,CancelAction,Unselect);
  if(result.Message!=null)return result;for(int i=0;i<10;i++){using(var s=Open(c))if(!AttackController.MonsterId(s.SelectedTarget))return result;System.Threading.Thread.Sleep(100);}result.Message=c.Name+": ESC enviado, mas o alvo continua selecionado.";return result;}
 // A following secondary keeps walking and the macro never lands: cancel the current action first (2A 00, as ESC).
 static AttackResult CancelFollowing(CharacterRef c){bool following;try{using(var st=new State(c.Pid))following=st.Following&&st.Id==c.Id;}catch{return null;}if(!following)return null;
  var result=Control(c,CancelAction);
  if(result.Message!=null)return result;for(int i=0;i<15;i++){try{using(var st=new State(c.Pid))if(!st.Following)return null;}catch{return null;}System.Threading.Thread.Sleep(20);}return null;}
 public AttackResult Start(CharacterRef c,uint target){var cancel=CancelFollowing(c);if(cancel!=null)return cancel;bool macroRunning;using(var s=Open(c)){if(!s.AttackConfigured())return new AttackResult{Message=c.Name+": cadastre habilidades na macro 0."};macroRunning=s.AnyMacroRunning();}
  return Use(c,(b,r)=>{if(macroRunning&&!b.StopMacro(-1)&&b.Read(b.Host+AttackProfile.HostMacro)!=0)throw new Exception("Macro em execução não parou.");b.Command(0,target);if(b.LastResult!=1)throw new Exception("Alvo indisponível ou não atacável.");b.Command(1,target);if(b.LastResult!=1||b.Read(b.Host+AttackProfile.HostTarget)!=target)throw new Exception("Alvo não confirmado.");b.Command(2,target);if(b.LastResult!=1||b.Combo==0)throw new Exception("Macro 0 não iniciou.");r.Combo=b.Combo;r.Message=c.Name+(macroRunning?": macro anterior interrompida; atacando o alvo do principal com a macro 0.":": atacando o alvo do principal com a macro 0.");});}
 public AttackResult Stop(CharacterRef c,uint combo,uint target){return Use(c,(b,r)=>{b.Command(3,target,combo);r.Message=b.LastResult==1?c.Name+": ataque encerrado.":null;});}
}
