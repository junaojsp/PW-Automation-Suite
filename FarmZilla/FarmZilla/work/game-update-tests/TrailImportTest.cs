using System;using System.IO;using System.Linq;
// Autoteste 0.47.1 sem jogo: rotas de outro programa ("Mat Name:" + "cordx = x, cordy = z", vírgula decimal, sem altura).
// Usa um exemplo próprio; com argumento (pasta), também lê cada .txt dessa pasta só para conferir (nada é copiado).
class TrailImportTest {
 static void Check(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS "+what);}
 static int Main(string[] args){try{var catalog=RotaAvalon.ResourceCatalog.LoadEmbedded();
  string file=Path.Combine(Path.GetTempPath(),"farmzilla-cordx-teste.txt");
  File.WriteAllText(file,"Mat Name:\r\nNúcleo de Mina de Carvão\r\nPedreira de Pedra de Amol\r\nAmeixeira de Cálice Verme\r\nCoisa Que Não Existe\r\n\r\nCoordenadas:\r\ncordx = 1699,899, cordy = -1643,908\r\ncordx = 1585,972, cordy = -1432,953\r\ncordx = 1585,972, cordy = -1432,953\r\ncordx = 953,9683, cordy = -1381,028\r\ncordx = 1691,03, cordy = -1630,996\r\n",new System.Text.UTF8Encoding(true));
  Check(RotaAvalon.TrailRoute.IsTrail(file)||RotaAvalon.TrailRoute.IsOtherFormat(file),"formato reconhecido");
  var t=RotaAvalon.TrailRoute.Read(file,catalog);
  Check(t.Imported&&t.Path.Count==4,"4 pontos (repetido seguido ignorado)");
  Check(Math.Abs(t.Path[0].X-1699.899)<0.001&&Math.Abs(t.Path[0].Z+1643.908)<0.001&&Math.Abs(t.Path[2].X-953.9683)<0.0001,"vírgula decimal lida: x = cordx, z = cordy");
  Check(t.Path.All(p=>double.IsNaN(p.Y)&&p.Flying),"sem altura: voa na altura atual");
  Check(t.Materials.Contains(3086),"nome cortado \"Pedreira de Pedra de Amol\" = Pedreira de Pedra de Amolar (3086)");
  Check(t.Materials.Contains(3543)&&!t.Materials.Contains(3537),"\"Ameixeira de Cálice Verme\" = Vermelho (3543), não Verde");
  Check(t.UnknownNames.Count==1&&t.UnknownNames[0].StartsWith("Coisa"),"nome desconhecido avisado");
  Check(t.Closed,"fim perto do início = circular");
  string back=Path.Combine(Path.GetTempPath(),"farmzilla-cordx-volta.txt");t.WriteOther(back,catalog);var again=RotaAvalon.TrailRoute.Read(back,catalog);
  Check(again.Path.Count==4&&Math.Abs(again.Path[3].X-1691.03)<0.001&&again.Materials.Contains(3086),"salvar no mesmo formato e ler de novo");
  string ours=Path.Combine(Path.GetTempPath(),"farmzilla-cordx-nosso.txt");t.Write(ours);var mine=RotaAvalon.TrailRoute.Read(ours,catalog);Check(!mine.Imported&&double.IsNaN(mine.Path[0].Y),"formato FarmZilla guarda \"sem altura\" (y ?)");
  foreach(var f in new[]{file,back,ours})File.Delete(f);
  if(args.Length>0)foreach(var f in Directory.GetFiles(args[0],"*.txt")){var r=RotaAvalon.TrailRoute.Read(f,catalog);
   Console.WriteLine("  "+Path.GetFileName(f)+": "+r.Path.Count+" pontos, "+r.Materials.Count+" variantes de material, "+(r.Closed?"circular":"ida e volta")+(r.UnknownNames.Count>0?", não reconhecidos: "+String.Join(" | ",r.UnknownNames):""));
   Check(r.Path.Count>=2&&r.UnknownNames.Count==0,Path.GetFileName(f)+" carregada com todos os materiais");}
  Console.WriteLine("Rotas de outro programa aprovadas.");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}
}
