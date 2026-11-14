using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;
// 0.39.0 (owner): the attack/support boxes were hard to read as something to tick. TickBox draws a larger rounded box with a
// clear border when off and a filled box with a check mark when on (ForeColor = attack red or support green).
sealed class TickBox : CheckBox {
 bool hover;
 public TickBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);Text="";Cursor=Cursors.Hand;MouseEnter+=(s,e)=>{hover=true;Invalidate();};MouseLeave+=(s,e)=>{hover=false;Invalidate();};}
 protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
 protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;Depth.ParentBackground(this,g);g.SmoothingMode=SmoothingMode.AntiAlias;
  int size=Math.Min(Width,Height)-3;var r=new Rectangle((Width-size)/2,(Height-size)/2,size,size);
  using(var path=Shapes.Round(r,4)){
   if(Checked&&Enabled){using(var b=new LinearGradientBrush(r,Rubi.Shade(ForeColor,22),Rubi.Shade(ForeColor,-18),LinearGradientMode.Vertical))g.FillPath(b,path);using(var p=new Pen(Rubi.Shade(ForeColor,-40)))g.DrawPath(p,path);
    using(var p=new Pen(Color.White,2.2f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})g.DrawLines(p,new[]{new PointF(r.X+r.Width*0.24f,r.Y+r.Height*0.52f),new PointF(r.X+r.Width*0.43f,r.Y+r.Height*0.72f),new PointF(r.X+r.Width*0.78f,r.Y+r.Height*0.30f)});}
   else{using(var b=new SolidBrush(Rubi.Field))g.FillPath(b,path);Color edge=!Enabled?Rubi.Line:hover?ForeColor:Rubi.Shade(Rubi.Muted,-10);using(var p=new Pen(edge,Enabled?1.6f:1f))g.DrawPath(p,path);}
  }
  if(Focused&&ShowFocusCues)using(var p=new Pen(Color.FromArgb(120,ForeColor)){DashStyle=DashStyle.Dot})g.DrawRectangle(p,0,0,Width-1,Height-1);}
}
