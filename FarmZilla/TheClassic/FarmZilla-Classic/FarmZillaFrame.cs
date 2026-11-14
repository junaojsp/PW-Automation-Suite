using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;
// Shared original FarmZilla frame. Client profiles do not own theme/layout painting.
static class FarmZillaFrame {
 public static void Paint(Graphics g,int w,int h,int sideWidth,bool showCards){g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var b=new LinearGradientBrush(new Rectangle(0,0,sideWidth,h),Rubi.Shade(Rubi.Background,-2),Rubi.Shade(Rubi.Background,-12),LinearGradientMode.Vertical))g.FillRectangle(b,0,0,sideWidth,h);
  using(var p=new Pen(Color.FromArgb(110,0,0,0)))g.DrawLine(p,sideWidth-1,0,sideWidth-1,h);using(var shade=new SolidBrush(Color.FromArgb(24,Rubi.Accent)))g.FillRectangle(shade,sideWidth-3,0,2,60);using(var p=new Pen(Color.FromArgb(24,255,255,255)))g.DrawLine(p,sideWidth,0,sideWidth,h);
  var badge=new Rectangle(14,15,28,26);Depth.Shadow(g,badge,6,2);Depth.Raised(g,badge,6,Color.FromArgb(12,19,25),Color.FromArgb(2,5,8),Color.FromArgb(36,133,192));using(var f=new Font("Segoe UI",(float)(9)*global::UiScale.F,FontStyle.Bold))TextRenderer.DrawText(g,"FZ",f,badge,Color.FromArgb(46,169,235),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  using(var p=new Pen(Color.FromArgb(70,0,0,0)))g.DrawLine(p,14,60,sideWidth-14,60);using(var p=new Pen(Color.FromArgb(20,255,255,255)))g.DrawLine(p,14,61,sideWidth-14,61);using(var accent=new Pen(Color.FromArgb(170,Rubi.Accent)))g.DrawLine(accent,14,61,58,61);
  var state=g.Save();g.TranslateTransform(sideWidth,0);g.SetClip(new Rectangle(0,0,w-sideWidth,h));PaintContent(g,w-sideWidth,h,showCards);g.Restore(state);
  using(var pen=new Pen(Rubi.Line))using(var path=Shapes.Round(new Rectangle(0,0,w-1,h-1),12))g.DrawPath(pen,path);using(var pen=new Pen(Color.FromArgb(30,255,255,255)))g.DrawLine(pen,12,1,w-13,1);}
 static void PaintContent(Graphics g,int w,int h,bool showCards){
  using(var b=new LinearGradientBrush(new Rectangle(0,0,w,h),Rubi.Shade(Rubi.Background,6),Rubi.Shade(Rubi.Background,-8),LinearGradientMode.Vertical))g.FillRectangle(b,0,0,w,h);
  using(var b=new LinearGradientBrush(new Rectangle(0,0,w,49),Rubi.Shade(Rubi.Panel,14),Rubi.Shade(Rubi.Panel,-6),LinearGradientMode.Vertical))g.FillRectangle(b,1,1,w-2,47);
  using(var p=new Pen(Color.FromArgb(90,0,0,0)))g.DrawLine(p,1,48,w-2,48);using(var p=new Pen(Color.FromArgb(28,255,255,255)))g.DrawLine(p,1,49,w-2,49);using(var accent=new Pen(Color.FromArgb(145,Rubi.Accent)))g.DrawLine(accent,16,48,102,48);
  var badge=new Rectangle(14,12,28,25);Depth.Shadow(g,badge,5,2);Depth.Raised(g,badge,5,Color.FromArgb(12,19,25),Color.FromArgb(2,5,8),Color.FromArgb(36,133,192));
  if(showCards){Card(g,new Rectangle(14,59,362,70));using(var p=new Pen(Rubi.Accent,3))g.DrawLine(p,16,67,16,121);
  using(var accent=new SolidBrush(Color.FromArgb(115,Rubi.Accent)))g.FillRectangle(accent,22,124,78,2);
  Card(g,new Rectangle(8,137,222,322));Card(g,new Rectangle(233,137,149,312));
  using(var line=new Pen(Color.FromArgb(90,Rubi.Accent)))g.DrawLine(line,17,161,74,161);
  using(var line=new Pen(Color.FromArgb(90,Rubi.Accent)))g.DrawLine(line,242,161,296,161);
  using(var footer=new SolidBrush(Color.FromArgb(95,Rubi.Panel)))g.FillRectangle(footer,9,598,374,50);using(var p=new Pen(Color.FromArgb(80,0,0,0)))g.DrawLine(p,14,568,376,568);using(var p=new Pen(Color.FromArgb(22,255,255,255)))g.DrawLine(p,14,569,376,569);}
}

 static void Card(Graphics g,Rectangle r){Depth.Shadow(g,r,8,2);Depth.Raised(g,r,8,Rubi.Shade(Rubi.Panel,11),Rubi.Shade(Rubi.Panel,-5),Rubi.Line);}
}
