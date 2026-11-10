using System;using System.Linq;using System.Collections.Generic;
sealed partial class CharacterMemory {public bool Sitting {get{return (U(Host+0x7D4)&0x20)!=0;}}}
sealed class MeditationResult {public bool Blocked;public string Error;}
interface IMeditationRuntime {bool Sitting(CharacterRef c);MeditationResult Set(CharacterRef c,bool sitting);}
sealed class NativeMeditationRuntime:IMeditationRuntime {
 public bool Sitting(CharacterRef c){using(var s=new CharacterMemory(c.Pid)){if(s.Id!=c.Id)throw new Exception("Personagem mudou; atualize a lista.");return s.Sitting;}}
 public MeditationResult Set(CharacterRef c,bool sitting){var result=new MeditationResult();MeditationBridge bridge=null;try{bridge=new MeditationBridge(c.Pid);if(bridge.CharacterId!=c.Id)throw new Exception("Personagem mudou antes da meditação.");bridge.SetSitting(sitting);if(bridge.LastSendResult!=1)throw new Exception("Cliente não aceitou o comando.");}catch(Exception e){result.Error=e.Message;}finally{if(bridge!=null){try{bridge.Dispose();}catch(Exception e){result.Error=e.Message;}result.Blocked=!bridge.RestoreSucceeded;}}return result;}
}
sealed class MeditationController {
 sealed class Pending {public string Key;public bool Desired,Reported,WasMatched;public int Attempts;public DateTime Next;}
 readonly IMeditationRuntime runtime;readonly HashSet<int> blocked;readonly Action<string> note;readonly Dictionary<int,Pending> attempts=new Dictionary<int,Pending>();readonly HashSet<int> seated=new HashSet<int>();string source="";
 public bool PrimarySitting;public MeditationController(IMeditationRuntime runtime,HashSet<int> blocked,Action<string> note){this.runtime=runtime;this.blocked=blocked;this.note=note;}
 public bool Holds(int pid){Pending p;return seated.Contains(pid)||attempts.TryGetValue(pid,out p)&&p.Desired&&PrimarySitting;}
 public void Tick(CharacterRef primary,CharacterRef[] selected,bool enabled){if(primary==null){source="";attempts.Clear();seated.Clear();PrimarySitting=false;return;}if(source!=primary.Key){source=primary.Key;attempts.Clear();}try{PrimarySitting=runtime.Sitting(primary);}catch{PrimarySitting=false;return;}foreach(int pid in attempts.Keys.Where(id=>!selected.Any(c=>c.Pid==id)).ToArray())attempts.Remove(pid);seated.Clear();
  foreach(var c in selected){if(c.Pid==primary.Pid||blocked.Contains(c.Pid))continue;bool current;try{current=runtime.Sitting(c);}catch{continue;}if(current)seated.Add(c.Pid);if(!enabled){attempts.Remove(c.Pid);continue;}Pending p;if(!attempts.TryGetValue(c.Pid,out p)||p.Key!=c.Key||p.Desired!=PrimarySitting){p=new Pending{Key=c.Key,Desired=PrimarySitting};attempts[c.Pid]=p;}
   if(current==p.Desired){p.WasMatched=true;if(p.Attempts>0&&!p.Reported){note(c.Name+(current?": meditação confirmada.":": levantou; comandos liberados."));p.Reported=true;}continue;}if(p.WasMatched){p.WasMatched=false;p.Attempts=0;p.Reported=false;p.Next=DateTime.MinValue;}
   if(p.Attempts>=3){if(!p.Reported){note(c.Name+": não confirmou "+(p.Desired?"meditação":"levantar")+" após 3 tentativas. Confira o estado do personagem.");p.Reported=true;}continue;}if(DateTime.UtcNow<p.Next)continue;
   // A state change of the principal during the batch cancels the old command.
   if(runtime.Sitting(primary)!=p.Desired)return;p.Attempts++;p.Next=DateTime.UtcNow.AddMilliseconds(1500);var result=runtime.Set(c,p.Desired);if(result.Blocked){blocked.Add(c.Pid);note(c.Name+": ponte de meditação não confirmou remoção; reinicie esse cliente.");}else if(result.Error!=null)note(c.Name+": meditação · "+result.Error);else note(c.Name+(p.Desired?": acompanhando meditação do principal.":": acompanhando o principal ao levantar."));
  }
 }
}
