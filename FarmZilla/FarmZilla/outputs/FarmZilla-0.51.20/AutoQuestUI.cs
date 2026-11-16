using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

// Auto Quest: interface-only preparation. No mission/game commands are sent.
partial class AvalonInterno {
 Panel autoQuestHost;bool autoQuestView;SideItem sideAutoQuest;
 ComboBox autoQuestCharacters;Label autoQuestStatus,autoQuestResult;Button autoQuestInspect;
 bool autoQuestReading;
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
  autoQuestStatus=QuestLabel("Aguardando leitura · nenhuma missão é executada",12,33,340,20);
  status.Controls.Add(autoQuestStatus);
  var characters=QuestCard(153,122);surface.Controls.Add(characters);
  characters.Controls.Add(QuestLabel("PERSONAGEM E MISSÃO",12,9,330,20,true));
  characters.Controls.Add(QuestLabel("Personagem",12,40,105,22));
  var character=new ComboBox{Bounds=new Rectangle(122,38,232,26),DropDownStyle=ComboBoxStyle.DropDownList,
   BackColor=Rubi.Field,ForeColor=Rubi.Text,Enabled=false};
  autoQuestCharacters=character;
  character.Items.Add("Selecione no Espelho");character.SelectedIndex=0;characters.Controls.Add(character);
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
  var start=new NeonButton{Text="Analisar personagem",Bounds=new Rectangle(12,37,166,35),Enabled=true};
  autoQuestInspect=start;start.Click+=async(sender,args)=>await ReadAutoQuestSnapshot();
  var stop=new NeonButton{Text="Parar",Bounds=new Rectangle(188,37,166,35),Enabled=false};
  commands.Controls.Add(start);commands.Controls.Add(stop);
  autoQuestResult=QuestLabel("Somente leitura · não aceita ou entrega missões.",14,575,358,26);
  surface.Controls.Add(autoQuestResult);
  return surface;
 }
 void OpenAutoQuest(){
  if(closeInProgress||authorizationDenied||controlLocked)return;
  // Always restore the mirror first so Frost, Mat Farm and Auto Quest cannot overlap.
  ShowEspelhoInline();
  EnsureAutoQuest();
  RefreshAutoQuestCharacters();
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
 void RefreshAutoQuestCharacters(){
  if(autoQuestCharacters==null||autoQuestReading)return;
  var old=autoQuestCharacters.SelectedItem as Entry;
  var options=main.Items.Cast<object>().OfType<Entry>().Where(x=>!blocked.Contains(x.Pid)).ToArray();
  autoQuestCharacters.BeginUpdate();
  try{
   autoQuestCharacters.Items.Clear();
   foreach(var choice in options)autoQuestCharacters.Items.Add(choice);
   autoQuestCharacters.Enabled=options.Length>0;
   if(options.Length>0){
    var selected=options.FirstOrDefault(x=>old!=null&&x.Pid==old.Pid)??options.FirstOrDefault(x=>ReferenceEquals(x,main.SelectedItem))??options[0];
    autoQuestCharacters.SelectedItem=selected;
   }else{autoQuestCharacters.Items.Add("Sincronize os personagens no Espelho");autoQuestCharacters.SelectedIndex=0;}
  }finally{autoQuestCharacters.EndUpdate();}
  if(autoQuestInspect!=null)autoQuestInspect.Enabled=options.Length>0;
 }
 sealed class AutoQuestSnapshot {
  public string Name;public int Level,MonsterCount;public float X,Y,Z;
  public ushort[] Active;
 }
 static AutoQuestSnapshot InspectAutoQuest(int pid,uint expectedId,string expectedName){
  using(var memory=new CharacterMemory(pid)){
   if(memory.Id!=expectedId||memory.Name!=expectedName)throw new InvalidOperationException("Personagem mudou. Sincronize novamente.");
   ushort[] active=memory.ActiveQuestIds();
   using(var game=new RotaAvalon.GameClient(pid,new Dictionary<string,bool>())){
    var info=game.Read();
    if(info.Name!=expectedName)throw new InvalidOperationException("Identidade do personagem inconsistente.");
    int monsters=-1;
    try{monsters=game.ReadMonsters(info).Count;}catch{ /* Optional read: unknown client layout must not fail quest listing. */ }
    return new AutoQuestSnapshot{Name=info.Name,Level=info.Level,X=info.X,Y=info.Y,Z=info.Z,Active=active,MonsterCount=monsters};
   }
  }
 }
 async Task ReadAutoQuestSnapshot(){
  if(autoQuestReading||!autoQuestView||authorizationDenied||controlLocked||closeInProgress)return;
  var entry=autoQuestCharacters.SelectedItem as Entry;
  if(entry==null||blocked.Contains(entry.Pid)||RotaLock.Busy(entry.Pid)){
   autoQuestStatus.Text="Selecione um personagem livre e sincronizado.";return;
  }
  autoQuestReading=true;autoQuestInspect.Enabled=false;autoQuestCharacters.Enabled=false;
  autoQuestStatus.Text="Lendo posição e missões ativas...";
  try{
   var found=await Task.Run(()=>InspectAutoQuest(entry.Pid,entry.Id,entry.Name));
   if(IsDisposed||!autoQuestView)return;
   autoQuestStatus.Text=found.Name+" · nível "+found.Level+" · "+found.Active.Length+" missão(ões) ativa(s)";
   autoQuestResult.Text="Posição: "+found.X.ToString("0")+" / "+found.Z.ToString("0")+" · IDs "+(found.Active.Length==0?"nenhum":String.Join(", ",found.Active.Take(5)))+(found.Active.Length>5?" …":"");
   hints.SetToolTip(autoQuestResult,"Missões ativas: "+String.Join(", ",found.Active)+"; coordenadas X/Y/Z: "+found.X+", "+found.Y+", "+found.Z+"; monstros próximos: "+(found.MonsterCount<0?"leitura indisponível":found.MonsterCount.ToString())+". Não identifica missões disponíveis em NPCs ainda.");
   DiagnosticLog.Write("AutoQuest somente leitura: "+entry.Name+" quests="+found.Active.Length+" pos="+found.X+","+found.Z);
  }catch(Exception ex){
   if(!IsDisposed&&autoQuestView){autoQuestStatus.Text="Não foi possível ler o personagem.";autoQuestResult.Text="Erro: "+ex.Message;}
   DiagnosticLog.Write("AutoQuest leitura: "+ex.Message);
  }finally{
   autoQuestReading=false;
   if(!IsDisposed&&autoQuestInspect!=null){autoQuestInspect.Enabled=autoQuestCharacters.SelectedItem is Entry;autoQuestCharacters.Enabled=autoQuestInspect.Enabled;}
  }
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
