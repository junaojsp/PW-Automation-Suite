using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using FarmZilla.TheClassic;
static class DiagnosticTests {
 static int tests;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);tests++;Console.WriteLine("PASS "+name);}
 static byte[] Fixture(){
  var b=new byte[0x800];Action<int,uint> u=(at,value)=>Buffer.BlockCopy(BitConverter.GetBytes(value),0,b,at,4);Action<int,ushort> h=(at,value)=>Buffer.BlockCopy(BitConverter.GetBytes(value),0,b,at,2);
  h(0,0x5a4d);u(0x3c,0x80);u(0x80,0x4550);h(0x84,0x14c);h(0x86,2);h(0x94,0xe0);int opt=0x98;h(opt,0x10b);u(opt+28,0x400000);u(opt+56,0x3000);
  int table=opt+0xe0;Buffer.BlockCopy(Encoding.ASCII.GetBytes(".text"),0,b,table,5);u(table+8,0x200);u(table+12,0x1000);u(table+16,0x200);u(table+20,0x200);u(table+36,0x60000020);
  table+=40;Buffer.BlockCopy(Encoding.ASCII.GetBytes(".rdata"),0,b,table,6);u(table+8,0x400);u(table+12,0x2000);u(table+16,0x400);u(table+20,0x400);u(table+36,0x40000040);
  var name=Encoding.ASCII.GetBytes(".?AVCECHostPlayer@@\0");Buffer.BlockCopy(name,0,b,0x408,name.Length);u(0x50c,0x402000);u(0x580,0x402100);u(0x584,0x401000);return b;
 }
 static void Reject(Action action,string name){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,name);}
 [STAThread] static int Main(){try{
  Check(Analyzer.Hash(Encoding.ASCII.GetBytes("abc"))=="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad","SHA-256 conhecido");
  var bytes=Fixture();var report=Analyzer.Inspect("C:\\usuario\\cliente.exe",bytes);
  Check(report.Architecture=="x86"&&!report.ReferenceMatch&&!report.AutomationEnabled,"PE válido desconhecido não libera automação");
  Check(report.FileName=="cliente.exe","Relatório não inclui caminho pessoal completo");
  Check(report.RttiCandidates["CECHostPlayer"].SequenceEqual(new[]{"0x00002184"}),"RTTI encontra vtable no PE sintético");
  Check(report.AvalonIncompatibilities.Length>0,"Ausência das assinaturas Avalon permanece explícita");
  Check(!Analyzer.IsReference(bytes),"Arquivo diferente não recebe identidade Classic");
  Reject(()=>Analyzer.Inspect("vazio",new byte[0]),"Recusa arquivo vazio");
  var x64=(byte[])bytes.Clone();x64[0x84]=0x64;x64[0x85]=0x86;Reject(()=>Analyzer.Inspect("x64",x64),"Recusa arquitetura diferente");
  var malformed=(byte[])bytes.Clone();Buffer.BlockCopy(BitConverter.GetBytes(0xffffffffu),0,malformed,0x98+56,4);Reject(()=>Analyzer.Inspect("invalido",malformed),"Recusa imagem de tamanho inválido");
  var serialized=Analyzer.Json(report);Check(serialized.Contains("\"AutomationEnabled\":false")&&!serialized.Contains("C:\\\\usuario"),"Exportação identifica bloqueio sem caminho pessoal");
  CharacterReaderTests.RunTests();
  var imports=typeof(LiveReader).Assembly.GetTypes().SelectMany(t=>t.GetMethods(BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)).Select(m=>m.GetCustomAttributes(typeof(DllImportAttribute),false).OfType<DllImportAttribute>().FirstOrDefault()).Where(a=>a!=null).Select(a=>a.EntryPoint).Distinct().OrderBy(s=>s).ToArray();
  Check(imports.SequenceEqual(new[]{"CloseHandle","OpenProcess","ReadProcessMemory"}),"Executável importa somente consulta, leitura e fechamento de processo");
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var form=new DiagnosticForm()){
   form.Show();form.Update();
   Check(form.Controls.OfType<TextBox>().Single().ReadOnly,"Saída da interface é somente leitura");
   foreach(Control c in form.Controls)Check(c.Left>=0&&c.Top>=0&&c.Right<=form.ClientSize.Width&&c.Bottom<=form.ClientSize.Height,"Controle dentro da janela: "+c.GetType().Name);
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostico-layout.png"),ImageFormat.Png);}
  }
  Console.WriteLine(tests+" testes concluídos; não substituem validação com o jogo aberto.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
