using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal class CyanCombo : ComboBox
{
	public Func<object, string> ItemTag;

	public Func<object, int> ItemClass;

	private int ClassOf(object item)
	{
		return (ItemClass == null || item == null) ? (-1) : ItemClass(item);
	}

	public CyanCombo()
	{
		FlatStyle = FlatStyle.Flat;
		BackColor = Rubi.Field;
		ForeColor = Rubi.Text;
		Font = new Font("Segoe UI", 8f);
		DrawMode = DrawMode.OwnerDrawFixed;
		ItemHeight = 21;
		DropDownHeight = 230;
		IntegralHeight = false;
		DrawItemEventHandler value = (object s, DrawItemEventArgs e) =>
		{
			if (e.Index >= 0)
			{
				using (SolidBrush brush = new SolidBrush(((e.State & DrawItemState.Selected) != 0) ? Rubi.Panel : Rubi.Field))
				{
					e.Graphics.FillRectangle(brush, e.Bounds);
				}
				string value2 = ((ItemTag == null) ? null : ItemTag(Items[e.Index]));
				Rectangle bounds = e.Bounds;
				if (!string.IsNullOrEmpty(value2))
				{
					Size size = TextRenderer.MeasureText(value2, Font);
					Rectangle bounds2 = new Rectangle(e.Bounds.Right - size.Width - 6, e.Bounds.Top, size.Width + 4, e.Bounds.Height);
					TextRenderer.DrawText(e.Graphics, value2, Font, bounds2, Rubi.Accent, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
					bounds.Width = Math.Max(10, bounds2.Left - bounds.Left);
				}
				int c = ClassOf(Items[e.Index]);
				if (ClassIcons.Known(c))
				{
					float num = Math.Min(19, e.Bounds.Height - 2);
					ClassIcons.Draw(e.Graphics, new RectangleF(e.Bounds.Left + 3, (float)e.Bounds.Top + ((float)e.Bounds.Height - num) / 2f, num, num), c);
					bounds.X += (int)num + 6;
					bounds.Width -= (int)num + 6;
				}
				TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, bounds, Rubi.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
			}
		};
		DrawItem += value;
	}

	protected override void OnSizeChanged(EventArgs e)
	{
		base.OnSizeChanged(e);
		if (Width > 20 && Height > 20)
		{
			using (GraphicsPath path = Shapes.Round(new Rectangle(0, 0, Width, Height), 5))
			{
				Region region = Region;
				Region = new Region(path);
				region?.Dispose();
			}
		}
	}

	private void DrawField(Graphics g)
	{
		g.SmoothingMode = SmoothingMode.AntiAlias;
		using (SolidBrush brush = new SolidBrush(Rubi.Field))
		{
			g.FillRectangle(brush, 0, 0, Width, Height);
		}
		Depth.Raised(g, new Rectangle(0, 0, Width - 1, Height - 1), 5, Rubi.Shade(Rubi.Field, 12), Rubi.Shade(Rubi.Field, -10), Rubi.Line);
		int c = ClassOf(SelectedItem);
		int num = 7;
		if (ClassIcons.Known(c))
		{
			float num2 = Math.Min(21, Height - 6);
			ClassIcons.Draw(g, new RectangleF(5f, ((float)Height - num2) / 2f, num2, num2), c);
			num = (int)(5f + num2 + 5f);
		}
		TextRenderer.DrawText(g, GetItemText(SelectedItem), Font, new Rectangle(num, 0, Math.Max(1, Width - 22 - num), Height), Rubi.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
		using Pen pen = new Pen(Rubi.Accent, 1.6f);
		int num3 = Height / 2 - 2;
		g.DrawLines(pen, new Point[3]
		{
			new Point(Width - 16, num3),
			new Point(Width - 12, num3 + 4),
			new Point(Width - 8, num3)
		});
	}

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		if (m.Msg == 15)
		{
			using (Graphics g = Graphics.FromHwnd(Handle))
			{
				DrawField(g);
				return;
			}
		}
		if ((m.Msg == 791 || m.Msg == 792) && m.WParam != IntPtr.Zero)
		{
			using (Graphics g = Graphics.FromHdc(m.WParam))
			{
				DrawField(g);
			}
		}
	}
}
