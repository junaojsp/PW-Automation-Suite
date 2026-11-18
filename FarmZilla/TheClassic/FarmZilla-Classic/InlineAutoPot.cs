using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

partial class AvalonInterno {
 Panel sharedPotBar,potDrawer;
 bool potDrawerOpen;
 NumericUpDown potHpInput,potMpInput;
 ComboBox potHpKeyInput,potMpKeyInput,potHpItemInput,potMpItemInput;
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
  PotLabel("Selecione o item da mochila e a porcentagem de ativação para HP e MP.",16,52,278,50,Rubi.Text);
  PotLabel("Use a barra 1 a 8 ou F1–F12 no jogo ativo. Coloque a poção na tecla selecionada.",16,102,278,54,Rubi.Muted);
  var scan=new NeonButton{Text="Ler mochila do principal",AccessibleName="Buscar consumíveis na mochila"};
  scan.SetBounds(16,166,278,32);scan.Click+=async(s,e)=>await ScanPotInventory();potDrawer.Controls.Add(scan);
  PotLabel("A lista mostra IDs reais e quantidades; a classe do item ajuda a localizar poções.",16,202,278,45,Rubi.Muted);
  PotLabel("HP",16,253,278,23,Rubi.Accent,10,FontStyle.Bold);
  PotLabel("Item de HP na mochila",16,282,278,20,Rubi.Text);
  potHpItemInput=PotItemSelect(305,potHpItemId);
  PotLabel("Usar quando HP ≤",16,343,160,24,Rubi.Text);
  potHpInput=PotNumber(potHp,190,340);PotLabel("%",266,343,22,24,Rubi.Text);
  PotLabel("Tecla da poção HP",16,381,150,24,Rubi.Text);potHpKeyInput=PotKey(potHpKey,182,378);
  PotLabel("MP",16,424,278,24,Rubi.Accent,10,FontStyle.Bold);
  PotLabel("Item de MP na mochila",16,453,278,20,Rubi.Text);
  potMpItemInput=PotItemSelect(477,potMpItemId);
  PotLabel("Usar quando MP ≤",16,515,164,24,Rubi.Text);
  potMpInput=PotNumber(potMp,190,512);PotLabel("%",266,515,22,24,Rubi.Text);
  PotLabel("Tecla da poção MP",16,551,150,24,Rubi.Text);potMpKeyInput=PotKey(potMpKey,182,548);
  PotLabel("Intervalo mínimo: 8 segundos por recurso.\nSem item na mochila, a tecla não será acionada.",16,590,278,52,Rubi.Muted);
  var save=new NeonButton{Text="Salvar configurações",Primary=true,AccessibleName="Salvar Auto Pot"};save.SetBounds(16,649,278,36);save.Click+=(s,e)=>SaveInlinePot();potDrawer.Controls.Add(save);
  potSaveStatus=PotLabel("Leia a mochila e selecione as poções.",16,692,278,65,Rubi.Muted);
  LayoutInlinePanels();
 }
 Label PotLabel(string text,int x,int y,int w,int h,Color color,float size=9,FontStyle style=FontStyle.Regular){
  var label=new Label{Text=text,Bounds=new Rectangle(x,y,w,h),ForeColor=color,Font=new Font("Segoe UI",size*global::UiScale.F,style)};potDrawer.Controls.Add(label);return label;
 }
 NumericUpDown PotNumber(int value,int x,int y){var control=new NumericUpDown{Minimum=1,Maximum=99,Value=value,Bounds=new Rectangle(x,y,70,27),BackColor=Rubi.Background,ForeColor=Rubi.Text};potDrawer.Controls.Add(control);return control;}
 ComboBox PotKey(Keys value,int x,int y){
  var control=new CyanCombo{DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(x,y,112,27),BackColor=Rubi.Background,ForeColor=Rubi.Text};
  control.FormattingEnabled=true;
  control.Format+=(sender,args)=>{
   if(args.ListItem is Keys){
    Keys key=(Keys)args.ListItem;
    if(key>=Keys.D1&&key<=Keys.D8)args.Value=((int)(key-Keys.D0)).ToString();
    else if(key==Keys.None)args.Value="Desativado";
   }
  };
  control.Items.Add(Keys.None);
  for(Keys key=Keys.D1;key<=Keys.D8;key++)control.Items.Add(key);
  for(Keys key=Keys.F1;key<=Keys.F12;key++)control.Items.Add(key);
  control.SelectedItem=value;potDrawer.Controls.Add(control);return control;
 }
 sealed class PotInventoryChoice {
  public int Id,Quantity;public bool LikelyPotion;public string Kind;
  public override string ToString(){return Id==0?"Sem seleção":"ID "+Id+" · "+Quantity+" un."+ (LikelyPotion?" · provável consumível":"");}
 }
 ComboBox PotItemSelect(int y,int selectedId){
  var control=new CyanCombo{DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(16,y,278,28),BackColor=Rubi.Background,ForeColor=Rubi.Text};
  control.Items.Add(new PotInventoryChoice{Id=0});
  if(selectedId>0)control.Items.Add(new PotInventoryChoice{Id=selectedId});
  control.SelectedIndex=selectedId>0?1:0;potDrawer.Controls.Add(control);return control;
 }
 static int SelectedPotId(ComboBox input){var item=input.SelectedItem as PotInventoryChoice;return item==null?0:item.Id;}
 static bool LikelyPotionType(string type){
  if(String.IsNullOrEmpty(type))return false;
  return type.IndexOf("medicine",StringComparison.OrdinalIgnoreCase)>=0||
         type.IndexOf("potion",StringComparison.OrdinalIgnoreCase)>=0||
         type.IndexOf("drug",StringComparison.OrdinalIgnoreCase)>=0||
         type.IndexOf("elixir",StringComparison.OrdinalIgnoreCase)>=0;
 }
 void FillPotChoices(ComboBox target,List<PotInventoryChoice> available,int previous){
  target.BeginUpdate();try{
   target.Items.Clear();target.Items.Add(new PotInventoryChoice{Id=0});
   foreach(var item in available)target.Items.Add(item);
   if(previous>0&&!available.Any(i=>i.Id==previous))
    target.Items.Add(new PotInventoryChoice{Id=previous,Quantity=0});
   for(int i=0;i<target.Items.Count;i++)if(((PotInventoryChoice)target.Items[i]).Id==previous){target.SelectedIndex=i;break;}
  }finally{target.EndUpdate();}
 }
 async Task ScanPotInventory(){
  var entry=main.SelectedItem as Entry;
  if(entry==null){potSaveStatus.Text="Selecione primeiro o personagem principal.";return;}
  potSaveStatus.Text="Lendo inventário de "+entry.Name+"...";
  try{
   var items=await Task.Run(()=>{
    using(var game=new RotaAvalon.GameClient(entry.Pid,new Dictionary<string,bool>())){
     return game.ReadInventory().Items
       .GroupBy(i=>i.Id)
       .Select(g=>new PotInventoryChoice{
        Id=g.Key,Quantity=g.Sum(i=>i.Quantity),
        Kind=g.First().NativeType,
        LikelyPotion=LikelyPotionType(g.First().NativeType)})
       .OrderByDescending(i=>i.LikelyPotion).ThenBy(i=>i.Id).ToList();
    }
   });
   if(IsDisposed||potHpItemInput==null||potMpItemInput==null)return;
   int hp=SelectedPotId(potHpItemInput),mp=SelectedPotId(potMpItemInput);
   FillPotChoices(potHpItemInput,items,hp);FillPotChoices(potMpItemInput,items,mp);
   int candidates=items.Count(i=>i.LikelyPotion);
   potSaveStatus.Text=items.Count+" tipos de item encontrados ("+candidates+
     " com classe provável de poção). Selecione o ID correto para HP e MP.";
  }catch(Exception ex){if(!IsDisposed)potSaveStatus.Text="Falha ao ler mochila: "+ex.Message;}
 }
 void SaveInlinePot(){
  Keys hp=(Keys)potHpKeyInput.SelectedItem,mp=(Keys)potMpKeyInput.SelectedItem;
  int hpId=SelectedPotId(potHpItemInput),mpId=SelectedPotId(potMpItemInput);
  if(hp!=Keys.None&&hp==mp){potSaveStatus.Text="Escolha teclas distintas para HP e MP.";return;}
  if(hpId>0&&hpId==mpId){potSaveStatus.Text="Escolha itens diferentes para HP e MP.";return;}
  try{
   Directory.CreateDirectory(Path.GetDirectoryName(PotConfigPath));
   File.WriteAllLines(PotConfigPath,new[]{potHpInput.Value.ToString("0"),potMpInput.Value.ToString("0"),((int)hp).ToString(),((int)mp).ToString(),hpId.ToString(),mpId.ToString()});
   potHp=(int)potHpInput.Value;potMp=(int)potMpInput.Value;potHpKey=hp;potMpKey=mp;potHpItemId=hpId;potMpItemId=mpId;
   potSaveStatus.Text="Salvo. Confirme que as poções estão nos atalhos 1 a 8 ou F escolhidos no jogo.";
  }catch(Exception ex){potSaveStatus.Text="Não foi possível salvar: "+ex.Message;}
 }
 void LayoutInlinePanels(){
  if(sharedPotBar==null)return;
  if(rotaView){
   // Overlay the existing Mat Farm canvas instead of resizing or tearing down
   // the route tool. The sidebar switch and settings button remain reachable.
   sharedPotBar.Visible=false;
   potDrawer.Bounds=new Rectangle(Math.Max(SideWidth,ClientSize.Width-310),50,310,ClientSize.Height-50);
   potDrawer.Visible=potDrawerOpen;
   if(potDrawer.Visible)potDrawer.BringToFront();
   return;
  }
  ClientSize=new Size(SideWidth+PanelWidth+(potDrawerOpen?310:0),PanelHeight);
  sharedPotBar.Bounds=new Rectangle(SideWidth,654,PanelWidth,36);sharedPotBar.Visible=true;
  potDrawer.Bounds=new Rectangle(SideWidth+PanelWidth,50,310,PanelHeight-50);potDrawer.Visible=potDrawerOpen;
  if(frostInlineHost!=null)frostInlineHost.Bounds=new Rectangle(SideWidth,50,PanelWidth,604);
  if(autoQuestHost!=null)autoQuestHost.Bounds=new Rectangle(SideWidth,50,PanelWidth,604);
  minimizeButton.Left=ClientSize.Width-69;closeButton.Left=ClientSize.Width-39;
  var area=Screen.FromControl(this).WorkingArea;
  Location=new Point(Math.Max(area.Left,Math.Min(Left,area.Right-Width)),Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)));
  Invalidate(true);
 }
}
