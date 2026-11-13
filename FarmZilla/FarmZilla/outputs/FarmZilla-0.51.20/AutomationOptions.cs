using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

// New automation options are explicitly opt-in and are initially in diagnostic-only mode.
// Never emit an unverified client packet for potion usage, buff effects or recall acceptance.
partial class AvalonInterno {
 readonly CheckBox autoPotOption=new CheckBox();
 readonly CheckBox recallOption=new CheckBox();
 readonly CheckBox epBuffOption=new CheckBox();
 bool autoPotWanted,recallWanted,epBuffWanted;

 string AutomationFlagPath(string name){return Path.Combine(Path.GetDirectoryName(settings),"farmzilla-"+name+".txt");}
 bool ReadAutomationFlag(string name){try{return File.Exists(AutomationFlagPath(name))&&File.ReadAllText(AutomationFlagPath(name)).Trim()=="1";}catch{return false;}}
 void WriteAutomationFlag(string name,bool enabled){
  try{Directory.CreateDirectory(Path.GetDirectoryName(settings));File.WriteAllText(AutomationFlagPath(name),enabled?"1":"0");}
  catch(Exception ex){Note("Não foi possível salvar "+name+": "+ex.Message);}
 }
 CheckBox AutomationBox(CheckBox box,string title,string name,int x,int y,int width,string hint,Action<bool> change){
  box.SetBounds(x,y,width,19);
  box.Text=title;box.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);
  box.ForeColor=Rubi.Muted;box.BackColor=CardTone(137,310,y);
  box.FlatStyle=FlatStyle.Flat;
  box.AccessibleName=title;box.Checked=ReadAutomationFlag(name);
  change(box.Checked);
  box.CheckedChanged+=(s,e)=>{
   change(box.Checked);WriteAutomationFlag(name,box.Checked);
   Note(title+(box.Checked?(name=="auto-pot"?": Auto Pot habilitado, configure as teclas na engrenagem.":": opção registrada; protocolo ainda não validado."):": desligado."));
  };
  Controls.Add(box);hints.SetToolTip(box,hint);return box;
 }
 void InitAutomationOptions(){
  // Compact CheckBoxes, same Flat style as Meditacao/Reviver; no buttons.
  AutomationBox(autoPotOption,"Auto Pot","auto-pot",238,402,74,
   "Auto Pot só no jogo em primeiro plano. Use a engrenagem para ajustar as teclas e os limites HP/MP.",
   enabled=>autoPotWanted=enabled);
  autoPotOption.MouseUp+=(s,e)=>{if(e.Button==MouseButtons.Right)ConfigureAutoPot();};
  InitAutoPot();
  AutomationBox(recallOption,"Aceitar Recall","aceitar-recall",238,424,138,
   "Pendente: validar protocolo e origem do convite de Recall do líder. Marcar não aceita convites ainda.",
   enabled=>recallWanted=enabled);
  AutomationBox(epBuffOption,"EP buff PT","ep-buff-pt",312,402,64,
   "Pendente: confirmar lista de efeitos dos membros e macro do EP antes de automatizar.",
   enabled=>epBuffWanted=enabled);
 }
 // Auto Pot only on the foreground ElementClient, with user-selected potion hotkeys.
 // No unverified game packet is sent; Windows keyboard input is limited to an identified client.
 [StructLayout(LayoutKind.Sequential)]struct PotInput {public uint type;public ushort vk,scan;public uint flags,time;public IntPtr extra;}
 [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint count,PotInput[] input,int size);
 System.Windows.Forms.Timer potTimer;bool potPolling;DateTime lastHp=DateTime.MinValue,lastMp=DateTime.MinValue;
 int potHp=50,potMp=35;Keys potHpKey=Keys.None,potMpKey=Keys.None;
 string PotConfigPath {get{return Path.Combine(Path.GetDirectoryName(settings),"farmzilla-auto-pot-config.txt");}}
 void InitAutoPot(){
  try{if(File.Exists(PotConfigPath)){var a=File.ReadAllLines(PotConfigPath);if(a.Length==4){
   int hp=int.Parse(a[0]),mp=int.Parse(a[1]);Keys h=(Keys)int.Parse(a[2]),m=(Keys)int.Parse(a[3]);
   if(hp>=1&&hp<=99&&mp>=1&&mp<=99&&PotKeyValid(h)&&PotKeyValid(m)){potHp=hp;potMp=mp;potHpKey=h;potMpKey=m;}
  }}}catch{Note("Auto Pot: configuração não encontrada ou inválida.");}
  potTimer=new System.Windows.Forms.Timer{Interval=2000};
  potTimer.Tick+=async(s,e)=>await AutoPotTick();
  Shown+=(s,e)=>potTimer.Start();
  FormClosed+=(s,e)=>{potTimer.Stop();potTimer.Dispose();};
 }
 static bool PotKeyValid(Keys key){return key==Keys.None||(key>=Keys.F1&&key<=Keys.F12);}
 static bool SendPotKey(Keys key){
  if(!PotKeyValid(key)||key==Keys.None)return false;
  var input=new[]{new PotInput{type=1,vk=(ushort)key},new PotInput{type=1,vk=(ushort)key,flags=2}};
  return SendInput(2,input,Marshal.SizeOf(typeof(PotInput)))==2;
 }
 async Task AutoPotTick(){
  if(potPolling||!autoPotWanted||closeInProgress||authorizationDenied||controlLocked||!shortcutActive||busy||teleport.Working||missions.Working)return;
  if(potHpKey==Keys.None&&potMpKey==Keys.None)return;
  IntPtr current=Win.GetForegroundWindow();uint pid=0;
  if(current==IntPtr.Zero||Win.GetWindowThreadProcessId(current,out pid)==0||pid==0)return;
  var entry=main.Items.Cast<object>().OfType<Entry>().FirstOrDefault(x=>x.Pid==(int)pid&&!blocked.Contains(x.Pid)&&!RotaLock.Busy(x.Pid));
  if(entry==null||IsDead(entry.Pid)||ResBusy(entry.Pid))return;
  potPolling=true;
  try{
   var state=await Task.Run(()=>{using(var game=new RotaAvalon.GameClient(entry.Pid,new System.Collections.Generic.Dictionary<string,bool>()))return game.Read();});
   if(IsDisposed||!autoPotWanted||state.Hp<=0||state.MaxHp<=0||Win.GetForegroundWindow()!=current)return;
   uint nowPid;Win.GetWindowThreadProcessId(Win.GetForegroundWindow(),out nowPid);
   if(nowPid!=(uint)entry.Pid)return;
   var now=DateTime.UtcNow;
   if(potHpKey!=Keys.None&&state.Hp*100L<=potHp*(long)state.MaxHp&&(now-lastHp).TotalSeconds>=8){
    lastHp=now;if(!SendPotKey(potHpKey))Note("Auto Pot HP: comando de teclado recusado pelo Windows.");
   }else if(potMpKey!=Keys.None&&state.MaxMp>0&&state.Mp*100L<=potMp*(long)state.MaxMp&&(now-lastMp).TotalSeconds>=8){
    lastMp=now;if(!SendPotKey(potMpKey))Note("Auto Pot MP: comando de teclado recusado pelo Windows.");
   }
  }catch(Exception ex){Note("Auto Pot: "+ex.Message);}
  finally{potPolling=false;}
 }
 void ConfigureAutoPot(){
  if(rotaView)return;
  potDrawerOpen=true;LayoutInlinePanels();
 }
}
