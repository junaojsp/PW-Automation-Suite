using System;using System.Globalization;using System.IO;using RotaAvalon;
static class FrostTests {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
 [STAThread]static void Main(){
  var previous=CultureInfo.CurrentCulture;try{
   CultureInfo.CurrentCulture=new CultureInfo("pt-BR");
   var snapshot=new ClientSnapshot{Pid=123,Name="Líder, \"Avalon\"",X=125.5f,Y=100,Z=-250,Hp=450,MaxHp=500,ReadAt=new DateTime(2026,11,11,17,0,0,DateTimeKind.Utc)};
   var row=FrostCapture.Row(snapshot);
   Assert(row.Contains(",125.5,100,-250,412.55,525,10,450,500"),"Coordenadas reais e mapa usam ponto decimal sob pt-BR");
   Assert(row.Contains("\"Líder, \"\"Avalon\"\"\""),"Nome com vírgula e aspas preservado no CSV");
   Assert(row.StartsWith("2026-11-11T17:00:00.0000000Z,"),"Captura registra horário UTC");
   snapshot.X=Single.NaN;bool failed=false;try{FrostCapture.Row(snapshot);}catch(InvalidDataException){failed=true;}Assert(failed,"Coordenada inválida não entra na captura");
   failed=false;try{FrostCapture.Row(null);}catch(InvalidDataException){failed=true;}Assert(failed,"Amostra ausente recusada");
   using(var form=new FrostDiagnosticsForm(()=>false)){Assert(form.Text.Contains("diagnóstico"),"Tela identifica teste de diagnóstico");}
   Console.WriteLine("6 testes Frost concluídos; não substituem teste com cliente Avalon.");
  }finally{CultureInfo.CurrentCulture=previous;}
 }
}
