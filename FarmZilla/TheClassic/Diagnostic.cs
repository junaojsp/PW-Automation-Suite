using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Reflection;

[assembly: AssemblyTitle("FarmZilla The Classic - Diagnóstico")]
[assembly: AssemblyVersion("0.5.0.0")]
[assembly: AssemblyFileVersion("0.5.0.0")]

namespace FarmZilla.TheClassic {
 public sealed class FileReport {
  public string Product="FarmZilla The Classic",Version="0.5.0",Mode="Somente leitura";
  public string FileName,Sha256,Architecture,ProfileStatus;
  public bool ReferenceMatch,AutomationEnabled=false;
  public long Bytes;public uint ImageSize;
  public Dictionary<string,string[]> RttiCandidates=new Dictionary<string,string[]>();
  public Dictionary<string,string> AvalonSignatureCandidates=new Dictionary<string,string>();
  public string[] AvalonIncompatibilities;
 }
 public static class Analyzer {
  public const string ReferenceSha="39d81c21ca72f373a787c936da1611281593d950668f2077016d77c1424338ce";
  public static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
  public static bool IsReference(byte[] bytes){return Hash(bytes)==ReferenceSha;}
  public static FileReport Inspect(string name,byte[] bytes){
   if(bytes.Length>64*1024*1024)throw new InvalidDataException("Arquivo excede 64 MB.");
   var pe=PeImage.Load(bytes);
   var report=new FileReport{FileName=Path.GetFileName(name),Bytes=bytes.Length,Sha256=Hash(bytes),Architecture="x86",ImageSize=pe.SizeOfImage};
   report.ReferenceMatch=report.Sha256==ReferenceSha;
   report.ProfileStatus=report.ReferenceMatch?"Cliente The Classic de referência; campos ainda não validados":"Arquivo diferente da referência; leitura do processo bloqueada";
   foreach(string type in new[]{"CECHostPlayer","CECGameRun","CECInventory","CECIvtrMedicine","CECAutoPolicy"}){
    var matches=SignatureScanner.Vtable(pe,".?AV"+type+"@@",0);
    report.RttiCandidates[type]=matches.Select(r=>"0x"+r.ToString("X8")).ToArray();
   }
   // Comparison only. Never use a partial Avalon resolution as a Classic profile.
   var resolution=SignatureScanner.Resolve(pe);
   foreach(var pair in resolution.Rva)report.AvalonSignatureCandidates[pair.Key]="0x"+pair.Value.ToString("X8");
   report.AvalonIncompatibilities=resolution.Errors.ToArray();
   return report;
  }
  public static byte[] ReadFile(string path){
   using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
    if(stream.Length>64*1024*1024)throw new InvalidDataException("Arquivo excede 64 MB.");
    using(var buffer=new MemoryStream()){stream.CopyTo(buffer);return buffer.ToArray();}
   }
  }
  public static string Json(object report){return new JavaScriptSerializer().Serialize(report);}
 }
 public sealed class ProcessReport {
  public int Pid;public string FileName,Status,Sha256,ModuleBase;
  public bool ReferenceMatch,HeaderMatches;
  public string CharacterStatus="Ainda não lido"; public CharacterSnapshot Character;
 }
 public static class LiveReader {
  sealed class ProcessReader:IReadMemory {readonly IntPtr handle;public ProcessReader(IntPtr handle){this.handle=handle;}public byte[] Read(uint address,int count){var bytes=new byte[count];UIntPtr got;if(!ReadProcessMemory(handle,new IntPtr(unchecked((int)address)),bytes,new UIntPtr((uint)count),out got)||got.ToUInt64()!=(ulong)count)throw new IOException("Memória indisponível em 0x"+address.ToString("X8"));return bytes;}}
  // Explicitly query/read only: no VM_WRITE, VM_OPERATION or CREATE_THREAD.
  const uint Access=0x0400|0x0010;
  [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr process,IntPtr at,byte[] bytes,UIntPtr size,out UIntPtr count);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr process);
  public static ProcessReport Inspect(Process process){
   var report=new ProcessReport{Pid=process.Id,Status="Não validado"};IntPtr handle=IntPtr.Zero;
   try{
    var module=process.MainModule;string path=module.FileName;report.FileName=Path.GetFileName(path);
    var bytes=Analyzer.ReadFile(path);report.Sha256=Analyzer.Hash(bytes);report.ReferenceMatch=report.Sha256==Analyzer.ReferenceSha;
    if(!report.ReferenceMatch){report.Status="Cliente diferente; memória não lida";return report;}
    var pe=PeImage.Load(bytes);
    if(module.ModuleMemorySize!=pe.SizeOfImage)throw new InvalidDataException("Tamanho do módulo difere do arquivo.");
    handle=OpenProcess(Access,false,process.Id);if(handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
    int count=Math.Min(512,bytes.Length);var actual=new byte[count];UIntPtr got;
    if(!ReadProcessMemory(handle,module.BaseAddress,actual,new UIntPtr((uint)count),out got)||got.ToUInt64()!=(ulong)count)throw new IOException("Cabeçalho do processo indisponível.");
    report.HeaderMatches=actual.SequenceEqual(bytes.Take(count));
    if(!report.HeaderMatches)throw new InvalidDataException("Cabeçalho em memória difere do arquivo.");
    if(process.HasExited)throw new IOException("Cliente encerrou durante a leitura.");
    report.ModuleBase="0x"+module.BaseAddress.ToInt64().ToString("X8");
    report.Status="Identidade e cabeçalho conferidos";
    try{
     uint imageBase=unchecked((uint)module.BaseAddress.ToInt32());var memory=new ProcessReader(handle);
     ClassicProfile.ValidateLoaded(memory,imageBase,pe);
     var character=CharacterReader.Read(memory,imageBase);
     if(process.HasExited)throw new IOException("Cliente encerrou durante a leitura.");
     report.Character=character;report.CharacterStatus="Leitura coerente; compare com o jogo antes de validar o perfil";
    }catch(Exception ex){report.Character=null;report.CharacterStatus="Leitura recusada: "+ex.Message;}
   }catch(Exception ex){report.Status="Leitura não concluída: "+ex.Message;}
   finally{if(handle!=IntPtr.Zero)CloseHandle(handle);}
   return report;
  }
  public static List<ProcessReport> Discover(){
   var list=new List<ProcessReport>();
   foreach(var process in Process.GetProcesses())using(process){
    try{if(process.ProcessName.StartsWith("elementclient",StringComparison.OrdinalIgnoreCase))list.Add(Inspect(process));}catch(InvalidOperationException){}catch(Win32Exception){}
   }
   return list;
  }
 }
 sealed class DiagnosticForm:Form {
  readonly TextBox output=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,WordWrap=false,BackColor=Color.FromArgb(9,14,20),ForeColor=Color.LightSkyBlue};
  readonly Button inspect=new Button{Text="Analisar executável",AutoSize=true},live=new Button{Text="Ler personagens conectados",AutoSize=true},save=new Button{Text="Salvar relatório",AutoSize=true};
  readonly Label state=new Label{AutoSize=true,Text="Somente leitura. Compare nome, nível, HP/MP e posição com o personagem no jogo."};
  object fileReport;List<ProcessReport> processes=new List<ProcessReport>();readonly List<object> samples=new List<object>();bool working;
  public DiagnosticForm(){
   Text="FarmZilla · The Classic 1.4.4 · diagnóstico 0.5.0";ClientSize=new Size(850,550);MinimumSize=new Size(680,420);BackColor=Color.FromArgb(16,22,29);ForeColor=Color.White;Font=new Font("Segoe UI",9);
   var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=85,Padding=new Padding(8),WrapContents=true};
   top.Controls.AddRange(new Control[]{inspect,live,save,state});Controls.Add(output);Controls.Add(top);
   output.Text="Selecione o elementclient.exe do The Classic para analisar o perfil.\r\n\r\nEste executável é independente do Avalon e não possui atualizador ou automações.\r\nOs relatórios contêm metadados e candidatos de assinatura; não são dumps de memória.";
   inspect.Click+=async(s,e)=>{using(var dialog=new OpenFileDialog{Filter="Executável Windows|*.exe",Title="Cliente do The Classic"})if(dialog.ShowDialog(this)==DialogResult.OK)await Work(async()=>{fileReport=await Task.Run(()=>Analyzer.Inspect(dialog.FileName,Analyzer.ReadFile(dialog.FileName)));Render();});};
   live.Click+=async(s,e)=>await Work(async()=>{processes=await Task.Run(()=>LiveReader.Discover());samples.Add(new{readAtUtc=DateTime.UtcNow.ToString("o"),processes=processes});if(samples.Count>12)samples.RemoveAt(0);Render();});
   save.Click+=(s,e)=>{using(var dialog=new SaveFileDialog{Filter="Relatório JSON|*.json",FileName="farmzilla-tclassic-diagnostico.json"})if(dialog.ShowDialog(this)==DialogResult.OK)try{File.WriteAllText(dialog.FileName,Report(),new UTF8Encoding(false));state.Text="Relatório salvo.";}catch(Exception ex){state.Text="Falha salvando: "+ex.Message;}};
  }
  string Report(){return Analyzer.Json(new{product="FarmZilla The Classic",version="0.5.0",file=fileReport,processes=processes,samples=samples,characterFields="Nome, classe, nível, HP/MP, posição interna e alvo: confirmar no jogo; inventário pendente",automationEnabled=false});}
  void Render(){
   var text=new StringBuilder();text.AppendLine("THE CLASSIC · DIAGNÓSTICO 0.5.0 · SOMENTE LEITURA");text.AppendLine("Amostras guardadas: "+samples.Count+" / 12. Compare os valores abaixo com o jogo.");
   foreach(var process in processes){text.AppendLine();text.AppendLine("PID "+process.Pid+" · "+process.Status);text.AppendLine(process.CharacterStatus);var c=process.Character;if(c==null)continue;text.AppendLine(c.Name+" · Classe ID "+c.ClassId+" · Nível "+c.Level+" · Personagem ID "+c.CharacterId);text.AppendLine("HP "+c.Hp+" / "+c.MaxHp+"    MP "+c.Mp+" / "+c.MaxMp);text.AppendLine(String.Format("Posição interna: X {0:F3} · Y {1:F3} · Z {2:F3} · Alvo ID {3}",c.X,c.Y,c.Z,c.TargetId));}
   if(processes.Count==0)text.AppendLine("Nenhum personagem lido. Abra o jogo e clique em Ler personagens conectados.");text.AppendLine();text.AppendLine("RELATÓRIO JSON (Salvar relatório exporta todas as amostras):");text.AppendLine(Report());output.Text=text.ToString();output.SelectionStart=0;output.SelectionLength=0;state.Text="Leitura concluída. Automação desligada; inventário ainda pendente.";
  }
  async Task Work(Func<Task> action){if(working)return;working=true;inspect.Enabled=live.Enabled=save.Enabled=false;state.Text="Lendo…";try{await action();}catch(Exception ex){state.Text="Não foi possível analisar: "+ex.Message;}finally{working=false;if(!IsDisposed)inspect.Enabled=live.Enabled=save.Enabled=true;}}
  protected override void OnFormClosing(FormClosingEventArgs e){if(working){e.Cancel=true;state.Text="Aguarde a leitura terminar para fechar.";}base.OnFormClosing(e);}
 }
 static class Program {
  [STAThread] static int Main(string[] args){
   if(args.Length>0){try{if(args.Length!=3||args[0]!="--inspect")throw new ArgumentException("Uso: --inspect caminho-do-exe relatorio.json");var r=Analyzer.Inspect(args[1],Analyzer.ReadFile(args[1]));File.WriteAllText(args[2],Analyzer.Json(r),new UTF8Encoding(false));return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new DiagnosticForm());return 0;
  }
 }
}
