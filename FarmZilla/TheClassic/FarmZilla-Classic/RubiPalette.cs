using System;using System.Drawing;
static class Rubi {
 public static readonly Color Background=Color.FromArgb(8,11,15),Panel=Color.FromArgb(17,22,29),Field=Color.FromArgb(23,31,41),Line=Color.FromArgb(48,68,84),Text=Color.FromArgb(230,243,250),Muted=Color.FromArgb(169,191,207),Accent=Color.FromArgb(46,169,235),OnAccent=Color.FromArgb(5,15,23);
 public static Color Shade(Color c,int amount){Func<int,int> f=v=>Math.Max(0,Math.Min(255,v+amount));return Color.FromArgb(c.A,f(c.R),f(c.G),f(c.B));}
}
