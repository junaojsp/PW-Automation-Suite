using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

public class TeleportCapture : IDisposable
{
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate uint TestFunction(IntPtr packet, uint size);

	private class Emitter
	{
		public List<byte> B = new List<byte>();

		public void Add(params byte[] b)
		{
			B.AddRange(b);
		}

		public void U(uint n)
		{
			B.AddRange(BitConverter.GetBytes(n));
		}

		public int J()
		{
			Add(15, 133);
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

	public sealed class CapturedCommand
	{
		public uint Sequence;

		public byte[] Packet;

		public int FullLength;
	}

	private IntPtr process;

	private int pid;

	private uint target;

	private uint data;

	private uint code;

	private bool installed;

	private bool self;

	private bool retained;

	private static readonly byte[] Original = new byte[5] { 85, 139, 236, 106, 255 };

	private readonly bool captureAll;

	private readonly HashSet<uint> delivered = new HashSet<uint>();

	public bool RecoveredOrphan { get; private set; }

	public bool CaptureAll => captureAll;

	public bool Restored => !installed;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess(uint a, bool inherit, int pid);

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

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(IntPtr h);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenThread(uint access, bool inherit, uint tid);

	[DllImport("kernel32.dll")]
	private static extern uint SuspendThread(IntPtr h);

	[DllImport("kernel32.dll")]
	private static extern uint ResumeThread(IntPtr h);

	[DllImport("kernel32.dll")]
	private static extern bool GetThreadContext(IntPtr h, IntPtr context);

	private static IntPtr P(uint v)
	{
		return new IntPtr((int)v);
	}

	private static uint A(IntPtr p)
	{
		return (uint)p.ToInt32();
	}

	public byte[] Read(uint at, int size)
	{
		byte[] array = new byte[size];
		if (!ReadProcessMemory(process, P(at), array, new UIntPtr((uint)size), out var got) || got.ToUInt32() != size)
		{
			throw new Exception("Leitura falhou: " + at.ToString("X8"));
		}
		return array;
	}

	private void Write(uint at, byte[] b)
	{
		if (!WriteProcessMemory(process, P(at), b, new UIntPtr((uint)b.Length), out var got) || got.ToUInt32() != b.Length)
		{
			throw new Exception("Gravacao incompleta em " + at.ToString("X8"));
		}
	}

	private static bool Same(byte[] a, byte[] b)
	{
		if (a.Length != b.Length)
		{
			return false;
		}
		for (int i = 0; i < a.Length; i++)
		{
			if (a[i] != b[i])
			{
				return false;
			}
		}
		return true;
	}

	private byte[] Build()
	{
		Emitter emitter = new Emitter();
		List<int> list = new List<int>();
		List<int> list2 = new List<int>();
		emitter.Add(156, 96, 240, 255, 5);
		emitter.U(data + 4);
		emitter.Add(139, 76, 36, 44, 139, 116, 36, 40);
		if (captureAll)
		{
			emitter.Add(131, 249, 2);
			emitter.Add(15, 130);
			list.Add(emitter.B.Count);
			emitter.U(0u);
			emitter.Add(233);
			list2.Add(emitter.B.Count);
			emitter.U(0u);
		}
		emitter.Add(131, 249, 6);
		int at = emitter.J();
		emitter.Add(102, 131, 62, 35);
		list.Add(emitter.J());
		emitter.Add(233);
		list2.Add(emitter.B.Count);
		emitter.U(0u);
		emitter.Fix(at);
		emitter.Add(131, 249, 14);
		int at2 = emitter.J();
		emitter.Add(129, 62);
		emitter.U(16777325u);
		list.Add(emitter.J());
		emitter.Add(233);
		list2.Add(emitter.B.Count);
		emitter.U(0u);
		emitter.Fix(at2);
		emitter.Add(131, 249, 18);
		int at3 = emitter.J();
		emitter.Add(102, 131, 62, 37);
		List<int> list3 = new List<int>();
		list3.Add(emitter.J());
		emitter.Add(131, 126, 2, 6);
		list.Add(emitter.J());
		emitter.Add(131, 126, 6, 8);
		list.Add(emitter.J());
		emitter.Add(233);
		list2.Add(emitter.B.Count);
		emitter.U(0u);
		emitter.Fix(at3);
		emitter.Add(131, 249, 22);
		int at4 = emitter.J();
		emitter.Add(102, 131, 62, 37);
		list3.Add(emitter.J());
		emitter.Add(131, 126, 2, 7);
		list.Add(emitter.J());
		emitter.Add(131, 126, 6, 12);
		list.Add(emitter.J());
		emitter.Add(233);
		list2.Add(emitter.B.Count);
		emitter.U(0u);
		emitter.Fix(at4);
		foreach (int item in list3)
		{
			emitter.Fix(item);
		}
		emitter.Add(131, 249, 9);
		int at5 = emitter.J();
		emitter.Add(102, 131, 62, 49);
		list.Add(emitter.J());
		emitter.Add(233);
		list2.Add(emitter.B.Count);
		emitter.U(0u);
		emitter.Fix(at5);
		emitter.Add(131, 249, 96);
		emitter.Add(15, 135);
		list.Add(emitter.B.Count);
		emitter.U(0u);
		emitter.Add(131, 249, 10);
		emitter.Add(15, 130);
		list.Add(emitter.B.Count);
		emitter.U(0u);
		emitter.Add(129, 62);
		emitter.U(16777325u);
		emitter.Add(15, 132);
		list2.Add(emitter.B.Count);
		emitter.U(0u);
		emitter.Add(131, 249, 10);
		list.Add(emitter.J());
		emitter.Add(129, 62);
		emitter.U(16777256u);
		list.Add(emitter.J());
		emitter.Add(129, 126, 6);
		emitter.U(52256u);
		list.Add(emitter.J());
		foreach (int item2 in list2)
		{
			emitter.Fix(item2);
		}
		emitter.Add(184);
		emitter.U(1u);
		emitter.Add(240, 15, 193, 5);
		emitter.U(data);
		emitter.Add(139, 208, 66, 131, 224, 15, 193, 224, 7, 5);
		emitter.U(data + 256);
		emitter.Add(139, 248, 199, 7);
		emitter.U(0u);
		emitter.Add(137, 87, 4, 137, 79, 8, 139, 68, 36, 24, 137, 71, 12, 139, 223, 131, 199, 32, 131, 249, 96, 118, 5, 185);
		emitter.U(96u);
		emitter.Add(252, 243, 164, 137, 19);
		foreach (int item3 in list)
		{
			emitter.Fix(item3);
		}
		emitter.Add(97, 157);
		emitter.Add(Original);
		emitter.Add(233);
		emitter.U(target + 5 - (uint)((int)code + emitter.B.Count + 4));
		return emitter.B.ToArray();
	}

	private bool Quiescent(Action action, bool checkAllocation)
	{
		if (self)
		{
			action();
			return true;
		}
		List<IntPtr> list = new List<IntPtr>();
		IntPtr intPtr = Marshal.AllocHGlobal(736);
		try
		{
			using (Process process = Process.GetProcessById(pid))
			{
				foreach (ProcessThread thread in process.Threads)
				{
					IntPtr intPtr2 = OpenThread(26u, inherit: false, (uint)thread.Id);
					if (intPtr2 == IntPtr.Zero)
					{
						throw new Exception("Nao foi possivel abrir thread " + thread.Id);
					}
					list.Add(intPtr2);
				}
			}
			int num = 0;
			try
			{
				foreach (IntPtr item in list)
				{
					if (SuspendThread(item) == uint.MaxValue)
					{
						throw new Exception("Falha suspendendo thread");
					}
					num++;
				}
				foreach (IntPtr item2 in list)
				{
					for (int i = 0; i < 716; i++)
					{
						Marshal.WriteByte(intPtr, i, 0);
					}
					Marshal.WriteInt32(intPtr, 0, 65537);
					if (!GetThreadContext(item2, intPtr))
					{
						throw new Exception("Falha lendo contexto de thread");
					}
					uint num2 = (uint)Marshal.ReadInt32(intPtr, 184);
					if (num2 >= target && num2 < target + 5)
					{
						return false;
					}
					if (checkAllocation && num2 >= code && num2 < code + 4096)
					{
						return false;
					}
				}
				action();
				return true;
			}
			finally
			{
				for (int i = num - 1; i >= 0; i--)
				{
					ResumeThread(list[i]);
				}
			}
		}
		finally
		{
			foreach (IntPtr item3 in list)
			{
				CloseHandle(item3);
			}
			Marshal.FreeHGlobal(intPtr);
		}
	}

	private void Patch(byte[] expected, byte[] replacement)
	{
		for (int i = 0; i < 100; i++)
		{
			if (Quiescent(() =>
			{
				if (!Same(Read(target, 5), expected))
				{
					throw new Exception("Codigo foi alterado por outro componente; operacao cancelada.");
				}
				if (!VirtualProtectEx(process, P(target), new UIntPtr(5u), 64u, out var old))
				{
					throw new Exception("Falha protegendo trecho");
				}
				try
				{
					Write(target, replacement);
					FlushInstructionCache(process, P(target), new UIntPtr(5u));
					if (!Same(Read(target, 5), replacement))
					{
						throw new Exception("Verificacao do trecho falhou");
					}
				}
				finally
				{
					VirtualProtectEx(process, P(target), new UIntPtr(5u), old, out var _);
				}
			}, checkAllocation: false))
			{
				return;
			}
			Thread.Sleep(10);
		}
		throw new Exception("Thread ocupando trecho; nao foi possivel alterar com seguranca.");
	}

	private bool OrphanCapture(byte[] current)
	{
		if (current[0] != 233)
		{
			return false;
		}
		if (!self)
		{
			Process currentProcess = Process.GetCurrentProcess();
			Process[] processesByName = Process.GetProcessesByName(currentProcess.ProcessName);
			foreach (Process process in processesByName)
			{
				using (process)
				{
					if (process.Id != currentProcess.Id)
					{
						return false;
					}
				}
			}
		}
		uint num = target + 5 + BitConverter.ToUInt32(current, 1);
		byte[] array;
		try
		{
			array = Read(num, 9);
		}
		catch
		{
			return false;
		}
		return array[0] == 156 && array[1] == 96 && array[2] == 240 && array[3] == byte.MaxValue && array[4] == 5 && BitConverter.ToUInt32(array, 5) == num - 4096 + 4;
	}

	private byte[] Jump()
	{
		byte[] array = new byte[5] { 233, 0, 0, 0, 0 };
		Buffer.BlockCopy(BitConverter.GetBytes(code - target - 5), 0, array, 1, 4);
		return array;
	}

	public TeleportCapture(int targetPid, uint testAddress)
		: this(targetPid, testAddress, diagnosticAll: false)
	{
	}

	public TeleportCapture(int targetPid, uint testAddress, bool diagnosticAll)
	{
		pid = targetPid;
		self = testAddress != 0;
		captureAll = diagnosticAll;
		try
		{
			using Process process = Process.GetProcessById(pid);
			if (self && pid != Process.GetCurrentProcess().Id)
			{
				throw new Exception("Autoteste exige proprio processo");
			}
			if (!self)
			{
				using FileStream inputStream = File.OpenRead(process.MainModule.FileName);
				using SHA256 sHA = SHA256.Create();
				if (BitConverter.ToString(sHA.ComputeHash(inputStream)).Replace("-", "").ToLowerInvariant() != "34fca82cafa1f38825228626aa3e8f98c56274c095cb46e317b2d665aaf69684")
				{
					throw new Exception("Cliente diferente do perfil");
				}
			}
			this.process = OpenProcess(1082u, inherit: false, pid);
			if (this.process == IntPtr.Zero)
			{
				throw new Exception("Acesso ao processo negado");
			}
			target = (self ? testAddress : (A(process.MainModule.BaseAddress) + 5325376));
			byte[] array = Read(target, 5);
			if (!Same(array, Original))
			{
				if (!OrphanCapture(array))
				{
					throw new Exception("Prologo inesperado; nao instalado");
				}
				Patch(array, Original);
				RecoveredOrphan = true;
			}
			data = A(VirtualAllocEx(this.process, IntPtr.Zero, new UIntPtr(8192u), 12288u, 4u));
			if (data == 0)
			{
				throw new Exception("Falha de alocacao");
			}
			code = data + 4096;
			Write(code, Build());
			if (!VirtualProtectEx(this.process, P(code), new UIntPtr(4096u), 32u, out var _))
			{
				throw new Exception("Falha protegendo capturador");
			}
			FlushInstructionCache(this.process, P(code), new UIntPtr(4096u));
			retained = true;
			Patch(Original, Jump());
			installed = true;
			retained = false;
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	public void Dispose()
	{
		if (this.process == IntPtr.Zero)
		{
			return;
		}
		try
		{
			if (installed)
			{
				Patch(Jump(), Original);
				installed = false;
				bool flag = false;
				for (int i = 0; i < 100; i++)
				{
					if (flag)
					{
						break;
					}
					flag = Quiescent(() =>
					{
						if (!VirtualFreeEx(this.process, P(data), UIntPtr.Zero, 32768u))
						{
							throw new Exception("Falha liberando captura");
						}
						data = 0u;
					}, checkAllocation: true);
					if (!flag)
					{
						Thread.Sleep(10);
					}
				}
				retained = !flag;
			}
			if (data != 0 && !installed && !retained)
			{
				VirtualFreeEx(this.process, P(data), UIntPtr.Zero, 32768u);
				data = 0u;
			}
		}
		catch
		{
			retained = true;
			try
			{
				using Process process = Process.GetProcessById(pid);
				if (process.HasExited)
				{
					installed = false;
				}
			}
			catch (ArgumentException)
			{
				installed = false;
			}
			if (installed)
			{
				throw;
			}
		}
		finally
		{
			if (!installed)
			{
				CloseHandle(this.process);
				this.process = IntPtr.Zero;
			}
		}
	}

	public List<CapturedCommand> Poll()
	{
		List<CapturedCommand> list = new List<CapturedCommand>();
		byte[] value = Read(data, 4096);
		for (int i = 0; i < 16; i++)
		{
			int num = 256 + i * 128;
			uint num2 = BitConverter.ToUInt32(value, num);
			if (num2 == 0 || delivered.Contains(num2))
			{
				continue;
			}
			byte[] array = Read(data + (uint)num, 128);
			if (BitConverter.ToUInt32(array, 0) != num2 || BitConverter.ToUInt32(array, 4) != num2)
			{
				continue;
			}
			int num3 = (int)BitConverter.ToUInt32(array, 8);
			bool num4;
			if (!captureAll)
			{
				if (num3 == 6 || num3 == 9 || num3 == 18 || num3 == 22)
				{
					goto IL_00f0;
				}
				if (num3 < 10)
				{
					goto IL_00e5;
				}
				num4 = num3 > 96;
			}
			else
			{
				num4 = num3 < 2;
			}
			if (num4)
			{
				goto IL_00e5;
			}
			goto IL_00f0;
			IL_00e5:
			throw new Exception("Comando capturado com tamanho invalido");
			IL_00f0:
			byte[] array2 = new byte[Math.Min(num3, 96)];
			Buffer.BlockCopy(array, 32, array2, 0, array2.Length);
			delivered.Add(num2);
			list.Add(new CapturedCommand
			{
				Sequence = num2,
				Packet = array2,
				FullLength = num3
			});
		}
		list.Sort((CapturedCommand a, CapturedCommand b) => a.Sequence.CompareTo(b.Sequence));
		return list;
	}
}
