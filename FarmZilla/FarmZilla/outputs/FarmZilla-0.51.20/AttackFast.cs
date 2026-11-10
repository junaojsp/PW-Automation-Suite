using System;using System.Threading.Tasks;
// 0.46.7 (owner: "virou no TG, já começa a combater todo mundo"): the attack no longer rides only the 300 ms timer that also
// pumps teleport, missions, meditation and follow. A 100 ms check reads the principal's target (memory read only) and runs the
// attack tick at once when, in Automático, a new monster is selected, and keeps checking running macros so a finished one is
// restarted and a dropped target stops everyone without waiting. A new target makes a running follow repair give way.
partial class AvalonInterno {
 System.Windows.Forms.Timer attackFast;bool attackFastRunning,attackTicking;
 void InitAttackFast(){attackFast=new System.Windows.Forms.Timer{Interval=100};attackFast.Tick+=async(s,e)=>await FastAttack();Shown+=(s,e)=>attackFast.Start();FormClosed+=(s,e)=>attackFast.Dispose();}
 async Task FastAttack(){
  if(attackFastRunning||attacks==null||authorizationDenied||controlLocked||!shortcutActive||closeInProgress||altoOn||buffing||MeditationPause||attackStopping||!attackJob.IsCompleted||teleport.Working||missions.Working)return;
  var primary=main.SelectedItem as Entry;if(primary==null)return;
  bool active=attacks.Active,fresh=false;
  if(!active){if(attackMode.SelectedIndex!=0)return;uint target;try{target=attacks.ReadTarget(primary.Ref());}catch{return;}if(!AttackController.MonsterId(target))return;fresh=target!=attacks.LastTarget;if(!fresh)return;}
  attackFastRunning=true;try{if(fresh&&busy&&repairingFollow)await YieldFollowRepair();await AttackTick();}catch{}finally{attackFastRunning=false;}
 }
}
