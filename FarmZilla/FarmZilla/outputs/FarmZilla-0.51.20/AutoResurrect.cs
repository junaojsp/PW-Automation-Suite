using System;using System.Collections.Generic;using System.Drawing;using System.IO;using System.Linq;using System.Windows.Forms;
// 0.50.3 (owner): a sacerdote (EP) of the list casts Prece da Vida by itself on any listed account that died. Same path as the
// support (0.19.23): select the dead player and run a macro on it — here macro 4 (fourth slot of the R window), where the owner
// puts Prece da Vida. One try per dead account every 8 s until it is alive again; the EP is kept out of the attack for 6 s so
// the support/attack restart does not cut the cast. The dead one accepts by itself when "Reviver" is on (0.50.2).
partial class AvalonInterno {
 const int ResurrectMacro=3;
 readonly CheckBox resurrectAuto=new CheckBox();volatile bool resurrectOn=true;
 readonly Dictionary<int,DateTime> resBusy=new Dictionary<int,DateTime>(),resNext=new Dictionary<int,DateTime>();readonly HashSet<int> resWarned=new HashSet<int>(),resAnnounced=new HashSet<int>();
 string ResurrectFile{get{return Path.Combine(Path.GetDirectoryName(settings),"ressuscitar.txt");}}
 readonly Dictionary<int,DateTime> castUntil=new Dictionary<int,DateTime>();// 0.50.7: one Prece at a time per sacerdote (6 s), so the next ESC does not cut it
 readonly HashSet<int> deadNow=new HashSet<int>();bool IsDead(int pid){lock(deadNow)return deadNow.Contains(pid);}
 bool ResBusy(int pid){lock(resBusy){DateTime until;return resBusy.TryGetValue(pid,out until)&&DateTime.UtcNow<until;}}
 void ResurrectField(int x,int y){try{if(File.Exists(ResurrectFile))resurrectOn=File.ReadAllText(ResurrectFile).Trim()!="0";}catch{}
  resurrectAuto.SetBounds(x,y,66,20);resurrectAuto.Text="EP revive";resurrectAuto.Checked=resurrectOn;resurrectAuto.ForeColor=SupportColor;resurrectAuto.FlatStyle=FlatStyle.Flat;resurrectAuto.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);
  resurrectAuto.CheckedChanged+=(o,e)=>{resurrectOn=resurrectAuto.Checked;try{File.WriteAllText(ResurrectFile,resurrectOn?"1":"0");}catch{}Note(resurrectOn?"EP ressuscita: o sacerdote usa a macro 4 (Prece da Vida) em quem morrer.":"EP ressuscita desligado.");};
  Controls.Add(resurrectAuto);hints.SetToolTip(resurrectAuto,"Marcado: um sacerdote da lista seleciona quem morreu e roda a macro 4 (quarto espaço da janela R). Cadastre a Prece da Vida nela.");}
 // A party member out of sight cannot be selected: 3 failed tries = leave it alone for 60 s (and do not hold the sacerdotes).
 readonly Dictionary<int,int> outFails=new Dictionary<int,int>();readonly Dictionary<int,DateTime> outSkip=new Dictionary<int,DateTime>();
 static int OutsiderKey(uint id){return -(int)(id&0x3FFFFFFF)-1;}
 List<CharacterRef> PartyDead(List<CharacterRef> clerics,List<CharacterRef> known){var found=new List<CharacterRef>();
  foreach(var c in clerics){try{using(var m=new CharacterMemory(c.Pid)){if(m.Id!=c.Id)continue;uint team=m.Team;if(team==0)continue;var party=m.ReadParty();if(party==null)continue;
    uint count=m.U(team+0x18),items=m.U(team+0x14);if(count>64)continue;
    for(uint i=0;i<count;i++){uint member=m.U(items+4*i);if(member==0)continue;uint id=m.U(member+0xC);int life=unchecked((int)m.U(member+0x1C)),max=unchecked((int)m.U(member+0x24));
     if(id==0||id==c.Id||max<=0||life>0||known.Any(k=>k.Id==id)||found.Any(f=>f.Id==id))continue;DateTime skip;if(outSkip.TryGetValue(OutsiderKey(id),out skip)&&DateTime.UtcNow<skip)continue;
     found.Add(new CharacterRef{Pid=OutsiderKey(id),Id=id,Name="membro da PT "+id});}}}catch{}}
  return found;}
 // Called from the 2 s cycle (worker thread) with who is dead and who is a living sacerdote.
 void ResurrectWork(List<CharacterRef> dead,List<CharacterRef> clerics,List<string> notes){
  var now=DateTime.UtcNow;foreach(var pid in resNext.Keys.Where(p=>p>=0&&dead.All(d=>d.Pid!=p)).ToList()){resNext.Remove(pid);resAnnounced.Remove(pid);}
  lock(deadNow){deadNow.Clear();foreach(var d in dead)deadNow.Add(d.Pid);}
  // 0.51.9 (owner): also party members outside FarmZilla — read from each living sacerdote's party (member: id +0xC, life +0x1C,
  // max life +0x24; checked 08/11 against the real life of Lima/Limeira). Key = negative number from the id.
  if(resurrectOn&&clerics.Count>0){dead=new List<CharacterRef>(dead);foreach(var o in PartyDead(clerics,dead))dead.Add(o);}
  foreach(var pid in resNext.Keys.Where(p=>p<0&&dead.All(d=>d.Pid!=p)).ToList()){resNext.Remove(pid);resAnnounced.Remove(pid);}
  if(!resurrectOn||dead.Count==0||clerics.Count==0)return;
  // 0.50.5: while someone is dead the sacerdotes stay out of attack/support/follow, so nothing cuts the Prece da Vida.
  lock(resBusy)foreach(var c in clerics)resBusy[c.Pid]=now.AddSeconds(4);
  foreach(var d in dead){DateTime next;if(resNext.TryGetValue(d.Pid,out next)&&now<next)continue;
   // 0.50.7 (registro 07/11 13:28:13: Lima e Limago mortos, Limeira reservada e nenhuma Prece): the protection above marks every
   // sacerdote as busy, so the choice must not skip busy ones (that is the reservation itself); one sacerdote serves the dead in turn.
   var ep=clerics.FirstOrDefault(c=>c.Pid!=d.Pid&&!blocked.Contains(c.Pid)&&!RotaLock.Busy(c.Pid)&&!(castUntil.ContainsKey(c.Pid)&&now<castUntil[c.Pid]));if(ep==null)continue;
   resNext[d.Pid]=now.AddSeconds(10);castUntil[ep.Pid]=now.AddSeconds(6);
   try{bool running;using(var m=new CharacterMemory(ep.Pid)){if(m.Id!=ep.Id)continue;if(!m.MacroConfigured(ResurrectMacro)){if(resWarned.Add(ep.Pid))notes.Add(ep.Name+": cadastre a Prece da Vida na macro 4 (quarto espaço da janela R) para ressuscitar sozinho.");continue;}running=m.AnyMacroRunning();}
    lock(resBusy)resBusy[ep.Pid]=now.AddSeconds(10);
    // 0.50.6 (owner): with "aura infinita" (or any action) going on the EP cannot pray; ESC first — cancel action (2A 00), the
    // same command the client sends on ESC — then the macro selects the dead one by itself.
    TeleportBridge esc=null;bool escBlocked=false;try{esc=new TeleportBridge(ep.Pid);if(esc.CharacterId!=ep.Id)throw new Exception("Personagem mudou.");esc.SendControl(new byte[]{0x2A,0x00});}
    finally{if(esc!=null){esc.Dispose();if(!esc.RestoreSucceeded){lock(blocked)blocked.Add(ep.Pid);notes.Add(ep.Name+": ponte sem restauração no ESC; reinicie esse cliente.");escBlocked=true;}}}
    if(escBlocked)continue;System.Threading.Thread.Sleep(300);
    AttackBridge b=null;bool ok=false;try{b=new AttackBridge(ep.Pid);if(b.CharacterId!=ep.Id)throw new Exception("Personagem mudou.");if(running)b.StopMacro(-1);ok=b.StartMacroOn(d.Id,ResurrectMacro);}
    finally{if(b!=null){b.Dispose();if(!b.RestoreSucceeded){lock(blocked)blocked.Add(ep.Pid);notes.Add(ep.Name+": ponte sem restauração ao ressuscitar; reinicie esse cliente.");}}}
    if(d.Pid<0){if(ok)outFails.Remove(d.Pid);else{int f;outFails.TryGetValue(d.Pid,out f);if(++f>=3){outFails.Remove(d.Pid);outSkip[d.Pid]=now.AddSeconds(60);}else outFails[d.Pid]=f;}}
    if(ok&&resAnnounced.Add(d.Pid))notes.Add(ep.Name+": Prece da Vida em "+d.Name+" (macro 4).");else if(!ok&&resAnnounced.Add(d.Pid))notes.Add(ep.Name+": não conseguiu usar a macro 4 em "+d.Name+" (longe ou fora de vista?); tentando a cada 10 s.");}
   catch(Exception e){if(resAnnounced.Add(d.Pid))notes.Add(ep.Name+": ressuscitar "+d.Name+": "+e.Message);}
  }
 }
}
