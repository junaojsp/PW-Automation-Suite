using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

// Render the real Windows controls with startup events suppressed: no Load/Shown,
// licensing, game discovery, hotkeys or background automation is started.
static class LayoutPreview {
 sealed class PreviewForm : AvalonInterno {
  protected override void OnLoad(EventArgs e){}
  protected override void OnShown(EventArgs e){}
 }
 [STAThread] static void Main(){
  Win.SetProcessDPIAware();Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  using(var form=new PreviewForm()){
   form.Show();form.Update();
   if(form.ClientSize!=new Size(562,654))throw new Exception("Tamanho inesperado da janela");
   foreach(Control control in form.Controls){
    if(control.Bounds.Left<0||control.Bounds.Top<0||control.Bounds.Right>form.ClientSize.Width||control.Bounds.Bottom>form.ClientSize.Height)
     throw new Exception("Controle fora da janela: "+control.Text);
   }
   var tools=form.Controls.OfType<SideItem>().Where(b=>b.AccessibleDescription!=null&&b.AccessibleDescription.StartsWith("Em preparação")).ToArray();
   if(tools.Length!=4)throw new Exception("Menu de ferramentas incompleto");
   foreach(var tool in tools){
    if(tool.Right>=172)throw new Exception("Ferramenta invade painel Espelho");
    if(tools.Any(other=>other!=tool&&other.Bounds.IntersectsWith(tool.Bounds)))throw new Exception("Ferramentas sobrepostas");
   }
   using(var bitmap=new Bitmap(form.Width,form.Height)){
    form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
    bitmap.Save("farmzilla-layout.png",ImageFormat.Png);
   }
  }
  Console.WriteLine("Layout Windows capturado; controles dentro da janela e menu sem sobreposições.");
 }
}
