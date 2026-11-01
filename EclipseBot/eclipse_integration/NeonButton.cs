using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal class NeonButton : Button
{
	public bool Primary;

	private bool hover;

	private bool pressed;

	public NeonButton()
	{
		FlatStyle = FlatStyle.Flat;
		FlatAppearance.BorderSize = 0;
		ForeColor = Rubi.Text;
		BackColor = Rubi.Panel;
		Font = new Font("Segoe UI", 8f);
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		EventHandler value = (object s, EventArgs e) =>
		{
			hover = true;
			Invalidate();
		};
		MouseEnter += value;
		MouseLeave += (object s, EventArgs e) =>
		{
			hover = (pressed = false);
			Invalidate();
		};
		MouseDown += (object s, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				pressed = true;
				Invalidate();
			}
		};
		MouseUp += (object s, MouseEventArgs e) =>
		{
			pressed = false;
			Invalidate();
		};
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		Depth.ParentBackground(this, graphics);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		int num = ((!pressed) ? 2 : 0);
		Rectangle rectangle = new Rectangle(0, 2 - num, Width - 1, Height - 3);
		int radius = Math.Min(6, (Height - 4) / 2);
		if (num > 0)
		{
			Depth.Shadow(graphics, rectangle, radius, num);
		}
		Color color;
		if (Primary)
		{
			color = Rubi.Accent;
		}
		else
		{
			color = (hover ? Rubi.Field : BackColor);
		}
		if (Primary && hover)
		{
			color = Rubi.Shade(Rubi.Accent, 12);
		}
		if (pressed)
		{
			Depth.Sunken(graphics, rectangle, radius, color);
		}
		else
		{
			Depth.Raised(graphics, rectangle, radius, Rubi.Shade(color, Primary ? 22 : 16), Rubi.Shade(color, Primary ? (-22) : (-8)), Primary ? Rubi.Shade(Rubi.Accent, -35) : Rubi.Line);
		}
		Graphics dc = graphics;
		string text = Text;
		Font font = Font;
		Rectangle bounds = rectangle;
		Color foreColor;
		if (Enabled)
		{
			foreColor = (Primary ? Rubi.OnAccent : ForeColor);
		}
		else
		{
			foreColor = Rubi.Muted;
		}
		TextRenderer.DrawText(dc, text, font, bounds, foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}
}
