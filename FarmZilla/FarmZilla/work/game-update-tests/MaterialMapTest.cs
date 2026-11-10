using System;using System.Collections.Generic;using System.Diagnostics;using System.Linq;
// Autoteste 0.36.0 sem jogo: mapa de materiais embutido (pontos de área), prioridade do escaneamento real e plano do farm.
class MaterialMapTest {
 static void Check(bool ok,string what){if(!ok)throw new Exception(what);}
 static int Main(){try{
  var catalog=RotaAvalon.ResourceCatalog.LoadEmbedded();Check(RotaAvalon.MaterialMap.Count==4490,"pontos "+RotaAvalon.MaterialMap.Count);Console.WriteLine("PASS 4490 pontos embutidos ("+RotaAvalon.MaterialMap.Source.Substring(0,30)+"…)");
  var player=new RotaAvalon.ClientSnapshot{Name="Teste",Level=100,X=0,Z=0};var world=new RotaAvalon.ResourceFrame{Terrain=new RotaAvalon.TerrainBounds{Left=-4096,Right=4096,Top=5632,Bottom=-5632}};
  var merged=RotaAvalon.MaterialMap.Merge(new List<RotaAvalon.ResourceMarker>(),catalog,player,world);Check(merged.Count==4490&&merged.All(m=>m.Seed&&m.Y==0&&m.Historical),"todos como área");
  var seed=merged.First(m=>m.TemplateId==3076);var real=new RotaAvalon.ResourceMarker{TemplateId=3076,X=seed.X+15,Y=230,Z=seed.Z-10,Name="real",LevelAllowed=true};
  var withReal=RotaAvalon.MaterialMap.Merge(new List<RotaAvalon.ResourceMarker>{real},catalog,player,world);Check(withReal.Count<4491&&withReal.Contains(real)&&!withReal.Any(m=>m.Seed&&m.TemplateId==3076&&Math.Abs(m.X-seed.X)<1&&Math.Abs(m.Z-seed.Z)<1),"escaneado tem prioridade");Console.WriteLine("PASS escaneamento real a até 40 substitui o ponto de área");
  var other=new RotaAvalon.ResourceFrame{Terrain=new RotaAvalon.TerrainBounds{Left=-512,Right=512,Top=512,Bottom=-512}};Check(RotaAvalon.MaterialMap.Merge(new List<RotaAvalon.ResourceMarker>(),catalog,player,other).Count==0,"outro mapa");Console.WriteLine("PASS fora do mapa do mundo não usa os pontos");
  var plan=RotaAvalon.FarmPlanner.Build(merged,new HashSet<int>{3076},player,1500);Check(plan.Points.Count>0&&plan.Points.All(p=>p.Area&&p.Y==0),"plano área");Console.WriteLine("PASS plano do farm com pontos de área ("+plan.Points.Count+" Salgueiros no raio 1500)");
  var clock=Stopwatch.StartNew();for(int i=0;i<20;i++)RotaAvalon.MaterialMap.Merge(withReal.Where(m=>!m.Seed).ToList(),catalog,player,world);Console.WriteLine("PASS custo por atualização: "+(clock.ElapsedMilliseconds/20.0).ToString("F1")+" ms");
  Console.WriteLine("Mapa de materiais aprovado.");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}
}
