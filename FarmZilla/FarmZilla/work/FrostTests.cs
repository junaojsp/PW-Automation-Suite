using System;using System.Globalization;using System.IO;using System.Linq;using System.Windows.Forms;using RotaAvalon;
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
   snapshot.X=125.5f;row=FrostCapture.Row(snapshot);var parsed=FrostCapture.ParseRow(row);
   Assert(parsed.Pid==123&&parsed.Name=="Líder, \"Avalon\""&&parsed.X==125.5&&parsed.MapZ==525,"CSV volta a coordenadas e nome com vírgula");
   var csv=FrostCapture.Header+Environment.NewLine+row+Environment.NewLine;
   Assert(FrostCapture.ParseCsv(csv).Count==1,"ParseCsv lê uma amostra depois do cabeçalho");
   failed=false;try{FrostCapture.ParseCsv("x,y\n1,2");}catch(InvalidDataException){failed=true;}Assert(failed,"Cabeçalho errado é recusado");

   Assert(FrostCommand.Catalog.Length>=10,"Catálogo lista os comandos Avalon do FarmZilla");
   Assert(FrostCommand.Catalog.Select(c=>c.Name).Distinct().Count()==FrostCommand.Catalog.Length,"Nomes do catálogo não se repetem");
   Assert(FrostCommand.Catalog.All(c=>c.Code.Length>0&&c.Packet.Length>0&&c.Use.Length>0),"Cada comando tem código, pacote e uso");
   Assert(FrostCommand.Summary().Contains("comandos Avalon já validados"),"Resumo identifica origem FarmZilla");

   var moving=new System.Collections.Generic.List<FrostCapture.Sample>();
   for(int i=0;i<=40;i++)moving.Add(new FrostCapture.Sample{Pid=1,Name="Lima",X=1000+i*10,Y=600,Z=2000,Hp=1,MaxHp=1,Utc=DateTime.UtcNow});
   var trail=FrostCapture.ToTrail(moving,"frost-teste");
   Assert(trail.Path.Count>=2&&trail.Path[0].X==1000&&trail.Character=="Lima","Captura em movimento vira trajeto do Mat Farm");
   Assert(trail.Path.All(p=>p.Flying),"Trajeto Frost assume voo, como o Mat Farm manual");
   string file=Path.Combine(Path.GetTempPath(),"farmzilla-frost-teste.txt");trail.Write(file);
   Assert(TrailRoute.IsTrail(file)&&TrailRoute.Read(file).Path.Count==trail.Path.Count,"Trajeto Frost é o .txt do Mat Farm");
   File.Delete(file);
   var still=new System.Collections.Generic.List<FrostCapture.Sample>{moving[0],new FrostCapture.Sample{Pid=1,Name="Lima",X=1000,Y=600,Z=2000,Hp=1,MaxHp=1}};
   failed=false;try{FrostCapture.ToTrail(still,"parado");}catch(InvalidDataException){failed=true;}Assert(failed,"Parado não gera trajeto");
   failed=false;try{FrostCapture.ToTrail(moving.Take(1).ToList(),"curto");}catch(InvalidDataException){failed=true;}Assert(failed,"Uma amostra não gera trajeto");

   using(var form=new FrostDiagnosticsForm(()=>false,false)){
    Assert(form.Text=="FarmZilla · Frost Farm","Tela identifica Frost Farm nativo");
    var tabs=form.Controls.OfType<TabControl>().Single();
    Assert(tabs.TabPages.Count==2&&tabs.TabPages[0].Text.Contains("Captura")&&tabs.TabPages[1].Text.Contains("Comandos"),"Abas de captura e catálogo");
    var list=tabs.TabPages[1].Controls.OfType<ListView>().Single();
    Assert(list.Items.Count==FrostCommand.Catalog.Length,"Lista mostra o catálogo inteiro");
   }
   Console.WriteLine("Testes Frost nativos concluídos; não substituem teste com cliente Avalon.");
  }finally{CultureInfo.CurrentCulture=previous;}
 }
}
