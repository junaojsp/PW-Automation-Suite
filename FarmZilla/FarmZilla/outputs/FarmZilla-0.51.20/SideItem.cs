using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;
sealed class SideItem : Control {
 bool selected,hover;
 public bool Selected{get{return selected;}set{selected=value;Invalidate();}}
 public SideItem(string text){Text=text;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Font=new Font("Segoe UI Semibold",(float)(9)*global::UiScale.F);Cursor=Cursors.Hand;Height=38;MouseEnter+=(s,e)=>{hover=true;Invalidate();};MouseLeave+=(s,e)=>{hover=false;Invalidate();};}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;Depth.ParentBackground(this,g);g.SmoothingMode=SmoothingMode.AntiAlias;var r=new Rectangle(0,1,Width-1,Height-5);
  Color top=selected?Rubi.Shade(Rubi.Accent,-158):Rubi.Shade(Rubi.Panel,hover?13:4),bottom=selected?Rubi.Shade(Rubi.Accent,-180):Rubi.Shade(Rubi.Panel,-7);
  Depth.Shadow(g,r,6,selected?2:1);Depth.Raised(g,r,6,top,bottom,selected?Rubi.Accent:Rubi.Line);
  if(selected)using(var p=new Pen(Rubi.Accent,3))g.DrawLine(p,r.X+3,r.Y+7,r.X+3,r.Bottom-7);
  var symbol=new Rectangle(r.X+13,r.Y+(r.Height-15)/2,15,15);
  using(var stroke=new Pen(selected?Rubi.Text:Rubi.Muted,1.6f)){
   if(Text=="Espelho"){g.DrawEllipse(stroke,symbol.X+3,symbol.Y,9,9);g.DrawArc(stroke,symbol.X,symbol.Y+9,15,7,195,150);}
   else {g.DrawRectangle(stroke,symbol.X+1,symbol.Y+2,13,11);g.DrawLine(stroke,symbol.X+4,symbol.Y+6,symbol.X+7,symbol.Y+9);g.DrawLine(stroke,symbol.X+7,symbol.Y+9,symbol.X+12,symbol.Y+4);}
  }
  TextRenderer.DrawText(g,Text,Font,new Rectangle(r.X+36,r.Y,r.Width-39,r.Height),selected?Rubi.Text:Rubi.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);}
}
