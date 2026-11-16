using System;
using System.Drawing;
using System.Windows.Forms;

// Auto Quest: interface-only preparation. No mission/game commands are sent.
partial class AvalonInterno {
 Panel autoQuestHost;bool autoQuestView;SideItem sideAutoQuest;
 void EnsureAutoQuest(){
  if(autoQuestHost!=null&&!autoQuestHost.IsDisposed)return;
  autoQuestHost=new Panel{BackColor=Rubi.Background,Visible=false};
  autoQuestHost.Controls.Add(BuildAutoQuestScreen());
  Controls.Add(autoQuestHost);
 }
 static Label QuestLabel(string title,int x,int y,int width,int height,bool strong=false){
  return new Label{Text=title,Bounds=new Rectangle(x,y,width,height),
   Font=new Font("Segoe UI",strong?10f:9f,strong?FontStyle.Bold:FontStyle.Regular),
   ForeColor=strong?Rubi.Text:Rubi.Muted,BackColor=Color.Transparent,AutoEllipsis=true};
 }
 static Panel QuestCard(int top,int height){
  return new Panel{Bounds=new Rectangle(12,top,366,height),BackColor=Rubi.Panel};
 }
 Panel BuildAutoQuestScreen(){
  var surface=new Panel{Dock=DockStyle.Fill,BackColor=Rubi.Background};
  surface.Controls.Add(QuestLabel("AUTO QUEST",15,16,300,28,true));
  surface.Controls.Add(QuestLabel("Missões automáticas · estrutura inicial",15,47,355,20));
  var status=QuestCard(82,60);surface.Controls.Add(status);
  status.Controls.Add(QuestLabel("STATUS DO MÓDULO",12,8,330,20,true));
  status.Controls.Add(QuestLabel("Em preparação · nenhuma missão é executada",12,33,340,20));
  var characters=QuestCard(153,122);surface.Controls.Add(characters);
  characters.Controls.Add(QuestLabel("PERSONAGEM E MISSÃO",12,9,330,20,true));
  characters.Controls.Add(QuestLabel("Personagem",12,40,105,22));
  var character=new ComboBox{Bounds=new Rectangle(122,38,232,26),DropDownStyle=ComboBoxStyle.DropDownList,
   BackColor=Rubi.Field,ForeColor=Rubi.Text,Enabled=false};
  character.Items.Add("Integração pendente");character.SelectedIndex=0;characters.Controls.Add(character);
  characters.Controls.Add(QuestLabel("Tipo de missão",12,79,105,22));
  var type=new ComboBox{Bounds=new Rectangle(122,77,232,26),DropDownStyle=ComboBoxStyle.DropDownList,
   BackColor=Rubi.Field,ForeColor=Rubi.Text,Enabled=false};
  type.Items.AddRange(new object[]{"Eliminar monstros","Coletar itens","Interagir com NPC","Cadeia de missões"});
  type.SelectedIndex=0;characters.Controls.Add(type);
  var tasks=QuestCard(286,163);surface.Controls.Add(tasks);
  tasks.Controls.Add(QuestLabel("ETAPAS PLANEJADAS",12,10,332,20,true));
  var steps=new[]{"Identificar e aceitar missão","Localizar objetivo e navegar","Executar combate ou coleta","Conferir progresso da missão","Retornar ao NPC e entregar"};
  for(int i=0;i<steps.Length;i++)tasks.Controls.Add(new CheckBox{
   Text=steps[i],Bounds=new Rectangle(15,36+i*24,333,21),Checked=false,Enabled=false,
   FlatStyle=FlatStyle.Flat,BackColor=Rubi.Panel,ForeColor=Rubi.Muted});
  var commands=QuestCard(460,106);surface.Controls.Add(commands);
  commands.Controls.Add(QuestLabel("CONTROLES",12,9,325,20,true));
  var start=new NeonButton{Text="Iniciar Auto Quest",Bounds=new Rectangle(12,37,166,35),Enabled=false};
  var stop=new NeonButton{Text="Parar",Bounds=new Rectangle(188,37,166,35),Enabled=false};
  commands.Controls.Add(start);commands.Controls.Add(stop);
  surface.Controls.Add(QuestLabel("Integração pendente · sem comandos de missão nesta PR.",14,579,358,22));
  return surface;
 }
 void OpenAutoQuest(){
  if(closeInProgress||authorizationDenied||controlLocked)return;
  // Always restore the mirror first so Frost, Mat Farm and Auto Quest cannot overlap.
  ShowEspelhoInline();
  EnsureAutoQuest();
  panelVisible.Clear();
  foreach(var c in panelBody){panelVisible[c]=c.Visible;c.Visible=false;}
  autoQuestView=true;
  sidePersonagens.Selected=false;sideRota.Selected=false;
  if(sideFrostInline!=null)sideFrostInline.Selected=false;
  if(sideAutoQuest!=null)sideAutoQuest.Selected=true;
  LayoutInlinePanels();
  autoQuestHost.Visible=true;autoQuestHost.BringToFront();
  if(sharedPotBar!=null)sharedPotBar.BringToFront();
  if(potDrawer!=null&&potDrawer.Visible)potDrawer.BringToFront();
  Invalidate(true);
 }
 void CloseAutoQuest(){
  if(!autoQuestView)return;
  autoQuestView=false;
  if(autoQuestHost!=null)autoQuestHost.Visible=false;
  foreach(var kv in panelVisible)kv.Key.Visible=kv.Value;
  panelVisible.Clear();
  if(sideAutoQuest!=null)sideAutoQuest.Selected=false;
  UpdateAttackRows();
 }
}
