using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FarmZilla.TheClassic {
 // Classic copy of AttackUI.cs: same AttackController, modes, Atq./Sup. rows and support target.
 sealed partial class ClassicMirrorForm {
  static readonly Color SupportColor=Color.FromArgb(110,205,120);
  const int AttackHotkey=5;
  [DllImport("user32.dll")]static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
  [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr window,int id);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
  readonly CheckBox primaryAttack=new TickBox{Checked=true};
  readonly CheckBox[] attackRows=new CheckBox[9],supportRows=new CheckBox[9];
  readonly ComboBox attackMode=new CyanCombo{DropDownStyle=ComboBoxStyle.DropDownList},supportTarget=new CyanCombo{DropDownStyle=ComboBoxStyle.DropDownList};
  readonly TextBox attackKey=new TextBox();
  readonly int[] rolePid=new int[9];
  readonly HashSet<int> attackBlocked=new HashSet<int>();
  AttackController attacks;Task attackJob=Task.FromResult(0);Timer attackTimer;
  bool attackRequested,attackStopRequested,attackTicking,attackStopping,hotkeyOn;
  Keys attackChoice=Keys.D1;bool attackCtrl,attackAlt,attackShift;
  string AttackKeyText(){return(attackCtrl?"Ctrl+":"")+(attackAlt?"Alt+":"")+(attackShift?"Shift+":"")+(attackChoice>=Keys.D0&&attackChoice<=Keys.D9?((int)(attackChoice-Keys.D0)).ToString():attackChoice.ToString());}
  static CharacterRef Ref(FollowAccount a){return a==null?null:new CharacterRef{Pid=a.Pid,Id=a.Id,Name=a.Name};}
  void InitAttack(){
   attacks=new AttackController(new ClassicAttackRuntime(),attackBlocked,s=>Note("ATTACK "+s));
   attackKey.Text=AttackKeyText();
   attackKey.KeyDown+=(s,e)=>{e.SuppressKeyPress=true;if(e.KeyCode==Keys.ControlKey||e.KeyCode==Keys.ShiftKey||e.KeyCode==Keys.Menu)return;attackChoice=e.KeyCode;attackCtrl=e.Control;attackAlt=e.Alt;attackShift=e.Shift;attackKey.Text=AttackKeyText();SetAttackHotkey(false);};
   attackMode.SelectedIndexChanged+=(s,e)=>{RequestAttackStop();Note("ATTACK_MODE "+attackMode.SelectedItem);};
   primaryAttack.CheckedChanged+=(s,e)=>RequestAttackStop();
   for(int i=0;i<attackRows.Length;i++){int k=i;supportRows[i].CheckedChanged+=(s,e)=>SupportStateChanged(k);attackRows[i].CheckedChanged+=(s,e)=>AttackStateChanged(k);}
   attackTimer=new Timer{Interval=300};attackTimer.Tick+=async(s,e)=>{UpdateAttackHotkey();await AttackTick();};attackTimer.Start();
   FormClosed+=(s,e)=>{attackTimer.Dispose();SetAttackHotkey(false);};
   UpdateAttackRows();
  }
  void FillSupportTargets(){
   var keep=supportTarget.SelectedItem as FollowAccount;supportTarget.Items.Clear();supportTarget.Items.Add("Líder (principal)");
   foreach(var row in rows){var a=row.SelectedItem as FollowAccount;if(a!=null)supportTarget.Items.Add(a);}
   int index=keep==null?-1:supportTarget.Items.IndexOf(keep);supportTarget.SelectedIndex=index>0?index:0;
  }
  // A character put in a slot starts attacking (a priest starts in support); the boxes can be changed any time.
  void UpdateAttackRows(){
   if(attackRows[0]==null)return;FillSupportTargets();
   for(int i=0;i<rows.Length;i++){
    var entry=rows[i].SelectedItem as FollowAccount;bool selected=entry!=null;attackRows[i].Enabled=selected;bool cleric=selected&&entry.ClassId==ClassIcons.Cleric;supportRows[i].Visible=supportRows[i].Enabled=cleric;if(!cleric)supportRows[i].Checked=false;
    if(!selected){attackRows[i].Checked=false;rolePid[i]=0;continue;}if(rolePid[i]==entry.Pid)continue;rolePid[i]=entry.Pid;
    if(cleric){attackRows[i].Checked=false;supportRows[i].Checked=true;}else attackRows[i].Checked=true;
   }
  }
  void SupportStateChanged(int i){var sup=supportRows[i];if(sup.Checked&&attackRows[i].Checked)attackRows[i].Checked=false;var entry=rows[i].SelectedItem as FollowAccount;if(entry!=null&&!syncingRows){RequestAttackStop();Note(entry.Name+(sup.Checked?": suporte ao líder (macro 2).":": suporte desligado."));}}
  void AttackStateChanged(int i){if(attackRows[i].Checked&&supportRows[i].Checked)supportRows[i].Checked=false;}
  void RequestAttackStop(){attackStopRequested=true;attackRequested=false;}
  bool GameInFront(){uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);return principal.Items.OfType<FollowAccount>().Any(a=>a.Pid==pid);}
  void UpdateAttackHotkey(){SetAttackHotkey(ready&&!closing&&attackMode.SelectedIndex==1&&GameInFront());}
  void SetAttackHotkey(bool on){
   if(on==hotkeyOn||!IsHandleCreated)return;
   if(!on){UnregisterHotKey(Handle,AttackHotkey);hotkeyOn=false;return;}
   uint mods=0x4000|(attackCtrl?2u:0u)|(attackAlt?1u:0u)|(attackShift?4u:0u);hotkeyOn=RegisterHotKey(Handle,AttackHotkey,mods,(uint)attackChoice);
   if(!hotkeyOn){Note("ATTACK Tecla de Atacar ocupada; escolha outra.");attackMode.SelectedIndex=0;}
  }
  protected override void WndProc(ref Message m){if(m.Msg==0x312&&m.WParam.ToInt32()==AttackHotkey){var _=RequestAttack();return;}base.WndProc(ref m);}
  Task RequestAttack(){
   if(closing||!ready)return Task.FromResult(0);
   if(attackMode.SelectedIndex!=1){Note("ATTACK A tecla Atacar é usada no modo Manual por tecla.");return Task.FromResult(0);}
   if(!primaryAttack.Checked&&!attackRows.Any(c=>c.Enabled&&c.Checked)&&!supportRows.Any(c=>c.Visible&&c.Checked)){Note("ATTACK Marque Atacar no principal ou nos secundários desejados.");return Task.FromResult(0);}
   var primary=principal.SelectedItem as FollowAccount;if(primary==null){Note("ATTACK Escolha o principal.");return Task.FromResult(0);}
   try{if(!AttackController.MonsterId(attacks.ReadTarget(Ref(primary)))){Note("ATTACK Selecione um monstro no principal antes de apertar Atacar.");return Task.FromResult(0);}}catch(Exception e){Note("ATTACK "+e.Message);return Task.FromResult(0);}
   attackRequested=true;return AttackTick();
  }
  async Task AttackTick(){
   if(attacks==null||!ready||closing||busy||attackTicking||attackStopping||!attackJob.IsCompleted)return;
   var primary=principal.SelectedItem as FollowAccount;
   var slots=rows.Select((row,i)=>new{Account=row.SelectedItem as FollowAccount,Index=i}).Where(x=>x.Account!=null&&primary!=null&&x.Account.Pid!=primary.Pid&&attackRows[x.Index].Enabled).ToArray();
   var selected=slots.Where(x=>attackRows[x.Index].Checked||supportRows[x.Index].Checked).Select(x=>Ref(x.Account)).ToList();
   if(primary!=null&&primaryAttack.Checked)selected.Insert(0,Ref(primary));
   var support=new HashSet<int>(slots.Where(x=>supportRows[x.Index].Checked).Select(x=>x.Account.Pid));
   var supportOn=Ref(supportTarget.SelectedItem as FollowAccount);
   bool stop=attackStopRequested,request=attackRequested,automatic=attackMode.SelectedIndex==0;attackStopRequested=attackRequested=false;
   attackTicking=true;
   try{var leader=Ref(primary);var list=selected.ToArray();attackJob=Task.Run(()=>{if(stop)attacks.StopAll();attacks.Tick(leader,list,true,automatic,request,support,supportOn);});await attackJob;}
   catch(Exception e){attackStopRequested=true;Note("ATTACK_ERROR "+e.Message);}
   finally{attackTicking=false;}
   if(attacks.TakeReleased()&&!closing)await ResumeFollow();
  }
  // The principal dropped the monster: accounts this session put on follow go back to following.
  async Task ResumeFollow(){
   if(owned.Count==0)return;var leader=principal.SelectedItem as FollowAccount;if(leader==null)return;
   await Work(async()=>{foreach(var account in owned.Values.ToArray()){if(attacks.Engaged(account.Pid))continue;try{await Task.Run(()=>service.Command(leader,account,false));}catch(Exception ex){Note("COMMAND_ERROR pid="+account.Pid+" "+ex.Message);}}});
  }
  async Task StopAttacks(){
   RequestAttackStop();if(attacks==null)return;attackStopping=true;
   try{try{await attackJob;}catch(Exception e){Note("ATTACK_ERROR anterior: "+e.Message);}await Task.Run(()=>attacks.StopAll());attackStopRequested=false;}
   finally{attackStopping=false;}
  }
 }
}
