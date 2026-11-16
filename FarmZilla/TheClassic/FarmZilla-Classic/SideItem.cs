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
   DrawToolIcon(g,stroke,symbol);
  }
  TextRenderer.DrawText(g,Text,Font,new Rectangle(r.X+36,r.Y,r.Width-39,r.Height),selected?Rubi.Text:Rubi.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);}
 // Each tool has an original GDI+ icon. No image dependencies, same spacing/theme as Espelho.
 void DrawToolIcon(Graphics g,Pen p,Rectangle r){
  int x=r.X,y=r.Y;
  if(Text=="Espelho"){
   g.DrawEllipse(p,x+3,y,9,9);g.DrawArc(p,x,y+9,15,7,195,150);return;
  }
  if(Text.StartsWith("Mat Farm",StringComparison.Ordinal)){
   g.DrawLine(p,x+7,y+14,x+7,y+5);
   g.DrawArc(p,x+1,y+3,7,6,195,180);
   g.DrawArc(p,x+7,y+1,7,7,0,165);
   g.DrawLine(p,x+3,y+14,x+12,y+14);return;
  }
  if(Text=="Vm Farm"){
   Point[] gem={new Point(x+7,y),new Point(x+14,y+6),new Point(x+7,y+15),new Point(x,y+6)};
   g.DrawPolygon(p,gem);g.DrawLine(p,x,y+6,x+14,y+6);g.DrawLine(p,x+4,y+6,x+7,y+15);g.DrawLine(p,x+10,y+6,x+7,y+15);return;
  }
  if(Text=="Token Farm"){
   g.DrawEllipse(p,x+1,y+1,13,13);g.DrawEllipse(p,x+4,y+4,7,7);
   g.DrawLine(p,x+7,y+3,x+7,y+12);return;
  }
  if(Text=="Frost Farm"){
   g.DrawLine(p,x+7,y,x+7,y+15);g.DrawLine(p,x,y+7,x+15,y+7);
   g.DrawLine(p,x+2,y+2,x+13,y+13);g.DrawLine(p,x+13,y+2,x+2,y+13);return;
  }
  if(Text=="Skill Sender"){
   Point[] bolt={new Point(x+9,y),new Point(x+3,y+8),new Point(x+7,y+8),new Point(x+5,y+15),new Point(x+13,y+5),new Point(x+9,y+5)};
   g.DrawLines(p,bolt);g.DrawLine(p,bolt[5],bolt[0]);return;
  }
  if(Text=="Auto Quest"){
   g.DrawRectangle(p,x+2,y+1,11,13);
   g.DrawLine(p,x+4,y+4,x+10,y+4);g.DrawLine(p,x+4,y+7,x+9,y+7);
   g.DrawLine(p,x+5,y+11,x+7,y+13);g.DrawLine(p,x+7,y+13,x+13,y+8);return;
  }
  g.DrawRectangle(p,x+1,y+2,13,11);
 }
}
