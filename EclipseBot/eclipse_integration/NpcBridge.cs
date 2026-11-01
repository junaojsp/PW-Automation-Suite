using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

public sealed class NpcBridge : IDisposable
{
	private class Code
	{
		public List<byte> B = new List<byte>();

		public void Bytes(params byte[] b)
		{
			B.AddRange(b);
		}

		public void U(uint v)
		{
			B.AddRange(BitConverter.GetBytes(v));
		}

		public void Push(uint v)
		{
			Bytes(104);
			U(v);
		}

		public void Call(uint v)
		{
			Bytes(184);
			U(v);
			Bytes(255, 208);
		}

		public void Mem(uint a)
		{
			U(a);
		}

		public int Jump(byte condition)
		{
			Bytes(15, condition);
			int count = B.Count;
			U(0u);
			return count;
		}

		public void Fix(int at)
		{
			byte[] bytes = BitConverter.GetBytes(B.Count - at - 4);
			for (int i = 0; i < 4; i++)
			{
				B[at + i] = bytes[i];
			}
		}
	}

	private const uint Magic = 1096173132u;

	private const uint Restore = 1096176211u;

	public static uint MessageId = RegisterWindowMessage("AvalonNPC.20261029.0D09");

	private IntPtr process;

	private IntPtr allocation;

	private IntPtr window;

	private uint data;

	private uint hook;

	private uint installer;

	private bool installed;

	private bool retain;

	public uint UiThread;

	public uint ExecutedThread;

	public uint Module;

	public uint Host;

	public uint CharacterId;

	public bool RestoreSucceeded;

	private uint setLong;

	private uint getLong;

	private uint callOld;

	private uint getThread;

	private uint testCloseVtable;

	public uint LastSendResult;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool ReadProcessMemory(IntPtr p, IntPtr a, byte[] b, UIntPtr n, out UIntPtr got);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool WriteProcessMemory(IntPtr p, IntPtr a, byte[] b, UIntPtr n, out UIntPtr got);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr n, uint type, uint protection);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool VirtualProtectEx(IntPtr p, IntPtr a, UIntPtr n, uint protection, out uint old);

	[DllImport("kernel32.dll")]
	private static extern bool VirtualFreeEx(IntPtr p, IntPtr a, UIntPtr n, uint type);

	[DllImport("kernel32.dll")]
	private static extern bool FlushInstructionCache(IntPtr p, IntPtr a, UIntPtr n);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr CreateRemoteThread(IntPtr p, IntPtr attr, UIntPtr size, IntPtr start, IntPtr arg, uint flags, out uint id);

	[DllImport("kernel32.dll")]
	private static extern uint WaitForSingleObject(IntPtr h, uint timeout);

	[DllImport("kernel32.dll")]
	private static extern bool GetExitCodeThread(IntPtr h, out uint code);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(IntPtr h);

	[DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
	private static extern IntPtr GetModuleHandle(string name);

	[DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
	private static extern IntPtr GetProcAddress(IntPtr module, string name);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern uint RegisterWindowMessage(string value);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr SendMessageTimeout(IntPtr window, uint msg, UIntPtr wp, IntPtr lp, uint flags, uint timeout, out UIntPtr result);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

	public static uint Addr(IntPtr p)
	{
		return (uint)p.ToInt32();
	}

	private static IntPtr Pointer(uint a)
	{
		return new IntPtr((int)a);
	}

	private static Exception Fail(string what)
	{
		return new Exception(what + " (Windows " + Marshal.GetLastWin32Error() + ")");
	}

	public uint Read(uint at)
	{
		byte[] array = new byte[4];
		if (!ReadProcessMemory(process, Pointer(at), array, new UIntPtr(4u), out var got) || got.ToUInt32() != 4)
		{
			throw Fail("Leitura em " + at.ToString("X8"));
		}
		return BitConverter.ToUInt32(array, 0);
	}

	private void Write(uint at, byte[] b)
	{
		if (!WriteProcessMemory(process, Pointer(at), b, new UIntPtr((uint)b.Length), out var got) || got.ToUInt32() != b.Length)
		{
			throw Fail("Gravação da ponte");
		}
	}

	private uint Api(Process target, string dll, string name)
	{
		IntPtr moduleHandle = GetModuleHandle(dll);
		IntPtr procAddress = GetProcAddress(moduleHandle, name);
		if (procAddress == IntPtr.Zero)
		{
			throw Fail("API " + name);
		}
		string text = null;
		uint num = 0u;
		foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
		{
			uint num2 = Addr(module.BaseAddress);
			uint num3 = num2 + (uint)module.ModuleMemorySize;
			if (Addr(procAddress) >= num2 && Addr(procAddress) < num3)
			{
				text = module.ModuleName;
				num = num2;
				break;
			}
		}
		if (text == null)
		{
			throw new Exception("Módulo local da API não encontrado.");
		}
		foreach (ProcessModule module2 in target.Modules)
		{
			if (string.Equals(module2.ModuleName, text, StringComparison.OrdinalIgnoreCase))
			{
				return Addr(module2.BaseAddress) + Addr(procAddress) - num;
			}
		}
		throw new Exception("Módulo remoto da API não encontrado: " + text);
	}

	public NpcBridge(int pid)
	{
		if (IntPtr.Size != 4)
		{
			throw new Exception("Ponte exige processo x86.");
		}
		try
		{
			using Process process = Process.GetProcessById(pid);
			using (FileStream inputStream = File.OpenRead(process.MainModule.FileName))
			{
				using SHA256 sHA = SHA256.Create();
				string text = BitConverter.ToString(sHA.ComputeHash(inputStream)).Replace("-", "").ToLowerInvariant();
				if (text != "34fca82cafa1f38825228626aa3e8f98c56274c095cb46e317b2d665aaf69684")
				{
					throw new Exception("Cliente diferente do perfil analisado.");
				}
			}
			window = process.MainWindowHandle;
			if (window == IntPtr.Zero)
			{
				throw new Exception("Janela do jogo não encontrada.");
			}
			UiThread = GetWindowThreadProcessId(window, out var pid2);
			if (pid2 != pid)
			{
				throw new Exception("Janela não pertence ao cliente.");
			}
			this.process = OpenProcess(1082u, inherit: false, pid);
			if (this.process == IntPtr.Zero)
			{
				throw Fail("Abrir cliente");
			}
			Module = Addr(process.MainModule.BaseAddress);
			uint num = Read(Module + 10192652);
			uint num2 = Read(num + 28);
			Host = Read(num2 + 52);
			CharacterId = Read(Host + 1264);
			uint num3 = Read(num + 32);
			if (CharacterId == 0 || num3 < 65536)
			{
				throw new Exception("Personagem não está pronto.");
			}
			setLong = Api(process, "user32.dll", "SetWindowLongA");
			getLong = Api(process, "user32.dll", "GetWindowLongA");
			callOld = Api(process, "user32.dll", "CallWindowProcA");
			getThread = Api(process, "kernel32.dll", "GetCurrentThreadId");
			allocation = VirtualAllocEx(this.process, IntPtr.Zero, new UIntPtr(8192u), 12288u, 4u);
			if (allocation == IntPtr.Zero)
			{
				throw Fail("Alocar ponte");
			}
			data = Addr(allocation);
			hook = data + 4096;
			installer = hook + 1024;
			byte[] array = new byte[64];
			Buffer.BlockCopy(BitConverter.GetBytes(num3), 0, array, 4, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(Module + 5325376), 0, array, 8, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(Module + 7318256), 0, array, 28, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(Module + 2239424), 0, array, 60, 4);
			Write(data, array);
			Write(data + 96, Encoding.ASCII.GetBytes("IDCANCEL\0"));
			byte[] array2 = BuildHook();
			if (array2.Length >= 1024)
			{
				throw new Exception("Ponte NPC excedeu limite do instalador");
			}
			Write(hook, array2);
			Write(installer, BuildInstaller());
			if (!VirtualProtectEx(this.process, Pointer(hook), new UIntPtr(4096u), 32u, out var _))
			{
				throw Fail("Proteger código");
			}
			FlushInstructionCache(this.process, Pointer(hook), new UIntPtr(4096u));
			IntPtr intPtr = CreateRemoteThread(this.process, IntPtr.Zero, UIntPtr.Zero, Pointer(installer), IntPtr.Zero, 0u, out var _);
			if (intPtr == IntPtr.Zero)
			{
				throw Fail("Instalar ponte");
			}
			try
			{
				if (WaitForSingleObject(intPtr, 3000u) != 0)
				{
					retain = true;
					throw new Exception("Instalação sem resposta. Memória preservada para não invalidar código em uso.");
				}
				GetExitCodeThread(intPtr, out var code);
				if (code != hook || Read(data) == 0)
				{
					throw new Exception("Subclasse interna não foi instalada.");
				}
				installed = true;
			}
			finally
			{
				CloseHandle(intPtr);
			}
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	public NpcBridge(IntPtr testWindow, uint context, uint function, uint closeFunction = 0u, uint closeVtable = 0u, uint npcCancelFunction = 0u)
	{
		testCloseVtable = closeVtable;
		try
		{
			UiThread = GetWindowThreadProcessId(testWindow, out var pid);
			if (pid != Process.GetCurrentProcess().Id)
			{
				throw new Exception("Autoteste só aceita o próprio processo.");
			}
			window = testWindow;
			using Process target = Process.GetProcessById((int)pid);
			process = OpenProcess(1082u, inherit: false, (int)pid);
			if (process == IntPtr.Zero)
			{
				throw Fail("Abrir autoteste");
			}
			setLong = Api(target, "user32.dll", "SetWindowLongA");
			getLong = Api(target, "user32.dll", "GetWindowLongA");
			callOld = Api(target, "user32.dll", "CallWindowProcA");
			getThread = Api(target, "kernel32.dll", "GetCurrentThreadId");
			allocation = VirtualAllocEx(process, IntPtr.Zero, new UIntPtr(8192u), 12288u, 4u);
			if (allocation == IntPtr.Zero)
			{
				throw Fail("Alocar autoteste");
			}
			data = Addr(allocation);
			hook = data + 4096;
			installer = hook + 1024;
			byte[] array = new byte[64];
			Buffer.BlockCopy(BitConverter.GetBytes(context), 0, array, 4, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(function), 0, array, 8, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(closeFunction), 0, array, 28, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(closeVtable), 0, array, 56, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(npcCancelFunction), 0, array, 60, 4);
			Write(data, array);
			Write(data + 96, Encoding.ASCII.GetBytes("IDCANCEL\0"));
			byte[] array2 = BuildHook();
			if (array2.Length >= 1024)
			{
				throw new Exception("Ponte NPC excedeu limite do instalador");
			}
			Write(hook, array2);
			Write(installer, BuildInstaller());
			if (!VirtualProtectEx(process, Pointer(hook), new UIntPtr(4096u), 32u, out var _))
			{
				throw Fail("Código do autoteste");
			}
			FlushInstructionCache(process, Pointer(hook), new UIntPtr(4096u));
			IntPtr intPtr = CreateRemoteThread(process, IntPtr.Zero, UIntPtr.Zero, Pointer(installer), IntPtr.Zero, 0u, out var _);
			if (intPtr == IntPtr.Zero)
			{
				throw Fail("Thread do autoteste");
			}
			try
			{
				if (WaitForSingleObject(intPtr, 3000u) != 0)
				{
					retain = true;
					throw new Exception("Tempo de instalação esgotado.");
				}
				GetExitCodeThread(intPtr, out var code);
				if (code != hook || Read(data) == 0)
				{
					throw new Exception("Instalação do autoteste falhou.");
				}
				installed = true;
			}
			finally
			{
				CloseHandle(intPtr);
			}
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	private byte[] BuildInstaller()
	{
		Code code = new Code();
		code.Push(4294967292u);
		code.Push(Addr(window));
		code.Call(getLong);
		code.Bytes(163);
		code.U(data);
		code.Push(hook);
		code.Push(4294967292u);
		code.Push(Addr(window));
		code.Call(setLong);
		code.Bytes(133, 192);
		int at = code.Jump(132);
		code.Bytes(184);
		code.U(hook);
		code.Bytes(194, 4, 0);
		code.Fix(at);
		code.Bytes(49, 192, 194, 4, 0);
		return code.B.ToArray();
	}

	private byte[] BuildHook()
	{
		Code code = new Code();
		code.Bytes(85, 139, 236, 129, 125, 12);
		code.U(MessageId);
		int at = code.Jump(133);
		code.Bytes(129, 125, 20);
		code.U(1096176211u);
		int at2 = code.Jump(132);
		code.Bytes(129, 125, 20);
		code.U(1096173132u);
		int at3 = code.Jump(133);
		code.Bytes(96, 156);
		code.Call(getThread);
		code.Bytes(163);
		code.U(data + 12);
		code.Bytes(131, 61);
		code.U(data + 24);
		code.Bytes(1);
		int at4 = code.Jump(132);
		code.Bytes(139, 13);
		code.U(data + 4);
		code.Bytes(255, 53);
		code.U(data + 20);
		code.Push(data + 32);
		code.Bytes(255, 21);
		code.U(data + 8);
		code.Bytes(163);
		code.U(data + 16);
		code.Bytes(233);
		int count = code.B.Count;
		code.U(0u);
		code.Fix(at4);
		code.Bytes(139, 53);
		code.U(data + 20);
		code.Bytes(191);
		code.U(data + 128);
		code.Bytes(49, 219);
		int count2 = code.B.Count;
		code.Bytes(133, 246);
		int at5 = code.Jump(132);
		code.Bytes(139, 15, 139, 1);
		List<int> list = new List<int>();
		uint[] array = new uint[4] { 9293192u, 9312760u, 9312916u, 9313072u };
		foreach (uint num in array)
		{
			code.Bytes(61);
			code.U(Module + num);
			list.Add(code.Jump(132));
		}
		code.Bytes(139, 21);
		code.U(data + 56);
		code.Bytes(133, 210);
		int at6 = code.Jump(132);
		code.Bytes(59, 194);
		int at7 = code.Jump(133);
		foreach (int item in list)
		{
			code.Fix(item);
		}
		code.Bytes(61);
		code.U(Module + 9293192);
		int at8 = code.Jump(133);
		code.Bytes(131, 61);
		code.U(data + 60);
		byte[] b = new byte[1];
		code.Bytes(b);
		int at9 = code.Jump(132);
		code.Push(data + 96);
		code.Bytes(255, 21);
		code.U(data + 60);
		code.Bytes(233);
		int count3 = code.B.Count;
		code.U(0u);
		code.Fix(at8);
		code.Bytes(128, 185);
		code.U(144u);
		b = new byte[1];
		code.Bytes(b);
		int at10 = code.Jump(132);
		code.Push(1u);
		code.Push(1u);
		code.Push(0u);
		code.Push(0u);
		code.Bytes(255, 21);
		code.U(data + 28);
		code.Fix(count3);
		code.Bytes(67);
		code.Fix(at9);
		code.Fix(at6);
		code.Fix(at7);
		code.Fix(at10);
		code.Bytes(131, 199, 4, 78, 233);
		code.U((uint)(count2 - code.B.Count - 4));
		code.Fix(at5);
		code.Bytes(137, 29);
		code.U(data + 16);
		code.Fix(count);
		code.Bytes(157, 97, 184);
		code.U(1096175435u);
		code.Bytes(93, 194, 16, 0);
		code.Fix(at2);
		code.Push(4294967292u);
		code.Bytes(255, 117, 8);
		code.Call(getLong);
		code.Bytes(61);
		code.U(hook);
		int at11 = code.Jump(133);
		code.Bytes(255, 53);
		code.U(data);
		code.Push(4294967292u);
		code.Bytes(255, 117, 8);
		code.Call(setLong);
		code.Bytes(184);
		code.U(1096176203u);
		code.Bytes(93, 194, 16, 0);
		code.Fix(at11);
		code.Bytes(49, 192, 93, 194, 16, 0);
		code.Fix(at);
		code.Fix(at3);
		code.Bytes(255, 117, 20, 255, 117, 16, 255, 117, 12, 255, 117, 8, 255, 53);
		code.U(data);
		code.Call(callOld);
		code.Bytes(93, 194, 16, 0);
		return code.B.ToArray();
	}

	public void SendNpc(byte[] packet)
	{
		if (!installed || packet == null)
		{
			throw new Exception("Comando NPC invalido");
		}
		bool flag = packet.Length == 6 && BitConverter.ToUInt16(packet, 0) == 35 && BitConverter.ToUInt32(packet, 2) != 0;
		bool flag2 = packet.Length == 22 && BitConverter.ToUInt16(packet, 0) == 37 && BitConverter.ToUInt32(packet, 2) == 7 && BitConverter.ToUInt32(packet, 6) == 12 && BitConverter.ToUInt32(packet, 10) != 0 && BitConverter.ToUInt32(packet, 10) <= 65535;
		bool flag3 = packet.Length == 18 && BitConverter.ToUInt16(packet, 0) == 37 && BitConverter.ToUInt32(packet, 2) == 6 && BitConverter.ToUInt32(packet, 6) == 8 && BitConverter.ToUInt32(packet, 10) != 0 && BitConverter.ToUInt32(packet, 10) <= 65535 && BitConverter.ToUInt32(packet, 14) == 0;
		if (!flag && !flag2 && !flag3)
		{
			throw new Exception("Comando fora do formato validado de NPC/missão");
		}
		Write(data + 24, BitConverter.GetBytes(0));
		Write(data + 32, packet);
		Write(data + 20, BitConverter.GetBytes(packet.Length));
		if (SendMessageTimeout(window, MessageId, UIntPtr.Zero, Pointer(1096173132u), 3u, 2000u, out var result) == IntPtr.Zero)
		{
			retain = true;
			throw Fail("NPC sem resposta");
		}
		ExecutedThread = Read(data + 12);
		LastSendResult = Read(data + 16) & 0xFF;
		if (result.ToUInt32() != 1096175435 || ExecutedThread != UiThread)
		{
			throw new Exception("Thread do NPC nao confirmada");
		}
	}

	public uint CloseDialogs(uint[] dialogs)
	{
		if (!installed || dialogs == null || dialogs.Length > 16)
		{
			throw new Exception("Lista de janelas invalida");
		}
		if (Read(data + 28) == 0)
		{
			throw new Exception("Funcao de fechar janelas indisponivel");
		}
		List<uint> list = new List<uint>();
		foreach (uint num in dialogs)
		{
			uint num2 = Read(num);
			if (num2 != Module + 9293192 && num2 != Module + 9312760 && num2 != Module + 9312916 && num2 != Module + 9313072 && (Module != 0 || testCloseVtable == 0 || num2 != testCloseVtable))
			{
				throw new Exception("Janela fora do perfil de NPC/missao");
			}
			if (!list.Contains(num))
			{
				list.Add(num);
			}
		}
		if (list.Count == 0)
		{
			return 0u;
		}
		byte[] array = new byte[list.Count * 4];
		for (int j = 0; j < list.Count; j++)
		{
			Buffer.BlockCopy(BitConverter.GetBytes(list[j]), 0, array, j * 4, 4);
		}
		Write(data + 128, array);
		Write(data + 20, BitConverter.GetBytes(list.Count));
		Write(data + 24, BitConverter.GetBytes(1));
		if (SendMessageTimeout(window, MessageId, UIntPtr.Zero, Pointer(1096173132u), 3u, 2000u, out var result) == IntPtr.Zero)
		{
			retain = true;
			throw Fail("Fechamento de NPC sem resposta");
		}
		ExecutedThread = Read(data + 12);
		if (result.ToUInt32() != 1096175435 || ExecutedThread != UiThread)
		{
			throw new Exception("Fechamento fora da thread da janela");
		}
		LastSendResult = Read(data + 16);
		return LastSendResult;
	}

	public void Dispose()
	{
		if (process == IntPtr.Zero)
		{
			return;
		}
		if (installed)
		{
			if (SendMessageTimeout(window, MessageId, UIntPtr.Zero, Pointer(1096176211u), 3u, 2000u, out var result) == IntPtr.Zero || result.ToUInt32() != 1096176203)
			{
				retain = true;
			}
			else
			{
				installed = false;
				RestoreSucceeded = true;
			}
		}
		if (allocation != IntPtr.Zero && !retain && !installed)
		{
			VirtualFreeEx(process, allocation, UIntPtr.Zero, 32768u);
		}
		CloseHandle(process);
		process = IntPtr.Zero;
	}
}
