using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FarmZilla.TheClassic {
 public sealed class FollowAccount {
  public int Pid;public uint Id;public string Name;public long Started;
  public override string ToString(){return Name+" · PID "+Pid;}
 }
 static class ClassicLog {
  static readonly object gate=new object();
  public static string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FarmZilla","TheClassic","logs");
  public static string PathName=Path.Combine(Folder,"teste-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Process.GetCurrentProcess().Id+".log");
  public static void Write(string text){lock(gate){Directory.CreateDirectory(Folder);File.AppendAllText(PathName,DateTime.UtcNow.ToString("o")+" "+text+Environment.NewLine,new UTF8Encoding(false));}}
 }
 public static class FollowState {
  public const uint WorkManager=0x13F8,ManagerVtable=0x9E8140,FollowVtable=0x9E8238;
  public static uint Target(IReadMemory memory,uint imageBase,uint host){
   Func<uint,uint> read=at=>{var b=memory.Read(at,4);if(b.Length!=4)throw new IOException("Leitura parcial do trabalho de seguir.");return BitConverter.ToUInt32(b,0);};
   uint manager=read(checked(host+WorkManager));
   if(manager<0x10000||read(manager)!=imageBase+ManagerVtable)throw new IOException("Gerenciador de trabalhos Classic indisponível.");
   uint target=0;
   foreach(uint offset in new uint[]{8,0x18}){
    uint begin=read(manager+offset),end=read(manager+offset+4);
    if(end<begin||end-begin>128||(end-begin)%4!=0)throw new IOException("Fila de trabalhos Classic inconsistente.");
    for(uint at=begin;at<end;at+=4){uint work=read(at);if(work<0x10000)continue;if(read(work)==imageBase+FollowVtable){uint value=read(work+0x20);if(target!=0&&value!=target)throw new IOException("Mais de um alvo de seguir.");target=value;}}
   }
   if(read(host+WorkManager)!=manager)throw new IOException("Gerenciador mudou durante leitura.");return target;
  }
 }
 sealed class ClassicFollowService {
  readonly Action<string> log;readonly Dictionary<int,long> blocked=new Dictionary<int,long>();
  public ClassicFollowService(Action<string> log){this.log=log;}
  public static void Validate(FollowAccount primary,FollowAccount follower){if(primary==null||follower==null||primary.Pid==follower.Pid||primary.Id<2||follower.Id<2||primary.Id==follower.Id)throw new InvalidOperationException("Escolha principal e secundário distintos e conectados.");}
  static ProcessReport Match(FollowAccount account){
   using(var process=Process.GetProcessById(account.Pid)){
    if(process.StartTime.ToUniversalTime().Ticks!=account.Started)throw new IOException("Processo foi substituído.");
    var report=LiveReader.Inspect(process);if(report.Character==null||report.Character.CharacterId!=account.Id||report.Character.Name!=account.Name)throw new IOException("Identidade mudou: "+report.CharacterStatus);return report;
   }
  }
  public void Command(FollowAccount primary,FollowAccount follower,bool stop){
   if(!stop){Validate(primary,follower);if(Match(primary).Character.Hp<=0)throw new IOException("Principal morto.");}var current=Match(follower);if(!stop&&current.Character.Hp<=0)throw new IOException("Secundário morto.");
   long started;if(blocked.TryGetValue(follower.Pid,out started)&&started==follower.Started)throw new IOException("Cliente bloqueado após falha de ponte; reinicie esse cliente antes de repetir.");
   log("COMMAND_BEGIN action="+(stop?"STOP":"FOLLOW")+" pid="+follower.Pid+" character="+follower.Name+" id="+follower.Id+" target="+(stop?1:primary.Id));
   InternalBridge bridge=null;bool restored=false;uint ui=0,executed=0;
   try{
    bridge=new InternalBridge(follower.Pid,follower.Id);ui=bridge.UiThread;
    bridge.Follow(stop?1u:primary.Id);executed=bridge.ExecutedThread;
    log("COMMAND_SENT pid="+follower.Pid+" uiThread="+ui+" executedThread="+executed);
   }catch{blocked[follower.Pid]=follower.Started;throw;}
   finally{
    if(bridge!=null){bridge.Dispose();restored=bridge.RestoreSucceeded;log("BRIDGE_CLEANUP pid="+follower.Pid+" restored="+restored);}
    if(!restored)blocked[follower.Pid]=follower.Started;
   }
   if(!restored)throw new IOException("Remoção da ponte não confirmada; cliente bloqueado.");
   // As in FarmZilla: distinguish a posted message from observed game state.
   for(int i=0;i<15;i++){
    Thread.Sleep(100);var report=Match(follower);uint target;
    using(var process=Process.GetProcessById(follower.Pid))using(var memory=new ReadOnlyProcess(follower.Pid)){
     uint image=unchecked((uint)process.MainModule.BaseAddress.ToInt32());uint host=Convert.ToUInt32(report.Character.PlayerAddress.Substring(2),16);target=FollowState.Target(memory,image,host);
    }
    if(stop?target==0:target==primary.Id){log("STATE_CONFIRMED pid="+follower.Pid+" followTarget="+target+" x="+report.Character.X+" z="+report.Character.Z);return;}
   }
   throw new IOException("Mensagem executada, mas estado de seguir não confirmado. Aproxime as contas e envie o log.");
  }
 }
 // Minimal query/read handle for state confirmation. No write operation here.
 sealed class ReadOnlyProcess:IReadMemory,IDisposable {
  [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
  [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr p,IntPtr a,byte[] b,UIntPtr n,out UIntPtr got);
  [System.Runtime.InteropServices.DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr p);
  IntPtr handle;
  public ReadOnlyProcess(int pid){handle=OpenProcess(0x410,false,pid);if(handle==IntPtr.Zero)throw new IOException("Leitura do estado negada.");}
  public byte[] Read(uint at,int count){var b=new byte[count];UIntPtr got;if(!ReadProcessMemory(handle,new IntPtr(unchecked((int)at)),b,new UIntPtr((uint)count),out got)||got.ToUInt64()!=(ulong)count)throw new IOException("Leitura de estado indisponível.");return b;}
  public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
 }
 sealed class ClassicMirrorForm:Form {
  readonly ComboBox principal=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
  readonly CheckedListBox followers=new CheckedListBox{CheckOnClick=true};
  readonly TextBox output=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
  readonly Button sync=new Button{Text="Sincronizar contas"},follow=new Button{Text="Seguir principal"},stop=new Button{Text="Parar seguimento"},logs=new Button{Text="Abrir pasta do log"};
  readonly Dictionary<int,FollowAccount> owned=new Dictionary<int,FollowAccount>();readonly ClassicFollowService service;bool busy,closing,ready;
  public ClassicMirrorForm(bool discover=true){
   Text="FarmZilla · The Classic · Espelho TESTE 0.3.0";ClientSize=new Size(620,600);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;BackColor=Color.FromArgb(10,16,22);ForeColor=Color.White;Font=new Font("Segoe UI",9);
   var title=new Label{Text="ESPELHO · THE CLASSIC · TESTE DE SEGUIR",Bounds=new Rectangle(16,12,580,25),ForeColor=Color.DeepSkyBlue};Controls.Add(title);
   Controls.Add(new Label{Text="Principal",Bounds=new Rectangle(16,46,580,20)});principal.SetBounds(16,70,400,28);sync.SetBounds(430,69,174,30);
   Controls.Add(new Label{Text="Secundários — marque as contas que vão seguir",Bounds=new Rectangle(16,112,588,22)});followers.SetBounds(16,138,588,160);followers.BackColor=BackColor;followers.ForeColor=ForeColor;
   follow.SetBounds(16,312,280,36);stop.SetBounds(310,312,294,36);output.SetBounds(16,362,588,178);output.BackColor=BackColor;output.ForeColor=Color.LightSkyBlue;logs.SetBounds(16,552,588,32);
   Controls.AddRange(new Control[]{principal,sync,followers,follow,stop,output,logs});service=new ClassicFollowService(Note);
   sync.Click+=async(s,e)=>await Work(Discover);follow.Click+=async(s,e)=>await Work(()=>Send(false));stop.Click+=async(s,e)=>await Work(()=>Send(true));logs.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo{FileName=ClassicLog.Folder,UseShellExecute=true});}catch(Exception ex){Note("LOG_OPEN_ERROR "+ex.Message);}};
   Shown+=async(s,e)=>{if(!discover)return;try{ClassicLog.Write("START version=0.3.0 executable=TheClassic-Teste profile="+Analyzer.ReferenceSha);Note("Log automático: "+ClassicLog.PathName);Note("Teste manual: só seguir/parar. Ataque, buffs e teleporte não estão habilitados.");ready=true;await Work(Discover);}catch(Exception ex){MessageBox.Show(this,"Não foi possível criar o log: "+ex.Message);follow.Enabled=stop.Enabled=false;}};
   FormClosing+=async(s,e)=>{if(closing)return;e.Cancel=true;if(busy){Note("CLOSE_WAIT aguarde a operação atual.");return;}await Work(()=>Send(true));closing=true;ClassicLog.Write("CLOSE");Close();};
  }
  void Note(string text){ClassicLog.Write(text);if(IsDisposed)return;if(InvokeRequired){BeginInvoke(new Action(()=>Append(text)));}else Append(text);}
  void Append(string text){if(IsDisposed)return;if(output.TextLength>60000)output.Clear();output.AppendText(DateTime.Now.ToString("HH:mm:ss.fff")+" "+text+Environment.NewLine);}
  async Task Discover(){
   if(owned.Count>0)throw new InvalidOperationException("Pare o seguimento antes de sincronizar as contas.");
   principal.Items.Clear();followers.Items.Clear();var reports=await Task.Run(()=>LiveReader.Discover());
   foreach(var report in reports){Note("DISCOVERY "+Analyzer.Json(report));if(report.Character==null)continue;using(var p=Process.GetProcessById(report.Pid)){var a=new FollowAccount{Pid=report.Pid,Id=report.Character.CharacterId,Name=report.Character.Name,Started=p.StartTime.ToUniversalTime().Ticks};principal.Items.Add(a);followers.Items.Add(a,false);}}
   if(principal.Items.Count>0)principal.SelectedIndex=0;Note("DISCOVERY_DONE valid="+principal.Items.Count);
  }
  async Task Send(bool stopping){
   var leader=principal.SelectedItem as FollowAccount;var selected=stopping?owned.Values.ToArray():followers.CheckedItems.Cast<FollowAccount>().Where(a=>leader==null||a.Pid!=leader.Pid).ToArray();
   if(!stopping&&(leader==null||selected.Length==0))throw new InvalidOperationException("Selecione o principal e marque pelo menos um secundário distinto.");
   foreach(var account in selected){try{if(!stopping){ClassicFollowService.Validate(leader,account);owned[account.Pid]=account;}await Task.Run(()=>service.Command(leader,account,stopping));if(stopping)owned.Remove(account.Pid);}catch(Exception ex){Note("COMMAND_ERROR pid="+account.Pid+" "+ex);}}
  }
  async Task Work(Func<Task> action){if(busy)return;busy=true;sync.Enabled=follow.Enabled=stop.Enabled=principal.Enabled=followers.Enabled=false;try{await action();}catch(Exception ex){try{Note("ERROR "+ex);}catch{ready=false;MessageBox.Show(this,"Log indisponível. Comandos bloqueados.");}}finally{busy=false;if(!IsDisposed){sync.Enabled=principal.Enabled=followers.Enabled=true;follow.Enabled=stop.Enabled=ready;}}}
 }
 static class ClassicMirrorProgram {
  [STAThread] static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new ClassicMirrorForm());}
 }
}
