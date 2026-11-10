using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

// Captura compartilhada: uso de item (pedra36905, Teleporte Especial 52256 + ponto 0x31, diagnostico de outros), aberturaNPC e servico de aceitar missao. Nao envia comandos.
public class TeleportCapture : IDisposable {
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint a,bool inherit,int pid);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr p,IntPtr a,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr p,IntPtr a,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr a,UIntPtr n,uint type,uint protection);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool VirtualProtectEx(IntPtr p,IntPtr a,UIntPtr n,uint protection,out uint old);
 [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr p,IntPtr a,UIntPtr n,uint type);
 [DllImport("kernel32.dll")] static extern bool FlushInstructionCache(IntPtr p,IntPtr a,UIntPtr n);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenThread(uint access,bool inherit,uint tid);
 [DllImport("kernel32.dll")] static extern uint SuspendThread(IntPtr h);
 [DllImport("kernel32.dll")] static extern uint ResumeThread(IntPtr h);
 [DllImport("kernel32.dll")] static extern bool GetThreadContext(IntPtr h,IntPtr context);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint TestFunction(IntPtr packet,uint size);
 static IntPtr P(uint v){return new IntPtr(unchecked((int)v));}
 static uint A(IntPtr p){return unchecked((uint)p.ToInt32());}
 IntPtr process; int pid; uint target,data,code; bool installed,self,retained;
 static readonly byte[] Original={0x55,0x8B,0xEC,0x6A,0xFF};
 public byte[] Read(uint at,int size){byte[] b=new byte[size];UIntPtr n;if(!ReadProcessMemory(process,P(at),b,new UIntPtr((uint)size),out n)||n.ToUInt32()!=size)throw new Exception("Leitura falhou: "+at.ToString("X8"));return b;}
 void Write(uint at,byte[] b){UIntPtr n;if(!WriteProcessMemory(process,P(at),b,new UIntPtr((uint)b.Length),out n)||n.ToUInt32()!=b.Length)throw new Exception("Gravacao incompleta em "+at.ToString("X8"));}
 static bool Same(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
 class Emitter {
  public List<byte> B=new List<byte>(); public void Add(params byte[] b){B.AddRange(b);} public void U(uint n){B.AddRange(BitConverter.GetBytes(n));}
  public int J(){Add(0x0F,0x85);int at=B.Count;U(0);return at;}
  public void Fix(int at){byte[] b=BitConverter.GetBytes(B.Count-at-4);for(int i=0;i<4;i++)B[at+i]=b[i];}
 }
 byte[] Build(){var e=new Emitter();var exits=new List<int>();var captures=new List<int>();
  e.Add(0x9C,0x60,0xF0,0xFF,0x05);e.U(data+4);e.Add(0x8B,0x4C,0x24,0x2C,0x8B,0x74,0x24,0x28);
  // Diagnostic mode (opt-in): record every command of 2+ bytes; the copy is clamped to 96 bytes below.
  if(captureAll){e.Add(0x83,0xF9,0x02);e.Add(0x0F,0x82);exits.Add(e.B.Count);e.U(0);e.Add(0xE9);captures.Add(e.B.Count);e.U(0);}
  e.Add(0x83,0xF9,0x06);int teleport=e.J();e.Add(0x66,0x83,0x3E,0x23);exits.Add(e.J());e.Add(0xE9);captures.Add(e.B.Count);e.U(0);
  e.Fix(teleport);e.Add(0x83,0xF9,0x0E);int quest=e.J();e.Add(0x81,0x3E);e.U(0x0100006D);exits.Add(e.J());e.Add(0xE9);captures.Add(e.B.Count);e.U(0);
  e.Fix(quest);e.Add(0x83,0xF9,0x12);int acceptance=e.J();e.Add(0x66,0x83,0x3E,0x25);var notQuest=new List<int>();notQuest.Add(e.J());e.Add(0x83,0x7E,0x02,0x06);exits.Add(e.J());e.Add(0x83,0x7E,0x06,0x08);exits.Add(e.J());e.Add(0xE9);captures.Add(e.B.Count);e.U(0);e.Fix(acceptance);e.Add(0x83,0xF9,0x16);int generic=e.J();e.Add(0x66,0x83,0x3E,0x25);notQuest.Add(e.J());e.Add(0x83,0x7E,0x02,0x07);exits.Add(e.J());e.Add(0x83,0x7E,0x06,0x0C);exits.Add(e.J());e.Add(0xE9);captures.Add(e.B.Count);e.U(0);
  // Other item uses (opcode 0x6D, 10..96 bytes) are recorded for diagnosis only; nothing is mirrored from them.
  e.Fix(generic);foreach(int j in notQuest)e.Fix(j);
  // Teleporte Especial: point choice 0x31 (9 bytes) and use of item 52256 (0x28, 10 bytes).
  e.Add(0x83,0xF9,0x09);int notPoint=e.J();e.Add(0x66,0x83,0x3E,0x31);exits.Add(e.J());e.Add(0xE9);captures.Add(e.B.Count);e.U(0);e.Fix(notPoint);
  e.Add(0x83,0xF9,0x60);e.Add(0x0F,0x87);exits.Add(e.B.Count);e.U(0);e.Add(0x83,0xF9,0x0A);e.Add(0x0F,0x82);exits.Add(e.B.Count);e.U(0);e.Add(0x81,0x3E);e.U(0x0100006D);e.Add(0x0F,0x84);captures.Add(e.B.Count);e.U(0);
  e.Add(0x83,0xF9,0x0A);exits.Add(e.J());e.Add(0x81,0x3E);e.U(0x01000028);exits.Add(e.J());e.Add(0x81,0x7E,0x06);e.U(52256);exits.Add(e.J());
  foreach(int j in captures)e.Fix(j);e.Add(0xB8);e.U(1);e.Add(0xF0,0x0F,0xC1,0x05);e.U(data);e.Add(0x8B,0xD0,0x42,0x83,0xE0,0x0F,0xC1,0xE0,0x07,0x05);e.U(data+256);
  e.Add(0x8B,0xF8,0xC7,0x07);e.U(0);e.Add(0x89,0x57,0x04,0x89,0x4F,0x08,0x8B,0x44,0x24,0x18,0x89,0x47,0x0C,0x8B,0xDF,0x83,0xC7,0x20,0x83,0xF9,0x60,0x76,0x05,0xB9);e.U(0x60);e.Add(0xFC,0xF3,0xA4,0x89,0x13);
  foreach(int j in exits)e.Fix(j);e.Add(0x61,0x9D);e.Add(Original);e.Add(0xE9);e.U(unchecked(target+5-(code+(uint)e.B.Count+4)));return e.B.ToArray();
 } // Pause target threads only around the five-byte patch. Never patch a thread's current instruction.
 bool Quiescent(Action action,bool checkAllocation){
  if(self){action();return true;}
  var handles=new List<IntPtr>();IntPtr ctx=Marshal.AllocHGlobal(736);
  try{using(var p=Process.GetProcessById(pid))foreach(ProcessThread t in p.Threads){IntPtr h=OpenThread(0x1A,false,(uint)t.Id);if(h==IntPtr.Zero)throw new Exception("Nao foi possivel abrir thread "+t.Id);handles.Add(h);}
   int suspended=0;try{
    foreach(IntPtr h in handles){if(SuspendThread(h)==0xFFFFFFFF)throw new Exception("Falha suspendendo thread");suspended++;}
    foreach(IntPtr h in handles){for(int i=0;i<716;i++)Marshal.WriteByte(ctx,i,0);Marshal.WriteInt32(ctx,0,0x10001);if(!GetThreadContext(h,ctx))throw new Exception("Falha lendo contexto de thread");uint ip=unchecked((uint)Marshal.ReadInt32(ctx,184));if(ip>=target&&ip<target+5)return false;if(checkAllocation&&ip>=code&&ip<code+4096)return false;}
    action();return true;
   }finally{for(int i=suspended-1;i>=0;i--)ResumeThread(handles[i]);}
  }finally{foreach(IntPtr h in handles)CloseHandle(h);Marshal.FreeHGlobal(ctx);}
 }
 void Patch(byte[] expected,byte[] replacement){
  for(int tries=0;tries<100;tries++){
   if(Quiescent(delegate{
    if(!Same(Read(target,5),expected))throw new Exception("Codigo foi alterado por outro componente; operacao cancelada.");
    uint old;if(!VirtualProtectEx(process,P(target),new UIntPtr(5),0x40,out old))throw new Exception("Falha protegendo trecho");
    try{Write(target,replacement);FlushInstructionCache(process,P(target),new UIntPtr(5));if(!Same(Read(target,5),replacement))throw new Exception("Verificacao do trecho falhou");}
    finally{uint unused;VirtualProtectEx(process,P(target),new UIntPtr(5),old,out unused);}
   },false))return;Thread.Sleep(10);
  }throw new Exception("Thread ocupando trecho; nao foi possivel alterar com seguranca.");
 }
 public bool RecoveredOrphan {get;private set;}
 // A capture left by a FarmZilla instance that ended without cleanup: jump to code starting with this capturer's signature.
 // Its allocation is kept on purpose, since a game thread may still be running inside it.
 bool OrphanCapture(byte[] current){if(current[0]!=0xE9)return false;if(!self){var me=Process.GetCurrentProcess();foreach(var other in Process.GetProcessesByName(me.ProcessName))using(other)if(other.Id!=me.Id)return false;}
  uint dest=unchecked(target+5+BitConverter.ToUInt32(current,1));byte[] head;try{head=Read(dest,9);}catch{return false;}
  return head[0]==0x9C&&head[1]==0x60&&head[2]==0xF0&&head[3]==0xFF&&head[4]==0x05&&BitConverter.ToUInt32(head,5)==unchecked(dest-4096+4);}
 byte[] Jump(){byte[] b=new byte[5];b[0]=0xE9;Buffer.BlockCopy(BitConverter.GetBytes(unchecked(code-target-5)),0,b,1,4);return b;}
 readonly bool captureAll;
 public bool CaptureAll {get{return captureAll;}}
 public TeleportCapture(int targetPid,uint testAddress):this(targetPid,testAddress,false){}
 public TeleportCapture(int targetPid,uint testAddress,bool diagnosticAll){
  pid=targetPid;self=testAddress!=0;captureAll=diagnosticAll;
  try{using(var p=Process.GetProcessById(pid)){
   if(self&&pid!=Process.GetCurrentProcess().Id)throw new Exception("Autoteste exige proprio processo");
   GameProfile G=self?null:GameProfile.Require(p);
   process=OpenProcess(0x43A,false,pid);if(process==IntPtr.Zero)throw new Exception("Acesso ao processo negado");target=self?testAddress:A(p.MainModule.BaseAddress)+G.SendData;
   byte[] current=Read(target,5);
   if(!Same(current,Original)){if(!OrphanCapture(current))throw new Exception("Prologo inesperado; nao instalado");Patch(current,Original);RecoveredOrphan=true;}
   data=A(VirtualAllocEx(process,IntPtr.Zero,new UIntPtr(8192),0x3000,4));if(data==0)throw new Exception("Falha de alocacao");code=data+4096;Write(code,Build());uint old;if(!VirtualProtectEx(process,P(code),new UIntPtr(4096),0x20,out old))throw new Exception("Falha protegendo capturador");FlushInstructionCache(process,P(code),new UIntPtr(4096));
   // Retain the allocation on any uncertain patch failure, including a partial write.
   retained=true;Patch(Original,Jump());installed=true;retained=false;
  }}catch{Dispose();throw;}
 }
 public void Dispose(){if(process==IntPtr.Zero)return;
  try{if(installed){Patch(Jump(),Original);installed=false;bool freed=false;for(int i=0;i<100&&!freed;i++){freed=Quiescent(delegate{if(!VirtualFreeEx(process,P(data),UIntPtr.Zero,0x8000))throw new Exception("Falha liberando captura");data=0;},true);if(!freed)Thread.Sleep(10);}retained=!freed;}
   if(data!=0&&!installed&&!retained){VirtualFreeEx(process,P(data),UIntPtr.Zero,0x8000);data=0;}
  }catch{retained=true;try{using(var p=Process.GetProcessById(pid)){if(p.HasExited)installed=false;}}catch(ArgumentException){installed=false;}if(installed)throw;}finally{if(!installed){CloseHandle(process);process=IntPtr.Zero;}}
 }
 public sealed class CapturedCommand {public uint Sequence;public byte[] Packet;public int FullLength;}
 readonly HashSet<uint> delivered=new HashSet<uint>();
 public List<CapturedCommand> Poll(){var result=new List<CapturedCommand>();byte[] state=Read(data,4096);for(int i=0;i<16;i++){int at=256+i*128;uint seq=BitConverter.ToUInt32(state,at);if(seq==0||delivered.Contains(seq))continue;byte[] check=Read(data+(uint)at,128);if(BitConverter.ToUInt32(check,0)!=seq||BitConverter.ToUInt32(check,4)!=seq)continue;int length=(int)BitConverter.ToUInt32(check,8);if(captureAll?length<2:(length!=6&&length!=9&&length!=18&&length!=22&&(length<10||length>96)))throw new Exception("Comando capturado com tamanho invalido");byte[] packet=new byte[Math.Min(length,96)];Buffer.BlockCopy(check,32,packet,0,packet.Length);delivered.Add(seq);result.Add(new CapturedCommand{Sequence=seq,Packet=packet,FullLength=length});}result.Sort((a,b)=>a.Sequence.CompareTo(b.Sequence));return result;} public bool Restored {get{return !installed;}}
}