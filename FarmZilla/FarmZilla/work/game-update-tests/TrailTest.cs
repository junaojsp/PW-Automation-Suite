using System;using System.Drawing;using System.IO;using System.Linq;using System.Windows.Forms;
// Autoteste 0.47.0 sem jogo: trajeto manual do Mat Farm — gravação enxuta, arquivo .txt ida e volta, circular x ida e volta,
// e a tela no modo Manual (renderizada fora da tela, sem jogo). Argumento opcional: caminho do PNG da prévia.
class TrailTest {
 static void Check(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS "+what);}
 [STAThread]static int Main(string[] args){try{
  var t=new RotaAvalon.TrailRoute{Name="Teste",Character="Lima",Reach=45,Materials={3090,3096}};var rec=new RotaAvalon.TrailRecorder(t);
  for(int i=0;i<=200;i++)rec.Offer(1000+i,600,2000,true);
  Check(t.Path.Count==11,"reta de 200 grava 1 ponto a cada 20 ("+t.Path.Count+" pontos)");
  Check(!rec.Offer(1200.5,600,2000.2,true),"parado não grava ponto");
  int before=t.Path.Count;for(int i=1;i<=10;i++)rec.Offer(1200,600,2000+i,true);Check(t.Path.Count==before+1,"curva fechada grava depois de 8 ("+(t.Path.Count-before)+" ponto)");
  rec.Offer(1200,640,2010,true);Check(t.Path.Last().Y==640,"mudança de altura grava ponto");
  t.AddSeen(new RotaAvalon.ResourceMarker{TemplateId=3090,ItemId=111,X=1100,Y=550,Z=2010});Check(!t.AddSeen(new RotaAvalon.ResourceMarker{TemplateId=3090,X=1102,Y=550,Z=2012}),"material visto no mesmo lugar entra uma vez");
  string file=Path.Combine(Path.GetTempPath(),"farmzilla-trajeto-teste.txt");t.Write(file);
  Check(RotaAvalon.TrailRoute.IsTrail(file),"arquivo reconhecido como trajeto");
  var back=RotaAvalon.TrailRoute.Read(file);
  Check(back.Path.Count==t.Path.Count&&back.Seen.Count==1&&back.Reach==45&&back.Materials.SequenceEqual(new[]{3090,3096})&&back.Character=="Lima","salvar e carregar mantém pontos, coletas, alcance e materiais");
  Check(File.ReadAllLines(file).Any(l=>l.StartsWith("trajeto=1000;600;2000;1")),"texto simples legível (trajeto=x;y;z;voo)");
  Check(!back.Closed,"caminho aberto = ida e volta");
  var loop=new RotaAvalon.TrailRoute();foreach(var p in new[]{new[]{0,0},new[]{100,0},new[]{100,100},new[]{10,20}})loop.Path.Add(new RotaAvalon.TrailPoint{X=p[0],Z=p[1],Y=500,Flying=true});Check(loop.Closed,"fim perto do início = circular");
  File.WriteAllText(file,"tipo=trajeto\ntrajeto=1;2;3\n");try{RotaAvalon.TrailRoute.Read(file);Check(false,"recusa");}catch(InvalidDataException){Check(true,"trajeto com 1 ponto é recusado");}
  File.Delete(file);
  using(var form=new Form{StartPosition=FormStartPosition.Manual,Location=new Point(-4000,-4000),Size=new Size(1180,640),ShowInTaskbar=false}){var panel=new RotaAvalon.FarmPanel();form.Controls.Add(panel);form.Show();Application.DoEvents();
   panel.SetMode(true);panel.SetTrail("Trajeto: Teste · 12 pontos · 240 de caminho · 1 materiais vistos · ida e volta");panel.ShowTrail(t.ToMap());Application.DoEvents();
   Check(panel.Manual,"tela no modo Manual");
   if(args.Length>0)using(var bmp=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,form.ClientSize));bmp.Save(args[0]);}
   panel.SetMode(false);Check(!panel.Manual,"volta para Automático");}
  Console.WriteLine("Trajeto manual aprovado.");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}
}
