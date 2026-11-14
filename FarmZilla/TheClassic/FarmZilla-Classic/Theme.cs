using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
static class DragNative {
 [DllImport("user32.dll")]public static extern bool ReleaseCapture();
 [DllImport("user32.dll")]public static extern IntPtr SendMessage(IntPtr window,int msg,IntPtr wp,IntPtr lp);
}
static class Shapes {public static GraphicsPath Round(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}}
// Raised surfaces: soft drop shadow, vertical gradient and a thin top highlight.
static class Depth {
 public static void Shadow(Graphics g,Rectangle r,int radius,int depth){for(int i=depth;i>=1;i--)using(var path=Shapes.Round(new Rectangle(r.X,r.Y+i,r.Width,r.Height),radius))using(var b=new SolidBrush(Color.FromArgb(55,0,0,0)))g.FillPath(b,path);}
 public static void Raised(Graphics g,Rectangle r,int radius,Color top,Color bottom,Color border){using(var path=Shapes.Round(r,radius)){using(var b=new LinearGradientBrush(new Rectangle(r.X,r.Y-1,r.Width,r.Height+2),top,bottom,LinearGradientMode.Vertical))g.FillPath(b,path);using(var p=new Pen(border))g.DrawPath(p,path);}using(var p=new Pen(Color.FromArgb(45,255,255,255)))g.DrawLine(p,r.X+radius,r.Y+1,r.Right-radius,r.Y+1);}
 public static void Sunken(Graphics g,Rectangle r,int radius,Color fill){using(var path=Shapes.Round(r,radius)){using(var b=new LinearGradientBrush(new Rectangle(r.X,r.Y-1,r.Width,r.Height+2),Rubi.Shade(fill,-14),fill,LinearGradientMode.Vertical))g.FillPath(b,path);using(var p=new Pen(Rubi.Shade(Rubi.Line,-18)))g.DrawPath(p,path);}using(var p=new Pen(Color.FromArgb(70,0,0,0)))g.DrawLine(p,r.X+radius,r.Y+1,r.Right-radius,r.Y+1);using(var p=new Pen(Color.FromArgb(22,255,255,255)))g.DrawLine(p,r.X+radius,r.Bottom,r.Right-radius,r.Bottom);}
 // Paint what the parent shows behind a control, so rounded corners sit on cards and gradients.
 public static void ParentBackground(Control c,Graphics g){if(c.Parent==null){g.Clear(Rubi.Background);return;}var state=g.Save();g.TranslateTransform(-c.Left,-c.Top);var clip=new Rectangle(c.Left,c.Top,c.Width,c.Height);using(var pe=new PaintEventArgs(g,clip))InvokeParent(c,pe);g.Restore(state);}
 static void InvokeParent(Control c,PaintEventArgs pe){var host=c.Parent as IParentPainter;if(host!=null)host.PaintBehind(pe);else using(var b=new SolidBrush(c.Parent.BackColor))pe.Graphics.FillRectangle(b,pe.ClipRectangle);}
}
interface IParentPainter {void PaintBehind(PaintEventArgs e);}
class NeonButton : Button {
 public bool Primary; bool hover,pressed;
 public NeonButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;ForeColor=Rubi.Text;BackColor=Rubi.Panel;Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);MouseEnter+=(s,e)=>{hover=true;Invalidate();};MouseLeave+=(s,e)=>{hover=pressed=false;Invalidate();};MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}};MouseUp+=(s,e)=>{pressed=false;Invalidate();};}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;Depth.ParentBackground(this,g);g.SmoothingMode=SmoothingMode.AntiAlias;int lift=pressed?0:2;var body=new Rectangle(0,2-lift,Width-1,Height-3);int radius=Math.Min(6,(Height-4)/2);if(lift>0)Depth.Shadow(g,body,radius,lift);
  Color baseColor=Primary?Rubi.Accent:(hover?Rubi.Field:BackColor);if(Primary&&hover)baseColor=Rubi.Shade(Rubi.Accent,12);
  if(pressed)Depth.Sunken(g,body,radius,baseColor);else Depth.Raised(g,body,radius,Rubi.Shade(baseColor,Primary?22:16),Rubi.Shade(baseColor,Primary?-22:-8),Primary?Rubi.Shade(Rubi.Accent,-35):Rubi.Line);
  Color ink=Enabled?(Primary?Rubi.OnAccent:ForeColor):Rubi.Muted;if(Text.IndexOf('\n')>=0){var size=TextRenderer.MeasureText(g,Text,Font,body.Size,TextFormatFlags.WordBreak);TextRenderer.DrawText(g,Text,Font,new Rectangle(body.X,body.Y+(body.Height-size.Height)/2,body.Width,size.Height),ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak);}else TextRenderer.DrawText(g,Text,Font,body,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
}
class CyanCombo : ComboBox {
 // Optional tag drawn at the right of an item in the open list (e.g. "● janela ativa").
 public Func<object,string> ItemTag;
 // Optional class emblem drawn before the item text (field and open list).
 public Func<object,int> ItemClass;
 int ClassOf(object item){return ItemClass==null||item==null?-1:ItemClass(item);}
 public CyanCombo(){FlatStyle=FlatStyle.Flat;BackColor=Rubi.Field;ForeColor=Rubi.Text;Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=21;DropDownHeight=230;IntegralHeight=false;DrawItem+=(s,e)=>{if(e.Index<0)return;using(var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0?Rubi.Panel:Rubi.Field))e.Graphics.FillRectangle(brush,e.Bounds);string tag=ItemTag==null?null:ItemTag(Items[e.Index]);var textBounds=e.Bounds;if(!String.IsNullOrEmpty(tag)){var size=TextRenderer.MeasureText(tag,Font);var tagBounds=new Rectangle(e.Bounds.Right-size.Width-6,e.Bounds.Top,size.Width+4,e.Bounds.Height);TextRenderer.DrawText(e.Graphics,tag,Font,tagBounds,Rubi.Accent,TextFormatFlags.VerticalCenter|TextFormatFlags.Right);textBounds.Width=Math.Max(10,tagBounds.Left-textBounds.Left);}int cls=ClassOf(Items[e.Index]);if(ClassIcons.Known(cls)){float size=Math.Min(19,e.Bounds.Height-2);ClassIcons.Draw(e.Graphics,new RectangleF(e.Bounds.Left+3,e.Bounds.Top+(e.Bounds.Height-size)/2,size,size),cls);textBounds.X+=(int)size+6;textBounds.Width-=(int)size+6;}TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,textBounds,Rubi.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};}

 protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(Width>20&&Height>20)using(var path=Shapes.Round(new Rectangle(0,0,Width,Height),5)){var previous=Region;Region=new Region(path);if(previous!=null)previous.Dispose();}}
 void DrawField(Graphics g){g.SmoothingMode=SmoothingMode.AntiAlias;using(var brush=new SolidBrush(Rubi.Field))g.FillRectangle(brush,0,0,Width,Height);Depth.Raised(g,new Rectangle(0,0,Width-1,Height-1),5,Rubi.Shade(Rubi.Field,12),Rubi.Shade(Rubi.Field,-10),Rubi.Line);int cls=ClassOf(SelectedItem);int left=7;if(ClassIcons.Known(cls)){float size=Math.Min(21,Height-6);ClassIcons.Draw(g,new RectangleF(5,(Height-size)/2,size,size),cls);left=(int)(5+size+5);}TextRenderer.DrawText(g,GetItemText(SelectedItem),Font,new Rectangle(left,0,Math.Max(1,Width-22-left),Height),Rubi.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);using(var pen=new Pen(Rubi.Accent,1.6f)){int y=Height/2-2;g.DrawLines(pen,new[]{new Point(Width-16,y),new Point(Width-12,y+4),new Point(Width-8,y)});}}
 protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg==0xF){using(var g=Graphics.FromHwnd(Handle))DrawField(g);}else if((m.Msg==0x317||m.Msg==0x318)&&m.WParam!=IntPtr.Zero){using(var g=Graphics.FromHdc(m.WParam))DrawField(g);}}
}

