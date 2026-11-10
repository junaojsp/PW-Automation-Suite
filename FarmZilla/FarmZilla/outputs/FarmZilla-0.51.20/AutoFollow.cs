using System;using System.Linq;
// 0.46.0 (owner): FarmZilla turns follow on by itself when it opens. 0.46.2 (log 22:04:49: only Limaozinho was in a slot
// at that moment, the others were placed afterwards and stayed still): while follow is wanted, every secondary that is
// or later gets into a slot follows the principal too. The follow key turning follow off, or Alto cultivo, stop this.
partial class AvalonInterno {
 bool followWanted=true;
 void InitAutoFollow(){
  var timer=new System.Windows.Forms.Timer{Interval=1500};
  timer.Tick+=(s,e)=>{try{EnsureEveryoneFollows();}catch(Exception ex){DiagnosticLog.Write("Seguir automático: "+ex.Message);}};
  Shown+=(s,e)=>timer.Start();FormClosed+=(s,e)=>timer.Dispose();
 }
 void EnsureEveryoneFollows(){
  if(!followWanted||!shortcutActive||altoOn||closeInProgress||authorizationDenied||controlLocked||busy||teleport.Working||missions.Working)return;
  var primary=main.SelectedItem as Entry;if(primary==null)return;
  var selected=followers.CheckedItems.Cast<Entry>().Where(x=>x.Pid!=primary.Pid&&!RotaLock.Busy(x.Pid)&&ProcessAlive(x.Pid)).ToArray();if(selected.Length==0)return;
  SyncFollowIntent();
  var missing=selected.Where(x=>followIntents.ContainsKey(x.Pid)&&!followIntents[x.Pid].Enabled).ToArray();if(missing.Length==0)return;
  foreach(var x in missing)followIntents[x.Pid].ToggleTo(true);
  status.Text="SEGUIR ATIVO · mantido após teleporte";
  Note("Seguir ligado em "+String.Join(", ",missing.Select(x=>x.Name))+" (todas as contas seguem o principal).");
  var task=MaintainFollowing();
 }
}
