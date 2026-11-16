using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

// Auto Quest: diagnóstico somente leitura e beta assistida em diálogo separado.
// A beta envia somente movimento e F12 por consentimento, nunca NPC/entrega.
partial class AvalonInterno {
 Panel autoQuestHost;bool autoQuestView;SideItem sideAutoQuest;
 ComboBox autoQuestCharacters;Label autoQuestStatus,autoQuestResult;Button autoQuestInspect,autoQuestRecord,autoQuestStop,autoQuestOpenFolder,autoQuestBeta,autoQuestCity;
 AutoQuestDiagnostic questDiagnostic;string questLastLog;
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
  surface.Controls.Add(QuestLabel("Missões por região · leitura do jogo e catálogo local",15,47,355,20));
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
  var tasks=QuestCard(286,189);surface.Controls.Add(tasks);
  tasks.Controls.Add(QuestLabel("ETAPAS PLANEJADAS",12,10,332,20,true));
  var steps=new[]{"Identificar e aceitar missão","Localizar objetivo e navegar","Executar combate ou coleta","Conferir progresso da missão","Retornar ao NPC e entregar"};
  for(int i=0;i<steps.Length;i++)tasks.Controls.Add(new CheckBox{
   Text=steps[i],Bounds=new Rectangle(15,36+i*24,333,21),Checked=false,Enabled=false,
   FlatStyle=FlatStyle.Flat,BackColor=Rubi.Panel,ForeColor=Rubi.Muted});
  autoQuestBeta=new NeonButton{Text="Teste beta com log",Bounds=new Rectangle(15,157,333,29)};
  autoQuestBeta.Click+=(sender,args)=>OpenAutoQuestBeta();
  tasks.Controls.Add(autoQuestBeta);
  var commands=QuestCard(484,151);surface.Controls.Add(commands);
  commands.Controls.Add(QuestLabel("CONTROLES",12,9,325,20,true));
  var start=new NeonButton{Text="Analisar",Bounds=new Rectangle(12,34,166,30),Enabled=true};
  autoQuestInspect=start;start.Click+=async(sender,args)=>await ReadAutoQuestSnapshot();
  autoQuestRecord=new NeonButton{Text="Gravar diagnóstico",Bounds=new Rectangle(188,34,166,30)};
  autoQuestRecord.Click+=(sender,args)=>StartAutoQuestDiagnostic();
  autoQuestStop=new NeonButton{Text="Parar gravação",Bounds=new Rectangle(12,69,166,30),Enabled=false};
  autoQuestStop.Click+=async(sender,args)=>await StopAutoQuestDiagnostic();
  autoQuestOpenFolder=new NeonButton{Text="Abrir pasta do log",Bounds=new Rectangle(188,69,166,30)};
  autoQuestOpenFolder.Click+=(sender,args)=>OpenAutoQuestLog();
  autoQuestCity=new NeonButton{Text="Escanear missões atuais e NPCs",Bounds=new Rectangle(12,105,342,30)};
  autoQuestCity.Click+=async(sender,args)=>await ScanCurrentCityQuests();
  commands.Controls.Add(start);commands.Controls.Add(autoQuestRecord);
  commands.Controls.Add(autoQuestStop);commands.Controls.Add(autoQuestOpenFolder);
  commands.Controls.Add(autoQuestCity);
  autoQuestResult=QuestLabel("Scanner: NPCs carregados e catálogo de missões local.",14,642,358,29);
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
  if(autoQuestCharacters==null||autoQuestReading||(questDiagnostic!=null&&questDiagnostic.Running))return;
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
  if(autoQuestRecord!=null)autoQuestRecord.Enabled=options.Length>0;
  if(autoQuestBeta!=null)autoQuestBeta.Enabled=options.Length>0;
  if(autoQuestCity!=null)autoQuestCity.Enabled=options.Length>0;
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
   autoQuestStatus.Text=found.Name+" · nível "+found.Level+" · "+found.Active.Length+" IDs internos de missões";
   autoQuestResult.Text="Posição: "+found.X.ToString("0")+" / "+found.Z.ToString("0")+" · IDs "+(found.Active.Length==0?"nenhum":String.Join(", ",found.Active.Take(5)))+(found.Active.Length>5?" …":"");
   hints.SetToolTip(autoQuestResult,"Registros internos (não correspondem necessariamente às missões iniciadas): "+String.Join(", ",found.Active)+"; coordenadas X/Y/Z: "+found.X+", "+found.Y+", "+found.Z+"; monstros próximos: "+(found.MonsterCount<0?"leitura indisponível":found.MonsterCount.ToString())+". Não identifica missões disponíveis em NPCs ainda.");
   DiagnosticLog.Write("AutoQuest somente leitura: "+entry.Name+" quests="+found.Active.Length+" pos="+found.X+","+found.Z);
  }catch(Exception ex){
   if(!IsDisposed&&autoQuestView){autoQuestStatus.Text="Não foi possível ler o personagem.";autoQuestResult.Text="Erro: "+ex.Message;}
   DiagnosticLog.Write("AutoQuest leitura: "+ex.Message);
  }finally{
   autoQuestReading=false;
   if(!IsDisposed&&autoQuestInspect!=null){autoQuestInspect.Enabled=autoQuestCharacters.SelectedItem is Entry;autoQuestCharacters.Enabled=autoQuestInspect.Enabled;}
  }
 }
 void StartAutoQuestDiagnostic(){
  if(closeInProgress||authorizationDenied||controlLocked||autoQuestReading)return;
  if(questDiagnostic!=null&&questDiagnostic.Running)return;
  var entry=autoQuestCharacters.SelectedItem as Entry;
  if(entry==null||blocked.Contains(entry.Pid)||RotaLock.Busy(entry.Pid)){
   autoQuestStatus.Text="Selecione um personagem disponível no Espelho.";return;
  }
  try{
   if(questDiagnostic!=null){questDiagnostic.Dispose();questDiagnostic=null;}
   var session=new AutoQuestDiagnostic(entry.Pid,entry.Id,entry.Name);session.Start();
   questDiagnostic=session;questLastLog=session.FilePath;
   autoQuestCharacters.Enabled=false;autoQuestInspect.Enabled=false;autoQuestRecord.Enabled=false;
   if(autoQuestCity!=null)autoQuestCity.Enabled=false;
   autoQuestStop.Enabled=true;
   autoQuestStatus.Text="Gravando diagnóstico · até 30 minutos";
   autoQuestResult.Text="Parar gravação salva o TXT a qualquer momento.";
   hints.SetToolTip(autoQuestResult,questLastLog);
  }catch(Exception ex){autoQuestStatus.Text="Falha ao iniciar diagnóstico.";autoQuestResult.Text=ex.Message;}
 }
 async Task StopAutoQuestDiagnostic(){
  var session=questDiagnostic;if(session==null)return;
  if(autoQuestStop!=null)autoQuestStop.Enabled=false;
  try{
   await session.StopAsync();
   questLastLog=session.FilePath;
   if(!IsDisposed){
    autoQuestStatus.Text="Diagnóstico salvo no computador.";
    autoQuestResult.Text="Arquivo: "+Path.GetFileName(questLastLog);
    hints.SetToolTip(autoQuestResult,questLastLog);
   }
  }catch(Exception ex){if(!IsDisposed){autoQuestStatus.Text="Falha ao finalizar o diagnóstico.";autoQuestResult.Text=ex.Message;}}
  finally{
   session.Dispose();if(ReferenceEquals(questDiagnostic,session))questDiagnostic=null;
   if(!IsDisposed&&autoQuestCharacters!=null){
    autoQuestCharacters.Enabled=autoQuestCharacters.SelectedItem is Entry;
    if(autoQuestInspect!=null)autoQuestInspect.Enabled=autoQuestCharacters.Enabled;
    if(autoQuestRecord!=null)autoQuestRecord.Enabled=autoQuestCharacters.Enabled;
    if(autoQuestCity!=null)autoQuestCity.Enabled=autoQuestCharacters.Enabled;
   }
  }
 }
 async Task ScanCurrentCityQuests(){
  if(autoQuestReading||!autoQuestView||closeInProgress||authorizationDenied||controlLocked)return;
  if(questDiagnostic!=null&&questDiagnostic.Running){
   autoQuestStatus.Text="Pare o diagnóstico antes de escanear a cidade.";return;
  }
  var entry=autoQuestCharacters==null?null:autoQuestCharacters.SelectedItem as Entry;
  if(entry==null||blocked.Contains(entry.Pid)||RotaLock.Busy(entry.Pid)){
   autoQuestStatus.Text="Selecione um personagem livre e sincronizado.";return;
  }
  autoQuestReading=true;autoQuestCity.Enabled=false;autoQuestInspect.Enabled=false;
  autoQuestCharacters.Enabled=false;
  autoQuestStatus.Text="Escaneando NPCs carregados e tasks.data...";
  try{
   var snapshot=await Task.Run(()=>AutoQuestCityScan.Capture(entry.Pid,entry.Id,entry.Name));
   if(IsDisposed)return;
   questLastLog=snapshot.FilePath;
   autoQuestStatus.Text="Scanner local · "+snapshot.Frame.Npcs.Count+
    " NPCs candidatos · "+snapshot.ActiveCount+" IDs internos";
   autoQuestResult.Text="Títulos: "+snapshot.MatchedTitles+" · NPCs próximos: "+snapshot.Frame.Npcs.Count+
    " · destino de missão: NÃO VALIDADO · TXT";
   hints.SetToolTip(autoQuestResult,"Setor: "+snapshot.ZoneKey+"; arquivo: "+snapshot.FilePath+
    "; NPC próximo não significa missão disponível. Sem objetivo validado, não voa nem caminha.");
  }catch(Exception ex){
   if(!IsDisposed){
    autoQuestStatus.Text="Falha no escaneamento da região.";
    autoQuestResult.Text="Detalhes: "+ex.Message;
   }
   DiagnosticLog.Write("AutoQuest city scan: "+ex.Message);
  }finally{
   autoQuestReading=false;
   if(!IsDisposed){
    bool ready=autoQuestCharacters.SelectedItem is Entry;
    autoQuestCharacters.Enabled=ready;
    autoQuestInspect.Enabled=ready;
    if(autoQuestCity!=null)autoQuestCity.Enabled=ready;
   }
  }
 }
 void OpenAutoQuestBeta(){
  if(closeInProgress||authorizationDenied||controlLocked||autoQuestReading)return;
  if(questDiagnostic!=null&&questDiagnostic.Running){
   autoQuestStatus.Text="Pare a gravação antes do teste beta.";return;
  }
  var entry=autoQuestCharacters==null?null:autoQuestCharacters.SelectedItem as Entry;
  if(entry==null||blocked.Contains(entry.Pid)||RotaLock.Busy(entry.Pid)){
   autoQuestStatus.Text="Personagem ocupado; escolha um personagem livre.";return;
  }
  if(busy||missions.Working||teleport.Working||altoOn||altoBusy){
   autoQuestStatus.Text="Desligue seguimento, Alto Cultivo, teleporte e ações ativas antes do teste.";
   return;
  }
  // The beta's RotaLock lease excludes other routes and most mirror/Frost operations.
  // It is launched only on an explicit click and stops before the dialog closes.
  try{
   using(var dialog=new AutoQuestBetaForm(entry.Pid,entry.Id,entry.Name))
    dialog.ShowDialog(this);
   autoQuestStatus.Text="Teste beta finalizado. Confira o TXT local.";
   RefreshAutoQuestCharacters();
  }catch(Exception ex){
   autoQuestStatus.Text="Teste beta não iniciado.";
   autoQuestResult.Text=ex.Message;
  }
 }
 void OpenAutoQuestLog(){
  try{
   string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FarmZillaClassic","diagnosticos");
   Directory.CreateDirectory(folder);
   if(!String.IsNullOrWhiteSpace(questLastLog)&&File.Exists(questLastLog))
    Process.Start("explorer.exe","/select,\""+questLastLog+"\"");
   else Process.Start("explorer.exe",folder);
  }catch(Exception ex){MessageBox.Show(this,ex.Message,"Abrir diagnóstico",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
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
