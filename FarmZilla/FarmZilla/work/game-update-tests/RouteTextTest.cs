using System;using System.IO;using System.Linq;
// Autoteste 0.42.0 sem jogo: rota do Mat Farm em .txt — salva e lê de volta igual, aceita vírgula decimal, recusa arquivo
// sem pontos ou com número inválido, completa os materiais pelos pontos.
class RouteTextTest {
 static void Check(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS "+what);}
 static void Reject(string text,string what){string p=Path.GetTempFileName();try{File.WriteAllText(p,text);bool fail=false;try{RotaAvalon.FarmRouteText.Read(p);}catch(InvalidDataException){fail=true;}Check(fail,what);}finally{File.Delete(p);}}
 static int Main(){try{string path=Path.Combine(Path.GetTempPath(),"rota-teste-"+Guid.NewGuid().ToString("N")+".txt");
  try{var a=new RotaAvalon.FarmRouteText{Name="Bangu ferro",Character="Lima",Height=75,Radius=2500,Materials={3080,3076}};
   a.Points.Add(new RotaAvalon.Waypoint{ResourceId=3080,ItemId=1234,X=-1520.37,Y=231.5,Z=880.12});a.Points.Add(new RotaAvalon.Waypoint{ResourceId=3076,ItemId=0,X=10,Y=0,Z=-20.5,Area=true});
   a.Write(path);var b=RotaAvalon.FarmRouteText.Read(path);
   Check(b.Name=="Bangu ferro"&&b.Character=="Lima"&&b.Height==75&&b.Radius==2500&&b.Materials.SequenceEqual(new[]{3080,3076}),"nome, personagem, altura, raio e materiais voltam iguais");
   Check(b.Points.Count==2&&b.Points[0].ResourceId==3080&&b.Points[0].ItemId==1234&&Math.Abs(b.Points[0].X+1520.37)<0.01&&Math.Abs(b.Points[0].Z-880.12)<0.01&&!b.Points[0].Area&&b.Points[1].Area&&b.Points[1].Y==0,"pontos voltam na mesma ordem e posição");
   var plan=b.ToPlan();Check(plan.Points.Count==2&&plan.ResourceIds.Contains(3076)&&plan.Points.All(p=>p.Action=="Coletar"),"vira o plano do farm");
   Check(File.ReadAllText(path).Contains("ponto=3080;1234;-1520.37;231.5;880.12;0"),"arquivo legível: ponto=material;item;x;y;z;area");}
  finally{File.Delete(path);}
  {string p=Path.GetTempFileName();try{File.WriteAllText(p,"altura=500\nponto=3090;0;1,5;0;2,5\n");var r=RotaAvalon.FarmRouteText.Read(p);Check(r.Height==300&&Math.Abs(r.Points[0].X-1.5)<0.001&&r.Materials.SequenceEqual(new[]{3090}),"vírgula decimal aceita, altura limitada a 300 e material completado pelo ponto");}finally{File.Delete(p);}}
  Reject("nome=vazia\naltura=60\n","rota sem pontos recusada");Reject("ponto=3080;0;abc;0;0\n","número inválido recusado");Reject("ponto=3080;0;1\n","ponto incompleto recusado");
  Console.WriteLine("Rota em txt aprovada.");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}
}
