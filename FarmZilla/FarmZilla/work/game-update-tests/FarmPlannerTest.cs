using System;using System.Collections.Generic;using System.Linq;
// Autoteste 0.26.0 sem jogo: plano do farm automático (só escolhidos com nível, sem repetir o mesmo lugar, mais próximo primeiro, limite).
class FarmPlannerTest {
 static void Check(bool ok,string what){if(!ok)throw new Exception(what);}
 static RotaAvalon.ResourceMarker M(int id,double x,double z,bool level=true){return new RotaAvalon.ResourceMarker{TemplateId=id,X=x,Y=220,Z=z,LevelAllowed=level,ItemId=id+1,Name="M"+id};}
 static int Main(){try{
  var player=new RotaAvalon.ClientSnapshot{Name="Teste",X=0,Z=0};
  var known=new List<RotaAvalon.ResourceMarker>{M(3076,300,0),M(3076,100,0),M(3090,200,0),M(9999,50,0),M(3076,102,2),M(3090,150,0,false)};
  var plan=RotaAvalon.FarmPlanner.Build(known,new HashSet<int>{3076,3090},player);
  Check(plan.Points.Count==3,"pontos "+plan.Points.Count);Console.WriteLine("PASS só materiais escolhidos e com nível; mesmo lugar não repete");
  Check(plan.Points.Select(p=>p.X).SequenceEqual(new double[]{100,200,300}),"ordem "+String.Join(",",plan.Points.Select(p=>p.X)));Console.WriteLine("PASS ordem do mais próximo ao seguinte");
  Check(plan.Points.All(p=>p.Action=="Coletar"&&p.ResourceId!=0)&&plan.ResourceIds.Contains(3076)&&plan.Name=="Farm automático"&&plan.Character=="Teste","campos");Console.WriteLine("PASS pontos de coleta com material e personagem");
  var many=Enumerable.Range(0,500).Select(i=>M(3076,i*20,(i%7)*20)).ToList();Check(RotaAvalon.FarmPlanner.Build(many,new HashSet<int>{3076},player).Points.Count==RotaAvalon.FarmPlanner.MaxPoints,"limite");Console.WriteLine("PASS limite de "+RotaAvalon.FarmPlanner.MaxPoints+" pontos por volta");
  Check(RotaAvalon.FarmPlanner.Build(known,new HashSet<int>{1},player).Points.Count==0&&RotaAvalon.FarmPlanner.Build(known,new HashSet<int>{3076},null).Points.Count==0,"vazio");Console.WriteLine("PASS sem pontos quando não há material escolhido conhecido");
  var near=RotaAvalon.FarmPlanner.Build(known,new HashSet<int>{3076,3090},player,150);Check(near.Points.Count==1&&near.Points[0].X==100,"raio "+near.Points.Count);Console.WriteLine("PASS raio do farm deixa de fora os pontos longe");
  var anchored=RotaAvalon.FarmPlanner.Build(known,new HashSet<int>{3076,3090},player,60,null,250,0);Check(anchored.Points.Select(p=>p.X).SequenceEqual(new double[]{200,300}),"centro "+String.Join(",",anchored.Points.Select(p=>p.X)));Console.WriteLine("PASS raio medido do ponto onde o farm começou");
  var removed=RotaAvalon.FarmPlanner.Build(known,new HashSet<int>{3076,3090},player,0,new HashSet<string>{RotaAvalon.FarmPlanner.Key(3090,200,0)});Check(removed.Points.Count==2&&removed.Points.All(p=>p.X!=200),"removido");Console.WriteLine("PASS ponto tirado pelo usuário não entra no plano");
  Console.WriteLine("Planejador do farm aprovado.");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}
}
