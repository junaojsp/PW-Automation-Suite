using System;using System.Collections.Generic;using System.Drawing;using System.IO;using System.Linq;using System.Threading;using System.Threading.Tasks;using System.Windows.Forms;
// 0.51.0 (owner): special combo of the WF (feiticeira) in combat. While the principal holds a monster TG and the WF is attacking,
// whenever the fox transformation (F7) and Ferir (F8, fox-only) are both ready: F7 → 2 s → Ferir on the monster → back to the
// normal macro 0 (she stands up by herself). Captured 07/11/2026 with Limaozinho principal:
//   13:37:12 50 00 38 01 00 00 0B 01 <own id>   skill 312 on herself = fox (F7)
//   13:37:21 29 00 40 01 00 00 0B 01 <monster>  skill 320 = Ferir (F8)
//   13:37:56 fox again (44 s later) and 13:37:58 Ferir again (36 s after the first).
// Cooldowns are timed (no reading of the game's cooldown yet), as the owner gave them: fox 7 s, Ferir 30 s (31 used). A refused cast just waits the next round.
partial class AvalonInterno {
 const uint FoxSkill=312,FerirSkill=320;const double FoxCooldown=8,FerirCooldown=30;// 0.51.6: owner — raposa 8 s, Ferir 30 s
 readonly CheckBox foxAuto=new CheckBox();volatile bool foxOn=true;System.Windows.Forms.Timer foxTimer;bool foxRunning;
 readonly Dictionary<int,DateTime> foxReady=new Dictionary<int,DateTime>(),foxBusy=new Dictionary<int,DateTime>();
 string FoxFile{get{return Path.Combine(Path.GetDirectoryName(settings),"wf-raposa.txt");}}
 bool FoxBusy(int pid){lock(foxBusy){DateTime until;return foxBusy.TryGetValue(pid,out until)&&DateTime.UtcNow<until;}}
 void FoxField(int x,int y){try{if(File.Exists(FoxFile))foxOn=File.ReadAllText(FoxFile).Trim()!="0";}catch{}
  foxAuto.SetBounds(x,y,78,20);foxAuto.Text="WF auto";foxAuto.Checked=foxOn;foxAuto.ForeColor=Rubi.Accent;foxAuto.FlatStyle=FlatStyle.Flat;foxAuto.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);
  foxAuto.CheckedChanged+=(o,e)=>{foxOn=foxAuto.Checked;try{File.WriteAllText(FoxFile,foxOn?"1":"0");}catch{}Note(foxOn?"WF auto: raposa + Ferir no combate e pet revivido/invocado sozinho.":"WF auto desligado.");};
  Controls.Add(foxAuto);hints.SetToolTip(foxAuto,"Pet: se o escorpião morrer, F5 (reviver) e F6 (invocar) sozinhos. Feiticeira atacando: quando a raposa (F7) e o Ferir (F8) estiverem prontos, vira raposa, dá o Ferir no monstro e volta ao combo. Repete a cada ~30 s (recarga do Ferir).");
  foxTimer=new System.Windows.Forms.Timer{Interval=500};foxTimer.Tick+=async(s,e)=>await FoxTick();Shown+=(s,e)=>foxTimer.Start();FormClosed+=(s,e)=>foxTimer.Dispose();}
 async Task FoxTick(){
  if(foxRunning||!foxOn||closeInProgress||authorizationDenied||!shortcutActive||altoOn||attacks==null)return;
  var primary=main.SelectedItem as Entry;if(primary==null)return;
  uint target=attacks.LastTarget;if(!AttackController.MonsterId(target))return;
  var wfs=new List<CharacterRef>();
  for(int i=0;i<rows.Length;i++){var e=rows[i].SelectedItem as Entry;if(e==null||e.Class!=ClassIcons.Venomancer||!attackRows[i].Enabled||!attackRows[i].Checked)continue;if(RotaLock.Busy(e.Pid)||IsDead(e.Pid)||ResBusy(e.Pid)||blocked.Contains(e.Pid)||!attacks.Engaged(e.Pid))continue;wfs.Add(e.Ref());}
  if(primary.Class==ClassIcons.Venomancer&&primaryAttack.Checked&&!RotaLock.Busy(primary.Pid)&&!IsDead(primary.Pid)&&!blocked.Contains(primary.Pid)&&attacks.Engaged(primary.Pid))wfs.Add(primary.Ref());
  var now=DateTime.UtcNow;wfs=wfs.Where(w=>{DateTime r;return !foxReady.TryGetValue(w.Pid,out r)||now>=r;}).ToList();if(wfs.Count==0)return;
  foxRunning=true;
  try{var notes=await Task.Run(()=>{var list=new List<string>();var jobs=wfs.Select(w=>Task.Run(()=>FoxOnce(w,target,list))).ToArray();Task.WaitAll(jobs);return list;});foreach(var n in notes)Note(n);}
  catch{}finally{foxRunning=false;}
 }
 void FoxOnce(CharacterRef wf,uint target,List<string> notes){
  var now=DateTime.UtcNow;lock(foxReady)foxReady[wf.Pid]=now.AddSeconds(Math.Max(FoxCooldown,FerirCooldown));
  // out of the attack while transforming, so the macro restart does not cut it
  // 0.51.1 (owner: she stayed as a fox after Ferir and macro 0 never came back): F7 again once the fox is ready (7 s after the
  // first F7) turns her back to standing; she stays out of the attack until then.
  lock(foxBusy)foxBusy[wf.Pid]=now.AddSeconds(FoxCooldown+10);
  // 0.51.16 (owner: "às vezes funciona, às vezes não"): each step now checks her real form — host+0xED = 1 fox (lying), 0 standing
  // (recorded 09/11 09:31–09:32 on Limaozinho, 6 changes, she ended standing with 0; +0x6B4/+0x420 change with it).
  try{
   bool fox=IsFox(wf);
   if(!fox){
    for(int i=0;i<2&&!fox;i++){FoxSend(wf,new byte[]{0x2A,0x00},null);Thread.Sleep(200);FoxSend(wf,null,Skill(0x50,FoxSkill,wf.Id));fox=WaitFox(wf,true,2500);}
    if(!fox){lock(foxReady)foxReady[wf.Pid]=DateTime.UtcNow.AddSeconds(3);lock(notes)notes.Add(wf.Name+": não virou raposa; tento de novo em 3 s.");return;}}
   var transformed=DateTime.UtcNow;Thread.Sleep(300);
   // Ferir (macro 2, only works lying) right after she is a fox; up to 3 tries while the macro does not start.
   bool ferir=false;for(int i=0;i<3&&!ferir&&IsFox(wf);i++){ferir=FoxMacro(wf,target);if(!ferir)Thread.Sleep(1000);}
   // 0.51.17 (owner: "deitou, deu ferir, volta a ficar em pé e começa a bater — rápido"): as soon as the Ferir macro ends (max 2,5 s)
   // she tries to stand with macro 3 every 0,8 s until the form says standing (the fox skill may still be recharging), up to
   // the fox recharge + 3 s. Without macro 3: ESC + warning.
   if(ferir)WaitMacroEnd(wf,2500);
   bool standing=!IsFox(wf);var giveUp=transformed.AddSeconds(FoxCooldown+3);
   while(!standing&&DateTime.UtcNow<giveUp){if(!FoxStandUp(wf)){FoxSend(wf,new byte[]{0x2A,0x00},null);lock(notes)if(standWarned.Add(wf.Pid))notes.Add(wf.Name+": para levantar sozinha, coloque a raposa (F7) sozinha na macro 3 (quarto espaço da janela R).");}
    standing=WaitFox(wf,false,800);}
   lock(foxBusy)foxBusy[wf.Pid]=DateTime.UtcNow.AddMilliseconds(300);
   lock(notes)notes.Add(wf.Name+": raposa → Ferir "+(ferir?"ok":"não iniciou")+" → "+(standing?"em pé":"ainda deitada")+"; próximo em ~"+(int)Math.Max(FoxCooldown,FerirCooldown)+" s.");
   if(!standing)lock(foxReady)foxReady[wf.Pid]=DateTime.UtcNow.AddSeconds(2);}
  catch(Exception e){lock(notes)notes.Add(wf.Name+": raposa/Ferir: "+e.Message);}
 }
 static bool IsFox(CharacterRef wf){try{using(var m=new CharacterMemory(wf.Pid)){if(m.Id!=wf.Id)return false;return m.Read(m.Host+0xED,1)[0]==1;}}catch{return false;}}
 static void WaitMacroEnd(CharacterRef wf,int ms){var clock=System.Diagnostics.Stopwatch.StartNew();Thread.Sleep(300);while(clock.ElapsedMilliseconds<ms){try{using(var m=new CharacterMemory(wf.Pid))if(!m.AnyMacroRunning())return;}catch{return;}Thread.Sleep(100);}}
 static bool WaitFox(CharacterRef wf,bool fox,int ms){var clock=System.Diagnostics.Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){if(IsFox(wf)==fox)return true;Thread.Sleep(100);}return IsFox(wf)==fox;}
 // Stand-up command (second F7 while a fox): not confirmed yet — null = none is sent.
 static Func<CharacterRef,byte[]> StandUp=null;
 const int FerirMacro=2,StandMacro=3;readonly HashSet<int> standWarned=new HashSet<int>();
 bool FoxStandUp(CharacterRef wf){bool configured;using(var m=new CharacterMemory(wf.Pid)){if(m.Id!=wf.Id)return false;configured=m.MacroConfigured(StandMacro);}if(!configured)return false;
  AttackBridge b=null;try{b=new AttackBridge(wf.Pid);if(b.CharacterId!=wf.Id)return false;return b.StartMacroOn(wf.Id,StandMacro);}
  finally{if(b!=null){b.Dispose();if(!b.RestoreSucceeded){lock(blocked)blocked.Add(wf.Pid);}}}}
 bool FoxMacro(CharacterRef wf,uint target){bool configured;using(var m=new CharacterMemory(wf.Pid)){if(m.Id!=wf.Id)return false;configured=m.MacroConfigured(FerirMacro);}
  if(!configured)throw new Exception("cadastre o Ferir sozinho na macro 2 (terceiro espaço da janela R) da WF.");
  AttackBridge b=null;try{b=new AttackBridge(wf.Pid);if(b.CharacterId!=wf.Id)throw new Exception("Personagem mudou.");return b.StartMacroOnMonster(target,FerirMacro);}
  finally{if(b!=null){b.Dispose();if(!b.RestoreSucceeded){lock(blocked)blocked.Add(wf.Pid);throw new Exception("ponte sem restauração; reinicie esse cliente.");}}}}
 static byte[] Skill(byte type,uint skill,uint target){var p=new byte[12];p[0]=type;p[1]=0;BitConverter.GetBytes(skill).CopyTo(p,2);p[6]=0x0B;p[7]=0x01;BitConverter.GetBytes(target).CopyTo(p,8);return p;}
 void FoxSend(CharacterRef c,byte[] control,byte[] skill){TeleportBridge b=null;
  try{b=new TeleportBridge(c.Pid);if(b.CharacterId!=c.Id)throw new Exception("Personagem mudou.");if(control!=null)b.SendControl(control);else b.SendFoxSkill(skill);}
  finally{if(b!=null){b.Dispose();if(!b.RestoreSucceeded){lock(blocked)blocked.Add(c.Pid);throw new Exception("ponte sem restauração; reinicie esse cliente.");}}}}
}
