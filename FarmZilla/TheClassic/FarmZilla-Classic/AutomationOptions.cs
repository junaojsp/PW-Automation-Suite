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
 // Win32 INPUT is a tagged union. On x86 its size is 28 bytes
 // (4-byte type + 24-byte MOUSEINPUT/KEYBDINPUT/HARDWAREINPUT union).
 // Flattening KEYBDINPUT alone produces 20 bytes: SendInput rejects cbSize.
 [StructLayout(LayoutKind.Explicit,Size=28)]struct PotInput {
  [FieldOffset(0)]public uint type;
  [FieldOffset(4)]public ushort vk;
  [FieldOffset(6)]public ushort scan;
  [FieldOffset(8)]public uint flags;
  [FieldOffset(12)]public uint time;
  [FieldOffset(16)]public IntPtr extra;
 }
 [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint count,PotInput[] input,int size);
 System.Windows.Forms.Timer potTimer;bool potPolling;DateTime lastHp=DateTime.MinValue,lastMp=DateTime.MinValue;
 int potHp=50,potMp=35,potHpItemId,potMpItemId;Keys potHpKey=Keys.None,potMpKey=Keys.None;
 string PotConfigPath {get{return Path.Combine(Path.GetDirectoryName(settings),"farmzilla-auto-pot-config.txt");}}
 void InitAutoPot(){
  try{if(File.Exists(PotConfigPath)){var a=File.ReadAllLines(PotConfigPath);if(a.Length>=4){
   int hp=int.Parse(a[0]),mp=int.Parse(a[1]);Keys h=(Keys)int.Parse(a[2]),m=(Keys)int.Parse(a[3]);
   if(hp>=1&&hp<=99&&mp>=1&&mp<=99&&PotKeyValid(h)&&PotKeyValid(m)){potHp=hp;potMp=mp;potHpKey=h;potMpKey=m;}
   // Backwards compatible with the old four-line configuration file.
   int id;if(a.Length>=5&&int.TryParse(a[4],out id)&&id>=0)potHpItemId=id;
   if(a.Length>=6&&int.TryParse(a[5],out id)&&id>=0)potMpItemId=id;
  }}}catch{Note("Auto Pot: configuração não encontrada ou inválida.");}
  potTimer=new System.Windows.Forms.Timer{Interval=2000};
  potTimer.Tick+=async(s,e)=>await AutoPotTick();
  Shown+=(s,e)=>potTimer.Start();
  FormClosed+=(s,e)=>{potTimer.Stop();potTimer.Dispose();};
 }
 static bool PotKeyValid(Keys key){return key==Keys.None||(key>=Keys.D1&&key<=Keys.D8)||(key>=Keys.F1&&key<=Keys.F12);}
 static bool SendPotKey(Keys key,out int win32Error){
  win32Error=0;
  if(!PotKeyValid(key)||key==Keys.None)return false;
  var input=new[]{new PotInput{type=1,vk=(ushort)key},new PotInput{type=1,vk=(ushort)key,flags=2}};
  // Enforce the documented x86 INPUT size instead of silently sending a
  // truncated structure, even if this application is rebuilt differently.
  if(IntPtr.Size!=4||Marshal.SizeOf(typeof(PotInput))!=28){win32Error=87;return false;}
  uint sent=SendInput(2,input,28);
  if(sent==2)return true;
  win32Error=Marshal.GetLastWin32Error();
  return false;
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
   // Read the selected item IDs together with HP/MP. A chosen potion that
   // disappeared from the bag must never trigger a different hotkey blindly.
   var sample=await Task.Run(()=>{
    using(var game=new RotaAvalon.GameClient(entry.Pid,new System.Collections.Generic.Dictionary<string,bool>())){
     var state=game.Read();
     var bag=(potHpItemId>0||potMpItemId>0)?game.ReadInventory():null;
     return Tuple.Create(state,bag);
    }
   });
   var state=sample.Item1;var bag=sample.Item2;
   if(IsDisposed||!autoPotWanted||state.Hp<=0||state.MaxHp<=0||Win.GetForegroundWindow()!=current)return;
   uint nowPid;Win.GetWindowThreadProcessId(Win.GetForegroundWindow(),out nowPid);
   if(nowPid!=(uint)entry.Pid)return;
   var now=DateTime.UtcNow;
   bool hasHp=potHpItemId<=0||(bag!=null&&bag.QuantityOf(potHpItemId)>0);
   bool hasMp=potMpItemId<=0||(bag!=null&&bag.QuantityOf(potMpItemId)>0);
   int inputError;
   if(potHpKey!=Keys.None&&hasHp&&state.Hp*100L<=potHp*(long)state.MaxHp&&(now-lastHp).TotalSeconds>=8){
    if(SendPotKey(potHpKey,out inputError))lastHp=now;
    else Note("Auto Pot HP: SendInput falhou (Win32 "+inputError+", tecla "+potHpKey+").");
   }else if(potMpKey!=Keys.None&&hasMp&&state.MaxMp>0&&state.Mp*100L<=potMp*(long)state.MaxMp&&(now-lastMp).TotalSeconds>=8){
    if(SendPotKey(potMpKey,out inputError))lastMp=now;
    else Note("Auto Pot MP: SendInput falhou (Win32 "+inputError+", tecla "+potMpKey+").");
   }
  }catch(Exception ex){Note("Auto Pot: "+ex.Message);}
  finally{potPolling=false;}
 }
 void ConfigureAutoPot(){
  if(rotaView)return;
  potDrawerOpen=true;LayoutInlinePanels();
 }
}
