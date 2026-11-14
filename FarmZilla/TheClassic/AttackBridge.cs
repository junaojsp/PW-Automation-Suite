using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FarmZilla.TheClassic;

// Classic copy of the FarmZilla AttackBridge (same modes 0–5, same window-thread execution and restore).
// Differences: Classic addresses from AttackProfile, and the attackable query takes 2 arguments (ret 8) instead of 3.
public sealed class AttackBridge : IDisposable {
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr p,IntPtr a,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool WriteProcessMemory(IntPtr p,IntPtr a,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr a,UIntPtr n,uint type,uint protection);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool VirtualProtectEx(IntPtr p,IntPtr a,UIntPtr n,uint protection,out uint old);
 [DllImport("kernel32.dll")]static extern bool VirtualFreeEx(IntPtr p,IntPtr a,UIntPtr n,uint type);
 [DllImport("kernel32.dll")]static extern bool FlushInstructionCache(IntPtr p,IntPtr a,UIntPtr n);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr CreateRemoteThread(IntPtr p,IntPtr attr,UIntPtr size,IntPtr start,IntPtr arg,uint flags,out uint id);
 [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr h,uint timeout);
 [DllImport("kernel32.dll")]static extern bool GetExitCodeThread(IntPtr h,out uint code);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetModuleHandle(string name);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetProcAddress(IntPtr module,string name);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string value);
 [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SendMessageTimeout(IntPtr window,uint msg,UIntPtr wp,IntPtr lp,uint flags,uint timeout,out UIntPtr result);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 public static uint MessageId=RegisterWindowMessage("FarmZilla.TheClassic.Attack.0.5");
 const uint Magic=0x4156464C,Restore=0x41565253;
 IntPtr process,allocation,window;uint data,hook,installer;bool installed,retain;
 public uint UiThread,ExecutedThread,Module,Host,CharacterId; public bool RestoreSucceeded;
 uint setLong,getLong,callOld,getThread;
 static uint Addr(IntPtr p){return unchecked((uint)p.ToInt32());}
 static IntPtr Pointer(uint a){return new IntPtr(unchecked((int)a));}
 static Exception Fail(string what){return new Exception(what+" (Windows "+Marshal.GetLastWin32Error()+")");}
 public uint Read(uint at){byte[] b=new byte[4];UIntPtr got;if(!ReadProcessMemory(process,Pointer(at),b,new UIntPtr(4),out got)||got.ToUInt32()!=4)throw Fail("Leitura em "+at.ToString("X8"));return BitConverter.ToUInt32(b,0);}
 void Write(uint at,byte[] b){UIntPtr got;if(!WriteProcessMemory(process,Pointer(at),b,new UIntPtr((uint)b.Length),out got)||got.ToUInt32()!=b.Length)throw Fail("Gravação da ponte");}
 uint Api(Process target,string dll,string name){IntPtr local=GetModuleHandle(dll),address=GetProcAddress(local,name);if(address==IntPtr.Zero)throw Fail("API "+name);string owner=null;uint ownerBase=0;foreach(ProcessModule m in Process.GetCurrentProcess().Modules){uint start=Addr(m.BaseAddress),end=start+(uint)m.ModuleMemorySize;if(Addr(address)>=start&&Addr(address)<end){owner=m.ModuleName;ownerBase=start;break;}}if(owner==null)throw new Exception("Módulo local da API não encontrado.");foreach(ProcessModule m in target.Modules)if(string.Equals(m.ModuleName,owner,StringComparison.OrdinalIgnoreCase))return Addr(m.BaseAddress)+Addr(address)-ownerBase;throw new Exception("Módulo remoto da API não encontrado: "+owner);}
 ProcessGate.Token gate;
 public AttackBridge(int pid){if(IntPtr.Size!=4)throw new Exception("Ponte exige processo x86.");gate=ProcessGate.Enter(pid);try{using(var target=Process.GetProcessById(pid)){
 Module=ClassicClient.Require(target);
 window=target.MainWindowHandle;if(window==IntPtr.Zero)throw new Exception("Janela do jogo não encontrada.");uint owner;UiThread=GetWindowThreadProcessId(window,out owner);if(owner!=pid)throw new Exception("Janela não pertence ao cliente.");
 process=OpenProcess(0x43A,false,pid);if(process==IntPtr.Zero)throw Fail("Abrir cliente");uint root=Read(Module+ClassicProfile.GameGlobal),run=Read(root+ClassicProfile.GameRunOffset);Host=Read(run+ClassicProfile.HostOffset);CharacterId=Read(Host+ClassicProfile.Id);uint manager=Read(run+AttackProfile.FollowReady);if(CharacterId==0||manager<0x10000)throw new Exception("Personagem não está pronto.");
 Install(target,Host,Module+AttackProfile.AttackableQuery,Module+AttackProfile.SelectTarget,Module+AttackProfile.MacroStart,Module+AttackProfile.MacroStop);
 }}catch{Dispose();throw;}}
 // Self-test restricted to a window of this process, never a game client.
 public AttackBridge(IntPtr testWindow,uint context,uint query,uint selectFunction,uint macroFunction,uint stopFunction){try{uint pid;UiThread=GetWindowThreadProcessId(testWindow,out pid);if(pid!=Process.GetCurrentProcess().Id)throw new Exception("Autoteste só aceita o próprio processo.");window=testWindow;using(var target=Process.GetProcessById((int)pid)){
 process=OpenProcess(0x43A,false,(int)pid);if(process==IntPtr.Zero)throw Fail("Abrir autoteste");Host=context;Install(target,context,query,selectFunction,macroFunction,stopFunction);
 }}catch{Dispose();throw;}}
 void Install(Process target,uint context,uint query,uint selectFunction,uint macroFunction,uint stopFunction){
 setLong=Api(target,"user32.dll","SetWindowLongA");getLong=Api(target,"user32.dll","GetWindowLongA");callOld=Api(target,"user32.dll","CallWindowProcA");getThread=Api(target,"kernel32.dll","GetCurrentThreadId");
 allocation=VirtualAllocEx(process,IntPtr.Zero,new UIntPtr(8192),0x3000,4);if(allocation==IntPtr.Zero)throw Fail("Alocar ponte");data=Addr(allocation);hook=data+4096;installer=hook+1024;
 byte[] state=new byte[64];Buffer.BlockCopy(BitConverter.GetBytes(context),0,state,4,4);Buffer.BlockCopy(BitConverter.GetBytes(query),0,state,8,4);Buffer.BlockCopy(BitConverter.GetBytes(selectFunction),0,state,32,4);Buffer.BlockCopy(BitConverter.GetBytes(macroFunction),0,state,36,4);Buffer.BlockCopy(BitConverter.GetBytes(stopFunction),0,state,40,4);Write(data,state);
 byte[] nativeCode=BuildHook();if(nativeCode.Length>=1024)throw new Exception("Ponte de ataque excedeu limite");Write(hook,nativeCode);Write(installer,BuildInstaller());uint old;if(!VirtualProtectEx(process,Pointer(hook),new UIntPtr(4096),0x20,out old))throw Fail("Proteger código");FlushInstructionCache(process,Pointer(hook),new UIntPtr(4096));
 uint thread;IntPtr worker=CreateRemoteThread(process,IntPtr.Zero,UIntPtr.Zero,Pointer(installer),IntPtr.Zero,0,out thread);if(worker==IntPtr.Zero)throw Fail("Instalar ponte");try{if(WaitForSingleObject(worker,3000)!=0){retain=true;throw new Exception("Instalação sem resposta. Memória preservada para não invalidar código em uso.");}uint exit;GetExitCodeThread(worker,out exit);if(exit!=hook||Read(data)==0)throw new Exception("Subclasse interna não foi instalada.");installed=true;}finally{CloseHandle(worker);}
 }
 class Code {public List<byte> B=new List<byte>();public void Bytes(params byte[] b){B.AddRange(b);}public void U(uint v){B.AddRange(BitConverter.GetBytes(v));}public void Push(uint v){Bytes(0x68);U(v);}public void Call(uint v){Bytes(0xB8);U(v);Bytes(0xFF,0xD0);}public int Jump(byte condition){Bytes(0x0F,condition);int at=B.Count;U(0);return at;}public void Fix(int at){byte[] value=BitConverter.GetBytes(B.Count-at-4);for(int i=0;i<4;i++)B[at+i]=value[i];}}
 byte[] BuildInstaller(){var c=new Code();c.Push(0xFFFFFFFC);c.Push(Addr(window));c.Call(getLong);c.Bytes(0xA3);c.U(data);c.Push(hook);c.Push(0xFFFFFFFC);c.Push(Addr(window));c.Call(setLong);c.Bytes(0x85,0xC0);int failed=c.Jump(0x84);c.Bytes(0xB8);c.U(hook);c.Bytes(0xC2,0x04,0x00);c.Fix(failed);c.Bytes(0x31,0xC0,0xC2,0x04,0x00);return c.B.ToArray();}
 // Classic attackable query: thiscall (target, force) with ret 8.
 void Query(Code c){c.Push(0);c.Bytes(0xFF,0x35);c.U(data+28);c.Bytes(0xFF,0x15);c.U(data+8);}
 byte[] BuildHook(){uint macro=AttackProfile.HostMacro,targetField=AttackProfile.HostTarget,comboVtable=Module+AttackProfile.ComboSkill;
 var c=new Code();c.Bytes(0x55,0x8B,0xEC,0x81,0x7D,0x0C);c.U(MessageId);int other=c.Jump(0x85);c.Bytes(0x81,0x7D,0x14);c.U(Restore);int cleanup=c.Jump(0x84);c.Bytes(0x81,0x7D,0x14);c.U(Magic);int other2=c.Jump(0x85);
 c.Bytes(0x60,0x9C);c.Call(getThread);c.Bytes(0xA3);c.U(data+12);c.Bytes(0x8B,0x0D);c.U(data+4);c.Bytes(0x83,0x3D);c.U(data+24);c.Bytes(1);int select=c.Jump(0x84);c.Bytes(0x83,0x3D);c.U(data+24);c.Bytes(2);int macroMode=c.Jump(0x84);c.Bytes(0x83,0x3D);c.U(data+24);c.Bytes(3);int stop=c.Jump(0x84);
 // Mode 4: stop the running macro object (type checked); data+44 = macro index to match, or -1 for any.
 c.Bytes(0x83,0x3D);c.U(data+24);c.Bytes(4);int notAny=c.Jump(0x85);c.Bytes(0x8B,0x81);c.U(macro);c.Bytes(0x85,0xC0);int anyMissing=c.Jump(0x84);c.Bytes(0x81,0x38);c.U(comboVtable);int anyType=c.Jump(0x85);c.Bytes(0x8B,0x15);c.U(data+44);c.Bytes(0x83,0xFA,0xFF);int anyIndex=c.Jump(0x84);c.Bytes(0x39,0x50,0x1C);int anyOther=c.Jump(0x85);c.Fix(anyIndex);c.Bytes(0xFF,0x15);c.U(data+40);c.Bytes(0xB8);c.U(1);c.Bytes(0xE9);int finishAny=c.B.Count;c.U(0);c.Fix(anyMissing);c.Fix(anyType);c.Fix(anyOther);c.Bytes(0x31,0xC0);c.Bytes(0xE9);int finishNone=c.B.Count;c.U(0);c.Fix(notAny);
 // Mode 5 (support): select the target in data+28, then start macro index data+44.
 c.Bytes(0x83,0x3D);c.U(data+24);c.Bytes(5);int notSupport=c.Jump(0x85);c.Bytes(0xFF,0x35);c.U(data+28);c.Bytes(0xFF,0x15);c.U(data+32);c.Bytes(0x8B,0x0D);c.U(data+4);c.Push(0xFFFFFFFF);c.Bytes(0xFF,0x35);c.U(data+48);c.Bytes(0xFF,0x35);c.U(data+44);c.Bytes(0xFF,0x15);c.U(data+36);c.Bytes(0xE9);int finishSupport=c.B.Count;c.U(0);c.Fix(notSupport);
 Query(c);c.Bytes(0xE9);int finishQuery=c.B.Count;c.U(0);
 c.Fix(select);c.Bytes(0xFF,0x35);c.U(data+28);c.Bytes(0xFF,0x15);c.U(data+32);c.Bytes(0xE9);int finishSelect=c.B.Count;c.U(0);
 c.Fix(macroMode);c.Bytes(0xA1);c.U(data+28);c.Bytes(0x39,0x81);c.U(targetField);int badTarget=c.Jump(0x85);Query(c);c.Bytes(0x83,0xF8,1);int notAttackable=c.Jump(0x85);c.Bytes(0x8B,0x0D);c.U(data+4);c.Push(0xFFFFFFFF);c.Bytes(0xFF,0x35);c.U(data+48);c.Push(0);c.Bytes(0xFF,0x15);c.U(data+36);
 c.Bytes(0xE9);int finishMacro=c.B.Count;c.U(0);c.Fix(badTarget);c.Fix(notAttackable);c.Bytes(0x31,0xC0);c.Fix(finishMacro);
 c.Bytes(0xE9);int finishStart=c.B.Count;c.U(0);
 c.Fix(stop);c.Bytes(0x8B,0x81);c.U(macro);c.Bytes(0x3B,0x05);c.U(data+44);int wrongOwner=c.Jump(0x85);c.Bytes(0x85,0xC0);int missing=c.Jump(0x84);c.Bytes(0x81,0x38);c.U(comboVtable);int wrongType=c.Jump(0x85);c.Bytes(0x83,0x78,0x1C,0);int wrongMacro=c.Jump(0x85);c.Bytes(0x8B,0x15);c.U(data+28);c.Bytes(0x39,0x50,0x2C);int wrongTarget=c.Jump(0x85);c.Bytes(0xFF,0x15);c.U(data+40);c.Bytes(0xB8);c.U(1);c.Bytes(0xE9);int finishStop=c.B.Count;c.U(0);c.Fix(wrongOwner);c.Fix(missing);c.Fix(wrongType);c.Fix(wrongMacro);c.Fix(wrongTarget);c.Bytes(0x31,0xC0);c.Fix(finishStop);c.Fix(finishStart);
 c.Fix(finishQuery);c.Fix(finishSelect);c.Fix(finishAny);c.Fix(finishNone);c.Fix(finishSupport);c.Bytes(0xA3);c.U(data+16);c.Bytes(0x8B,0x0D);c.U(data+4);c.Bytes(0x8B,0x81);c.U(macro);c.Bytes(0xA3);c.U(data+20);c.Bytes(0x9D,0x61,0xB8);c.U(0x41564F4B);c.Bytes(0x5D,0xC2,0x10,0x00);
 c.Fix(cleanup);c.Push(0xFFFFFFFC);c.Bytes(0xFF,0x75,0x08);c.Call(getLong);c.Bytes(0x3D);c.U(hook);int busy=c.Jump(0x85);c.Bytes(0xFF,0x35);c.U(data);c.Push(0xFFFFFFFC);c.Bytes(0xFF,0x75,0x08);c.Call(setLong);c.Bytes(0xB8);c.U(0x4156524B);c.Bytes(0x5D,0xC2,0x10,0x00);c.Fix(busy);c.Bytes(0x31,0xC0,0x5D,0xC2,0x10,0x00);
 c.Fix(other);c.Fix(other2);c.Bytes(0xFF,0x75,0x14,0xFF,0x75,0x10,0xFF,0x75,0x0C,0xFF,0x75,0x08,0xFF,0x35);c.U(data);c.Call(callOld);c.Bytes(0x5D,0xC2,0x10,0x00);return c.B.ToArray();}
 public uint LastResult,Combo;
 public bool ClearTarget(){Command(1,0);return LastResult==1;}
 public bool StartMacroOn(uint player,int macroIndex){if(macroIndex<0||macroIndex>=AttackProfile.MacroCount)throw new Exception("Macro inválida.");Command(5,player,(uint)macroIndex);return LastResult==1&&Combo!=0;}
 public bool StartMacroOnMonster(uint monster,int macroIndex){if(macroIndex<0||macroIndex>=AttackProfile.MacroCount||(monster&0xC0000000)!=0x80000000)throw new Exception("Macro inválida.");Command(5,monster,(uint)macroIndex);return LastResult==1&&Combo!=0;}
 public bool StopMacro(int macroIndex){Command(4,0,unchecked((uint)macroIndex));return LastResult==1;}
 public void Command(uint mode,uint target,uint expectedCombo=0,bool once=false){if(!installed||mode>5||mode==5&&(target==0||(target&0x80000000)!=0&&(target&0xC0000000)!=0x80000000)||mode<4&&!(mode==1&&target==0)&&(target==0||(target&0xC0000000)!=0x80000000))throw new Exception("Comando inválido.");Write(data+44,BitConverter.GetBytes(expectedCombo));Write(data+48,BitConverter.GetBytes(once?1:0));Write(data+24,BitConverter.GetBytes(mode));Write(data+28,BitConverter.GetBytes(target));UIntPtr result;if(SendMessageTimeout(window,MessageId,UIntPtr.Zero,Pointer(Magic),3,2000,out result)==IntPtr.Zero){retain=true;throw Fail("Comando interno sem resposta");}ExecutedThread=Read(data+12);LastResult=Read(data+16)&255;Combo=Read(data+20);if(result.ToUInt32()!=0x41564F4B||ExecutedThread!=UiThread)throw new Exception("Execução não foi confirmada na thread da janela.");}
 public void Dispose(){try{DisposeCore();}finally{ProcessGate.Exit(ref gate);}}
 void DisposeCore(){if(process==IntPtr.Zero)return;if(installed){UIntPtr result;if(SendMessageTimeout(window,MessageId,UIntPtr.Zero,Pointer(Restore),3,2000,out result)==IntPtr.Zero||result.ToUInt32()!=0x4156524B)retain=true;else{installed=false;RestoreSucceeded=true;}}if(allocation!=IntPtr.Zero&&!retain&&!installed)VirtualFreeEx(process,allocation,UIntPtr.Zero,0x8000);CloseHandle(process);process=IntPtr.Zero;}
}

// Classic copy of the FarmZilla TeleportBridge restricted to the ESC commands the attack flow uses (2A 00, 08 00).
public sealed class TeleportBridge : IDisposable {
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr p,IntPtr a,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool WriteProcessMemory(IntPtr p,IntPtr a,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr a,UIntPtr n,uint type,uint protection);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool VirtualProtectEx(IntPtr p,IntPtr a,UIntPtr n,uint protection,out uint old);
 [DllImport("kernel32.dll")]static extern bool VirtualFreeEx(IntPtr p,IntPtr a,UIntPtr n,uint type);
 [DllImport("kernel32.dll")]static extern bool FlushInstructionCache(IntPtr p,IntPtr a,UIntPtr n);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr CreateRemoteThread(IntPtr p,IntPtr attr,UIntPtr size,IntPtr start,IntPtr arg,uint flags,out uint id);
 [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr h,uint timeout);
 [DllImport("kernel32.dll")]static extern bool GetExitCodeThread(IntPtr h,out uint code);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetModuleHandle(string name);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetProcAddress(IntPtr module,string name);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string value);
 [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SendMessageTimeout(IntPtr window,uint msg,UIntPtr wp,IntPtr lp,uint flags,uint timeout,out UIntPtr result);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 public static uint MessageId=RegisterWindowMessage("FarmZilla.TheClassic.Send.0.5");
 const uint Magic=0x4156464C,Restore=0x41565253;
 IntPtr process,allocation,window;uint data,hook,installer;bool installed,retain;
 public uint UiThread,ExecutedThread,Module,Host,CharacterId,LastSendResult; public bool RestoreSucceeded;
 uint setLong,getLong,callOld,getThread;
 static uint Addr(IntPtr p){return unchecked((uint)p.ToInt32());}
 static IntPtr Pointer(uint a){return new IntPtr(unchecked((int)a));}
 static Exception Fail(string what){return new Exception(what+" (Windows "+Marshal.GetLastWin32Error()+")");}
 public uint Read(uint at){byte[] b=new byte[4];UIntPtr got;if(!ReadProcessMemory(process,Pointer(at),b,new UIntPtr(4),out got)||got.ToUInt32()!=4)throw Fail("Leitura em "+at.ToString("X8"));return BitConverter.ToUInt32(b,0);}
 void Write(uint at,byte[] b){UIntPtr got;if(!WriteProcessMemory(process,Pointer(at),b,new UIntPtr((uint)b.Length),out got)||got.ToUInt32()!=b.Length)throw Fail("Gravação da ponte");}
 uint Api(Process target,string dll,string name){IntPtr local=GetModuleHandle(dll),address=GetProcAddress(local,name);if(address==IntPtr.Zero)throw Fail("API "+name);string owner=null;uint ownerBase=0;foreach(ProcessModule m in Process.GetCurrentProcess().Modules){uint start=Addr(m.BaseAddress),end=start+(uint)m.ModuleMemorySize;if(Addr(address)>=start&&Addr(address)<end){owner=m.ModuleName;ownerBase=start;break;}}if(owner==null)throw new Exception("Módulo local da API não encontrado.");foreach(ProcessModule m in target.Modules)if(string.Equals(m.ModuleName,owner,StringComparison.OrdinalIgnoreCase))return Addr(m.BaseAddress)+Addr(address)-ownerBase;throw new Exception("Módulo remoto da API não encontrado: "+owner);}
 ProcessGate.Token gate;
 public TeleportBridge(int pid){if(IntPtr.Size!=4)throw new Exception("Ponte exige processo x86.");gate=ProcessGate.Enter(pid);try{using(var target=Process.GetProcessById(pid)){
 Module=ClassicClient.Require(target);
 window=target.MainWindowHandle;if(window==IntPtr.Zero)throw new Exception("Janela do jogo não encontrada.");uint owner;UiThread=GetWindowThreadProcessId(window,out owner);if(owner!=pid)throw new Exception("Janela não pertence ao cliente.");
 process=OpenProcess(0x43A,false,pid);if(process==IntPtr.Zero)throw Fail("Abrir cliente");uint root=Read(Module+ClassicProfile.GameGlobal),run=Read(root+ClassicProfile.GameRunOffset);Host=Read(run+ClassicProfile.HostOffset);CharacterId=Read(Host+ClassicProfile.Id);uint manager=Read(root+AttackProfile.GameNet);if(CharacterId==0||manager<0x10000)throw new Exception("Personagem não está pronto.");
 Install(target,manager,Module+AttackProfile.SendData);
 }}catch{Dispose();throw;}}
 public TeleportBridge(IntPtr testWindow,uint context,uint function){try{uint pid;UiThread=GetWindowThreadProcessId(testWindow,out pid);if(pid!=Process.GetCurrentProcess().Id)throw new Exception("Autoteste só aceita o próprio processo.");window=testWindow;using(var target=Process.GetProcessById((int)pid)){
 process=OpenProcess(0x43A,false,(int)pid);if(process==IntPtr.Zero)throw Fail("Abrir autoteste");Install(target,context,function);
 }}catch{Dispose();throw;}}
 void Install(Process target,uint context,uint function){
 setLong=Api(target,"user32.dll","SetWindowLongA");getLong=Api(target,"user32.dll","GetWindowLongA");callOld=Api(target,"user32.dll","CallWindowProcA");getThread=Api(target,"kernel32.dll","GetCurrentThreadId");
 allocation=VirtualAllocEx(process,IntPtr.Zero,new UIntPtr(8192),0x3000,4);if(allocation==IntPtr.Zero)throw Fail("Alocar ponte");data=Addr(allocation);hook=data+4096;installer=hook+512;
 byte[] state=new byte[64];Buffer.BlockCopy(BitConverter.GetBytes(context),0,state,4,4);Buffer.BlockCopy(BitConverter.GetBytes(function),0,state,8,4);Write(data,state);
 Write(hook,BuildHook());Write(installer,BuildInstaller());uint old;if(!VirtualProtectEx(process,Pointer(hook),new UIntPtr(4096),0x20,out old))throw Fail("Proteger código");FlushInstructionCache(process,Pointer(hook),new UIntPtr(4096));
 uint thread;IntPtr worker=CreateRemoteThread(process,IntPtr.Zero,UIntPtr.Zero,Pointer(installer),IntPtr.Zero,0,out thread);if(worker==IntPtr.Zero)throw Fail("Instalar ponte");try{if(WaitForSingleObject(worker,3000)!=0){retain=true;throw new Exception("Instalação sem resposta. Memória preservada para não invalidar código em uso.");}uint exit;GetExitCodeThread(worker,out exit);if(exit!=hook||Read(data)==0)throw new Exception("Subclasse interna não foi instalada.");installed=true;}finally{CloseHandle(worker);}
 }
 class Code {public List<byte> B=new List<byte>();public void Bytes(params byte[] b){B.AddRange(b);}public void U(uint v){B.AddRange(BitConverter.GetBytes(v));}public void Push(uint v){Bytes(0x68);U(v);}public void Call(uint v){Bytes(0xB8);U(v);Bytes(0xFF,0xD0);}public int Jump(byte condition){Bytes(0x0F,condition);int at=B.Count;U(0);return at;}public void Fix(int at){byte[] value=BitConverter.GetBytes(B.Count-at-4);for(int i=0;i<4;i++)B[at+i]=value[i];}}
 byte[] BuildInstaller(){var c=new Code();c.Push(0xFFFFFFFC);c.Push(Addr(window));c.Call(getLong);c.Bytes(0xA3);c.U(data);c.Push(hook);c.Push(0xFFFFFFFC);c.Push(Addr(window));c.Call(setLong);c.Bytes(0x85,0xC0);int failed=c.Jump(0x84);c.Bytes(0xB8);c.U(hook);c.Bytes(0xC2,0x04,0x00);c.Fix(failed);c.Bytes(0x31,0xC0,0xC2,0x04,0x00);return c.B.ToArray();}
 byte[] BuildHook(){var c=new Code();c.Bytes(0x55,0x8B,0xEC,0x81,0x7D,0x0C);c.U(MessageId);int other=c.Jump(0x85);c.Bytes(0x81,0x7D,0x14);c.U(Restore);int cleanup=c.Jump(0x84);c.Bytes(0x81,0x7D,0x14);c.U(Magic);int other2=c.Jump(0x85);
 c.Bytes(0x60,0x9C);c.Call(getThread);c.Bytes(0xA3);c.U(data+12);c.Bytes(0x8B,0x0D);c.U(data+4);c.Bytes(0xFF,0x35);c.U(data+24);c.Push(data+32);c.Bytes(0xFF,0x15);c.U(data+8);c.Bytes(0xA3);c.U(data+16);c.Bytes(0x9D,0x61,0xB8);c.U(0x41564F4B);c.Bytes(0x5D,0xC2,0x10,0x00);
 c.Fix(cleanup);c.Push(0xFFFFFFFC);c.Bytes(0xFF,0x75,0x08);c.Call(getLong);c.Bytes(0x3D);c.U(hook);int busy=c.Jump(0x85);c.Bytes(0xFF,0x35);c.U(data);c.Push(0xFFFFFFFC);c.Bytes(0xFF,0x75,0x08);c.Call(setLong);c.Bytes(0xB8);c.U(0x4156524B);c.Bytes(0x5D,0xC2,0x10,0x00);c.Fix(busy);c.Bytes(0x31,0xC0,0x5D,0xC2,0x10,0x00);
 c.Fix(other);c.Fix(other2);c.Bytes(0xFF,0x75,0x14,0xFF,0x75,0x10,0xFF,0x75,0x0C,0xFF,0x75,0x08,0xFF,0x35);c.U(data);c.Call(callOld);c.Bytes(0x5D,0xC2,0x10,0x00);return c.B.ToArray();}
 public void SendControl(byte[] packet){if(packet==null||packet.Length!=2||packet[1]!=0||(packet[0]!=0x2A&&packet[0]!=0x08))throw new Exception("Comando de controle invalido");Send(packet);}
 void Send(byte[] packet){if(!installed)throw new Exception("Comando invalido");Write(data+24,BitConverter.GetBytes((uint)packet.Length));Write(data+32,packet);UIntPtr result;if(SendMessageTimeout(window,MessageId,UIntPtr.Zero,Pointer(Magic),3,2000,out result)==IntPtr.Zero){retain=true;throw Fail("Envio sem resposta");}ExecutedThread=Read(data+12);LastSendResult=Read(data+16)&255;if(result.ToUInt32()!=0x41564F4B||ExecutedThread!=UiThread)throw new Exception("Execucao na thread da janela nao confirmada");}
 public void Dispose(){try{DisposeCore();}finally{ProcessGate.Exit(ref gate);}}
 void DisposeCore(){if(process==IntPtr.Zero)return;if(installed){UIntPtr result;if(SendMessageTimeout(window,MessageId,UIntPtr.Zero,Pointer(Restore),3,2000,out result)==IntPtr.Zero||result.ToUInt32()!=0x4156524B)retain=true;else{installed=false;RestoreSucceeded=true;}}if(allocation!=IntPtr.Zero&&!retain&&!installed)VirtualFreeEx(process,allocation,UIntPtr.Zero,0x8000);CloseHandle(process);process=IntPtr.Zero;}
}
