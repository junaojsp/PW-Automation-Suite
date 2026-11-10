using System;using System.Runtime.InteropServices;using System.Threading.Tasks;using System.Windows.Forms;
class NpcCloseServiceTest:Form {
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr n,uint t,uint p);
 [DllImport("kernel32.dll")]static extern bool VirtualProtect(IntPtr a,UIntPtr n,uint p,out uint old);
 [DllImport("kernel32.dll")]static extern bool VirtualFree(IntPtr a,UIntPtr n,uint t);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr w,uint m,IntPtr a,IntPtr b);
 static int result=1;
 protected override void WndProc(ref Message m){if(m.Msg==0x8001){m.Result=new IntPtr(42);return;}base.WndProc(ref m);}
 NpcCloseServiceTest(){Shown+=async(s,e)=>{IntPtr w=Handle;await Task.Run(()=>Run(w));Close();};}
 static void Run(IntPtr w){IntPtr code=VirtualAlloc(IntPtr.Zero,new UIntPtr(4096),0x3000,4),context=Marshal.AllocHGlobal(16),dialog=Marshal.AllocHGlobal(256);bool restored=false;try{
  byte[] send={0x8B,0x44,0x24,0x08,0x89,0x01,0xB8,1,0,0,0,0xC2,8,0};Marshal.Copy(send,0,code,send.Length);
  byte[] close={0x8B,0x44,0x24,4,0x89,0x81,0xA0,0,0,0,0x8B,0x44,0x24,8,0x89,0x81,0xA4,0,0,0,0x8B,0x44,0x24,12,0x89,0x81,0xA8,0,0,0,0x8B,0x44,0x24,16,0x89,0x81,0xAC,0,0,0,0xC6,0x81,0x90,0,0,0,0,0xC2,16,0};IntPtr closeCode=IntPtr.Add(code,128);Marshal.Copy(close,0,closeCode,close.Length);byte[] cancel={0x8B,0x44,0x24,4,0x89,0x81,0xB0,0,0,0,0xC6,0x81,0x90,0,0,0,0,0xC7,0x81,0xB4,0,0,0,0,0,0,0,0xC2,4,0};IntPtr cancelCode=IntPtr.Add(code,256);Marshal.Copy(cancel,0,cancelCode,cancel.Length);uint old;VirtualProtect(code,new UIntPtr(4096),0x20,out old);for(int i=0;i<256;i++)Marshal.WriteByte(dialog,i,0);Marshal.WriteInt32(dialog,0,0x12345678);Marshal.WriteByte(dialog,0x90,1);
  NpcBridge b;using(b=new NpcBridge(w,NpcBridge.Addr(context),NpcBridge.Addr(code),NpcBridge.Addr(closeCode),0x12345678,NpcBridge.Addr(cancelCode))){
   if(b.CloseDialogs(new[]{NpcBridge.Addr(dialog)})!=1||Marshal.ReadByte(dialog,0x90)!=0||Marshal.ReadInt32(dialog,0xA0)!=0||Marshal.ReadInt32(dialog,0xA4)!=0||Marshal.ReadInt32(dialog,0xA8)!=1||Marshal.ReadInt32(dialog,0xAC)!=1||b.ExecutedThread!=b.UiThread)throw new Exception("Fechamento, argumentos ou thread incorretos");Console.WriteLine("PASS fechamento interno na thread da janela e argumentos Show(false)");
   b.SendNpc(new byte[]{0x23,0,4,0x78,0x10,0x80});if(Marshal.ReadInt32(context)!=6||b.LastSendResult!=1)throw new Exception("Envio apos fechar incorreto");Console.WriteLine("PASS envio NPC preservado apos fechar janelas");
   Marshal.WriteInt32(dialog,0,0x8DCD88);Marshal.WriteInt32(dialog,0xB4,1);Marshal.WriteByte(dialog,0x90,0);if(b.CloseDialogs(new[]{NpcBridge.Addr(dialog)})!=1||Marshal.ReadInt32(dialog,0xB4)!=0)throw new Exception("NPC oculto nao encerrou servico");if(Marshal.PtrToStringAnsi(new IntPtr(Marshal.ReadInt32(dialog,0xB0)))!="IDCANCEL")throw new Exception("Comando de cancelamento incorreto");Console.WriteLine("PASS NPC oculto recebe IDCANCEL nativo e encerra servico na thread correta");Marshal.WriteInt32(dialog,0,0x11111111);bool rejected=false;try{b.CloseDialogs(new[]{NpcBridge.Addr(dialog)});}catch{rejected=true;}if(!rejected)throw new Exception("Janela desconhecida aceita");Console.WriteLine("PASS janela fora do perfil rejeitada");
  }restored=b.RestoreSucceeded;if(!restored||SendMessage(w,0x8001,IntPtr.Zero,IntPtr.Zero).ToInt32()!=42)throw new Exception("Restauracao falhou");Console.WriteLine("PASS restauracao do procedimento original");result=0;
 }catch(Exception e){Console.WriteLine(e);}finally{if(restored){VirtualFree(code,UIntPtr.Zero,0x8000);Marshal.FreeHGlobal(context);Marshal.FreeHGlobal(dialog);}}}
 [STAThread]static int Main(){Application.Run(new NpcCloseServiceTest());return result;}
}
