using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal static class Depth
{
	public static void Shadow(Graphics g, Rectangle r, int radius, int depth)
	{
		for (int num = depth; num >= 1; num--)
		{
			using GraphicsPath path = Shapes.Round(new Rectangle(r.X, r.Y + num, r.Width, r.Height), radius);
			using SolidBrush brush = new SolidBrush(Color.FromArgb(55, 0, 0, 0));
			g.FillPath(brush, path);
		}
	}

	public static void Raised(Graphics g, Rectangle r, int radius, Color top, Color bottom, Color border)
	{
		using (GraphicsPath path = Shapes.Round(r, radius))
		{
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(r.X, r.Y - 1, r.Width, r.Height + 2), top, bottom, LinearGradientMode.Vertical))
			{
				g.FillPath(brush, path);
			}
			using Pen borderPen = new Pen(border);
			g.DrawPath(borderPen, path);
		}
		using Pen pen = new Pen(Color.FromArgb(45, 255, 255, 255));
		g.DrawLine(pen, r.X + radius, r.Y + 1, r.Right - radius, r.Y + 1);
	}

	public static void Sunken(Graphics g, Rectangle r, int radius, Color fill)
	{
		using (GraphicsPath path = Shapes.Round(r, radius))
		{
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(r.X, r.Y - 1, r.Width, r.Height + 2), Rubi.Shade(fill, -14), fill, LinearGradientMode.Vertical))
			{
				g.FillPath(brush, path);
			}
			using Pen sunkenBorderPen = new Pen(Rubi.Shade(Rubi.Line, -18));
			g.DrawPath(sunkenBorderPen, path);
		}
		using (Pen shadowLinePen = new Pen(Color.FromArgb(70, 0, 0, 0)))
		{
			g.DrawLine(shadowLinePen, r.X + radius, r.Y + 1, r.Right - radius, r.Y + 1);
		}
		using Pen pen = new Pen(Color.FromArgb(22, 255, 255, 255));
		g.DrawLine(pen, r.X + radius, r.Bottom, r.Right - radius, r.Bottom);
	}

	public static void ParentBackground(Control c, Graphics g)
	{
		if (c.Parent == null)
		{
			g.Clear(Rubi.Background);
			return;
		}
		GraphicsState gstate = g.Save();
		g.TranslateTransform(-c.Left, -c.Top);
		Rectangle clipRect = new Rectangle(c.Left, c.Top, c.Width, c.Height);
		using (PaintEventArgs pe = new PaintEventArgs(g, clipRect))
		{
			InvokeParent(c, pe);
		}
		g.Restore(gstate);
	}

	private static void InvokeParent(Control c, PaintEventArgs pe)
	{
		if (c.Parent is IParentPainter parentPainter)
		{
			parentPainter.PaintBehind(pe);
			return;
		}
		using SolidBrush brush = new SolidBrush(c.Parent.BackColor);
		pe.Graphics.FillRectangle(brush, pe.ClipRectangle);
	}
}
