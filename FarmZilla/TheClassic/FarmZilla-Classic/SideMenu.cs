using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;using System.Linq;using System.Windows.Forms;
// Side menu (0.21.0, owner request): one window. "Personagens" is the approved 390×606 panel, unchanged and shifted right;
// "Rota" shows the Rota tool in the same window (it grows to fit and returns to the original size afterwards).
partial class AvalonInterno {
 const int SideWidth=172,PanelWidth=390,PanelHeight=690;
 SideItem sidePersonagens,sideRota,sideAjustes;Panel rotaHost;bool rotaView;Point? panelLocation;readonly List<Control> panelBody=new List<Control>();readonly Dictionary<Control,bool> panelVisible=new Dictionary<Control,bool>();
 Control minimizeButton,closeButton;
 void ApplySideMenu(){SuspendLayout();try{
  minimizeButton=Controls.OfType<Button>().Single(b=>b.Text=="—");closeButton=Controls.OfType<Button>().Single(b=>b.Text=="×");
  foreach(Control c in Controls){c.Left+=SideWidth;if(c.Top>=50)panelBody.Add(c);}
  ClientSize=new Size(SideWidth+PanelWidth,PanelHeight);
  var brand=RubiLabel("FARMZILLA",50,13,115,20,10,FontStyle.Bold,Rubi.Accent);EnableRubiDrag(brand);var engine=RubiLabel("THE CLASSIC · x86",50,33,116,15,7,FontStyle.Regular,Rubi.Muted);EnableRubiDrag(engine);
  RubiLabel("FERRAMENTAS",16,72,130,16,7,FontStyle.Bold,Rubi.Text);
  sidePersonagens=SideAt("Espelho",94,ShowEspelhoInline);sideRota=SideAt("Mat Farm",136,()=>{ShowEspelhoInline();ShowRotaView(true);});
  // Reference layout: reserve the four tools without attaching game operations.
  PendingTool("Vm Farm",178);PendingTool("Token Farm",220);
  var frostButton=SideAt("Frost Farm",262,OpenFrostAutomation);sideFrostInline=frostButton;frostButton.AccessibleName="Frost Farm";frostButton.AccessibleDescription="DG Frost na mesma janela; líder, secundários e registro";hints.SetToolTip(frostButton,"Abre a Frost nesta janela e mantém as configurações compartilhadas de Auto Pot.");PendingTool("Skill Sender",304);
  hints.SetToolTip(sideRota,"Rotas de coleta por coordenadas (mapa, materiais, inventário e log), na mesma janela. Quem está em rota fica fora do seguir, ataque, teleporte e missões.");
  var footer=RubiLabel("FarmZilla · The Classic",16,PanelHeight-46,150,16,7,FontStyle.Regular,Rubi.Muted);var footer2=RubiLabel("v"+ReleaseInfo.Version+" · motor original",16,PanelHeight-30,153,16,7,FontStyle.Regular,Rubi.Muted);footer.Anchor=footer2.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;
  rotaHost=new Panel{Visible=false,BackColor=Rubi.Background};Controls.Add(rotaHost);
  InitializeInlinePot();sidePersonagens.Selected=true;Invalidate();
 }finally{ResumeLayout(false);}}
 SideItem SideAt(string text,int y,Action action){var item=new SideItem(text){Bounds=new Rectangle(12,y,SideWidth-24,38)};item.Click+=(s,e)=>{if(!authorizationDenied||item==sidePersonagens)action();};Controls.Add(item);return item;}
 void PendingTool(string title,int y){
  var button=SideAt(title,y,()=>MessageBox.Show(this,
   title+" está em preparação e ainda não executa automações.",
   title,MessageBoxButtons.OK,MessageBoxIcon.Information));
  button.AccessibleName=title;
  button.AccessibleDescription="Em preparação; ferramenta ainda indisponível";
  hints.SetToolTip(button,title+": em preparação; ainda indisponível.");
 }
 void ShowRotaView(bool show){
  if(show==rotaView){if(show&&rotaWindow!=null)rotaWindow.Focus();return;}
  if(show){if(closeInProgress)return;if(!AuthorizationSession.Has("rota")){Note("Mat Farm não liberado para este computador. Fale com o administrador.");return;}if(!EnsureRota())return;}
  SuspendLayout();try{
   if(show){if(potDrawer!=null)potDrawer.Visible=false;if(sharedPotBar!=null)sharedPotBar.Visible=false;}
   rotaView=show;sidePersonagens.Selected=!show;sideRota.Selected=show;
   var area=Screen.FromControl(this).WorkingArea;
   if(show){panelLocation=Location;int w=Math.Min(SideWidth+1120,area.Width),h=Math.Min(800,area.Height);ClientSize=new Size(w,h);Location=new Point(Math.Max(area.Left,Math.Min(Left,area.Right-Width)),Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)));
    rotaHost.Bounds=new Rectangle(SideWidth,50,ClientSize.Width-SideWidth-2,ClientSize.Height-52);panelVisible.Clear();foreach(var c in panelBody){panelVisible[c]=c.Visible;c.Visible=false;}rotaHost.Visible=true;rotaHost.BringToFront();rotaView=true;SelectRota(rotaWindow);}
   else{rotaHost.Visible=false;foreach(var w in rotaWindows)if(!w.IsDisposed)w.Visible=false;ClientSize=new Size(SideWidth+PanelWidth,PanelHeight);if(panelLocation.HasValue)Location=panelLocation.Value;foreach(var kv in panelVisible)kv.Key.Visible=kv.Value;UpdateAttackRows();LayoutInlinePanels();}
   if(lockedNotice!=null){lockedNotice.Visible=controlLocked&&!show;if(lockedNotice.Visible)lockedNotice.BringToFront();}
   minimizeButton.Left=ClientSize.Width-69;closeButton.Left=ClientSize.Width-39;
  }finally{ResumeLayout(true);}
  Invalidate(true);if(show)Note("Mat Farm aberto.");
 }
 // Full window: background, side menu, then the panel (header band always, cards only on Personagens) and the outer border.
 void PaintRubi(Graphics g){FarmZillaFrame.Paint(g,ClientSize.Width,ClientSize.Height,SideWidth,!rotaView&&!frostInlineView);}

}
