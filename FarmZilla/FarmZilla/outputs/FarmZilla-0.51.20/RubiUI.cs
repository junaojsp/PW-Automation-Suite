using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;using System.Linq;using System.Windows.Forms;
static class Rubi {
 public static readonly Color Background=Color.FromArgb(16,18,23),Panel=Color.FromArgb(29,32,39),Field=Color.FromArgb(39,43,51),Line=Color.FromArgb(65,71,82),Text=Color.FromArgb(244,246,249),Muted=Color.FromArgb(181,190,204),Accent=Color.FromArgb(239,73,89),OnAccent=Color.FromArgb(255,255,255);
 public static Color Shade(Color c,int amount){Func<int,int> f=v=>Math.Max(0,Math.Min(255,v+amount));return Color.FromArgb(c.A,f(c.R),f(c.G),f(c.B));}
}
partial class AvalonInterno : IParentPainter {
 void ApplyRubiCompact(){
  SuspendLayout();try{
   var mount=Controls.OfType<Button>().Single(b=>b.Text=="Montar PT");var remount=Controls.OfType<Button>().Single(b=>b.Text=="Remontar PT");var refresh=Controls.OfType<Button>().Single(b=>b.Text=="Atualizar");var minimize=Controls.OfType<Button>().Single(b=>b.Text=="—");var close=Controls.OfType<Button>().Single(b=>b.Text=="×");
   var keep=new HashSet<Control>{reviveAuto,resurrectAuto,foxAuto,main,key,partyKey,remountKey,buffKey,attackKey,attackModeKey,attackMode,meditationMirror,status,missionCheck,teleportStatus,action,updateSettingsButton,mount,remount,refresh,minimize,close};foreach(var row in rows)keep.Add(row);foreach(var box in attackRows)keep.Add(box);foreach(var box in supportRows)keep.Add(box);keep.Add(supportTarget);keep.Add(primaryAttack);
   foreach(Control control in Controls.Cast<Control>().ToArray())if(!keep.Contains(control)){Controls.Remove(control);control.Dispose();}
   Text="FarmZilla • "+ReleaseInfo.Version+" / 32 bits";AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(390,654);BackColor=Rubi.Background;ForeColor=Rubi.Text;Font=new Font("Segoe UI",(float)(9)*global::UiScale.F);Padding=Padding.Empty;
   MouseDown+=(s,e)=>{if(e.Y<48&&e.Button==MouseButtons.Left){DragNative.ReleaseCapture();DragNative.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};
   var mark=RubiLabel("FZ",14,12,28,25,9,FontStyle.Bold,Color.FromArgb(46,169,235));mark.TextAlign=ContentAlignment.MiddleCenter;EnableRubiDrag(mark);
   var brand=RubiLabel("FarmZilla",51,8,180,32,13,FontStyle.Bold,Rubi.Text);brand.Font=new Font("Segoe UI Semibold",19,FontStyle.Bold,GraphicsUnit.Pixel);brand.AutoEllipsis=false;brand.TextAlign=ContentAlignment.MiddleLeft;EnableRubiDrag(brand);
   var version=RubiLabel("v"+ReleaseInfo.Version,232,18,79,18,8,FontStyle.Regular,Rubi.Muted);EnableRubiDrag(version);
   minimize.SetBounds(321,10,25,28);close.SetBounds(351,10,25,28);
   RubiLabel("PERSONAGEM PRINCIPAL",27,66,208,17,8,FontStyle.Bold,Rubi.Text);main.SetBounds(27,88,300,29);var primaryHead=RubiLabel("Ataque",326,66,48,17,8,FontStyle.Regular,Rubi.Accent);primaryHead.TextAlign=ContentAlignment.MiddleCenter;primaryAttack.SetBounds(339,90,23,23);primaryAttack.FlatStyle=FlatStyle.Flat;primaryAttack.ForeColor=Rubi.Accent;primaryAttack.BackColor=CardTone(59,70,102);primaryAttack.AccessibleName="Atacar com o principal";main.AccessibleName="Principal";
   RubiLabel("SECUNDÁRIOS",14,143,128,18,8,FontStyle.Bold,Rubi.Text);var attackHead=RubiLabel("Atq.",150,143,34,18,8,FontStyle.Bold,Rubi.Accent);var supportHead=RubiLabel("Sup.",192,143,34,18,8,FontStyle.Bold,SupportColor);hints.SetToolTip(attackHead,"Ataque dos personagens secundários");hints.SetToolTip(supportHead,"Suporte dos personagens secundários");attackHead.TextAlign=supportHead.TextAlign=ContentAlignment.MiddleCenter;RubiLabel("COMANDOS",238,143,138,18,8,FontStyle.Bold,Rubi.Text);
   for(int i=0;i<rows.Length;i++){rows[i].SetBounds(14,168+i*32,128,27);rows[i].AccessibleName="Secundário "+(i+1);attackRows[i].SetBounds(155,170+i*32,24,24);supportRows[i].SetBounds(198,170+i*32,24,24);supportRows[i].BackColor=CardTone(137,322,171+i*32+10);attackRows[i].AccessibleName="Atacar com secundário "+(i+1);attackRows[i].ForeColor=Rubi.Accent;attackRows[i].FlatStyle=FlatStyle.Flat;attackRows[i].BackColor=CardTone(137,322,171+i*32+10);}
   // 0.46.1 (owner): the Alto cultivo key sits with the other keys; rows 27 apart so the card keeps its size.
   altoKey=AltoKeyField();
   var labels=new[]{"Montar PT","Remont. PT","Bufar","Seguir","Atacar","Alto cultivo"};var fields=new[]{partyKey,remountKey,buffKey,key,attackKey,altoKey};
   for(int i=0;i<fields.Length;i++){int y=166+i*27;RubiLabel(labels[i],238,y,78,20,8,FontStyle.Regular,Rubi.Text);fields[i].SetBounds(321,y,52,20);fields[i].BorderStyle=BorderStyle.FixedSingle;fields[i].BackColor=Rubi.Field;fields[i].ForeColor=Rubi.Accent;fields[i].Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);fields[i].AccessibleName="Tecla "+labels[i];}
   RubiLabel("Modo ataque",238,330,80,18,8,FontStyle.Regular,Rubi.Muted);attackModeKey.SetBounds(321,328,52,20);attackModeKey.BorderStyle=BorderStyle.FixedSingle;attackModeKey.BackColor=Rubi.Field;attackModeKey.ForeColor=Rubi.Accent;attackModeKey.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);attackModeKey.AccessibleName="Tecla Modo de ataque";attackMode.SetBounds(238,351,138,26);attackMode.AccessibleName="Modo de ataque";
   meditationMirror.SetBounds(238,380,78,20);meditationMirror.Text="Meditação";ReviveField(316,380);meditationMirror.ForeColor=Rubi.Muted;meditationMirror.FlatStyle=FlatStyle.Flat;meditationMirror.BackColor=CardTone(137,310,390);InitAutomationOptions();
   mount.SetBounds(238,454,138,28);remount.SetBounds(238,488,138,28);var buff=ButtonAt("Bufar",238,522,138,()=>{var task=BuffParty();});buff.Height=28;hints.SetToolTip(buff,"Executa a macro 1 uma única vez no principal e nos secundários selecionados.");
   teleportStatus.SetBounds(14,491,205,14);SupportTargetField(14,463);teleportStatus.ForeColor=Rubi.Muted;teleportStatus.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);teleportStatus.AutoEllipsis=true;
   missionCheck.SetBounds(14,551,218,19);ResurrectField(238,553);FoxField(304,553);missionCheck.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);status.SetBounds(14,577,362,19);status.ForeColor=Rubi.Muted;status.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);teleportStatus.BackColor=missionCheck.BackColor=status.BackColor=Color.Transparent;
   action.SetBounds(14,607,168,34);refresh.SetBounds(188,607,92,34);refresh.Text="Sincronizar\npersonagens";Controls.Remove(updateSettingsButton);updateSettingsButton.Dispose();updateSettingsButton=ButtonAt("Update",286,607,90,()=>{var task=CheckUpdate(true);});updateSettingsButton.Height=34;
   foreach(var button in Controls.OfType<NeonButton>()){button.BackColor=Rubi.Panel;button.ForeColor=Rubi.Text;button.Primary=button==action;}
   foreach(var combo in Controls.OfType<ComboBox>()){combo.BackColor=Rubi.Field;combo.ForeColor=Rubi.Text;combo.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);combo.ItemHeight=21;combo.DropDownHeight=220;}
   hints.SetToolTip(updateSettingsButton,"Procura a versão mais nova do FarmZilla e instala.");hints.SetToolTip(refresh,"Procura de novo os clientes do jogo abertos e atualiza os personagens.");Invalidate();
  }finally{ResumeLayout(false);}
 }
 Label RubiLabel(string text,int x,int y,int w,int h,float size,FontStyle style,Color color){var label=new Label{Text=text,Bounds=new Rectangle(x,y,w,h),Font=new Font("Segoe UI",(float)(size)*global::UiScale.F,style),ForeColor=color,BackColor=Color.Transparent,AutoEllipsis=true};Controls.Add(label);return label;}
 void EnableRubiDrag(Control control){control.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){DragNative.ReleaseCapture();DragNative.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};}
 public void PaintBehind(PaintEventArgs e){using(var b=new SolidBrush(BackColor))e.Graphics.FillRectangle(b,e.ClipRectangle);PaintRubi(e.Graphics);}
 // Depth: lit header, raised cards with drop shadows, and a gradient FZ badge; controls keep their 0.19 positions.
 void PaintContent(Graphics g,int w,int h){
  using(var b=new LinearGradientBrush(new Rectangle(0,0,w,h),Rubi.Shade(Rubi.Background,6),Rubi.Shade(Rubi.Background,-8),LinearGradientMode.Vertical))g.FillRectangle(b,0,0,w,h);
  using(var b=new LinearGradientBrush(new Rectangle(0,0,w,49),Rubi.Shade(Rubi.Panel,14),Rubi.Shade(Rubi.Panel,-6),LinearGradientMode.Vertical))g.FillRectangle(b,1,1,w-2,47);
  using(var p=new Pen(Color.FromArgb(90,0,0,0)))g.DrawLine(p,1,48,w-2,48);using(var p=new Pen(Color.FromArgb(28,255,255,255)))g.DrawLine(p,1,49,w-2,49);using(var accent=new Pen(Color.FromArgb(145,Rubi.Accent)))g.DrawLine(accent,16,48,102,48);
  var badge=new Rectangle(14,12,28,25);Depth.Shadow(g,badge,5,2);Depth.Raised(g,badge,5,Color.FromArgb(12,19,25),Color.FromArgb(2,5,8),Color.FromArgb(36,133,192));
  if(!rotaView){Card(g,new Rectangle(14,59,362,70));using(var p=new Pen(Rubi.Accent,3))g.DrawLine(p,16,67,16,121);
  using(var accent=new SolidBrush(Color.FromArgb(115,Rubi.Accent)))g.FillRectangle(accent,22,124,78,2);
  Card(g,new Rectangle(8,137,222,322));Card(g,new Rectangle(233,137,149,312));
  using(var line=new Pen(Color.FromArgb(90,Rubi.Accent)))g.DrawLine(line,17,161,74,161);
  using(var line=new Pen(Color.FromArgb(90,Rubi.Accent)))g.DrawLine(line,242,161,296,161);
  using(var footer=new SolidBrush(Color.FromArgb(95,Rubi.Panel)))g.FillRectangle(footer,9,598,374,50);using(var p=new Pen(Color.FromArgb(80,0,0,0)))g.DrawLine(p,14,568,376,568);using(var p=new Pen(Color.FromArgb(22,255,255,255)))g.DrawLine(p,14,569,376,569);}
}
 // Flat check boxes fill their box with BackColor (white when transparent), so match the card gradient at their row.
 static Color CardTone(int top,int height,int y){var a=Rubi.Shade(Rubi.Panel,8);var b=Rubi.Shade(Rubi.Panel,-5);float t=Math.Max(0f,Math.Min(1f,(y-top)/(float)height));return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
 static void Card(Graphics g,Rectangle r){Depth.Shadow(g,r,8,2);Depth.Raised(g,r,8,Rubi.Shade(Rubi.Panel,11),Rubi.Shade(Rubi.Panel,-5),Rubi.Line);}
}