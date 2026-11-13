using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Suppress application startup: no licensing, hotkeys or game automation.
static class LayoutPreview {
 sealed class PreviewForm : AvalonInterno {
  protected override void OnLoad(EventArgs e){}
  protected override void OnShown(EventArgs e){}
 }
 static object Get(object target,string name){return typeof(AvalonInterno).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
 static void Call(object target,string name){typeof(AvalonInterno).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);}
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Capture(Form form,string filename){form.Update();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(filename,ImageFormat.Png);}}
 static void Bounds(Control parent){foreach(Control c in parent.Controls){if(!c.Visible)continue;Check(c.Left>=0&&c.Top>=0&&c.Right<=parent.ClientSize.Width&&c.Bottom<=parent.ClientSize.Height,"Controle fora da janela: "+c.Text);if(c.HasChildren)Bounds(c);}}
 [STAThread] static void Main(){
  Win.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var form=new PreviewForm()){
   string temp=Path.Combine(Path.GetTempPath(),"farmzilla-layout-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(temp);
   typeof(AvalonInterno).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,Path.Combine(temp,"config.txt"));
   form.Show();form.Update();
   Check(form.ClientSize==new Size(562,690),"Tamanho inicial inesperado");
   var tools=form.Controls.OfType<SideItem>().ToArray();
   Check(tools.Length==6,"Menu incompleto");
   Check(tools.Single(t=>t.Selected).Text=="Espelho","A tela inicial deve ser Espelho");
   Check(Get(form,"frostAutomation")==null,"Frost não deve abrir na inicialização");
   foreach(var t in tools){Check(t.Right<172,"Ferramenta invade Espelho");Check(!tools.Any(o=>o!=t&&o.Bounds.IntersectsWith(t.Bounds)),"Menu sobreposto");}
   Bounds(form);Capture(form,"farmzilla-layout.png");
   var bar=(Panel)Get(form,"sharedPotBar");
   var gear=bar.Controls.OfType<Button>().Single(b=>b.AccessibleName=="Configurar Auto Pot");gear.PerformClick();
   var drawer=(Panel)Get(form,"potDrawer");
   Check(drawer.Visible&&form.ClientSize.Width==872,"Engrenagem não abre painel lateral");
   var hp=drawer.Controls.OfType<NumericUpDown>().OrderBy(c=>c.Top).First();hp.Value=47;
   Bounds(form);Capture(form,"farmzilla-espelho-autopot-layout.png");
   // Exercise the actual sidebar Click handler, rather than calling navigation directly.
   typeof(Control).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(tools.Single(t=>t.Text=="Frost Farm"),new object[]{EventArgs.Empty});
   var frost=(FrostAutomationForm)Get(form,"frostAutomation");
   Check(frost!=null&&!frost.TopLevel&&frost.Parent!=null,"Frost deve ficar incorporada");
   Check(form.OwnedForms.Length==0,"Frost abriu janela separada");
   Check(drawer.Visible&&ReferenceEquals(drawer,Get(form,"potDrawer"))&&hp.Value==47,"Frost perdeu configurações do Auto Pot");
   Check(tools.Single(t=>t.Selected).Text=="Frost Farm","Seleção lateral Frost incorreta");
   Check(!frost.Controls.OfType<Button>().Any(b=>b.Text.Contains("Dragão")||b.Text.Contains("Lobo")),"Botões de boss não removidos");
   Check(frost.Controls.OfType<CheckedListBox>().Count()==1&&frost.Controls.OfType<ComboBox>().Count()==1,"Seleção de contas ausente");
   Bounds(form);Capture(form,"farmzilla-frost-layout.png");
   Call(form,"ShowEspelhoInline");
   Check(!frost.Visible&&!frost.IsDisposed,"Trocar aba encerrou ou exibiu Frost");
   Check(drawer.Visible&&hp.Value==47,"Espelho perdeu Auto Pot");
   Call(form,"OpenFrostAutomation");Check(ReferenceEquals(frost,Get(form,"frostAutomation")),"Frost recriada ao alternar");
   gear.PerformClick();Check(!drawer.Visible&&form.ClientSize.Width==562,"Engrenagem não recolhe painel");
   Call(form,"ShowEspelhoInline");Check(tools.Single(t=>t.Selected).Text=="Espelho","Volta ao Espelho incorreta");
   Check(hp.Value==47,"Recolher painel perdeu ajustes");
   var keys=drawer.Controls.OfType<ComboBox>().OrderBy(c=>c.Top).ToArray();
   keys[0].SelectedItem=Keys.F1;keys[1].SelectedItem=Keys.F2;
   Call(form,"SaveInlinePot");
   string config=Path.Combine(temp,"farmzilla-auto-pot-config.txt");
   var saved=File.ReadAllLines(config);
   Check(saved.Length==4&&saved[0]=="47"&&saved[2]==((int)Keys.F1).ToString()&&saved[3]==((int)Keys.F2).ToString(),"Persistência HP/MP incompatível");
   keys[1].SelectedItem=Keys.F1;Call(form,"SaveInlinePot");
   Check(File.ReadAllLines(config).SequenceEqual(saved),"Teclas duplicadas sobrescreveram configuração válida");
   Directory.Delete(temp,true);
  }
  Console.WriteLine("Layout Windows: Espelho inicial, Frost incorporada, painel Auto Pot compartilhado e navegação sem perda de ajustes.");
 }
}
