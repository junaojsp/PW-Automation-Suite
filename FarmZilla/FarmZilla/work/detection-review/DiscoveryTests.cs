using System;using System.Diagnostics;using System.Linq;using System.Runtime.InteropServices;
class DiscoveryTests {
 delegate IntPtr Proc(IntPtr h,uint m,IntPtr w,IntPtr l);static readonly Proc Callback=DefWindowProc;
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct WndClass {public uint style;public IntPtr procedure;public int clsExtra,winExtra;public IntPtr instance,icon,cursor,background;public string menu,className;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern ushort RegisterClass(ref WndClass wc);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool UnregisterClass(string name,IntPtr instance);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr DefWindowProc(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateWindowEx(uint ex,string cls,string text,uint style,int x,int y,int w,int h,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
 [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr window);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static int Main(){IntPtr first=IntPtr.Zero,second=IntPtr.Zero,instance=GetModuleHandle(null);bool registered=false;try{
  var wc=new WndClass{procedure=Marshal.GetFunctionPointerForDelegate(Callback),instance=instance,className="ElementClient Window"};if(RegisterClass(ref wc)==0)throw new Exception("RegisterClass: "+Marshal.GetLastWin32Error());registered=true;
  first=CreateWindowEx(0,wc.className,"Discovery self-test A",0,0,0,100,100,IntPtr.Zero,IntPtr.Zero,instance,IntPtr.Zero);second=CreateWindowEx(0,wc.className,"Discovery self-test B",0,0,0,100,100,IntPtr.Zero,IntPtr.Zero,instance,IntPtr.Zero);if(first==IntPtr.Zero||second==IntPtr.Zero)throw new Exception("Criar janelas locais de teste");
  int pid=Process.GetCurrentProcess().Id;var mine=ClientDiscovery.Find(s=>{}).Where(c=>c.Pid==pid).ToArray();
  Check(mine.Length==1,"duas janelas do mesmo processo geram um candidato");
  Check(mine[0].ByClass&&!mine[0].ByName,"classe reconhece processo com nome diferente de ElementClient");
  Check(mine[0].WindowTitle.StartsWith("Discovery self-test"),"título disponível para diagnóstico sem fingir nome de personagem");
  Check(!ClientDiscovery.Compatible(pid,s=>{}),"classe correta não libera executável fora do perfil validado");
  bool invalid=false;try{ClientDiscovery.Compatible(-123,s=>{});}catch(Exception e){invalid=e.Message.Contains("Windows");}Check(invalid,"falha de consulta informa erro do Windows");
  return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}finally{if(first!=IntPtr.Zero)DestroyWindow(first);if(second!=IntPtr.Zero)DestroyWindow(second);if(registered)UnregisterClass("ElementClient Window",instance);}}
}
