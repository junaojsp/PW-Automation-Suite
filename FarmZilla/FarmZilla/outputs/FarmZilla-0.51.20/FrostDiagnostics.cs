using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using RotaAvalon;

// First Avalon Frost test: observations only. No offsets, routes or commands copied
// from the protected third-party executable are treated as validated Avalon data.
static class FrostCapture {
 public static string Header="utc,pid,name,x,y,z,map_x,map_z,map_height,hp,max_hp";
 static string N(double value){return value.ToString("R",CultureInfo.InvariantCulture);}
 public static string Row(ClientSnapshot s){
  if(s==null||s.Pid<=0||!GameClient.ValidCoordinate(s.X)||!GameClient.ValidCoordinate(s.Y)||!GameClient.ValidCoordinate(s.Z))throw new InvalidDataException("Amostra Frost inválida.");
  return String.Join(",",new[]{s.ReadAt.ToUniversalTime().ToString("o"),s.Pid.ToString(CultureInfo.InvariantCulture),"\""+(s.Name??"").Replace("\"","\"\"")+"\"",N(s.X),N(s.Y),N(s.Z),N(s.MapX),N(s.MapZ),N(s.MapHeight),s.Hp.ToString(CultureInfo.InvariantCulture),s.MaxHp.ToString(CultureInfo.InvariantCulture)});
 }
}
sealed class FrostDiagnosticsForm : Form {
 sealed class Character {public int Pid;public string Name;public override string ToString(){return Name+" · PID "+Pid;}}
 readonly ComboBox leader=new ComboBox();readonly TextBox log=new TextBox();readonly Label state=new Label();
 readonly Button start=new NeonButton(),refresh=new NeonButton();readonly Timer timer=new Timer();
 readonly Func<bool> allowed;GameClient client;StreamWriter capture;string capturePath;int samples;bool active;
 public FrostDiagnosticsForm(Func<bool> allowed,bool discoverOnShow=true){
  this.allowed=allowed;Text="FarmZilla · Frost Farm · diagnóstico";ClientSize=new Size(430,610);MinimumSize=Size;BackColor=Rubi.Background;ForeColor=Rubi.Text;StartPosition=FormStartPosition.CenterParent;
  Font=new Font("Segoe UI",9);LabelAt("BOT FROST · TESTE AVALON",12,12,400,24,Rubi.Accent);
  LabelAt("Captura de coordenadas e diagnóstico; sem comandos ao jogo.",12,42,405,36,Rubi.Muted);
  LabelAt("Boss e DG automática: aguardam recuperação e validação da lógica.",12,80,405,40,Rubi.Muted);
  LabelAt("Personagem para capturar a rota:",12,124,400,20,Rubi.Text);
  leader.SetBounds(12,150,284,28);leader.DropDownStyle=ComboBoxStyle.DropDownList;leader.BackColor=Rubi.Field;leader.ForeColor=Rubi.Text;Controls.Add(leader);
  refresh.Text="Atualizar";refresh.SetBounds(306,148,112,30);refresh.Click+=(s,e)=>RefreshCharacters();Controls.Add(refresh);
  start.Text="Iniciar captura de coordenadas";start.SetBounds(12,190,406,36);start.Click+=(s,e)=>{if(active)Stop("Captura encerrada por você.");else Start();};Controls.Add(start);
  state.SetBounds(12,234,406,52);state.Text="Parado. Mova o personagem manualmente durante a captura.";Controls.Add(state);
  log.SetBounds(12,294,406,254);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BackColor=Rubi.Panel;log.ForeColor=Rubi.Text;log.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(log);
  var folder=new NeonButton{Text="Abrir pasta das capturas"};folder.SetBounds(12,560,406,36);folder.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;folder.Click+=(s,e)=>{try{Directory.CreateDirectory(Folder);Process.Start("explorer.exe",Folder);}catch(Exception ex){Write("PASTA_ERRO "+ex.Message);}};Controls.Add(folder);
  timer.Interval=1000;timer.Tick+=(s,e)=>Sample();Shown+=(s,e)=>{if(discoverOnShow)RefreshCharacters();};FormClosing+=(s,e)=>Stop("Janela Frost fechada.");
 }
 static string Folder{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AvalonInterno","Frost");}}
 Label LabelAt(string text,int x,int y,int w,int h,Color color){var l=new Label{Text=text,ForeColor=color,Bounds=new Rectangle(x,y,w,h)};Controls.Add(l);return l;}
 void Write(string message){DiagnosticLog.Write("Frost: "+message);if(log.TextLength>30000)log.Clear();log.AppendText(DateTime.Now.ToString("HH:mm:ss")+" "+message+Environment.NewLine);}
 void RefreshCharacters(){
  if(active)return;leader.Items.Clear();int failures=0;
  foreach(var process in Process.GetProcessesByName("ElementClient")){using(process){try{using(var game=new GameClient(process.Id,new Dictionary<string,bool>())){var snapshot=game.Read();leader.Items.Add(new Character{Pid=snapshot.Pid,Name=snapshot.Name});}}catch(Exception ex){failures++;Write("CLIENTE_ERRO pid="+process.Id+" "+ex.GetType().Name+": "+ex.Message);}}}
  if(leader.Items.Count>0)leader.SelectedIndex=0;Write("DESCOBERTA válidos="+leader.Items.Count+" falhas="+failures);
 }
 void Start(){
  if(!allowed()){Write("CAPTURA_RECUSADA sessão indisponível.");return;}var chosen=leader.SelectedItem as Character;if(chosen==null){Write("Selecione um personagem Avalon carregado.");return;}
  try{
   client=new GameClient(chosen.Pid,new Dictionary<string,bool>());var snapshot=client.Read();Directory.CreateDirectory(Folder);capturePath=Path.Combine(Folder,"frost-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".csv");
   capture=new StreamWriter(new FileStream(capturePath,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(true));capture.WriteLine(FrostCapture.Header);capture.Flush();samples=0;active=true;leader.Enabled=refresh.Enabled=false;start.Text="Parar captura";
   Write("CAPTURA_INICIO versão="+ReleaseInfo.Version+" pid="+chosen.Pid+" perfil="+client.Profile.Sha256+" arquivo="+Path.GetFileName(capturePath));Record(snapshot);timer.Start();
  }catch(Exception ex){Write("INICIO_ERRO "+ex.GetType().Name+": "+ex.Message);Stop("Captura não iniciada.");}
 }
 void Record(ClientSnapshot snapshot){capture.WriteLine(FrostCapture.Row(snapshot));capture.Flush();samples++;state.Text="Amostras: "+samples+" · "+snapshot.Name+Environment.NewLine+String.Format(CultureInfo.InvariantCulture,"Mapa: {0:F1}, {1:F1} · altura: {2:F1}",snapshot.MapX,snapshot.MapZ,snapshot.MapHeight);if(samples==1||samples%10==0)Write("AMOSTRA pid="+snapshot.Pid+" n="+samples+String.Format(CultureInfo.InvariantCulture," mundo=({0:F2},{1:F2},{2:F2}) HP={3}/{4}",snapshot.X,snapshot.Y,snapshot.Z,snapshot.Hp,snapshot.MaxHp));}
 void Sample(){if(!active)return;try{if(!allowed()){Stop("Sessão indisponível; captura interrompida.");return;}Record(client.Read());}catch(Exception ex){Write("LEITURA_ERRO "+ex.GetType().Name+": "+ex.Message);Stop("Falha de leitura; captura interrompida.");}}
 void Stop(string reason){timer.Stop();bool wasActive=active;active=false;if(capture!=null){try{capture.Dispose();}catch(Exception ex){Write("ARQUIVO_ERRO "+ex.Message);}capture=null;}if(client!=null){try{client.Dispose();}catch(Exception ex){Write("CLIENTE_FECHAR_ERRO "+ex.Message);}client=null;}leader.Enabled=refresh.Enabled=true;start.Text="Iniciar captura de coordenadas";if(wasActive){state.Text="Parado. "+samples+" amostras salvas.";Write("CAPTURA_FIM "+reason+" amostras="+samples);}}
 protected override void Dispose(bool disposing){if(disposing){Stop("Janela descartada.");timer.Dispose();}base.Dispose(disposing);}
}
partial class AvalonInterno {
 FrostDiagnosticsForm frostDiagnostics;
 void OpenFrostDiagnostics(){if(closeInProgress||authorizationDenied)return;if(frostDiagnostics==null||frostDiagnostics.IsDisposed)frostDiagnostics=new FrostDiagnosticsForm(()=>!authorizationDenied&&!closeInProgress);if(!frostDiagnostics.Visible)frostDiagnostics.Show(this);frostDiagnostics.BringToFront();}
}
