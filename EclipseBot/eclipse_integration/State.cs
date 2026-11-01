using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

internal sealed class State : IDisposable
{
	public int Pid;

	public uint Id;

	public uint Target;

	public bool Following;

	private IntPtr handle;

	public State(int pid)
	{
		Pid = pid;
		try
		{
			using Process process = Process.GetProcessById(pid);
			uint num = InternalBridge.Addr(process.MainModule.BaseAddress);
			handle = Win.OpenProcess(1040u, inherit: false, pid);
			if (handle == IntPtr.Zero)
			{
				throw new Exception("Acesso de leitura negado para PID " + pid);
			}
			uint num2 = Read(num + 10192652);
			uint num3 = Read(num2 + 28);
			uint num4 = Read(num3 + 52);
			Id = Read(num4 + 1264);
			if (Id < 2)
			{
				throw new Exception("Entre no personagem antes de conectar.");
			}
			uint num5 = Read(num4 + 7452);
			uint[] array = new uint[2] { 8u, 24u };
			foreach (uint num6 in array)
			{
				uint num7 = Read(num5 + num6);
				uint num8 = Read(num5 + num6 + 4);
				if (num8 < num7 || num8 - num7 > 128)
				{
					continue;
				}
				for (uint num9 = num7; num9 < num8; num9 += 4)
				{
					uint num10 = Read(num9);
					if (num10 > 65536 && Read(num10) == num + 9337072)
					{
						Following = true;
						Target = Read(num10 + 32);
					}
				}
			}
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	private uint Read(uint at)
	{
		byte[] array = new byte[4];
		if (!Win.ReadProcessMemory(handle, new IntPtr((int)at), array, new UIntPtr(4u), out var read) || read.ToUInt32() != 4)
		{
			throw new Exception("Cliente carregando ou leitura indisponível (PID " + Pid + ").");
		}
		return BitConverter.ToUInt32(array, 0);
	}

	public void Dispose()
	{
		if (handle != IntPtr.Zero)
		{
			Win.CloseHandle(handle);
			handle = IntPtr.Zero;
		}
	}

	public static bool Validate(int pid)
	{
		using Process process = Process.GetProcessById(pid);
		using FileStream inputStream = File.OpenRead(process.MainModule.FileName);
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(inputStream)).Replace("-", "").ToLowerInvariant() == "34fca82cafa1f38825228626aa3e8f98c56274c095cb46e317b2d665aaf69684";
	}
}
