using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using FarmZilla.TheClassic;
static class ClassicFollowTests {
 static int count;static Exception failure;
 [UnmanagedFunctionPointer(CallingConvention.ThisCall)]delegate void Receiver(IntPtr context,IntPtr message);
 static void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS Follow "+name);}
 static void Reject(Action action,string name){bool failed=false;try{action();}catch{failed=true;}Check(failed,name);}
 sealed class Memory:IReadMemory {public Dictionary<uint,uint> Values=new Dictionary<uint,uint>();public byte[] Read(uint at,int size){return BitConverter.GetBytes(Values[at]);}}
 static void PureTests(){
  var p=new FollowAccount{Pid=1,Id=100,Name="Principal"};var f=new FollowAccount{Pid=2,Id=200,Name="Segundo"};ClassicFollowService.Validate(p,f);Check(true,"contas distintas");Reject(()=>ClassicFollowService.Validate(p,p),"principal não segue a si mesmo");Reject(()=>ClassicFollowService.Validate(null,f),"principal ausente");
  uint image=0x400000,host=0x200000,manager=0x300000,work=0x400000,vector=0x500000;
  var m=new Memory();m.Values[host+FollowState.WorkManager]=manager;m.Values[manager]=image+FollowState.ManagerVtable;m.Values[manager+8]=vector;m.Values[manager+12]=vector+4;m.Values[manager+0x18]=0;m.Values[manager+0x1c]=0;m.Values[vector]=work;m.Values[work]=image+FollowState.FollowVtable;m.Values[work+0x20]=100;
  Check(FollowState.Target(m,image,host)==100,"estado lê alvo do trabalho real de seguir");m.Values[manager+12]=vector;Check(FollowState.Target(m,image,host)==0,"fila vazia não confirma seguimento");m.Values[manager+12]=vector+129;Reject(()=>FollowState.Target(m,image,host),"fila inválida recusada");
  var profile=GameProfile.Current();Check(profile.HostOffset==0x28&&profile.CharacterIdOffset==0x4b8&&profile.FollowManagerOffset==0x68&&profile.FollowPost==0x425090,"perfil específico Classic");
  var folder=Path.Combine(Path.GetTempPath(),"farmzilla-classic-test-"+Guid.NewGuid().ToString("N"));ClassicLog.Folder=folder;ClassicLog.PathName=Path.Combine(folder,"test.log");ClassicLog.Write("FOLLOW_TEST");Check(File.ReadAllText(ClassicLog.PathName).Contains("FOLLOW_TEST"),"log gravado automaticamente");
 }
 [STAThread] static int Main(){try{
  PureTests();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var window=new Form{Text="Bridge self-test",ClientSize=new Size(200,100)}){
   int calls=0;uint target=0,opcode=0;Receiver receiver=(context,message)=>{calls++;opcode=unchecked((uint)Marshal.ReadInt32(message));target=unchecked((uint)Marshal.ReadInt32(message,12));};
   IntPtr function=Marshal.GetFunctionPointerForDelegate(receiver);
   window.Shown+=async(s,e)=>{IntPtr handle=window.Handle;try{
    await Task.Run(()=>{var bridge=new InternalBridge(handle,0x123456,function.ToInt32()==0?0:unchecked((uint)function.ToInt32()));try{bridge.Follow(100);Check(bridge.ExecutedThread==bridge.UiThread,"ponte compartilhada executa na thread UI");}finally{bridge.Dispose();}Check(bridge.RestoreSucceeded,"ponte compartilhada restaura WndProc");});
    Check(calls==1&&opcode==0xEE&&target==100,"mensagem EE e alvo preservados pela ponte");
    await Task.Run(()=>{var bridge=new InternalBridge(handle,0x123456,unchecked((uint)function.ToInt32()));try{bridge.Follow(1);}finally{bridge.Dispose();}Check(bridge.RestoreSucceeded,"ponte de parar restaura WndProc");});
    Check(calls==2&&target==1,"argumento parar preservado do FarmZilla");
   }catch(Exception ex){failure=ex;}finally{window.Close();}};
   var timeout=new Timer{Interval=15000};timeout.Tick+=(s,e)=>{failure=new Exception("Autoteste de ponte excedeu tempo");window.Close();};timeout.Start();Application.Run(window);timeout.Dispose();GC.KeepAlive(receiver);if(failure!=null)throw failure;
  }
  using(var form=new ClassicMirrorForm(false)){
   form.Show();form.Update();
   foreach(Control c in form.Controls)Check(c.Left>=0&&c.Top>=0&&c.Right<=form.ClientSize.Width&&c.Bottom<=form.ClientSize.Height,"controle dentro do Espelho: "+c.GetType().Name);
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"espelho-layout.png"),ImageFormat.Png);}
   Check(form.Controls.OfType<CheckedListBox>().Count()==1&&form.Controls.OfType<ComboBox>().Count()==1,"seleção de principal e secundários");
  }
  Console.WriteLine(count+" verificações do seguir; teste no jogo ainda necessário.");Directory.Delete(ClassicLog.Folder,true);return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
