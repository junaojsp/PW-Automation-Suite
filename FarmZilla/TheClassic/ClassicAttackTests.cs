using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using FarmZilla.TheClassic;

static class ClassicAttackTests {
 static int count;static Exception failure;
 static void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS Attack "+name);}
 static void Reject(Action action,string name){bool failed=false;try{action();}catch{failed=true;}Check(failed,name);}
 // Same calling conventions as the Classic client functions (thiscall; callee pops 8, 4, 12 and 0 bytes).
 [UnmanagedFunctionPointer(CallingConvention.ThisCall)]delegate int Query(IntPtr host,uint target,int force);
 [UnmanagedFunctionPointer(CallingConvention.ThisCall)]delegate byte Select(IntPtr host,uint target);
 [UnmanagedFunctionPointer(CallingConvention.ThisCall)]delegate byte MacroStart(IntPtr host,int index,int once,int setting);
 [UnmanagedFunctionPointer(CallingConvention.ThisCall)]delegate void MacroStop(IntPtr host);
 [UnmanagedFunctionPointer(CallingConvention.ThisCall)]delegate byte Send(IntPtr net,IntPtr buffer,int length);
 sealed class Fake:IAttackRuntime {
  public uint Current=0x80000101;public int Starts,Stops,Releases,Supports;public bool Alive=true;
  public uint Target(CharacterRef c){return Current;}public bool ValidTarget(CharacterRef c,uint t){return true;}public bool Running(CharacterRef c,uint combo,uint t){return Alive;}
  public AttackResult Start(CharacterRef c,uint t){Starts++;return new AttackResult{Combo=(uint)(100+Starts)};}public AttackResult Stop(CharacterRef c,uint combo,uint t){Stops++;return new AttackResult();}
  public AttackResult Release(CharacterRef c){Releases++;return new AttackResult();}public int Step(CharacterRef c,uint combo){return 1;}
  public AttackResult StartSupport(CharacterRef c,CharacterRef leader){Supports++;return new AttackResult{Combo=900};}public bool RunningSupport(CharacterRef c,uint combo){return Alive;}
  public AttackResult StopSupport(CharacterRef c){Stops++;return new AttackResult();}public AttackResult ReleaseSupport(CharacterRef c){Releases++;return new AttackResult();}
 }
 static void PureTests(){
  Check(AttackProfile.HostMacro==0x1418&&AttackProfile.HostTarget==0x59C&&AttackProfile.MacroConfig==0xFE&&AttackProfile.MacroCount==8,"campos Classic do ataque");
  Check(AttackProfile.Anchors.Length==21&&AttackProfile.Anchors.Select(a=>a.Rva).Distinct().Count()==21,"guardas de ataque distintas");
  Check(AttackProfile.Anchors.Any(a=>a.Rva==AttackProfile.AttackableQuery)&&AttackProfile.Anchors.Any(a=>a.Rva==AttackProfile.SelectTarget)&&AttackProfile.Anchors.Any(a=>a.Rva==AttackProfile.MacroStart)&&AttackProfile.Anchors.Any(a=>a.Rva==AttackProfile.MacroStop)&&AttackProfile.Anchors.Any(a=>a.Rva==AttackProfile.SendData),"toda função chamada tem guarda");
  var primary=new CharacterRef{Pid=1,Id=1,Name="principal"};var second=new CharacterRef{Pid=2,Id=2,Name="secundario"};var ep=new CharacterRef{Pid=3,Id=3,Name="ep"};
  var fake=new Fake();var released=new List<string>();var c=new AttackController(fake,new HashSet<int>(),s=>released.Add(s));
  c.Tick(primary,new[]{primary,second},true,false,false);Check(fake.Starts==0,"manual aguarda a tecla");
  c.Tick(primary,new[]{primary,second},true,false,true);Check(fake.Starts==2&&c.Engaged(2),"tecla inicia macro 0 no principal e secundário");
  c.Tick(primary,new[]{primary,second,ep},true,true,false,new HashSet<int>{3});Check(fake.Supports==1&&c.Engaged(3),"sacerdote marcado em Sup. inicia macro 2");
  fake.Current=1;c.Tick(primary,new[]{primary,second,ep},true,true,false,new HashSet<int>{3});Check(!c.Active&&c.TakeReleased()&&fake.Releases>=2,"principal solta o monstro: para e libera o seguir");
 }
 static void BridgeTests(IntPtr window){
  IntPtr host=Marshal.AllocHGlobal(0x2000),combo=Marshal.AllocHGlobal(0x40),net=Marshal.AllocHGlobal(16);
  try{
   for(int i=0;i<0x2000;i+=4)Marshal.WriteInt32(host,i,0);for(int i=0;i<0x40;i+=4)Marshal.WriteInt32(combo,i,0);
   uint lastQueryTarget=0;int lastForce=-9,lastIndex=-9,lastSetting=0,stops=0;byte[] sent=null;
   Query query=(h,t,f)=>{lastQueryTarget=t;lastForce=f;return t==0x80000123?1:0;};
   Select select=(h,t)=>{Marshal.WriteInt32(h,(int)AttackProfile.HostTarget,unchecked((int)t));return 1;};
   MacroStart start=(h,index,once,setting)=>{lastIndex=index;lastSetting=setting;Marshal.WriteInt32(combo,0,unchecked((int)AttackProfile.ComboSkill));Marshal.WriteInt32(combo,4,h.ToInt32());Marshal.WriteInt32(combo,0x1C,index);Marshal.WriteInt32(combo,0x2C,Marshal.ReadInt32(h,(int)AttackProfile.HostTarget));Marshal.WriteByte(combo,0x24,0);Marshal.WriteInt32(h,(int)AttackProfile.HostMacro,combo.ToInt32());return 1;};
   MacroStop stop=h=>{stops++;Marshal.WriteInt32(h,(int)AttackProfile.HostMacro,0);};
   Send send=(n,b,l)=>{sent=new byte[l];Marshal.Copy(b,sent,0,l);return 1;};
   Func<Delegate,uint> fn=d=>unchecked((uint)Marshal.GetFunctionPointerForDelegate(d).ToInt32());
   var bridge=new AttackBridge(window,unchecked((uint)host.ToInt32()),fn(query),fn(select),fn(start),fn(stop));
   try{
    bridge.Command(0,0x80000123);Check(bridge.LastResult==1&&lastQueryTarget==0x80000123&&lastForce==0,"consulta atacável com 2 argumentos e pilha preservada");
    Check(bridge.ExecutedThread==bridge.UiThread,"ataque executa na thread da janela");
    bridge.Command(0,0x80000999);Check(bridge.LastResult==0,"alvo não atacável recusado");
    bridge.Command(1,0x80000123);Check(bridge.LastResult==1&&bridge.Read(bridge.Host+AttackProfile.HostTarget)==0x80000123,"seleção grava alvo em +0x59C");
    bridge.Command(2,0x80000123);Check(bridge.LastResult==1&&bridge.Combo==unchecked((uint)combo.ToInt32())&&lastIndex==0&&lastSetting==-1,"macro 0 iniciada no alvo selecionado");
    bridge.Command(3,0x80000999,bridge.Combo);Check(bridge.LastResult==0&&stops==0,"parar exige o mesmo alvo da macro");
    bridge.Command(3,0x80000123,bridge.Combo);Check(bridge.LastResult==1&&stops==1&&bridge.Read(bridge.Host+AttackProfile.HostMacro)==0,"parar encerra a macro do ataque");
    Check(bridge.StartMacroOn(0x00000042,2)&&lastIndex==2&&bridge.Read(bridge.Host+AttackProfile.HostTarget)==0x42,"suporte seleciona líder e inicia macro 2");
    Check(!bridge.StopMacro(0)&&stops==1,"parar outra macro não encerra a de suporte");Check(bridge.StopMacro(-1)&&stops==2,"parar qualquer macro");
    Reject(()=>bridge.StartMacroOn(0x42,8),"Classic aceita somente macros 0 a 7");Reject(()=>bridge.Command(2,0x42),"macro de ataque recusa jogador");
   }finally{bridge.Dispose();}
   Check(bridge.RestoreSucceeded,"ponte de ataque restaura WndProc");
   var packets=new TeleportBridge(window,unchecked((uint)net.ToInt32()),fn(send));
   try{packets.SendControl(new byte[]{0x2A,0});Check(sent!=null&&sent.SequenceEqual(new byte[]{0x2A,0})&&packets.LastSendResult==1,"cancelar ação 2A 00 enviado");packets.SendControl(new byte[]{0x08,0});Check(sent.SequenceEqual(new byte[]{0x08,0}),"desmarcar 08 00 enviado");Reject(()=>packets.SendControl(new byte[]{0x02,0}),"somente comandos de ESC são aceitos");}
   finally{packets.Dispose();}
   Check(packets.RestoreSucceeded,"ponte de envio restaura WndProc");
   GC.KeepAlive(query);GC.KeepAlive(select);GC.KeepAlive(start);GC.KeepAlive(stop);GC.KeepAlive(send);
  }finally{Marshal.FreeHGlobal(host);Marshal.FreeHGlobal(combo);Marshal.FreeHGlobal(net);}
 }
 static void LayoutTests(){
  using(var form=new ClassicMirrorForm(false)){
   form.Show();form.Update();
   var boxes=form.Controls.OfType<CheckBox>().ToArray();
   Func<string,CheckBox> Box=name=>boxes.Single(b=>b.AccessibleName==name);
   var mode=form.Controls.OfType<ComboBox>().Single(c=>c.AccessibleName=="Modo de ataque");var support=form.Controls.OfType<ComboBox>().Single(c=>c.AccessibleName=="Suporte em");
   Check(mode.Enabled&&mode.Items.Count==2&&(string)mode.SelectedItem=="Manual por tecla","modos Automático e Manual como no Avalon");
   Check(Box("Atacar com o principal").Enabled&&Box("Atacar com o principal").Checked,"principal ataca por padrão");
   Check(form.Controls.OfType<TextBox>().Single(t=>t.AccessibleName=="Tecla Atacar").Text=="1","tecla Atacar padrão 1");
   var rows=form.Controls.OfType<ComboBox>().Where(c=>c.AccessibleName!=null&&c.AccessibleName.StartsWith("Secundário ")).OrderBy(c=>c.Top).ToArray();
   var leader=new FollowAccount{Pid=1,Id=100,Name="Principal",ClassId=0};var bm=new FollowAccount{Pid=2,Id=200,Name="BM",ClassId=4};var ep=new FollowAccount{Pid=3,Id=300,Name="EP",ClassId=ClassIcons.Cleric};
   form.BindAccounts(new List<FollowAccount>{leader,bm,ep});
   Check(!Box("Atacar com o secundário 1").Enabled&&!Box("Suporte com o secundário 1").Visible,"linha vazia sem ataque");
   rows[0].SelectedItem=bm;rows[1].SelectedItem=ep;
   Check(Box("Atacar com o secundário 1").Checked&&!Box("Suporte com o secundário 1").Visible,"secundário comum começa atacando");
   Check(!Box("Atacar com o secundário 2").Checked&&Box("Suporte com o secundário 2").Visible&&Box("Suporte com o secundário 2").Checked,"sacerdote começa em suporte");
   Box("Atacar com o secundário 2").Checked=true;Check(!Box("Suporte com o secundário 2").Checked,"Atq. e Sup. se excluem");
   Check(support.Items.Count==3&&support.Items.Contains(bm)&&support.Items.Contains(ep),"suporte em líder ou secundários");
   rows[0].SelectedIndex=0;Check(!Box("Atacar com o secundário 1").Enabled&&!Box("Atacar com o secundário 1").Checked,"remover secundário desliga o ataque");
   foreach(Control c in form.Controls)if(c.Visible)Check(c.Left>=0&&c.Top>=0&&c.Right<=form.ClientSize.Width&&c.Bottom<=form.ClientSize.Height,"controle dentro do Espelho: "+(c.AccessibleName??c.GetType().Name));
  }
 }
 [STAThread] static int Main(){try{
  var folder=Path.Combine(Path.GetTempPath(),"farmzilla-classic-attack-"+Guid.NewGuid().ToString("N"));ClassicLog.Folder=folder;ClassicLog.PathName=Path.Combine(folder,"test.log");
  PureTests();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var window=new Form{Text="Attack self-test",ClientSize=new Size(200,100)}){
   window.Shown+=async(s,e)=>{IntPtr handle=window.Handle;try{await Task.Run(()=>BridgeTests(handle));}catch(Exception ex){failure=ex;}finally{window.Close();}};
   var timeout=new Timer{Interval=15000};timeout.Tick+=(s,e)=>{failure=new Exception("Autoteste de ataque excedeu tempo");window.Close();};timeout.Start();Application.Run(window);timeout.Dispose();if(failure!=null)throw failure;
  }
  LayoutTests();
  Console.WriteLine(count+" verificações do ataque; teste no jogo ainda necessário.");if(Directory.Exists(folder))Directory.Delete(folder,true);return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
