using System;using System.Diagnostics;using System.Linq;using System.Runtime.InteropServices;using System.Threading.Tasks;using System.Windows.Forms;
// Autoteste 0.19.9 só no próprio processo, sem jogo: captura do Teleporte Especial (uso 52256 + ponto 0x31) e ponte com tamanho variável e lista fechada.
class SpecialTeleportTest : Form {
 [DllImport("kernel32.dll")]static extern IntPtr OpenProcess(uint a,bool b,int p);
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr a,UIntPtr n,uint t,uint prot);
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr n,uint t,uint p);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr w,uint m,IntPtr a,IntPtr b);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate uint Function(IntPtr bytes,uint length);
 static uint A(IntPtr p){return unchecked((uint)p.ToInt32());}
 static int result=1;
 protected override void WndProc(ref Message m){if(m.Msg==0x8001){m.Result=new IntPtr(42);return;}base.WndProc(ref m);}
 SpecialTeleportTest(){Text="Autoteste Teleporte Especial";Shown+=async(s,e)=>{IntPtr w=Handle;await Task.Run(()=>Run(w));Close();};}
 static byte[] Use(ushort slot){return new byte[]{0x28,0,0,1,(byte)slot,(byte)(slot>>8),0x20,0xCC,0,0};}
 static readonly byte[] Point={0x31,0,3,0,0,0,1,0xDF,0xF0};
 static void CaptureCheck(){IntPtr h=OpenProcess(0x43A,false,Process.GetCurrentProcess().Id),fn=VirtualAllocEx(h,IntPtr.Zero,new UIntPtr(4096),0x3000,0x40),packet=Marshal.AllocHGlobal(64);
  byte[] code={0x55,0x8B,0xEC,0x6A,0xFF,0x8B,0x45,0x08,0x33,0x45,0x0C,0x8B,0xE5,0x5D,0xC2,8,0};Marshal.Copy(code,0,fn,code.Length);var call=(Function)Marshal.GetDelegateForFunctionPointer(fn,typeof(Function));
  var accepted=new[]{Use(7),Point,new byte[]{0x6D,0,0,1,6,0,0x29,0x90,0,0,0x35,8,0,0}};
  var rejected=new[]{new byte[]{0x28,0,0,1,7,0,0x21,0xCC,0,0},new byte[]{0x32,0,3,0,0,0,1,0xDF,0xF0},new byte[]{0x28,0,0,1,7,0,0x20,0xCC,0,0,0},new byte[]{0x31,0,3,0,0,0,1,0xDF},new byte[]{0x58,0}};
  using(var c=new TeleportCapture(Process.GetCurrentProcess().Id,A(fn))){
   foreach(var b in accepted){Marshal.Copy(b,0,packet,b.Length);if(call(packet,(uint)b.Length)!=(A(packet)^(uint)b.Length))throw new Exception("Retorno alterado");}
   var got=c.Poll();if(got.Count!=accepted.Length)throw new Exception("Aceitos "+got.Count);for(int i=0;i<got.Count;i++)if(!got[i].Packet.SequenceEqual(accepted[i]))throw new Exception("Dados "+i);
   foreach(var b in rejected){Marshal.Copy(b,0,packet,b.Length);call(packet,(uint)b.Length);}if(c.Poll().Count!=0)throw new Exception("Formato estranho aceito");
  }byte[] now=new byte[5];Marshal.Copy(fn,now,0,5);if(!now.SequenceEqual(code.Take(5)))throw new Exception("Código não restaurado");}
 static void Bridge(IntPtr w){IntPtr fn=VirtualAlloc(IntPtr.Zero,new UIntPtr(4096),0x3000,0x40),context=Marshal.AllocHGlobal(16);
  // thiscall(ecx=context) stdcall(packet,length): grava tamanho e os 4 primeiros bytes; retorna 1.
  byte[] code={0x8B,0x44,0x24,0x04,0x8B,0x54,0x24,0x08,0x89,0x11,0x8B,0x10,0x89,0x51,0x04,0xB8,1,0,0,0,0xC2,8,0};Marshal.Copy(code,0,fn,code.Length);
  TeleportBridge bridge;using(bridge=new TeleportBridge(w,TeleportBridge.Addr(context),TeleportBridge.Addr(fn))){
   bridge.SendSpecial(Use(9));if(Marshal.ReadInt32(context)!=10||Marshal.ReadInt32(context,4)!=0x01000028)throw new Exception("Uso do item: tamanho/dados");
   bridge.SendSpecial(Point);if(Marshal.ReadInt32(context)!=9||Marshal.ReadInt32(context,4)!=0x00030031)throw new Exception("Ponto: tamanho/dados");
   bridge.SendTeleport(new byte[]{0x6D,0,0,1,9,0,0x29,0x90,0,0,0x35,8,0,0});if(Marshal.ReadInt32(context)!=14)throw new Exception("Pedra: tamanho");
   if(bridge.ExecutedThread!=bridge.UiThread||bridge.LastSendResult!=1)throw new Exception("Thread/retorno");bridge.SendControl(new byte[]{0x2A,0});if(Marshal.ReadInt32(context)!=2||(Marshal.ReadInt32(context,4)&0xFFFF)!=0x002A)throw new Exception("ESC cancelar acao");bridge.SendControl(new byte[]{0x08,0});if(Marshal.ReadInt32(context)!=2||(Marshal.ReadInt32(context,4)&0xFFFF)!=0x0008)throw new Exception("ESC tirar selecao");foreach(var bad in new[]{new byte[]{0x02,0},new byte[]{0x2A,1},new byte[]{0x08,0,0},new byte[]{0x29,0}}){bool no=false;try{bridge.SendControl(bad);}catch{no=true;}if(!no)throw new Exception("Controle fora da lista aceito");}Console.WriteLine("PASS ESC (2A 00 e 08 00) aceito; outros controles recusados");
   foreach(var bad in new[]{new byte[]{0x31,0,1,0,0,0,1,0xDF,0xF0},new byte[]{0x31,0,3,0,0,0,2,0xDF,0xF0},new byte[]{0x28,0,0,1,7,0,0x21,0xCC,0,0},new byte[]{0x58,0},new byte[]{0x31,0,3,0,0,0,1,0xDF},new byte[]{0x6D,0,0,1,9,0,0x29,0x90,0,0,0x35,8,0,0}}){bool refused=false;try{bridge.SendSpecial(bad);}catch{refused=true;}if(!refused)throw new Exception("Ponte aceitou comando fora da lista");}
   if(SendMessage(w,0x8001,IntPtr.Zero,IntPtr.Zero).ToInt32()!=42)throw new Exception("Mensagem original perdida");}
  if(!bridge.RestoreSucceeded||SendMessage(w,0x8001,IntPtr.Zero,IntPtr.Zero).ToInt32()!=42)throw new Exception("Restauração");}
 static void Run(IntPtr w){try{CaptureCheck();Bridge(w);Console.WriteLine("PASS: captura de uso 52256 e ponto 0x31 (formatos vizinhos recusados); ponte envia 10/9/14 bytes na thread da janela, recusa fora da lista e restaura.");result=0;}catch(Exception e){Console.WriteLine("FAIL "+e);}}
 [STAThread]static int Main(){Application.Run(new SpecialTeleportTest());return result;}
}
