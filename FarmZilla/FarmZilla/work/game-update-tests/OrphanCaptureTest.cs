using System;using System.Diagnostics;using System.Linq;using System.Runtime.InteropServices;
// Autoteste 0.19.7 só no próprio processo: captura abandonada é reconhecida e trocada; código estranho continua recusado.
class OrphanCaptureTest {
 [DllImport("kernel32.dll")]static extern IntPtr OpenProcess(uint a,bool b,int p);
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr a,UIntPtr n,uint t,uint prot);
 [DllImport("kernel32.dll")]static extern bool VirtualProtectEx(IntPtr p,IntPtr a,UIntPtr n,uint prot,out uint old);
 [DllImport("kernel32.dll")]static extern bool VirtualFreeEx(IntPtr p,IntPtr a,UIntPtr n,uint t);
 [DllImport("kernel32.dll")]static extern bool FlushInstructionCache(IntPtr p,IntPtr a,UIntPtr n);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate uint Function(IntPtr bytes,uint length);
 static uint A(IntPtr p){return unchecked((uint)p.ToInt32());}
 static int Main(){IntPtr h=OpenProcess(0x43A,false,Process.GetCurrentProcess().Id),fn=VirtualAllocEx(h,IntPtr.Zero,new UIntPtr(4096),0x3000,0x40),packet=Marshal.AllocHGlobal(64);int pid=Process.GetCurrentProcess().Id;try{
  byte[] code={0x55,0x8B,0xEC,0x6A,0xFF,0x8B,0x45,0x08,0x33,0x45,0x0C,0x8B,0xE5,0x5D,0xC2,8,0};Marshal.Copy(code,0,fn,code.Length);var call=(Function)Marshal.GetDelegateForFunctionPointer(fn,typeof(Function));byte[] stone={0x6D,0,0,1,6,0,0x29,0x90,0,0,0x35,8,0,0};
  var orphan=new TeleportCapture(pid,A(fn)); // simula app encerrado sem limpeza: não chama Dispose agora
  var fresh=new TeleportCapture(pid,A(fn));if(!fresh.RecoveredOrphan)throw new Exception("Órfã não reconhecida");
  Marshal.Copy(stone,0,packet,14);if(call(packet,14)!=(A(packet)^14))throw new Exception("Retorno alterado");if(fresh.Poll().Count!=1)throw new Exception("Nova captura não registrou");if(orphan.Poll().Count!=0)throw new Exception("Órfã ainda recebe comandos");
  fresh.Dispose();byte[] now=new byte[5];Marshal.Copy(fn,now,0,5);if(!now.SequenceEqual(code.Take(5)))throw new Exception("Código não restaurado após nova captura");
  try{orphan.Dispose();}catch{} Marshal.Copy(fn,now,0,5);if(!now.SequenceEqual(code.Take(5)))throw new Exception("Dispose da órfã alterou o código");
  byte[] foreign={0xE9,0,0,0,0};BitConverter.GetBytes(unchecked(A(fn)+64-A(fn)-5)).CopyTo(foreign,1);Marshal.Copy(foreign,0,fn,5);bool refused=false;try{new TeleportCapture(pid,A(fn)).Dispose();}catch(Exception e){refused=e.Message.Contains("Prologo inesperado");}if(!refused)throw new Exception("Gancho estranho foi aceito");
  Console.WriteLine("PASS: captura órfã reconhecida e substituída; nova captura funciona e restaura; gancho estranho recusado.");return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}finally{Marshal.FreeHGlobal(packet);}}
}
