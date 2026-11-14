using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

// Ponte x86 específica do cliente analisado. Sem entradas de mouse ou teclado.
public sealed class TeleportBridge : IDisposable {
 // 0.50.0: endereços do jogo vêm do perfil (validado ou por assinaturas). Autotestes usam o perfil validado.
 public GameProfile G=GameProfile.Current();
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
 public static uint MessageId=RegisterWindowMessage("AvalonTeleporte.Teste.20261029.91D7");
 const uint Magic=0x4156464C,Restore=0x41565253;
 IntPtr process,allocation,window;uint data,hook,installer;bool installed,retain;
 public uint UiThread,ExecutedThread,Module,Host,CharacterId; public bool RestoreSucceeded;
 uint setLong,getLong,callOld,getThread;
 public static uint Addr(IntPtr p){return unchecked((uint)p.ToInt32());}
 static IntPtr Pointer(uint a){return new IntPtr(unchecked((int)a));}
 static Exception Fail(string what){return new Exception(what+" (Windows "+Marshal.GetLastWin32Error()+")");}
 public uint Read(uint at){byte[] b=new byte[4];UIntPtr got;if(!ReadProcessMemory(process,Pointer(at),b,new UIntPtr(4),out got)||got.ToUInt32()!=4)throw Fail("Leitura em "+at.ToString("X8"));return BitConverter.ToUInt32(b,0);}
 void Write(uint at,byte[] b){UIntPtr got;if(!WriteProcessMemory(process,Pointer(at),b,new UIntPtr((uint)b.Length),out got)||got.ToUInt32()!=b.Length)throw Fail("Gravação da ponte");}
 uint Api(Process target,string dll,string name){IntPtr local=GetModuleHandle(dll),address=GetProcAddress(local,name);if(address==IntPtr.Zero)throw Fail("API "+name);string owner=null;uint ownerBase=0;foreach(ProcessModule m in Process.GetCurrentProcess().Modules){uint start=Addr(m.BaseAddress),end=start+(uint)m.ModuleMemorySize;if(Addr(address)>=start&&Addr(address)<end){owner=m.ModuleName;ownerBase=start;break;}}if(owner==null)throw new Exception("Módulo local da API não encontrado.");foreach(ProcessModule m in target.Modules)if(string.Equals(m.ModuleName,owner,StringComparison.OrdinalIgnoreCase))return Addr(m.BaseAddress)+Addr(address)-ownerBase;throw new Exception("Módulo remoto da API não encontrado: "+owner);}
 ProcessGate.Token gate;
 public TeleportBridge(int pid){if(IntPtr.Size!=4)throw new Exception("Ponte exige processo x86.");gate=ProcessGate.Enter(pid);try{using(var target=Process.GetProcessById(pid)){
 G=GameProfile.Require(target);
 window=target.MainWindowHandle;if(window==IntPtr.Zero)throw new Exception("Janela do jogo não encontrada.");uint owner;UiThread=GetWindowThreadProcessId(window,out owner);if(owner!=pid)throw new Exception("Janela não pertence ao cliente.");
 process=OpenProcess(0x43A,false,pid);if(process==IntPtr.Zero)throw Fail("Abrir cliente");Module=Addr(target.MainModule.BaseAddress);uint root=Read(Module+G.Root),run=Read(root+0x1C);Host=Read(run+ClassicLayout.RunHost);CharacterId=Read(Host+ClassicLayout.HostId);uint manager=Read(root+0x20);if(CharacterId==0||manager<0x10000)throw new Exception("Personagem não está pronto.");
 setLong=Api(target,"user32.dll","SetWindowLongA");getLong=Api(target,"user32.dll","GetWindowLongA");callOld=Api(target,"user32.dll","CallWindowProcA");getThread=Api(target,"kernel32.dll","GetCurrentThreadId");
 allocation=VirtualAllocEx(process,IntPtr.Zero,new UIntPtr(8192),0x3000,4);if(allocation==IntPtr.Zero)throw Fail("Alocar ponte");data=Addr(allocation);hook=data+4096;installer=hook+512;
 byte[] state=new byte[64];Buffer.BlockCopy(BitConverter.GetBytes(manager),0,state,4,4);Buffer.BlockCopy(BitConverter.GetBytes(Module+G.SendData),0,state,8,4);Write(data,state);
 Write(hook,BuildHook());Write(installer,BuildInstaller());uint old;if(!VirtualProtectEx(process,Pointer(hook),new UIntPtr(4096),0x20,out old))throw Fail("Proteger código");FlushInstructionCache(process,Pointer(hook),new UIntPtr(4096));
 uint thread;IntPtr worker=CreateRemoteThread(process,IntPtr.Zero,UIntPtr.Zero,Pointer(installer),IntPtr.Zero,0,out thread);if(worker==IntPtr.Zero)throw Fail("Instalar ponte");try{if(WaitForSingleObject(worker,3000)!=0){retain=true;throw new Exception("Instalação sem resposta. Memória preservada para não invalidar código em uso.");}uint exit;GetExitCodeThread(worker,out exit);if(exit!=hook||Read(data)==0)throw new Exception("Subclasse interna não foi instalada.");installed=true;}finally{CloseHandle(worker);}
 }}catch{Dispose();throw;}}
 // Autoteste restrito a uma janela do próprio processo, sem cliente de jogo.
 public TeleportBridge(IntPtr testWindow,uint context,uint function){try{uint pid;UiThread=GetWindowThreadProcessId(testWindow,out pid);if(pid!=Process.GetCurrentProcess().Id)throw new Exception("Autoteste só aceita o próprio processo.");window=testWindow;using(var target=Process.GetProcessById((int)pid)){
 process=OpenProcess(0x43A,false,(int)pid);if(process==IntPtr.Zero)throw Fail("Abrir autoteste");setLong=Api(target,"user32.dll","SetWindowLongA");getLong=Api(target,"user32.dll","GetWindowLongA");callOld=Api(target,"user32.dll","CallWindowProcA");getThread=Api(target,"kernel32.dll","GetCurrentThreadId");allocation=VirtualAllocEx(process,IntPtr.Zero,new UIntPtr(8192),0x3000,4);if(allocation==IntPtr.Zero)throw Fail("Alocar autoteste");data=Addr(allocation);hook=data+4096;installer=hook+512;byte[] state=new byte[64];Buffer.BlockCopy(BitConverter.GetBytes(context),0,state,4,4);Buffer.BlockCopy(BitConverter.GetBytes(function),0,state,8,4);Write(data,state);Write(hook,BuildHook());Write(installer,BuildInstaller());uint old;if(!VirtualProtectEx(process,Pointer(hook),new UIntPtr(4096),0x20,out old))throw Fail("Código do autoteste");FlushInstructionCache(process,Pointer(hook),new UIntPtr(4096));uint id;IntPtr worker=CreateRemoteThread(process,IntPtr.Zero,UIntPtr.Zero,Pointer(installer),IntPtr.Zero,0,out id);if(worker==IntPtr.Zero)throw Fail("Thread do autoteste");try{if(WaitForSingleObject(worker,3000)!=0){retain=true;throw new Exception("Tempo de instalação esgotado.");}uint exit;GetExitCodeThread(worker,out exit);if(exit!=hook||Read(data)==0)throw new Exception("Instalação do autoteste falhou.");installed=true;}finally{CloseHandle(worker);}
 }}catch{Dispose();throw;}}
 class Code {public List<byte> B=new List<byte>();public void Bytes(params byte[] b){B.AddRange(b);}public void U(uint v){B.AddRange(BitConverter.GetBytes(v));}public void Push(uint v){Bytes(0x68);U(v);}public void Call(uint v){Bytes(0xB8);U(v);Bytes(0xFF,0xD0);}public void Mem(uint a){U(a);}public int Jump(byte condition){Bytes(0x0F,condition);int at=B.Count;U(0);return at;}public void Fix(int at){byte[] value=BitConverter.GetBytes(B.Count-at-4);for(int i=0;i<4;i++)B[at+i]=value[i];}}
 byte[] BuildInstaller(){var c=new Code();c.Push(0xFFFFFFFC);c.Push(Addr(window));c.Call(getLong);c.Bytes(0xA3);c.U(data);c.Push(hook);c.Push(0xFFFFFFFC);c.Push(Addr(window));c.Call(setLong);c.Bytes(0x85,0xC0);int failed=c.Jump(0x84);c.Bytes(0xB8);c.U(hook);c.Bytes(0xC2,0x04,0x00);c.Fix(failed);c.Bytes(0x31,0xC0,0xC2,0x04,0x00);return c.B.ToArray();}
 byte[] BuildHook(){var c=new Code();c.Bytes(0x55,0x8B,0xEC,0x81,0x7D,0x0C);c.U(MessageId);int other=c.Jump(0x85);c.Bytes(0x81,0x7D,0x14);c.U(Restore);int cleanup=c.Jump(0x84);c.Bytes(0x81,0x7D,0x14);c.U(Magic);int other2=c.Jump(0x85);
 c.Bytes(0x60,0x9C);c.Call(getThread);c.Bytes(0xA3);c.U(data+12);c.Bytes(0x8B,0x0D);c.U(data+4);c.Bytes(0xFF,0x35);c.U(data+24);c.Push(data+32);c.Bytes(0xFF,0x15);c.U(data+8);c.Bytes(0xA3);c.U(data+16);c.Bytes(0x9D,0x61,0xB8);c.U(0x41564F4B);c.Bytes(0x5D,0xC2,0x10,0x00); c.Fix(cleanup);c.Push(0xFFFFFFFC);c.Bytes(0xFF,0x75,0x08);c.Call(getLong);c.Bytes(0x3D);c.U(hook);int busy=c.Jump(0x85);c.Bytes(0xFF,0x35);c.U(data);c.Push(0xFFFFFFFC);c.Bytes(0xFF,0x75,0x08);c.Call(setLong);c.Bytes(0xB8);c.U(0x4156524B);c.Bytes(0x5D,0xC2,0x10,0x00);c.Fix(busy);c.Bytes(0x31,0xC0,0x5D,0xC2,0x10,0x00);
 c.Fix(other);c.Fix(other2);c.Bytes(0xFF,0x75,0x14,0xFF,0x75,0x10,0xFF,0x75,0x0C,0xFF,0x75,0x08,0xFF,0x35);c.U(data);c.Call(callOld);c.Bytes(0x5D,0xC2,0x10,0x00);return c.B.ToArray();}
 public uint LastSendResult;
 public void SendTeleport(byte[] packet){if(!installed||packet==null||packet.Length!=14||BitConverter.ToUInt32(packet,0)!=0x0100006D||Array.IndexOf(TeleportService.StoneIds,BitConverter.ToUInt32(packet,6))<0)throw new Exception("Comando de pedra invalido");Send(packet);}
 // Teleporte Especial: only the use of item 52256 (10 bytes) and the point choice 0x31 (9 bytes) are accepted.
 public void SendSpecial(byte[] packet){if(packet==null||!(packet.Length==10&&BitConverter.ToUInt32(packet,0)==0x01000028&&BitConverter.ToUInt32(packet,6)==52256||packet.Length==9&&BitConverter.ToUInt16(packet,0)==0x31&&BitConverter.ToUInt32(packet,2)==3&&packet[6]==1))throw new Exception("Comando de Teleporte Especial invalido");Send(packet);}
 // Automatic quest window: 31 00 | 03 00 00 00 | 03 or 04 | quest id. Mirrored only after the principal shows the quest as accepted.
 public void SendQuestChoice(byte[] packet){if(packet==null||packet.Length!=9||BitConverter.ToUInt16(packet,0)!=0x31||BitConverter.ToUInt32(packet,2)!=3||(packet[6]!=3&&packet[6]!=4))throw new Exception("Comando de janela de missao invalido");Send(packet);}
 // ESC as the client sends it (captured on the principal): cancel action 2A 00 and unselect 08 00. Nothing else is accepted.
 // 0.49.0: equip from the bag (11 00 <bag slot> <equipment slot>), captured on the principal; only the tactical bag slot 22.
 public void SendEquip(byte[] packet){if(packet==null||packet.Length!=4||packet[0]!=0x11||packet[1]!=0||packet[2]>200||packet[3]!=0x16)throw new Exception("Comando de equipar invalido");Send(packet);}
 // 0.50.2: accept a resurrection offer (Prece da Vida), captured on the principal 06/11/2026 22:24:47: 57 00 00 00 00 00.
 // 0.50.4: revive at the nearest town, captured 07/11/2026 12:04:27: 04 00 00 00 00 00.
 // 0.51.0: WF fox (skill 312 on herself, type 0x50) and Ferir (skill 320 on the monster, type 0x29) — closed list.
 public void SendFoxSkill(byte[] p){if(p==null||p.Length!=12||p[1]!=0||p[6]!=0x0B||p[7]!=1)throw new Exception("Habilidade invalida");uint skill=BitConverter.ToUInt32(p,2);if(!(p[0]==0x50&&skill==312||p[0]==0x29&&skill==320))throw new Exception("Habilidade invalida");Send(p);}
 // 0.51.2: WF pet — F5 revive (skill 329 on herself, 29 00 49 01 00 00 0B 01 <id>) and F6 call (64 00 00 00 00 00); closed list.
 public void SendPetCare(byte[] p){bool revive=p!=null&&p.Length==12&&p[0]==0x29&&p[1]==0&&BitConverter.ToUInt32(p,2)==329&&p[6]==0x0B&&p[7]==1&&BitConverter.ToUInt32(p,8)==CharacterId;bool call=p!=null&&p.Length==6&&p[0]==0x64&&p[1]==0&&BitConverter.ToUInt32(p,2)==0;if(!revive&&!call)throw new Exception("Comando de pet invalido");Send(p);}
	public void SendSelfSkill(byte type, uint skill)
	{
		if ((type != 41 || skill != 186) && (type != 80 || (skill != 188 && skill != 112)))
		{
			throw new Exception("Habilidade invalida");
		}
		byte[] array = new byte[12]
		{
			type, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0
		};
		BitConverter.GetBytes(skill).CopyTo(array, 2);
		array[6] = 11;
		array[7] = 1;
		BitConverter.GetBytes(CharacterId).CopyTo(array, 8);
		Send(array);
	}

	public void SendTargetSkill(uint skill, uint target)
	{
		if (skill != 185 || (target & 0xC0000000u) != 2147483648u)
		{
			throw new Exception("Habilidade invalida");
		}
		byte[] array = new byte[12]
		{
			41, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0
		};
		BitConverter.GetBytes(skill).CopyTo(array, 2);
		array[6] = 11;
		array[7] = 1;
		BitConverter.GetBytes(target).CopyTo(array, 8);
		Send(array);
	}

	public void SendLoot(IList<KeyValuePair<uint, uint>> drops)
	{
		if (drops == null || drops.Count < 1 || drops.Count > 40)
		{
			throw new Exception("Lista de drops invalida");
		}
		byte[] array = new byte[6 + 8 * drops.Count];
		array[0] = 184;
		BitConverter.GetBytes((uint)drops.Count).CopyTo(array, 2);
		for (int i = 0; i < drops.Count; i++)
		{
			if ((drops[i].Key & 0xC0000000u) != 3221225472u || drops[i].Value == 0 || drops[i].Value > 1000000)
			{
				throw new Exception("Drop invalido");
			}
			BitConverter.GetBytes(drops[i].Key).CopyTo(array, 6 + 8 * i);
			BitConverter.GetBytes(drops[i].Value).CopyTo(array, 10 + 8 * i);
		}
		Send(array);
	}

	public void SendDgPacket(byte[] p)
	{
		if (p == null || p.Length < 2 || p[1] != 0 || ((p[0] != 35 || p.Length != 6) && (p[0] != 37 || p.Length < 10 || p.Length > 32) && (p[0] != 49 || p.Length != 9) && (p[0] != 86 || p.Length != 10) && (p[0] != 2 || p.Length != 6)))
		{
			throw new Exception("Comando da DG invalido");
		}
		Send(p);
	}


 public void SendTownRevive(){Send(new byte[]{0x04,0,0,0,0,0});}
 public void SendReviveAccept(){Send(new byte[]{0x57,0,0,0,0,0});}
 public void SendControl(byte[] packet){if(packet==null||packet.Length!=2||packet[1]!=0||(packet[0]!=0x2A&&packet[0]!=0x08))throw new Exception("Comando de controle invalido");Send(packet);}
 void Send(byte[] packet){if(!installed)throw new Exception("Comando invalido");Write(data+24,BitConverter.GetBytes((uint)packet.Length));Write(data+32,packet);UIntPtr result;if(SendMessageTimeout(window,MessageId,UIntPtr.Zero,Pointer(Magic),3,2000,out result)==IntPtr.Zero){retain=true;throw Fail("Envio sem resposta");}ExecutedThread=Read(data+12);LastSendResult=Read(data+16)&255;if(result.ToUInt32()!=0x41564F4B||ExecutedThread!=UiThread)throw new Exception("Execucao na thread da janela nao confirmada");} public void Dispose(){try{DisposeCore();}finally{ProcessGate.Exit(ref gate);}}
 void DisposeCore(){if(process==IntPtr.Zero)return;if(installed){UIntPtr result;if(SendMessageTimeout(window,MessageId,UIntPtr.Zero,Pointer(Restore),3,2000,out result)==IntPtr.Zero||result.ToUInt32()!=0x4156524B)retain=true;else{installed=false;RestoreSucceeded=true;}}if(allocation!=IntPtr.Zero&&!retain&&!installed)VirtualFreeEx(process,allocation,UIntPtr.Zero,0x8000);CloseHandle(process);process=IntPtr.Zero;}
}
