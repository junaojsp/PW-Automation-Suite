using System;
using System.Runtime.InteropServices;

internal static class DragNative
{
	[DllImport("user32.dll")]
	public static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	public static extern IntPtr SendMessage(IntPtr window, int msg, IntPtr wp, IntPtr lp);
}
