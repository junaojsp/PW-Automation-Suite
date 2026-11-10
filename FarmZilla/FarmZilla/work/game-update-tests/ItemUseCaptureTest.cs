using System;using System.Diagnostics;using System.Linq;using System.Runtime.InteropServices;
// Autoteste 0.19.6 só no próprio processo: captura de uso de item além da pedra, NPC e missões; recusas; restauração. Não toca no jogo.
class ItemUseCaptureTest {
 [DllImport("kernel32.dll")]static extern IntPtr OpenProcess(uint a,bool b,int p);
 [DllImport("kernel32.dll")]static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr a,UIntPtr n,uint t,uint prot);
 [DllImport("kernel32.dll")]static extern bool VirtualProtectEx(IntPtr p,IntPtr a,UIntPtr n,uint prot,out uint old);
 [DllImport("kernel32.dll")]static extern bool VirtualFreeEx(IntPtr p,IntPtr a,UIntPtr n,uint t);
 [DllImport("kernel32.dll")]static extern bool FlushInstructionCache(IntPtr p,IntPtr a,UIntPtr n);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate uint Function(IntPtr bytes,uint length);
 static uint A(IntPtr p){return unchecked((uint)p.ToInt32());}
 static byte[] Item(int length,uint item){var b=new byte[length];b[0]=0x6D;b[3]=1;b[4]=3;var id=BitConverter.GetBytes(item);for(int i=0;i<4&&6+i<length;i++)b[6+i]=id[i];for(int i=10;i<length;i++)b[i]=(byte)i;return b;}
 static int Main(){IntPtr h=OpenProcess(0x43A,false,Process.GetCurrentProcess().Id),fn=VirtualAllocEx(h,IntPtr.Zero,new UIntPtr(4096),0x3000,4),packet=Marshal.AllocHGlobal(256);try{byte[] code={0x55,0x8B,0xEC,0x6A,0xFF,0x8B,0x45,0x08,0x33,0x45,0x0C,0x8B,0xE5,0x5D,0xC2,8,0};Marshal.Copy(code,0,fn,code.Length);uint old;VirtualProtectEx(h,fn,new UIntPtr(4096),0x20,out old);FlushInstructionCache(h,fn,new UIntPtr(4096));var call=(Function)Marshal.GetDelegateForFunctionPointer(fn,typeof(Function));
  var accepted=new[]{new byte[]{0x6D,0,0,1,6,0,0x29,0x90,0,0,0x35,8,0,0},Item(14,11272),new byte[]{0x23,0,4,0x78,0x10,0x80},new byte[]{0x25,0,7,0,0,0,12,0,0,0,0x0F,0x1B,0,0,0,0,0,0,0,0,0,0},new byte[]{0x25,0,6,0,0,0,8,0,0,0,1,2,3,4,5,6,7,8},Item(18,23753),Item(22,24394),Item(26,36766),Item(10,3366),Item(96,63384)};
  var rejected=new[]{new byte[]{0x1B,0,1,2,3,4},new byte[0],new byte[64],new byte[]{0x25,0,8,0,0,0,12,0,0,0,0x0F,0x1B,0,0,0,0,0,0,0,0,0,0},Item(97,1),Item(8,1),Item(9,1),new byte[14]};
  using(var capture=new TeleportCapture(Process.GetCurrentProcess().Id,A(fn))){
   foreach(var bytes in accepted){Marshal.Copy(bytes,0,packet,bytes.Length);if(call(packet,(uint)bytes.Length)!=(A(packet)^(uint)bytes.Length))throw new Exception("Retorno alterado");}
   var got=capture.Poll();if(got.Count!=accepted.Length)throw new Exception("Aceitos "+got.Count+"/"+accepted.Length);for(int i=0;i<got.Count;i++)if(!got[i].Packet.SequenceEqual(accepted[i]))throw new Exception("Dados diferentes no item "+i);
   foreach(var bytes in rejected){if(bytes.Length>0)Marshal.Copy(bytes,0,packet,bytes.Length);if(call(packet,(uint)bytes.Length)!=(A(packet)^(uint)bytes.Length))throw new Exception("Retorno alterado na recusa");}
   if(capture.Poll().Count!=0)throw new Exception("Filtro deixou passar comando fora do formato");
   for(int i=1;i<=130;i++){var bytes=accepted[i%accepted.Length];Marshal.Copy(bytes,0,packet,bytes.Length);call(packet,(uint)bytes.Length);}if(capture.Poll().Count!=16)throw new Exception("Ringbuffer invalido");
  }byte[] restored=new byte[5];Marshal.Copy(fn,restored,0,5);if(!restored.SequenceEqual(code.Take(5)))throw new Exception("Codigo nao restaurado");
  Console.WriteLine("PASS: pedra, item 14/18/22/26/10/96 bytes, NPC e missões capturados; 8 formatos recusados; ringbuffer 16; retorno e restauração.");return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}finally{Marshal.FreeHGlobal(packet);VirtualFreeEx(h,fn,UIntPtr.Zero,0x8000);CloseHandle(h);}}
}
