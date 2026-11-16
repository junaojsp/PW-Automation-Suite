using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

// Separate explicit test dialog. The existing Auto Quest diagnostic is unchanged.
// No background gameplay can survive closing the dialog.
sealed class AutoQuestBetaForm : Form {
 readonly int pid;readonly uint characterId;readonly string characterName;
 readonly TextBox xBox,zBox,taskBox,console;
 readonly CheckBox attemptFlight,acknowledge;
 readonly Button start,returnNpc,combat,stop,folder;
 readonly Label state,help;
 AutoQuestBetaSession session;
 bool closingForStop,allowClose;
 public AutoQuestBetaForm(int processId,uint id,string name){
  pid=processId;characterId=id;characterName=name;
  Text="Auto Quest 0.6.11 · teste assistido com log";
  ClientSize=new Size(545,530);
  StartPosition=FormStartPosition.CenterParent;
  FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
  BackColor=Rubi.Background;ForeColor=Rubi.Text;Font=new Font("Segoe UI",9f);
  AddLabel("AUTO QUEST BETA · "+name,18,13,503,26,true);
  AddLabel("Aceite a missão no jogo e confira a região do mob antes de começar.",18,43,510,24);
  AddLabel("Destino X",18,82,85,20);
  xBox=AddInput(106,79,96);
  AddLabel("Destino Z",214,82,75,20);
  zBox=AddInput(295,79,97);
  AddLabel("ID interno*",400,59,130,20);
  taskBox=AddInput(400,79,122);
  attemptFlight=new CheckBox{Text="Modo voo: altitude fixa; se não decolar, PARAR sem caminhar",Left=18,Top=119,
   Width=512,Height=24,Checked=true,ForeColor=Rubi.Text};
  Controls.Add(attemptFlight);
  help=AddLabel("* Opcional. Sem ID, testa só o deslocamento (F12 fica bloqueado).",18,147,510,40);
  help.ForeColor=Rubi.Muted;
  acknowledge=new CheckBox{Text="Confirmo o destino e aceito testar a movimentação no personagem escolhido.",
   Left=18,Top=187,Width=515,Height=28,ForeColor=Rubi.Text};
  Controls.Add(acknowledge);
  start=AddButton("Iniciar teste",18,223,118,34,(s,e)=>StartBeta());
  combat=AddButton("Ligar F12",144,223,110,34,(s,e)=>{
   if(session!=null){session.RequestCombat();Sync();}
  });
  returnNpc=AddButton("Voltar à origem",262,223,142,34,(s,e)=>{
   if(session!=null){session.RequestReturn();Sync();}
  });
  stop=AddButton("Parar",412,223,110,34,async(s,e)=>await StopBeta());
  state=AddLabel("Aguardando início. NPC e entrega sempre manuais.",18,266,506,26,true);
  console=new TextBox{Left=18,Top=299,Width=509,Height=166,Multiline=true,
   ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Rubi.Field,
   ForeColor=Rubi.Text,Font=new Font("Consolas",8f)};
  Controls.Add(console);
  folder=AddButton("Abrir TXT do teste",18,476,190,32,(s,e)=>OpenLog());
  AddLabel("Limite: 15 minutos · trava exclusiva · parada de F12 ao finalizar.",218,479,311,29);
  // Do not prefill legacy cross-map coordinates: they were only route-test
  // points and are NOT verified objectives of the character's current quest.
  help.Text="Destino manual somente para testar navegação. Scanner não identificou missão/objetivo.";
  FormClosing+=OnClosing;
  FormClosed+=(s,e)=>{if(session!=null){session.Dispose();session=null;}};
  Sync();
 }
 Label AddLabel(string text,int x,int y,int w,int h,bool bold=false){
  var l=new Label{Text=text,Left=x,Top=y,Width=w,Height=h,
   ForeColor=Rubi.Text,Font=new Font("Segoe UI",bold?10f:9f,bold?FontStyle.Bold:FontStyle.Regular),
   AutoEllipsis=true};Controls.Add(l);return l;
 }
 TextBox AddInput(int x,int y,int width){
  var t=new TextBox{Left=x,Top=y,Width=width,BackColor=Rubi.Field,
   ForeColor=Rubi.Text};Controls.Add(t);return t;
 }
 Button AddButton(string text,int x,int y,int width,int height,EventHandler callback){
  var b=new NeonButton{Text=text,Left=x,Top=y,Width=width,Height=height};
  b.Click+=callback;Controls.Add(b);return b;
 }
 static bool Number(string text,out double number){
  return Double.TryParse((text??"").Trim().Replace(',','.'),NumberStyles.Float,
   CultureInfo.InvariantCulture,out number);
 }
 void StartBeta(){
  if(session!=null&&session.Running)return;
  if(!acknowledge.Checked){
   state.Text="Marque a confirmação do destino antes do teste.";return;
  }
  double x,z;ushort id=0;
  if(!Number(xBox.Text,out x)||!Number(zBox.Text,out z)||
    !AutoQuestBetaSession.ValidDestination(x,z)){
   state.Text="Destino X/Z inválido (coordenadas do jogo).";return;
  }
  if(!String.IsNullOrWhiteSpace(taskBox.Text)&&!UInt16.TryParse(taskBox.Text.Trim(),out id)){
   state.Text="ID interno inválido; use um número de 1 a 65535.";return;
  }
  try{
   if(session!=null){session.Dispose();session=null;}
   var candidate=new AutoQuestBetaSession(pid,characterId,characterName,x,z,id,attemptFlight.Checked);
   candidate.Message+=Display;
   candidate.Finished+=Finish;
   candidate.Start();session=candidate;
   console.Clear();
   state.Text="Teste ativo; confira a movimentação no jogo.";
   Sync();
  }catch(Exception ex){state.Text="Falha iniciando teste: "+ex.Message;}
 }
 void Display(string line){
  if(IsDisposed||!IsHandleCreated)return;
  try{BeginInvoke((Action)(()=>{
   if(IsDisposed)return;
   console.AppendText(line+Environment.NewLine);
   if(console.TextLength>18000)console.Text=console.Text.Substring(console.TextLength-15000);
   Sync();
  }));}catch(InvalidOperationException){}
 }
 void Finish(){
  if(IsDisposed||!IsHandleCreated)return;
  try{BeginInvoke((Action)(()=>{if(IsDisposed)return;state.Text="Teste encerrado. Abra e envie o TXT gerado.";Sync();}));}
  catch(InvalidOperationException){}
 }
 void Sync(){
  bool running=session!=null&&session.Running;
  int phase=running?session.Phase:4;
  start.Enabled=!running&&!closingForStop;
  xBox.Enabled=zBox.Enabled=taskBox.Enabled=attemptFlight.Enabled=acknowledge.Enabled=!running;
  stop.Enabled=running;
  combat.Enabled=running&&phase==1&&!String.IsNullOrWhiteSpace(taskBox.Text);
  returnNpc.Enabled=running&&(phase==0||phase==1);
  if(running&&phase==1)state.Text="Região alcançada. Verifique os mobs; F12 exige ID ativo.";
  if(running&&phase==2)state.Text="Retornando ao ponto inicial; sem entrega automática.";
  if(running&&phase==3)state.Text="Na origem. Entregue a missão manualmente no NPC.";
 }
 async System.Threading.Tasks.Task StopBeta(){
  var current=session;if(current==null)return;
  stop.Enabled=false;
  state.Text="Interrompendo o teste e liberando a trava...";
  try{await current.StopAsync();state.Text="Teste parado e salvo. Abra o TXT.";}
  catch(Exception ex){state.Text="Falha ao parar: "+ex.Message;}
  Sync();
 }
 async void OnClosing(object sender,FormClosingEventArgs e){
  if(allowClose||session==null||!session.Running)return;
  e.Cancel=true;
  if(closingForStop)return;
  closingForStop=true;
  await StopBeta();
  allowClose=true;closingForStop=false;
  Close();
 }
 void OpenLog(){
  try{
   string file=session==null?null:session.FilePath;
   if(!String.IsNullOrWhiteSpace(file)&&File.Exists(file))
    Process.Start("explorer.exe","/select,\""+file+"\"");
   else {
    string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
     "FarmZillaClassic","diagnosticos");
    Directory.CreateDirectory(dir);Process.Start("explorer.exe",dir);
   }
  }catch(Exception ex){MessageBox.Show(this,ex.Message,"Abrir TXT",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
 }
}
