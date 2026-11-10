using System;using System.Collections.Generic;using System.Drawing;using System.Linq;using System.Threading.Tasks;using System.Windows.Forms;
// Mat Farm inside the main window (side menu), sharing FarmZilla's license, log and class icons. 0.40.0 (owner): up to 10
// accounts at once, one tab each with the same screen as before (its own map, materials, plan and farm); a running farm in
// a hidden tab keeps going.
partial class AvalonInterno {
 const int MaxRotaAccounts=10;
 // Off-screen previews only: the accounts open offline (no process scan, no timer).
 internal static bool RotaOffline;
 RotaAvalon.RotaForm rotaWindow;readonly List<RotaAvalon.RotaForm> rotaWindows=new List<RotaAvalon.RotaForm>();
 Panel rotaTabs,rotaBody;System.Windows.Forms.Timer rotaTabTimer;string rotaTabState="";
 bool EnsureRota(){
  if(rotaWindow!=null&&!rotaWindow.IsDisposed)return true;
  try{if(rotaTabs==null){rotaTabs=new Panel{Dock=DockStyle.Top,Height=40,BackColor=Rubi.Background};rotaBody=new Panel{Dock=DockStyle.Fill,BackColor=Rubi.Background};rotaHost.Controls.Add(rotaBody);rotaHost.Controls.Add(rotaTabs);
    rotaTabTimer=new System.Windows.Forms.Timer{Interval=1000};rotaTabTimer.Tick+=(s,e)=>RenderRotaTabs(false);rotaTabTimer.Start();FormClosed+=(s,e)=>rotaTabTimer.Dispose();}
   AddRotaAccount();return rotaWindow!=null;}
  catch(Exception e){rotaWindow=null;Note("Mat Farm: "+e.Message);return false;}
 }
 void AddRotaAccount(){
  if(rotaWindows.Count>=MaxRotaAccounts){Note("Mat Farm: limite de "+MaxRotaAccounts+" contas.");return;}
  int slot=Enumerable.Range(1,MaxRotaAccounts).First(n=>rotaWindows.All(w=>w.Slot!=n));
  var form=new RotaAvalon.RotaForm(RotaOffline,slot){ClassOf=ClassOfPid,Allowed=()=>!authorizationDenied&&!closeInProgress,Notify=Note,StartAttack=RotaStartAttack,AttackRunning=RotaAttackRunning,ReviveInTown=RotaReviveInTown};form.Embed(rotaBody);
  rotaWindows.Add(form);rotaWindows.Sort((a,b)=>a.Slot.CompareTo(b.Slot));SelectRota(form);
 }
 void SelectRota(RotaAvalon.RotaForm form){rotaWindow=form;foreach(var w in rotaWindows)if(!w.IsDisposed)w.Visible=rotaView&&w==form;if(rotaView&&!form.IsDisposed)form.BringToFront();RenderRotaTabs(true);}
 async Task RemoveRotaAccount(RotaAvalon.RotaForm form){
  if(rotaWindows.Count<=1){Note("Mat Farm: deixe pelo menos uma conta.");return;}
  if(form.FarmRunning&&MessageBox.Show(this,"Parar o farm de "+(form.AccountName??"Conta "+form.Slot)+" e fechar esta conta?","Mat Farm",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
  int index=rotaWindows.IndexOf(form);rotaWindows.Remove(form);await form.CloseForOwner();var next=rotaWindows[Math.Max(0,Math.Min(index,rotaWindows.Count-1))];SelectRota(next);
 }
 // Tab strip: one button per account (character name when connected, ● while farming), "+ Conta" and close for the selected one.
 void RenderRotaTabs(bool force){
  if(rotaTabs==null||IsDisposed)return;
  string state=String.Join("|",rotaWindows.Select(w=>w.Slot+":"+(w.AccountName??"")+":"+w.FarmRunning+":"+(w==rotaWindow)));if(!force&&state==rotaTabState)return;rotaTabState=state;
  rotaTabs.SuspendLayout();try{foreach(Control c in rotaTabs.Controls.Cast<Control>().ToArray()){rotaTabs.Controls.Remove(c);c.Dispose();}
   int x=12;foreach(var w in rotaWindows){var form=w;string name=form.AccountName??"Conta "+form.Slot;var tab=new NeonButton{Text=(form.FarmRunning?"● ":"")+name,Left=x,Top=6,Width=Math.Min(130,Math.Max(76,TextRenderer.MeasureText(name,new Font("Segoe UI",(float)(8)*global::UiScale.F)).Width+30)),Height=28,Primary=form==rotaWindow};
    tab.Click+=(s,e)=>SelectRota(form);hints.SetToolTip(tab,(form.Connected?"Conectado: "+name:"Conta "+form.Slot+" sem personagem conectado")+(form.FarmRunning?" · farm rodando":""));rotaTabs.Controls.Add(tab);x+=tab.Width+6;}
   if(rotaWindows.Count<MaxRotaAccounts){var add=new NeonButton{Text="+ Conta",Left=x,Top=6,Width=72,Height=28};add.Click+=(s,e)=>AddRotaAccount();hints.SetToolTip(add,"Abre outra conta do Mat Farm (até "+MaxRotaAccounts+"), com o mapa e o farm dela.");rotaTabs.Controls.Add(add);x+=78;}
   if(rotaWindows.Count>1&&rotaWindow!=null){var current=rotaWindow;var close=new NeonButton{Text="Fechar conta",Left=Math.Max(x,rotaTabs.Width-112),Top=6,Width=100,Height=28,Anchor=AnchorStyles.Top|AnchorStyles.Right};close.Click+=async(s,e)=>await RemoveRotaAccount(current);rotaTabs.Controls.Add(close);}
  }finally{rotaTabs.ResumeLayout(true);}
 }
 // Defense during a route (route thread): macro 0 on the monster through the same bridge as Atk; the character is out of
 // FarmZilla's own attack while it runs a route (RotaLock), so the two never drive it at once.
 readonly NativeAttackRuntime rotaAttacks=new NativeAttackRuntime();readonly Dictionary<int,uint> rotaCombos=new Dictionary<int,uint>();
 CharacterRef RotaRef(int pid){if(IsDisposed)return null;var entry=(Entry)Invoke(new Func<Entry>(()=>main.Items.Cast<Entry>().FirstOrDefault(e=>e.Pid==pid)));return entry==null?null:entry.Ref();}
 // 0.50.4: revive at the nearest town for a dead Mat Farm account (command captured 07/11/2026 12:04:27, Limago principal).
 string RotaReviveInTown(int pid){var c=RotaRef(pid);if(c==null)return "personagem fora da lista do FarmZilla; clique Sincronizar personagens.";TeleportBridge b=null;
  try{b=new TeleportBridge(pid);if(b.CharacterId!=c.Id)return "personagem mudou";b.SendTownRevive();return null;}catch(Exception e){return e.Message;}
  finally{if(b!=null){b.Dispose();if(!b.RestoreSucceeded)lock(blocked)blocked.Add(pid);}}}
 string RotaStartAttack(int pid,uint monster){var c=RotaRef(pid);if(c==null)return "personagem fora da lista do FarmZilla; clique Sincronizar personagens.";var result=rotaAttacks.Start(c,monster);lock(rotaCombos)rotaCombos[pid]=result.Combo;return result.Message;}
 bool RotaAttackRunning(int pid,uint monster){var c=RotaRef(pid);if(c==null)return false;uint combo;lock(rotaCombos)if(!rotaCombos.TryGetValue(pid,out combo))return false;try{return combo!=0&&rotaAttacks.Running(c,combo,monster);}catch{return false;}}
 int ClassOfPid(int pid){var entry=main.Items.Cast<Entry>().FirstOrDefault(e=>e.Pid==pid);return entry==null?-1:entry.Class;}
 // FarmZilla closing or Mat Farm taken away: every account's route stops (waiting for the native call in progress) and
 // unsaved edits go to an autosave file.
 async Task CloseRota(){foreach(var window in rotaWindows.ToArray())if(!window.IsDisposed)await window.CloseForOwner();rotaWindows.Clear();rotaWindow=null;RenderRotaTabs(true);}
}
