using System;using System.Diagnostics;using System.Linq;using System.Runtime.InteropServices;
// Autoteste 0.19.8 só no próprio processo: modo diagnóstico ampliado registra qualquer comando (cópia limitada a 96 bytes) e restaura.
class CaptureAllTest {
 [DllImport("kernel32.dll")]static extern IntPtr OpenProcess(uint a,bool b,int p);
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr a,UIntPtr n,uint t,uint prot);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate uint Function(IntPtr bytes,uint length);
 static uint A(IntPtr p){return unchecked((uint)p.ToInt32());}
 static int Main(){IntPtr h=OpenProcess(0x43A,false,Process.GetCurrentProcess().Id),fn=VirtualAllocEx(h,IntPtr.Zero,new UIntPtr(4096),0x3000,0x40),packet=Marshal.AllocHGlobal(512);try{
  byte[] code={0x55,0x8B,0xEC,0x6A,0xFF,0x8B,0x45,0x08,0x33,0x45,0x0C,0x8B,0xE5,0x5D,0xC2,8,0};Marshal.Copy(code,0,fn,code.Length);var call=(Function)Marshal.GetDelegateForFunctionPointer(fn,typeof(Function));
  var sizes=new[]{2,7,50,96,97,200};var fill=new byte[300];for(int i=0;i<fill.Length;i++)fill[i]=(byte)(i*7+3);
  using(var c=new TeleportCapture(Process.GetCurrentProcess().Id,A(fn),true)){
   foreach(int n in sizes){Marshal.Copy(fill,0,packet,n);if(call(packet,(uint)n)!=(A(packet)^(uint)n))throw new Exception("Retorno alterado");}
   Marshal.Copy(fill,0,packet,1);call(packet,1);call(packet,0);
   var got=c.Poll();if(got.Count!=sizes.Length)throw new Exception("Capturados "+got.Count);
   for(int i=0;i<got.Count;i++){int n=sizes[i];if(got[i].FullLength!=n||got[i].Packet.Length!=Math.Min(n,96)||!got[i].Packet.SequenceEqual(fill.Take(Math.Min(n,96))))throw new Exception("Comando "+n+" incorreto");}
  }byte[] now=new byte[5];Marshal.Copy(fn,now,0,5);if(!now.SequenceEqual(code.Take(5)))throw new Exception("Código não restaurado");
  Console.WriteLine("PASS: modo ampliado registra 2..200 bytes (cópia até 96, tamanho real preservado), recusa 0..1, retorno e restauração.");return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}finally{Marshal.FreeHGlobal(packet);}}
}
