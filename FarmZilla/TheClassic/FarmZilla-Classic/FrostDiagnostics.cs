using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using RotaAvalon;

// FarmZilla-native Frost Farm: coordinate capture, catalog of already-validated packets, and Mat Farm trail export.
// Nothing here is taken from a third-party protected executable.
static class FrostCapture {
 public static string Header="utc,pid,name,x,y,z,map_x,map_z,map_height,hp,max_hp";
 static string N(double value){return value.ToString("R",CultureInfo.InvariantCulture);}
 public static string Row(ClientSnapshot s){
  if(s==null||s.Pid<=0||!GameClient.ValidCoordinate(s.X)||!GameClient.ValidCoordinate(s.Y)||!GameClient.ValidCoordinate(s.Z))throw new InvalidDataException("Amostra Frost inválida.");
  return String.Join(",",new[]{s.ReadAt.ToUniversalTime().ToString("o"),s.Pid.ToString(CultureInfo.InvariantCulture),"\""+(s.Name??"").Replace("\"","\"\"")+"\"",N(s.X),N(s.Y),N(s.Z),N(s.MapX),N(s.MapZ),N(s.MapHeight),s.Hp.ToString(CultureInfo.InvariantCulture),s.MaxHp.ToString(CultureInfo.InvariantCulture)});
 }
 public sealed class Sample {
  public DateTime Utc;public int Pid;public string Name;public double X,Y,Z,MapX,MapZ,MapHeight;public int Hp,MaxHp;
 }
 public static List<string> SplitCsv(string line){
  if(line==null)throw new InvalidDataException("Linha CSV ausente.");
  var fields=new List<string>();var current=new StringBuilder();bool quoted=false;
  for(int i=0;i<line.Length;i++){
   char c=line[i];
   if(quoted){
    if(c=='"'){if(i+1<line.Length&&line[i+1]=='"'){current.Append('"');i++;}else quoted=false;}
    else current.Append(c);
   }else if(c=='"')quoted=true;
   else if(c==','){fields.Add(current.ToString());current.Length=0;}
   else current.Append(c);
  }
  fields.Add(current.ToString());
  return fields;
 }
 public static Sample ParseRow(string line){
  var fields=SplitCsv(line);
  if(fields.Count!=11)throw new InvalidDataException("Linha Frost com colunas a menos ou a mais.");
  var sample=new Sample();
  if(!DateTime.TryParse(fields[0],CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out sample.Utc))throw new InvalidDataException("Horário UTC inválido.");
  if(!Int32.TryParse(fields[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out sample.Pid)||sample.Pid<=0)throw new InvalidDataException("PID inválido.");
  sample.Name=fields[2];
  sample.X=Number(fields[3]);sample.Y=Number(fields[4]);sample.Z=Number(fields[5]);
  sample.MapX=Number(fields[6]);sample.MapZ=Number(fields[7]);sample.MapHeight=Number(fields[8]);
  if(!Int32.TryParse(fields[9],NumberStyles.Integer,CultureInfo.InvariantCulture,out sample.Hp)||!Int32.TryParse(fields[10],NumberStyles.Integer,CultureInfo.InvariantCulture,out sample.MaxHp))throw new InvalidDataException("HP inválido.");
  if(!GameClient.ValidCoordinate(sample.X)||!GameClient.ValidCoordinate(sample.Y)||!GameClient.ValidCoordinate(sample.Z))throw new InvalidDataException("Coordenada inválida.");
  return sample;
 }
 static double Number(string text){
  double value;if(!Double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||Double.IsNaN(value)||Double.IsInfinity(value))throw new InvalidDataException("Número inválido.");
  return value;
 }
 public static List<Sample> ParseCsv(string text){
  if(String.IsNullOrWhiteSpace(text))throw new InvalidDataException("Captura Frost vazia.");
  var samples=new List<Sample>();
  using(var reader=new StringReader(text)){
   string line=reader.ReadLine();
   if(line==null)throw new InvalidDataException("Captura Frost vazia.");
   if(!line.Trim().Equals(Header,StringComparison.Ordinal))throw new InvalidDataException("Cabeçalho Frost inesperado.");
   while((line=reader.ReadLine())!=null){if(line.Length==0)continue;samples.Add(ParseRow(line));}
  }
  return samples;
 }
 public static TrailRoute ToTrail(IList<Sample> samples,string name){
  if(samples==null||samples.Count<2)throw new InvalidDataException("A captura precisa de pelo menos 2 amostras.");
  var trail=new TrailRoute{Name=String.IsNullOrWhiteSpace(name)?"frost":name.Trim(),Character=samples[0].Name??"",Reach=50};
  var recorder=new TrailRecorder(trail);
  foreach(var sample in samples){
   if(!GameClient.ValidCoordinate(sample.X)||!GameClient.ValidCoordinate(sample.Y)||!GameClient.ValidCoordinate(sample.Z))continue;
   recorder.Offer(sample.X,sample.Y,sample.Z,true);
  }
  var last=samples[samples.Count-1];
  if(trail.Path.Count==1){
   var first=trail.Path[0];
   if(Math.Abs(first.X-last.X)>=0.01||Math.Abs(first.Z-last.Z)>=0.01||Math.Abs(first.Y-last.Y)>=0.01)
    trail.Path.Add(new TrailPoint{X=last.X,Y=last.Y,Z=last.Z,Flying=true});
  }
  if(trail.Path.Count<2)throw new InvalidDataException("A captura não se moveu o bastante para um trajeto.");
  if(trail.Path.Count>20000)throw new InvalidDataException("Trajeto com mais de 20 000 pontos.");
  return trail;
 }
}

sealed class FrostDiagnosticsForm : Form {
 sealed class Character {public int Pid;public string Name;public override string ToString(){return Name+" · PID "+Pid;}}
 readonly ComboBox leader=new ComboBox();readonly TextBox log=new TextBox();readonly Label state=new Label();
 readonly Button start=new NeonButton(),refresh=new NeonButton(),exportTrail=new NeonButton();readonly Timer timer=new Timer();
 readonly Func<bool> allowed;GameClient client;StreamWriter capture;string capturePath;int samples;bool active;
 readonly List<FrostCapture.Sample> session=new List<FrostCapture.Sample>();
 public FrostDiagnosticsForm(Func<bool> allowed,bool discoverOnShow=true){
  this.allowed=allowed;Text="FarmZilla · Frost Farm";ClientSize=new Size(450,640);MinimumSize=Size;BackColor=Rubi.Background;ForeColor=Rubi.Text;StartPosition=FormStartPosition.CenterParent;
  Font=new Font("Segoe UI",9);Padding=new Padding(8);
  var tabs=new TabControl{Dock=DockStyle.Fill};
  tabs.TabPages.Add(BuildCapturePage());tabs.TabPages.Add(BuildCommandsPage());
  Controls.Add(tabs);
  timer.Interval=1000;timer.Tick+=(s,e)=>Sample();Shown+=(s,e)=>{if(discoverOnShow)RefreshCharacters();};FormClosing+=(s,e)=>Stop("Janela Frost fechada.");
 }
 TabPage BuildCapturePage(){
  var page=new TabPage("Captura e trajeto"){BackColor=Rubi.Background,ForeColor=Rubi.Text,Padding=new Padding(8)};
  var top=new Panel{Dock=DockStyle.Top,Height=258,BackColor=Rubi.Background};
  LabelOn(top,"FROST FARM · THE CLASSIC",4,4,400,22,Rubi.Accent);
  LabelOn(top,"Captura coordenadas no leitor do jogo e gera trajeto do Mat Farm. Não envia pacotes sozinho.",4,28,400,36,Rubi.Muted);
  LabelOn(top,"Personagem para capturar a rota:",4,66,400,18,Rubi.Text);
  leader.SetBounds(4,86,284,28);leader.DropDownStyle=ComboBoxStyle.DropDownList;leader.BackColor=Rubi.Field;leader.ForeColor=Rubi.Text;top.Controls.Add(leader);
  refresh.Text="Atualizar";refresh.SetBounds(294,84,110,30);refresh.Click+=(s,e)=>RefreshCharacters();top.Controls.Add(refresh);
  start.Text="Iniciar captura de coordenadas";start.SetBounds(4,124,400,32);start.Click+=(s,e)=>{if(active)Stop("Captura encerrada por você.");else Start();};top.Controls.Add(start);
  exportTrail.Text="Gerar trajeto Mat Farm";exportTrail.SetBounds(4,160,400,30);exportTrail.Enabled=false;exportTrail.Click+=(s,e)=>ExportTrail();top.Controls.Add(exportTrail);
  state.SetBounds(4,196,400,52);state.Text="Parado. Mova o personagem; depois gere o trajeto e carregue-o no Mat Farm.";top.Controls.Add(state);
  var folder=new NeonButton{Text="Abrir pasta das capturas",Dock=DockStyle.Bottom,Height=32};folder.Click+=(s,e)=>{try{Directory.CreateDirectory(Folder);Process.Start("explorer.exe",Folder);}catch(Exception ex){Write("PASTA_ERRO "+ex.Message);}};
  log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BackColor=Rubi.Panel;log.ForeColor=Rubi.Text;log.Dock=DockStyle.Fill;log.Margin=new Padding(0);
  page.Controls.Add(log);page.Controls.Add(folder);page.Controls.Add(top);
  return page;
 }
 TabPage BuildCommandsPage(){
  var page=new TabPage("Comandos do jogo"){BackColor=Rubi.Background,ForeColor=Rubi.Text,Padding=new Padding(8)};
  var top=new Panel{Dock=DockStyle.Top,Height=76,BackColor=Rubi.Background};
  LabelOn(top,"CATÁLOGO DO FARMZILLA",4,4,400,22,Rubi.Accent);
  LabelOn(top,FrostCommand.Summary()+" O Frost lista; Espelho e Mat Farm executam.",4,28,400,40,Rubi.Muted);
  var list=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HeaderStyle=ColumnHeaderStyle.Nonclickable,BackColor=Rubi.Panel,ForeColor=Rubi.Text,ShowItemToolTips=true};
  list.Columns.Add("Comando",150);list.Columns.Add("Código",70);list.Columns.Add("Pacote",170);
  foreach(var command in FrostCommand.Catalog){
   var row=new ListViewItem(command.Name);row.SubItems.Add(command.Code);row.SubItems.Add(command.Packet);row.ToolTipText=command.Use;list.Items.Add(row);
  }
  page.Controls.Add(list);page.Controls.Add(top);
  return page;
 }
 static string Folder{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FarmZillaClassic","Frost");}}
 static Label LabelOn(Control parent,string text,int x,int y,int w,int h,Color color){var l=new Label{Text=text,ForeColor=color,Bounds=new Rectangle(x,y,w,h)};parent.Controls.Add(l);return l;}
 void Write(string message){DiagnosticLog.Write("Frost: "+message);if(log.TextLength>30000)log.Clear();log.AppendText(DateTime.Now.ToString("HH:mm:ss")+" "+message+Environment.NewLine);}
 void RefreshCharacters(){
  if(active)return;leader.Items.Clear();int failures=0;
  foreach(var process in Process.GetProcessesByName("ElementClient")){using(process){try{using(var game=new GameClient(process.Id,new Dictionary<string,bool>())){var snapshot=game.Read();leader.Items.Add(new Character{Pid=snapshot.Pid,Name=snapshot.Name});}}catch(Exception ex){failures++;Write("CLIENTE_ERRO pid="+process.Id+" "+ex.GetType().Name+": "+ex.Message);}}}
  if(leader.Items.Count>0)leader.SelectedIndex=0;Write("DESCOBERTA válidos="+leader.Items.Count+" falhas="+failures+" "+FrostCommand.Summary());
 }
 void Start(){
  if(!allowed()){Write("CAPTURA_RECUSADA sessão indisponível.");return;}var chosen=leader.SelectedItem as Character;if(chosen==null){Write("Selecione um personagem carregado.");return;}
  try{
   client=new GameClient(chosen.Pid,new Dictionary<string,bool>());var snapshot=client.Read();Directory.CreateDirectory(Folder);capturePath=Path.Combine(Folder,"frost-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".csv");
   capture=new StreamWriter(new FileStream(capturePath,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(true));capture.WriteLine(FrostCapture.Header);capture.Flush();samples=0;session.Clear();active=true;leader.Enabled=refresh.Enabled=exportTrail.Enabled=false;start.Text="Parar captura";
   Write("CAPTURA_INICIO versão="+ReleaseInfo.Version+" pid="+chosen.Pid+" perfil="+client.Profile.Sha256+" arquivo="+Path.GetFileName(capturePath));Record(snapshot);timer.Start();
  }catch(Exception ex){Write("INICIO_ERRO "+ex.GetType().Name+": "+ex.Message);Stop("Captura não iniciada.");}
 }
 void Record(ClientSnapshot snapshot){
  var row=FrostCapture.Row(snapshot);capture.WriteLine(row);capture.Flush();session.Add(FrostCapture.ParseRow(row));samples++;
  state.Text="Amostras: "+samples+" · "+snapshot.Name+Environment.NewLine+String.Format(CultureInfo.InvariantCulture,"Mapa: {0:F1}, {1:F1} · altura: {2:F1}",snapshot.MapX,snapshot.MapZ,snapshot.MapHeight);
  if(samples==1||samples%10==0)Write("AMOSTRA pid="+snapshot.Pid+" n="+samples+String.Format(CultureInfo.InvariantCulture," mundo=({0:F2},{1:F2},{2:F2}) HP={3}/{4}",snapshot.X,snapshot.Y,snapshot.Z,snapshot.Hp,snapshot.MaxHp));
 }
 void Sample(){if(!active)return;try{if(!allowed()){Stop("Sessão indisponível; captura interrompida.");return;}Record(client.Read());}catch(Exception ex){Write("LEITURA_ERRO "+ex.GetType().Name+": "+ex.Message);Stop("Falha de leitura; captura interrompida.");}}
 void Stop(string reason){
  timer.Stop();bool wasActive=active;active=false;
  if(capture!=null){try{capture.Dispose();}catch(Exception ex){Write("ARQUIVO_ERRO "+ex.Message);}capture=null;}
  if(client!=null){try{client.Dispose();}catch(Exception ex){Write("CLIENTE_FECHAR_ERRO "+ex.Message);}client=null;}
  leader.Enabled=refresh.Enabled=true;exportTrail.Enabled=session.Count>=2;start.Text="Iniciar captura de coordenadas";
  if(wasActive){state.Text="Parado. "+samples+" amostras salvas.";Write("CAPTURA_FIM "+reason+" amostras="+samples);if(session.Count>=2)TryWriteTrail(false);}
 }
 void ExportTrail(){TryWriteTrail(true);}
 void TryWriteTrail(bool requested){
  try{
   var trail=FrostCapture.ToTrail(session,"frost-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
   Directory.CreateDirectory(Folder);
   string path=capturePath==null?Path.Combine(Folder,trail.Name+".txt"):Path.ChangeExtension(capturePath,"txt");
   trail.Write(path);
   state.Text="Trajeto: "+trail.Path.Count+" pontos · "+trail.Length().ToString("F0",CultureInfo.InvariantCulture)+" de caminho. Carregue no Mat Farm.";
   Write("TRAJETO_OK pontos="+trail.Path.Count+" comprimento="+trail.Length().ToString("F1",CultureInfo.InvariantCulture)+" arquivo="+Path.GetFileName(path));
  }catch(Exception ex){
   if(requested){Write("TRAJETO_ERRO "+ex.GetType().Name+": "+ex.Message);state.Text="Não gerou trajeto: "+ex.Message;}
   else Write("TRAJETO_ADIADO "+ex.Message);
  }
 }
 protected override void Dispose(bool disposing){if(disposing){Stop("Janela descartada.");timer.Dispose();}base.Dispose(disposing);}
}
partial class AvalonInterno {
 FrostDiagnosticsForm frostDiagnostics;
 void OpenFrostDiagnostics(){if(closeInProgress||authorizationDenied)return;if(frostDiagnostics==null||frostDiagnostics.IsDisposed)frostDiagnostics=new FrostDiagnosticsForm(()=>!authorizationDenied&&!closeInProgress);if(!frostDiagnostics.Visible)frostDiagnostics.Show(this);frostDiagnostics.BringToFront();}
}
