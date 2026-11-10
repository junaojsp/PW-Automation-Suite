using System;using System.Diagnostics;using System.Linq;using System.Runtime.InteropServices;
class UnifiedCaptureTest {
 [DllImport("kernel32.dll")]static extern IntPtr OpenProcess(uint a,bool b,int p);
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr a,UIntPtr n,uint t,uint prot);
 [DllImport("kernel32.dll")]static extern bool VirtualProtectEx(IntPtr p,IntPtr a,UIntPtr n,uint prot,out uint old);
 [DllImport("kernel32.dll")]static extern bool VirtualFreeEx(IntPtr p,IntPtr a,UIntPtr n,uint t);
 [DllImport("kernel32.dll")]static extern bool FlushInstructionCache(IntPtr p,IntPtr a,UIntPtr n);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate uint Function(IntPtr bytes,uint length);
 static uint A(IntPtr p){return unchecked((uint)p.ToInt32());}
 static byte[] Sample(int n){if(n%3==1)return new byte[]{0x23,0,4,0x78,0x10,0x80};if(n%3==2)return new byte[]{0x6D,0,0,1,6,0,0x29,0x90,0,0,0x35,8,0,0};return new byte[]{0x25,0,7,0,0,0,12,0,0,0,0x0F,0x1B,0,0,0,0,0,0,0,0,0,0};}
 static int Main(){IntPtr h=OpenProcess(0x43A,false,Process.GetCurrentProcess().Id),fn=VirtualAllocEx(h,IntPtr.Zero,new UIntPtr(4096),0x3000,4),packet=Marshal.AllocHGlobal(256);try{byte[] code={0x55,0x8B,0xEC,0x6A,0xFF,0x8B,0x45,0x08,0x33,0x45,0x0C,0x8B,0xE5,0x5D,0xC2,8,0};Marshal.Copy(code,0,fn,code.Length);uint old;VirtualProtectEx(h,fn,new UIntPtr(4096),0x20,out old);FlushInstructionCache(h,fn,new UIntPtr(4096));var call=(Function)Marshal.GetDelegateForFunctionPointer(fn,typeof(Function));
  using(var capture=new TeleportCapture(Process.GetCurrentProcess().Id,A(fn))){for(int i=1;i<=130;i++){byte[] bytes=Sample(i);Marshal.Copy(bytes,0,packet,bytes.Length);if(call(packet,(uint)bytes.Length)!=(A(packet)^(uint)bytes.Length))throw new Exception("Retorno alterado");}var results=capture.Poll();if(results.Count!=16)throw new Exception("Ringbuffer invalido");foreach(var result in results)if(!result.Packet.SequenceEqual(Sample((int)result.Sequence)))throw new Exception("Ordem ou dados invalidos");if(results.First().Sequence!=115||results.Last().Sequence!=130)throw new Exception("Sequencia invalida");
   foreach(var bytes in new[]{new byte[]{0x1B,0,1,2,3,4},new byte[0],new byte[64],new byte[]{0x25,0,8,0,0,0,12,0,0,0,0x0F,0x1B,0,0,0,0,0,0,0,0,0,0}}){Marshal.Copy(bytes,0,packet,bytes.Length);call(packet,(uint)bytes.Length);}byte[] wrongItem=Sample(2);wrongItem[6]=0;Marshal.Copy(wrongItem,0,packet,14);call(packet,14);if(capture.Poll().Count!=0)throw new Exception("Filtro deixou passar outro comando");
  }byte[] restored=new byte[5];Marshal.Copy(fn,restored,0,5);if(!restored.SequenceEqual(code.Take(5)))throw new Exception("Codigo nao restaurado");Console.WriteLine("PASS: captura compartilhada de teleporte/NPC/missao; 130 registros; ordem, filtro, retorno e restauracao.");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}finally{Marshal.FreeHGlobal(packet);VirtualFreeEx(h,fn,UIntPtr.Zero,0x8000);CloseHandle(h);}}
}
