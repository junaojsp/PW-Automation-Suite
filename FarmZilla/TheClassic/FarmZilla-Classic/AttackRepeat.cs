using System;using System.Threading.Tasks;
// 0.41.0 (owner): in "Manual por tecla" the Atacar key now toggles a repeat — it attacks at once and again every 4 s on the
// principal's current monster, until the key is pressed again (or the mode changes, FarmZilla is turned off or closes).
// Without a monster selected a cycle is just skipped; the repeat keeps waiting for one.
partial class AvalonInterno {
 const int AttackRepeatMs=4000;
 bool attackRepeat,attackAccepted;System.Windows.Forms.Timer attackRepeatTimer;
 async Task AttackKey(){
  if(attackRepeat){StopAttackRepeat("Atacar: repetição desligada.");return;}
  // 0.46.7: the key comes first — a running follow repair or attack check gives way instead of "Aguarde a operação atual".
  await YieldFollowRepair();
  attackAccepted=false;var task=RequestAttack();
  if(attackAccepted){attackRepeat=true;if(attackRepeatTimer==null){attackRepeatTimer=new System.Windows.Forms.Timer{Interval=AttackRepeatMs};attackRepeatTimer.Tick+=(s,e)=>RepeatAttack();FormClosed+=(s,e)=>attackRepeatTimer.Dispose();}attackRepeatTimer.Start();Note("Atacar: repetindo a cada "+AttackRepeatMs/1000+" s. Aperte a tecla de novo para parar.");}
  await task;
 }
 void StopAttackRepeat(string reason){if(!attackRepeat)return;attackRepeat=false;if(attackRepeatTimer!=null)attackRepeatTimer.Stop();RequestAttackStop();if(reason!=null)Note(reason);}
 void RepeatAttack(){
  if(!attackRepeat)return;
  if(closeInProgress||authorizationDenied||controlLocked||!shortcutActive||attackMode.SelectedIndex!=1){StopAttackRepeat("Atacar: repetição encerrada.");return;}
  if(busy||buffing||MeditationPause||teleport.Working||missions.Working)return;
  var primary=main.SelectedItem as Entry;if(primary==null)return;
  try{if(!AttackController.MonsterId(attacks.ReadTarget(primary.Ref())))return;}catch{return;}
  attackRequested=true;var task=AttackTick();
 }
}
