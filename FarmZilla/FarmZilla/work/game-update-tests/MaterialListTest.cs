using System;using System.Collections.Generic;using System.Diagnostics;using System.Drawing;using System.Linq;using System.Reflection;using System.Windows.Forms;
// Autoteste 0.43.0 sem jogo: lista do Mat Farm com todos os coletáveis do catálogo, uma linha por nome, pesquisa sem acento
// e custo por atualização (a tela atualiza a cada segundo).
class MaterialListTest {
 static void Check(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS "+what);}
 [STAThread]static int Main(){try{
  var catalog=RotaAvalon.ResourceCatalog.LoadEmbedded();var items=catalog.Definitions.Values.Where(d=>catalog.Ground(d.id)).Select(d=>new RotaAvalon.FarmMaterial{Id=d.id,Name=d.nome.Trim(),Points=d.id%7==0?12:0}).ToList();
  using(var form=new Form{StartPosition=FormStartPosition.Manual,Location=new Point(-3000,-3000),Size=new Size(900,700),ShowInTaskbar=false}){var panel=new RotaAvalon.FarmPanel();form.Controls.Add(panel);form.Show();Application.DoEvents();
   var chosen=new HashSet<int>{items.First(m=>m.Name.Contains("Carv")).Id};panel.SetMaterials(items,chosen);
   var list=(ListView)typeof(RotaAvalon.FarmPanel).GetField("materials",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);int names=items.Select(m=>m.Name).Distinct().Count();
   Check(list.Items.Count==names,"todos os "+names+" coletáveis aparecem, um por nome ("+items.Count+" variantes)");
   Check(list.Items[0].Checked,"escolhido fica no topo");
   var search=(TextBox)typeof(RotaAvalon.FarmPanel).GetField("search",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);search.Text="CARVAO";Application.DoEvents();
   Check(list.Items.Count>0&&list.Items.Cast<ListViewItem>().All(r=>r.Text.ToLowerInvariant().Contains("carv")),"pesquisa \"CARVAO\" acha os de carvão ("+list.Items.Count+")");search.Text="";Application.DoEvents();
   var clock=Stopwatch.StartNew();for(int i=0;i<50;i++){items[i%items.Count].Points++;panel.SetMaterials(items,chosen);}double ms=clock.ElapsedMilliseconds/50.0;
   Check(ms<5,"atualização da lista leve: "+ms.ToString("F2")+" ms por segundo de farm (pontos novos reordenam no máximo a cada 3 s)");
   form.Hide();clock.Restart();for(int i=0;i<50;i++)panel.SetMaterials(items,chosen);Check(clock.ElapsedMilliseconds<5,"oculta não redesenha ("+clock.ElapsedMilliseconds+" ms em 50)");}
  Check(catalog.Ground(3090)&&catalog.Ground(3096)&&!catalog.Definitions.Values.Any(d=>d.nome.StartsWith("Ba")&&catalog.Ground(d.id)),"só materiais de chão (carvão e ervas sim, baús não)");
  var shown=catalog.Definitions.Values.Where(d=>catalog.Ground(d.id)).Select(d=>catalog.FarmName(d.id)).ToList();
  Check(catalog.FarmName(3082)=="Liga de Aço"&&catalog.FarmName(3080)=="Aço de Alto Carbono"&&catalog.FarmName(3076)=="Tábua Polida"&&catalog.MaterialName(3082)=="Minério de Ferro Negro","lista mostra o item que o material dá (0.47.9)");
  Check(shown.Distinct().Count()==shown.Count,"cada material com nome de item diferente ("+shown.Count+")");
  var withSource=items.Select(m=>new RotaAvalon.FarmMaterial{Id=m.Id,Name=catalog.FarmName(m.Id),Source=catalog.MaterialName(m.Id)}).ToList();
  using(var form2=new Form{StartPosition=FormStartPosition.Manual,Location=new Point(-3000,-3000),Size=new Size(900,700),ShowInTaskbar=false}){var p2=new RotaAvalon.FarmPanel();form2.Controls.Add(p2);form2.Show();p2.SetMaterials(withSource,new HashSet<int>());Application.DoEvents();
   var list2=(ListView)typeof(RotaAvalon.FarmPanel).GetField("materials",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p2);var s2=(TextBox)typeof(RotaAvalon.FarmPanel).GetField("search",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p2);
   s2.Text="ferro negro";Application.DoEvents();Check(list2.Items.Count==2&&list2.Items.Cast<ListViewItem>().Any(r=>r.Text=="Liga de Aço"),"pesquisa pelo nome do material acha o item (\"ferro negro\" → Liga de Aço e Aço de Alto Carbono)");}
  Console.WriteLine("Lista de materiais aprovada.");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}
}
