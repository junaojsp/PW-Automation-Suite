using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;using System.Linq;using System.Windows.Forms;
// Side menu (0.21.0, owner request): one window. "Personagens" is the approved 390×606 panel, unchanged and shifted right;
// "Rota" shows the Rota tool in the same window (it grows to fit and returns to the original size afterwards).
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
partial class AvalonInterno {
 const int SideWidth=172,PanelWidth=390,PanelHeight=690;
 SideItem sidePersonagens,sideRota,sideAjustes;Panel rotaHost;bool rotaView;Point? panelLocation;readonly List<Control> panelBody=new List<Control>();readonly Dictionary<Control,bool> panelVisible=new Dictionary<Control,bool>();
 Control minimizeButton,closeButton;
 void ApplySideMenu(){SuspendLayout();try{
  minimizeButton=Controls.OfType<Button>().Single(b=>b.Text=="—");closeButton=Controls.OfType<Button>().Single(b=>b.Text=="×");
  foreach(Control c in Controls){c.Left+=SideWidth;if(c.Top>=50)panelBody.Add(c);}
  ClientSize=new Size(SideWidth+PanelWidth,PanelHeight);
  var brand=RubiLabel("FARMZILLA",50,13,115,20,10,FontStyle.Bold,Rubi.Accent);EnableRubiDrag(brand);var engine=RubiLabel("MOTOR AVALON · x86",50,33,116,15,7,FontStyle.Regular,Rubi.Muted);EnableRubiDrag(engine);
  RubiLabel("FERRAMENTAS",16,72,130,16,7,FontStyle.Bold,Rubi.Text);
  sidePersonagens=SideAt("Espelho",94,()=>ShowRotaView(false));sideRota=SideAt("Mat Farm",136,()=>ShowRotaView(true));
  // 0.46.4 (owner): no Ajustes button any more; the tools stay on top.
  hints.SetToolTip(sideRota,"Rotas de coleta por coordenadas (mapa, materiais, inventário e log), na mesma janela. Quem está em rota fica fora do seguir, ataque, teleporte e missões.");
  var footer=RubiLabel("FarmZilla · Rubi",16,PanelHeight-46,150,16,7,FontStyle.Regular,Rubi.Muted);var footer2=RubiLabel("v"+ReleaseInfo.Version+" · motor original",16,PanelHeight-30,153,16,7,FontStyle.Regular,Rubi.Muted);footer.Anchor=footer2.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
  rotaHost=new Panel{Visible=false,BackColor=Rubi.Background};Controls.Add(rotaHost);
  sidePersonagens.Selected=true;Invalidate();
 }finally{ResumeLayout(false);}}
 SideItem SideAt(string text,int y,Action action){var item=new SideItem(text){Bounds=new Rectangle(12,y,SideWidth-24,38)};item.Click+=(s,e)=>{if(!authorizationDenied||item==sidePersonagens)action();};Controls.Add(item);return item;}
 void ShowRotaView(bool show){
  if(show==rotaView){if(show&&rotaWindow!=null)rotaWindow.Focus();return;}
  if(show){if(closeInProgress)return;if(!AuthorizationSession.Has("rota")){Note("Mat Farm não liberado para este computador. Fale com o administrador.");return;}if(!EnsureRota())return;}
  SuspendLayout();try{
   rotaView=show;sidePersonagens.Selected=!show;sideRota.Selected=show;
   var area=Screen.FromControl(this).WorkingArea;
   if(show){panelLocation=Location;int w=Math.Min(SideWidth+1120,area.Width),h=Math.Min(800,area.Height);ClientSize=new Size(w,h);Location=new Point(Math.Max(area.Left,Math.Min(Left,area.Right-Width)),Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)));
    rotaHost.Bounds=new Rectangle(SideWidth,50,ClientSize.Width-SideWidth-2,ClientSize.Height-52);panelVisible.Clear();foreach(var c in panelBody){panelVisible[c]=c.Visible;c.Visible=false;}rotaHost.Visible=true;rotaHost.BringToFront();rotaView=true;SelectRota(rotaWindow);}
   else{rotaHost.Visible=false;foreach(var w in rotaWindows)if(!w.IsDisposed)w.Visible=false;ClientSize=new Size(SideWidth+PanelWidth,PanelHeight);if(panelLocation.HasValue)Location=panelLocation.Value;foreach(var kv in panelVisible)kv.Key.Visible=kv.Value;UpdateAttackRows();}
   if(lockedNotice!=null){lockedNotice.Visible=controlLocked&&!show;if(lockedNotice.Visible)lockedNotice.BringToFront();}
   minimizeButton.Left=ClientSize.Width-69;closeButton.Left=ClientSize.Width-39;
  }finally{ResumeLayout(true);}
  Invalidate(true);if(show)Note("Mat Farm aberto.");
 }
 // Full window: background, side menu, then the panel (header band always, cards only on Personagens) and the outer border.
 void PaintRubi(Graphics g){g.SmoothingMode=SmoothingMode.AntiAlias;int w=ClientSize.Width,h=ClientSize.Height;
  using(var b=new LinearGradientBrush(new Rectangle(0,0,SideWidth,h),Rubi.Shade(Rubi.Background,-2),Rubi.Shade(Rubi.Background,-12),LinearGradientMode.Vertical))g.FillRectangle(b,0,0,SideWidth,h);
  using(var p=new Pen(Color.FromArgb(110,0,0,0)))g.DrawLine(p,SideWidth-1,0,SideWidth-1,h);using(var shade=new SolidBrush(Color.FromArgb(24,Rubi.Accent)))g.FillRectangle(shade,SideWidth-3,0,2,60);using(var p=new Pen(Color.FromArgb(24,255,255,255)))g.DrawLine(p,SideWidth,0,SideWidth,h);
  var badge=new Rectangle(14,15,28,26);Depth.Shadow(g,badge,6,2);Depth.Raised(g,badge,6,Rubi.Shade(Rubi.Accent,30),Rubi.Shade(Rubi.Accent,-35),Rubi.Shade(Rubi.Accent,-50));using(var f=new Font("Segoe UI",(float)(9)*global::UiScale.F,FontStyle.Bold))TextRenderer.DrawText(g,"FZ",f,badge,Rubi.OnAccent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  using(var p=new Pen(Color.FromArgb(70,0,0,0)))g.DrawLine(p,14,60,SideWidth-14,60);using(var p=new Pen(Color.FromArgb(20,255,255,255)))g.DrawLine(p,14,61,SideWidth-14,61);using(var accent=new Pen(Color.FromArgb(170,Rubi.Accent)))g.DrawLine(accent,14,61,58,61);
  var state=g.Save();g.TranslateTransform(SideWidth,0);g.SetClip(new Rectangle(0,0,w-SideWidth,h));PaintContent(g,w-SideWidth,h);g.Restore(state);
  using(var pen=new Pen(Rubi.Line))using(var path=Shapes.Round(new Rectangle(0,0,w-1,h-1),12))g.DrawPath(pen,path);using(var pen=new Pen(Color.FromArgb(30,255,255,255)))g.DrawLine(pen,12,1,w-13,1);}
}
