using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;using System.IO;using System.Linq;using System.Runtime.InteropServices;using System.Security.Cryptography;using System.Windows.Forms;
// Characters running a Rota route. FarmZilla's follow, attack, teleport, buff, quest and party mirroring leave them out
// until the route stops, so the two tools never send competing commands to the same client.
static class RotaLock {
 static readonly object gate=new object();static readonly Dictionary<int,string> busy=new Dictionary<int,string>();
 public static void Set(int pid,string name){lock(gate)busy[pid]=name;}
 public static void Clear(int pid){lock(gate)busy.Remove(pid);}
 public static bool Busy(int pid){lock(gate)return busy.ContainsKey(pid);}
}
namespace RotaAvalon {
 // Rota keeps its files beside FarmZilla's (%LOCALAPPDATA%\AvalonInterno\Rota), never next to the exe.
 // The native bridge travels inside Bagaceira.exe and is written out only when a route needs it; its SHA-256 is pinned
 // here and checked before every load.
 static class RotaPaths {
  public const string BridgeSha256="5d0183acf9c531e1aea90680ffffe3ce228ba76c0b062b8e675c283432b778b6";
  public static string TestRoot;
  public static string Root{get{return TestRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AvalonInterno","Rota");}}
  static string Folder(string name){string d=Path.Combine(Root,name);Directory.CreateDirectory(d);return d;}
  public static string Logs{get{return Folder("logs");}}
  public static string Data{get{return Folder("dados_usuario");}}
  public static string Routes{get{return Folder("rotas");}}
  static readonly object gate=new object();
  static string Sha(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
  public static byte[] Embedded(){using(var s=typeof(RotaPaths).Assembly.GetManifestResourceStream("RotaAvalon.NativeBridge")){if(s==null)throw new IOException("Ponte nativa da Rota ausente neste FarmZilla.");using(var ms=new MemoryStream()){s.CopyTo(ms);var data=ms.ToArray();if(Sha(data)!=BridgeSha256)throw new InvalidDataException("Ponte nativa da Rota com hash inesperado; não usada.");return data;}}}
  public static string Bridge(){lock(gate){Directory.CreateDirectory(Root);string path=Path.Combine(Root,"RotaBridge-"+BridgeSha256.Substring(0,12)+".dll");
   if(File.Exists(path)&&Sha(File.ReadAllBytes(path))==BridgeSha256)return path;
   var data=Embedded();string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllBytes(temp,data);if(File.Exists(path))File.Delete(path);File.Move(temp,path);Verify(path);return path;}}
  public static void Verify(string path){if(Sha(File.ReadAllBytes(path))!=BridgeSha256)throw new InvalidDataException("Ponte nativa da Rota alterada no disco; não carregada.");}
 }
 // Rubi look for the Rota window: the original layout stays, colors and chrome follow FarmZilla.
 static class RotaTheme {
  public static readonly Color Good=Color.FromArgb(110,205,120),Mana=Color.FromArgb(96,150,235);
  [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)]static extern int SetWindowTheme(IntPtr window,string app,string id);
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
  static readonly Font BadgeFont=new Font("Segoe UI",(float)(11)*global::UiScale.F,FontStyle.Bold);
  static bool Same(Color c,int r,int g,int b){return c.A==255&&c.R==r&&c.G==g&&c.B==b;}
  public static Color Map(Color c){
   if(c.A==0)return c;
   if(Same(c,255,255,255))return Rubi.Panel;
   if(Same(c,244,247,250)||Same(c,240,240,240))return Rubi.Background;
   if(Same(c,226,232,237))return Rubi.Field;
   if(Same(c,230,237,244))return Rubi.Shade(Rubi.Panel,10);
   if(Same(c,247,249,251))return Rubi.Shade(Rubi.Field,-5);
   if(Same(c,28,47,66)||Same(c,0,0,0))return Rubi.Text;
   if(Same(c,93,111,128)||Same(c,112,128,144))return Rubi.Muted;
   if(Same(c,36,109,169))return Rubi.Accent;
   return c;}
  public static void DarkTitle(Form form){try{int on=1;DwmSetWindowAttribute(form.Handle,20,ref on,4);int caption=ColorTranslator.ToWin32(Rubi.Panel);DwmSetWindowAttribute(form.Handle,35,ref caption,4);int text=ColorTranslator.ToWin32(Rubi.Text);DwmSetWindowAttribute(form.Handle,36,ref text,4);}catch{}}
  static void Dark(Control c,Action<IntPtr> extra){Action set=()=>{try{SetWindowTheme(c.Handle,"DarkMode_Explorer",null);if(extra!=null)extra(c.Handle);}catch{}};if(c.IsHandleCreated)set();c.HandleCreated+=(s,e)=>set();}
  public static void Apply(Control root){Apply(root,false);var form=root as Form;if(form!=null){if(form.IsHandleCreated)DarkTitle(form);form.HandleCreated+=(s,e)=>DarkTitle(form);}}
  static void Apply(Control c,bool inCard){
   bool card=c.GetType()==typeof(Panel)&&Same(c.BackColor,255,255,255);
   c.BackColor=inCard&&c is Label?Color.Transparent:Map(c.BackColor);c.ForeColor=Map(c.ForeColor);
   if(card)c.Paint+=(s,e)=>{var r=new Rectangle(0,0,c.Width-1,c.Height-1);using(var b=new LinearGradientBrush(new Rectangle(0,0,c.Width,c.Height+1),Rubi.Shade(Rubi.Panel,8),Rubi.Shade(Rubi.Panel,-5),LinearGradientMode.Vertical))e.Graphics.FillRectangle(b,r);using(var p=new Pen(Rubi.Line))e.Graphics.DrawRectangle(p,r);using(var p=new Pen(Color.FromArgb(40,255,255,255)))e.Graphics.DrawLine(p,1,1,c.Width-2,1);};
   var button=c as Button;
   if(button!=null){bool primary=button.BackColor==Rubi.Accent;button.FlatStyle=FlatStyle.Flat;button.BackColor=primary?Rubi.Accent:Rubi.Field;button.ForeColor=primary?Rubi.OnAccent:Rubi.Text;button.FlatAppearance.BorderColor=primary?Rubi.Shade(Rubi.Accent,-35):Rubi.Line;button.FlatAppearance.MouseOverBackColor=Rubi.Shade(button.BackColor,12);button.FlatAppearance.MouseDownBackColor=Rubi.Shade(button.BackColor,-10);button.Cursor=Cursors.Hand;}
   var text=c as TextBox;if(text!=null){text.BorderStyle=BorderStyle.FixedSingle;text.BackColor=Rubi.Field;text.ForeColor=Rubi.Text;if(text.Multiline)Dark(text,null);}
   var number=c as NumericUpDown;if(number!=null){number.BorderStyle=BorderStyle.FixedSingle;number.BackColor=Rubi.Field;number.ForeColor=Rubi.Text;}
   var check=c as CheckBox;if(check!=null){check.FlatStyle=FlatStyle.Flat;check.FlatAppearance.BorderColor=Rubi.Line;check.FlatAppearance.CheckedBackColor=Rubi.Field;check.ForeColor=Rubi.Text;}
   var list=c as ListView;if(list!=null)List(list);
   var grid=c as DataGridView;if(grid!=null)Grid(grid);
   if(c is ScrollBar)Dark(c,null);
   var page=c as TabPage;if(page!=null){page.UseVisualStyleBackColor=false;page.BackColor=Rubi.Background;}
   var split=c as SplitContainer;if(split!=null){split.BackColor=Rubi.Background;split.Panel1.BackColor=split.Panel2.BackColor=Rubi.Background;}
   foreach(Control child in c.Controls)Apply(child,card||(inCard&&!(child is Panel)));}
  static void List(ListView list){list.BackColor=Rubi.Field;list.ForeColor=Rubi.Text;list.BorderStyle=BorderStyle.None;list.OwnerDraw=true;
   list.DrawColumnHeader+=(s,e)=>{using(var b=new SolidBrush(Rubi.Panel))e.Graphics.FillRectangle(b,e.Bounds);using(var p=new Pen(Rubi.Line)){e.Graphics.DrawLine(p,e.Bounds.Right-1,e.Bounds.Top+5,e.Bounds.Right-1,e.Bounds.Bottom-5);e.Graphics.DrawLine(p,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);}TextRenderer.DrawText(e.Graphics,e.Header.Text,list.Font,Rectangle.Inflate(e.Bounds,-5,0),Rubi.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);};
   list.DrawItem+=(s,e)=>e.DrawDefault=true;list.DrawSubItem+=(s,e)=>e.DrawDefault=true;
   Dark(list,h=>{IntPtr header=SendMessage(h,0x101F,IntPtr.Zero,IntPtr.Zero);if(header!=IntPtr.Zero)SetWindowTheme(header,"DarkMode_ItemsView",null);});}
  static void Grid(DataGridView g){g.BackgroundColor=Rubi.Panel;g.GridColor=Rubi.Line;g.BorderStyle=BorderStyle.None;g.EnableHeadersVisualStyles=false;g.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;g.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.Single;g.RowHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
   var selection=Color.FromArgb(96,44,56);
   foreach(var style in new[]{g.DefaultCellStyle,g.AlternatingRowsDefaultCellStyle}){style.ForeColor=Rubi.Text;style.SelectionBackColor=selection;style.SelectionForeColor=Rubi.Text;}
   g.DefaultCellStyle.BackColor=Rubi.Field;g.AlternatingRowsDefaultCellStyle.BackColor=Rubi.Shade(Rubi.Field,-5);
   foreach(var style in new[]{g.ColumnHeadersDefaultCellStyle,g.RowHeadersDefaultCellStyle}){style.BackColor=Rubi.Panel;style.ForeColor=Rubi.Muted;style.SelectionBackColor=Rubi.Panel;style.SelectionForeColor=Rubi.Text;}
   foreach(DataGridViewColumn column in g.Columns){var combo=column as DataGridViewComboBoxColumn;if(combo!=null){combo.FlatStyle=FlatStyle.Flat;combo.DisplayStyle=DataGridViewComboBoxDisplayStyle.Nothing;}}
   g.EditingControlShowing+=(s,e)=>{e.Control.BackColor=Rubi.Field;e.Control.ForeColor=Rubi.Text;};}
  public static void Badge(Graphics g,Rectangle r){g.SmoothingMode=SmoothingMode.AntiAlias;Depth.Shadow(g,r,6,2);Depth.Raised(g,r,6,Rubi.Shade(Rubi.Accent,30),Rubi.Shade(Rubi.Accent,-35),Rubi.Shade(Rubi.Accent,-50));TextRenderer.DrawText(g,"FZ",BadgeFont,r,Rubi.OnAccent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
  // The native tab strip cannot be darkened; it is hidden (RubiTabs) and this strip is drawn above the pages.
  public static Control WithStrip(TabControl tabs){var host=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};host.Controls.Add(new RubiTabStrip(tabs));host.Controls.Add(tabs);tabs.BringToFront();return host;}
 }
 sealed class RubiTabs : TabControl {
  protected override void WndProc(ref Message m){if(m.Msg==0x1328&&!DesignMode){m.Result=(IntPtr)1;return;}base.WndProc(ref m);}
 }
 sealed class RubiTabStrip : Control {
  readonly TabControl tabs;
  public RubiTabStrip(TabControl tabs){this.tabs=tabs;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Height=38;Dock=DockStyle.Top;Font=new Font("Segoe UI",(float)(9.5f)*global::UiScale.F,FontStyle.Bold);Cursor=Cursors.Hand;tabs.SelectedIndexChanged+=(s,e)=>Invalidate();tabs.ControlAdded+=(s,e)=>Invalidate();tabs.ControlRemoved+=(s,e)=>Invalidate();}
  Rectangle[] Slots(Graphics g){var r=new Rectangle[tabs.TabPages.Count];int x=0;for(int i=0;i<r.Length;i++){int w=TextRenderer.MeasureText(g,tabs.TabPages[i].Text,Font).Width+34;r[i]=new Rectangle(x,5,w,Height-9);x+=w+6;}return r;}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;using(var b=new SolidBrush(Rubi.Background))g.FillRectangle(b,ClientRectangle);var slots=Slots(g);
   for(int i=0;i<slots.Length;i++){bool on=i==tabs.SelectedIndex;var r=slots[i];if(on){Depth.Shadow(g,r,6,2);Depth.Raised(g,r,6,Rubi.Shade(Rubi.Panel,14),Rubi.Shade(Rubi.Panel,-4),Rubi.Line);using(var p=new Pen(Rubi.Accent,2))g.DrawLine(p,r.X+12,r.Bottom-3,r.Right-12,r.Bottom-3);}TextRenderer.DrawText(g,tabs.TabPages[i].Text,Font,r,on?Rubi.Text:Rubi.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
   using(var p=new Pen(Rubi.Line))g.DrawLine(p,0,Height-1,Width,Height-1);}
  protected override void OnMouseDown(MouseEventArgs e){using(var g=CreateGraphics()){var slots=Slots(g);for(int i=0;i<slots.Length;i++)if(slots[i].Contains(e.Location)){tabs.SelectedIndex=i;break;}}base.OnMouseDown(e);}
 }
 sealed class RubiBar : ProgressBar {
  public Color Bar=Rubi.Accent;
  public RubiBar(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;using(var b=new SolidBrush(Rubi.Field))g.FillRectangle(b,ClientRectangle);int w=(int)(Width*(Value-Minimum)/(double)Math.Max(1,Maximum-Minimum));if(w>0)using(var b=new LinearGradientBrush(new Rectangle(0,0,w,Math.Max(1,Height)),Rubi.Shade(Bar,25),Rubi.Shade(Bar,-25),LinearGradientMode.Vertical))g.FillRectangle(b,0,0,w,Height);}
 }
}
