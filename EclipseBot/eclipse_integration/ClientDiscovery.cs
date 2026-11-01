using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

internal static class ClientDiscovery
{
	private const string Known = "34fca82cafa1f38825228626aa3e8f98c56274c095cb46e317b2d665aaf69684";

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int size);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(IntPtr process);

	internal static ClientCandidate[] Find(Action<string> log)
	{
		Dictionary<int, ClientCandidate> dictionary = new Dictionary<int, ClientCandidate>();
		Process[] processesByName = Process.GetProcessesByName("ElementClient");
		foreach (Process process in processesByName)
		{
			using (process)
			{
				dictionary[process.Id] = new ClientCandidate
				{
					Pid = process.Id,
					ByName = true
				};
			}
		}
		IntPtr intPtr = IntPtr.Zero;
		HashSet<IntPtr> hashSet = new HashSet<IntPtr>();
		while ((intPtr = FindWindowEx(IntPtr.Zero, intPtr, "ElementClient Window", null)) != IntPtr.Zero && hashSet.Add(intPtr))
		{
			GetWindowThreadProcessId(intPtr, out var pid);
			if (pid != 0)
			{
				if (!dictionary.TryGetValue((int)pid, out var value))
				{
					value = (dictionary[(int)pid] = new ClientCandidate
					{
						Pid = (int)pid
					});
				}
				value.ByClass = true;
				StringBuilder stringBuilder = new StringBuilder(256);
				GetWindowText(intPtr, stringBuilder, stringBuilder.Capacity);
				value.WindowTitle = stringBuilder.ToString();
			}
		}
		log("Detecção: " + Enumerable.Count(dictionary.Values, (ClientCandidate c) => c.ByName) + " por nome, " + Enumerable.Count(dictionary.Values, (ClientCandidate c) => c.ByClass) + " por classe de janela; " + dictionary.Count + " cliente(s) distinto(s).");
		return dictionary.Values.OrderBy((ClientCandidate c) => c.Pid).ToArray();
	}

	internal static bool Compatible(int pid, Action<string> log)
	{
		IntPtr intPtr = OpenProcess(4096u, inherit: false, pid);
		if (intPtr == IntPtr.Zero)
		{
			throw new Exception("Consulta negada (Windows " + Marshal.GetLastWin32Error() + ")");
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(32768);
			int size = stringBuilder.Capacity;
			if (!QueryFullProcessImageName(intPtr, 0u, stringBuilder, ref size))
			{
				throw new Exception("Caminho indisponível (Windows " + Marshal.GetLastWin32Error() + ")");
			}
			ushort num2;
			string text;
			using (FileStream fileStream = File.OpenRead(stringBuilder.ToString()))
			{
				using (BinaryReader binaryReader = new BinaryReader(fileStream, Encoding.UTF8, leaveOpen: true))
				{
					if (binaryReader.ReadUInt16() != 23117)
					{
						throw new Exception("Executável não PE");
					}
					fileStream.Position = 60L;
					int num = binaryReader.ReadInt32();
					fileStream.Position = num;
					if (binaryReader.ReadUInt32() != 17744)
					{
						throw new Exception("PE inválido");
					}
					num2 = binaryReader.ReadUInt16();
				}
				fileStream.Position = 0L;
				using SHA256 sHA = SHA256.Create();
				text = BitConverter.ToString(sHA.ComputeHash(fileStream)).Replace("-", "").ToLowerInvariant();
			}
			log("PID " + pid + ": " + Path.GetFileName(stringBuilder.ToString()) + " · arquitetura " + num2.ToString("X4") + " · SHA-256 " + text);
			if (num2 != 332)
			{
				log("PID " + pid + ": encontrado, mas arquitetura incompatível com o perfil x86.");
				return false;
			}
			if (text != "34fca82cafa1f38825228626aa3e8f98c56274c095cb46e317b2d665aaf69684")
			{
				log("PID " + pid + ": encontrado, mas compilação diferente do perfil Avalon validado.");
				return false;
			}
			return true;
		}
		finally
		{
			CloseHandle(intPtr);
		}
	}
}
