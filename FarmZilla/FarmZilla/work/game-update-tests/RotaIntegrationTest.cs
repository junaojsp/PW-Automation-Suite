using System;using System.Diagnostics;using System.IO;using System.Threading.Tasks;using System.Windows.Forms;
// Autoteste 0.20.0 só no próprio processo, sem jogo: autoverificação original da Rota (sem ler clientes), ponte nativa embutida
// (hash fixado, extração, recusa de arquivo alterado), ida e volta real da DLL recompilada pelo gancho na janela deste
// processo (operação 0, que não chama nada do jogo), trava de personagem e montagem da janela Rubi.
class RotaIntegrationTest : Form {
 static int result=1;
 RotaIntegrationTest(){Text="Autoteste Rota";Shown+=async(s,e)=>{IntPtr w=Handle;await Task.Run(()=>Run(w));Close();};}
 static void Check(bool ok,string what){if(!ok)throw new Exception(what);}
 static void Run(IntPtr window){string root=Path.Combine(Path.GetTempPath(),"FarmZilla-Rota-Test-"+Guid.NewGuid().ToString("N"));
  try{DiagnosticLog.ToFile=false;RotaAvalon.RotaPaths.TestRoot=root;
   Check(RotaAvalon.SelfTest.Run(false)==0,"Autoverificação original da Rota falhou");
   string expected=Path.Combine(root,"RotaBridge-"+RotaAvalon.RotaPaths.BridgeSha256.Substring(0,12)+".dll");Directory.CreateDirectory(root);File.WriteAllBytes(expected,new byte[]{1,2,3});
   string dll=RotaAvalon.RotaPaths.Bridge();Check(dll==expected&&new FileInfo(dll).Length==RotaAvalon.RotaPaths.Embedded().Length,"Arquivo sujo no lugar da DLL não foi reposto");
   Check(RotaAvalon.RotaPaths.Bridge()==dll,"Segunda extração mudou o caminho");Console.WriteLine("PASS ponte embutida extraída na pasta da Rota com hash fixado; arquivo sujo reposto");
   var r=RotaAvalon.NativeBridge.Ping(Process.GetCurrentProcess().Id,window,dll);Check(r.State==2&&r.Error==0&&r.Operation==0&&r.ExecutedThread!=0,"Ida e volta da ponte");Console.WriteLine("PASS DLL recompilada respondeu pelo gancho na thread da janela (operação 0)");
   string copy=Path.Combine(root,"alterada.dll");var bytes=File.ReadAllBytes(dll);bytes[bytes.Length-1]^=0x55;File.WriteAllBytes(copy,bytes);bool refused=false;try{RotaAvalon.NativeBridge.Ping(Process.GetCurrentProcess().Id,window,copy);}catch(InvalidDataException){refused=true;}Check(refused,"DLL alterada foi carregada");Console.WriteLine("PASS DLL alterada recusada antes de carregar");
   Check(!RotaLock.Busy(4242),"Trava inicial");RotaLock.Set(4242,"Teste");Check(RotaLock.Busy(4242)&&!RotaLock.Busy(4243),"Trava por PID");RotaLock.Clear(4242);Check(!RotaLock.Busy(4242),"Trava não saiu");Console.WriteLine("PASS trava de personagem em rota");
   result=0;Console.WriteLine("PASS: Rota integrada (autoverificação, ponte, trava).");}
  catch(Exception e){Console.WriteLine("FAIL "+e);}
  finally{try{Directory.Delete(root,true);}catch{}}}
 [STAThread]static int Main(){Application.EnableVisualStyles();Application.Run(new RotaIntegrationTest());return result;}
}
