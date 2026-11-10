using System;using System.Linq;using System.Text;using System.Web.Script.Serialization;
// Autoteste 0.19.11 sem rede e sem jogo: fila do registro remoto (ordem, limites do lote, devolução após falha, teto de memória).
class LogQueueTest {
 static void Assert(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS "+what);}
 static int Main(){try{DiagnosticLog.ToFile=false;
  while(DiagnosticLog.Pending>0)DiagnosticLog.Take(500,1<<20);
  for(int i=0;i<10;i++)DiagnosticLog.Write("teste "+i+" ação");
  var a=DiagnosticLog.Take(4,1<<20);Assert(a.Count==4&&a[0].EndsWith("teste 0 ação")&&a[3].EndsWith("teste 3 ação"),"lote sai na ordem, limitado por quantidade");
  DiagnosticLog.PutBack(a);var all=DiagnosticLog.Take(200,1<<20);Assert(all.Count==10&&all[0].EndsWith("teste 0 ação")&&all[9].EndsWith("teste 9 ação"),"falha devolve o lote na frente, sem perder ordem");
  DiagnosticLog.Write(new string('x',2000));var longLine=DiagnosticLog.Take(10,1<<20);Assert(longLine.Count==1&&longLine[0].Length<=481,"linha longa é cortada abaixo do limite do servidor");
  for(int i=0;i<300;i++)DiagnosticLog.Write("linha média com acentuação · "+new string('é',150));var batch=DiagnosticLog.Take(200,12000);int bytes=Encoding.UTF8.GetByteCount(new JavaScriptSerializer().Serialize(new{version=ReleaseInfo.Version,lines=batch}));Assert(batch.Count>0&&batch.Count<200&&bytes<16000,"lote respeita 16 KB do servidor mesmo com acentos ("+batch.Count+" linhas, "+bytes+" bytes)");
  while(DiagnosticLog.Pending>0)DiagnosticLog.Take(500,1<<20);for(int i=0;i<3500;i++)DiagnosticLog.Write("n "+i);Assert(DiagnosticLog.Pending==3000,"fila limitada a 3000 linhas sem internet");var first=DiagnosticLog.Take(1,1<<20);Assert(first[0].EndsWith("n 500"),"mais antigas descartadas primeiro");
  RemoteLog.Upload(null,"x");RemoteLog.Upload("https://control.example",null);Assert(DiagnosticLog.Pending==2999,"sem serviço ou autorização nada é enviado nem perdido");
  Console.WriteLine("Fila do registro remoto aprovada.");return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}
}
