using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

partial class AvalonInterno {
 Panel sharedPotBar,potDrawer;
 bool potDrawerOpen;
 NumericUpDown potHpInput,potMpInput;
 ComboBox potHpKeyInput,potMpKeyInput;
 Label potSaveStatus;
 void InitializeInlinePot(){
  sharedPotBar=new Panel{BackColor=Rubi.Background};Controls.Add(sharedPotBar);
  panelBody.Remove(autoPotOption);sharedPotBar.Controls.Add(autoPotOption);
  autoPotOption.SetBounds(12,8,78,20);autoPotOption.BackColor=Rubi.Background;
  var gear=new NeonButton{Text="⚙",AccessibleName="Configurar Auto Pot"};
  gear.SetBounds(92,3,34,30);gear.Click+=(s,e)=>{potDrawerOpen=!potDrawerOpen;LayoutInlinePanels();};sharedPotBar.Controls.Add(gear);
  hints.SetToolTip(gear,"Abrir ou recolher as mesmas configurações de Auto Pot no Espelho e na Frost.");
  var summary=new Label{Text="Configurações compartilhadas",ForeColor=Rubi.Muted,Bounds=new Rectangle(140,10,235,20)};sharedPotBar.Controls.Add(summary);
  potDrawer=new Panel{BackColor=Rubi.Panel,Visible=false,AutoScroll=true};Controls.Add(potDrawer);
  PotLabel("AUTO POT",16,14,230,28,Rubi.Accent,12,FontStyle.Bold);
  var collapse=new NeonButton{Text="‹",AccessibleName="Recolher Auto Pot"};collapse.SetBounds(262,12,32,30);collapse.Click+=(s,e)=>{potDrawerOpen=false;LayoutInlinePanels();};potDrawer.Controls.Add(collapse);
  PotLabel("Mesmas configurações no Espelho e na Frost.",16,54,278,38,Rubi.Text);
  PotLabel("Modo atual: teclas do jogo em primeiro plano.\nA Frost em segundo plano não usa poções por este modo.",16,100,278,60,Rubi.Muted);
  PotLabel("HP",16,176,278,24,Rubi.Accent,10,FontStyle.Bold);
  PotLabel("Usar quando HP ≤",16,211,164,24,Rubi.Text);
  potHpInput=PotNumber(potHp,190,208);PotLabel("%",266,211,22,24,Rubi.Text);
  PotLabel("Tecla da poção HP",16,250,150,24,Rubi.Text);potHpKeyInput=PotKey(potHpKey,182,246);
  PotLabel("MP",16,302,278,24,Rubi.Accent,10,FontStyle.Bold);
  PotLabel("Usar quando MP ≤",16,337,164,24,Rubi.Text);
  potMpInput=PotNumber(potMp,190,334);PotLabel("%",266,337,22,24,Rubi.Text);
  PotLabel("Tecla da poção MP",16,376,150,24,Rubi.Text);potMpKeyInput=PotKey(potMpKey,182,372);
  PotLabel("Intervalo mínimo: 8 segundos por recurso.\nConfigure as mesmas teclas nos clientes do jogo.",16,424,278,70,Rubi.Muted);
  var save=new NeonButton{Text="Salvar configurações",Primary=true,AccessibleName="Salvar Auto Pot"};save.SetBounds(16,510,278,36);save.Click+=(s,e)=>SaveInlinePot();potDrawer.Controls.Add(save);
  potSaveStatus=PotLabel("Configurações globais de HP e MP.",16,558,278,64,Rubi.Muted);
  LayoutInlinePanels();
 }
 Label PotLabel(string text,int x,int y,int w,int h,Color color,float size=9,FontStyle style=FontStyle.Regular){
  var label=new Label{Text=text,Bounds=new Rectangle(x,y,w,h),ForeColor=color,Font=new Font("Segoe UI",size*global::UiScale.F,style)};potDrawer.Controls.Add(label);return label;
 }
 NumericUpDown PotNumber(int value,int x,int y){var control=new NumericUpDown{Minimum=1,Maximum=99,Value=value,Bounds=new Rectangle(x,y,70,27),BackColor=Rubi.Background,ForeColor=Rubi.Text};potDrawer.Controls.Add(control);return control;}
 ComboBox PotKey(Keys value,int x,int y){
  var control=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(x,y,112,27),BackColor=Rubi.Background,ForeColor=Rubi.Text};
  control.Items.Add(Keys.None);for(Keys key=Keys.F1;key<=Keys.F12;key++)control.Items.Add(key);
  control.SelectedItem=value;potDrawer.Controls.Add(control);return control;
 }
 void SaveInlinePot(){
  Keys hp=(Keys)potHpKeyInput.SelectedItem,mp=(Keys)potMpKeyInput.SelectedItem;
  if(hp!=Keys.None&&hp==mp){potSaveStatus.Text="Escolha teclas distintas para HP e MP.";return;}
  try{
   Directory.CreateDirectory(Path.GetDirectoryName(PotConfigPath));
   File.WriteAllLines(PotConfigPath,new[]{potHpInput.Value.ToString("0"),potMpInput.Value.ToString("0"),((int)hp).ToString(),((int)mp).ToString()});
   potHp=(int)potHpInput.Value;potMp=(int)potMpInput.Value;potHpKey=hp;potMpKey=mp;
   potSaveStatus.Text="Configurações salvas para Espelho e Frost.";
  }catch(Exception ex){potSaveStatus.Text="Não foi possível salvar: "+ex.Message;}
 }
 void LayoutInlinePanels(){
  if(sharedPotBar==null)return;
  if(rotaView){sharedPotBar.Visible=false;potDrawer.Visible=false;return;}
  ClientSize=new Size(SideWidth+PanelWidth+(potDrawerOpen?310:0),PanelHeight);
  sharedPotBar.Bounds=new Rectangle(SideWidth,654,PanelWidth,36);sharedPotBar.Visible=true;
  potDrawer.Bounds=new Rectangle(SideWidth+PanelWidth,50,310,PanelHeight-50);potDrawer.Visible=potDrawerOpen;
  if(frostInlineHost!=null)frostInlineHost.Bounds=new Rectangle(SideWidth,50,PanelWidth,604);
  minimizeButton.Left=ClientSize.Width-69;closeButton.Left=ClientSize.Width-39;
  var area=Screen.FromControl(this).WorkingArea;
  Location=new Point(Math.Max(area.Left,Math.Min(Left,area.Right-Width)),Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)));
  Invalidate(true);
 }
}
