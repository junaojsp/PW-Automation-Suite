using System;
using System.Drawing;

internal static class Rubi
{
	public static readonly Color Background = Color.FromArgb(12, 18, 30);

	public static readonly Color Panel = Color.FromArgb(23, 36, 54);

	public static readonly Color Field = Color.FromArgb(29, 47, 68);

	public static readonly Color Line = Color.FromArgb(45, 78, 104);

	public static readonly Color Text = Color.FromArgb(225, 239, 249);

	public static readonly Color Muted = Color.FromArgb(153, 179, 201);

	public static readonly Color Accent = Color.FromArgb(65, 214, 246);

	public static readonly Color OnAccent = Color.FromArgb(10, 30, 45);

	public static Color Shade(Color c, int amount)
	{
		Func<int, int> func = (int v) => Math.Max(0, Math.Min(255, v + amount));
		return Color.FromArgb(c.A, func(c.R), func(c.G), func(c.B));
	}
}
