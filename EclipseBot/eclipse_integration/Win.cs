using System;
using System.Runtime.InteropServices;

internal static class Win
{
	[DllImport("kernel32.dll", SetLastError = true)]
	public static extern IntPtr OpenProcess(uint rights, bool inherit, int pid);

	[DllImport("kernel32.dll")]
	public static extern bool ReadProcessMemory(IntPtr h, IntPtr at, byte[] b, UIntPtr size, out UIntPtr read);

	[DllImport("kernel32.dll")]
	public static extern bool CloseHandle(IntPtr h);

	[DllImport("user32.dll")]
	public static extern bool RegisterHotKey(IntPtr w, int id, uint mods, uint key);

	[DllImport("user32.dll")]
	public static extern bool UnregisterHotKey(IntPtr w, int id);

	[DllImport("user32.dll")]
	public static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	public static extern uint GetWindowThreadProcessId(IntPtr w, out uint pid);

	[DllImport("user32.dll")]
	public static extern bool SetProcessDPIAware();
}
