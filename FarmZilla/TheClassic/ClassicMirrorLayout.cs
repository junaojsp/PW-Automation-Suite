using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FarmZilla.TheClassic {
 sealed partial class ClassicMirrorForm {
  const int SideWidth=172;readonly ComboBox[] rows=new ComboBox[9];readonly ToolTip hints=new ToolTip{ShowAlways=true};
  readonly Label mirrorStatus=new Label();bool syncingRows;
  [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
  public static void SetDpiAware(){SetProcessDPIAware();}
  Label LabelAt(string text,int x,int y,int w,int h,float size=8,Color? color=null,FontStyle style=FontStyle.Regular){var l=new Label{Text=text,Bounds=new Rectangle(x,y,w,h),Font=new Font("Segoe UI",size*UiScale.F,style),ForeColor=color??Rubi.Text,BackColor=Color.Transparent,AutoEllipsis=true};Controls.Add(l);return l;}
  void Drag(Control c){c.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){DragNative.ReleaseCapture();DragNative.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};}
  void Pending(Control c){c.Enabled=false;c.AccessibleDescription="Ainda não validado para o The Classic";hints.SetToolTip(c,c.AccessibleDescription);}
  Button ButtonAt(string text,int x,int y,int w,int h,bool enabled=false){var b=new NeonButton{Text=text,Bounds=new Rectangle(x,y,w,h)};Controls.Add(b);if(!enabled)Pending(b);return b;}
  CheckBox PendingBox(string text,int x,int y,int width){bool caption=text.Length>0;var box=new TickBox{Bounds=new Rectangle(x,y,caption?13:width,caption?16:20),ForeColor=Rubi.Muted,BackColor=Rubi.Panel};Controls.Add(box);Pending(box);if(caption){var label=LabelAt(text,x+16,y,Math.Max(1,width-16),20,7.5f,Rubi.Muted);hints.SetToolTip(label,"Ainda não validado para o The Classic");}return box;}
  void BuildMirrorLayout(){
   Text="FarmZilla · The Classic · 0.5.0";AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(562,690);FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;BackColor=Rubi.Background;ForeColor=Rubi.Text;Font=new Font("Segoe UI",9*UiScale.F);DoubleBuffered=true;
   try{Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
   MouseDown+=(s,e)=>{if(e.Y<48&&e.Button==MouseButtons.Left){DragNative.ReleaseCapture();DragNative.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};
   Drag(LabelAt("FARMZILLA",50,13,115,20,10,Rubi.Accent,FontStyle.Bold));Drag(LabelAt("THE CLASSIC 1.4.4 · x86",50,33,116,15,7,Rubi.Muted));
   LabelAt("FERRAMENTAS",16,72,130,16,7,Rubi.Text,FontStyle.Bold);
   string[] tools={"Espelho","Mat Farm","Vm Farm","Token Farm","Frost Farm","Skill Sender"};
   for(int i=0;i<tools.Length;i++){var tool=new SideItem(tools[i]){Bounds=new Rectangle(12,94+i*42,148,38),Selected=i==0,AccessibleName=tools[i]};Controls.Add(tool);if(i>0)Pending(tool);}
   LabelAt("FarmZilla · The Classic",16,644,150,16,7,Rubi.Muted);LabelAt("v0.5.0 · perfil Classic",16,660,153,16,7,Rubi.Muted);
   var mark=LabelAt("FZ",186,12,28,25,9,Rubi.Accent,FontStyle.Bold);mark.TextAlign=ContentAlignment.MiddleCenter;Drag(mark);
   var brand=LabelAt("FarmZilla",223,8,180,32,13,Rubi.Text,FontStyle.Bold);brand.Font=new Font("Segoe UI Semibold",19,FontStyle.Bold,GraphicsUnit.Pixel);brand.TextAlign=ContentAlignment.MiddleLeft;Drag(brand);Drag(LabelAt("v0.5.0",404,18,79,18,8,Rubi.Muted));
   ButtonAt("—",493,10,25,28,true).Click+=(s,e)=>WindowState=FormWindowState.Minimized;ButtonAt("×",523,10,25,28,true).Click+=(s,e)=>Close();
   LabelAt("PERSONAGEM PRINCIPAL",199,66,208,17,8,Rubi.Text,FontStyle.Bold);LabelAt("Ataque",498,66,48,17,8,Rubi.Accent);
   principal.SetBounds(199,88,300,29);principal.AccessibleName="Principal";principal.SelectedIndexChanged+=(s,e)=>SyncRowsToFollowers();
   ((CyanCombo)principal).ItemClass=o=>o is FollowAccount?((FollowAccount)o).ClassId:-1;
   primaryAttack.Bounds=new Rectangle(511,90,23,23);primaryAttack.BackColor=Rubi.Panel;primaryAttack.AccessibleName="Atacar com o principal";Controls.Add(primaryAttack);hints.SetToolTip(primaryAttack,"O principal também ataca com a macro 0 (Automático e Manual).");
   LabelAt("SECUNDÁRIOS",186,143,128,18,8,Rubi.Text,FontStyle.Bold);LabelAt("Atq.",322,143,34,18,8,Rubi.Accent,FontStyle.Bold);LabelAt("Sup.",364,143,34,18,8,Color.FromArgb(110,205,120),FontStyle.Bold);LabelAt("COMANDOS",410,143,138,18,8,Rubi.Text,FontStyle.Bold);
   for(int i=0;i<rows.Length;i++){
    var row=new CyanCombo{DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(186,168+i*32,128,27),AccessibleName="Secundário "+(i+1)};row.ItemClass=o=>o is FollowAccount?((FollowAccount)o).ClassId:-1;rows[i]=row;row.SelectedIndexChanged+=(s,e)=>SyncRowsToFollowers();Controls.Add(row);
    attackRows[i]=new TickBox{Bounds=new Rectangle(327,170+i*32,24,24),BackColor=Rubi.Panel,Enabled=false,AccessibleName="Atacar com o secundário "+(i+1)};supportRows[i]=new TickBox{Bounds=new Rectangle(370,170+i*32,24,24),BackColor=Rubi.Panel,ForeColor=SupportColor,Enabled=false,AccessibleName="Suporte com o secundário "+(i+1)};
    Controls.Add(attackRows[i]);Controls.Add(supportRows[i]);hints.SetToolTip(attackRows[i],"Ataca o monstro do principal com a macro 0.");hints.SetToolTip(supportRows[i],"Sacerdote: suporte ao líder com a macro 2 durante o combo (no lugar do ataque).");
   }
   string[] commands={"Montar PT","Remont. PT","Bufar","Seguir","Atacar","Alto cultivo"};
   for(int i=0;i<commands.Length;i++){
    LabelAt(commands[i],410,166+i*27,78,20);
    if(i==4){attackKey.Bounds=new Rectangle(493,166+i*27,52,20);attackKey.ReadOnly=true;attackKey.BackColor=Rubi.Field;attackKey.ForeColor=Rubi.Accent;attackKey.TextAlign=HorizontalAlignment.Center;attackKey.BorderStyle=BorderStyle.FixedSingle;attackKey.AccessibleName="Tecla Atacar";Controls.Add(attackKey);hints.SetToolTip(attackKey,"Tecla para iniciar a macro 0 no modo Manual (com o jogo em primeiro plano). Clique e pressione a tecla. Padrão: 1.");continue;}
    var key=new TextBox{Text=i==3?"Botão":"—",Bounds=new Rectangle(493,166+i*27,52,20),ReadOnly=true,BackColor=Rubi.Field,ForeColor=Rubi.Accent,TextAlign=HorizontalAlignment.Center,BorderStyle=BorderStyle.FixedSingle,AccessibleName="Tecla "+commands[i]};Controls.Add(key);Pending(key);
   }
   LabelAt("Modo ataque",410,330,80,18,8,Rubi.Muted);var modeKey=new TextBox{Text="—",Bounds=new Rectangle(493,328,52,20),BackColor=Rubi.Field,ForeColor=Rubi.Accent,TextAlign=HorizontalAlignment.Center};Controls.Add(modeKey);Pending(modeKey);
   attackMode.Bounds=new Rectangle(410,351,138,26);attackMode.AccessibleName="Modo de ataque";attackMode.Items.AddRange(new object[]{"Automático","Manual por tecla"});attackMode.SelectedIndex=1;Controls.Add(attackMode);hints.SetToolTip(attackMode,"Automático: enquanto o principal tiver um monstro selecionado, os marcados repetem a macro 0; selecione você mesmo para parar. Manual: uma vez por tecla Atacar.");
   PendingBox("Meditação",410,380,78);PendingBox("Reviver",488,380,64);PendingBox("EP buff",484,402,64);PendingBox("Aceitar Recall",410,424,138);
   ButtonAt("Montar PT",410,454,138,28);ButtonAt("Remontar PT",410,488,138,28);ButtonAt("Bufar",410,522,138,28);
   LabelAt("Suporte em",186,470,64,18,8,Color.FromArgb(110,205,120));supportTarget.Bounds=new Rectangle(250,463,141,26);supportTarget.AccessibleName="Suporte em";supportTarget.Items.Add("Líder (principal)");supportTarget.SelectedIndex=0;Controls.Add(supportTarget);hints.SetToolTip(supportTarget,"Quem recebe a macro 2 dos sacerdotes em suporte.");
   LabelAt("The Classic · Seguir e Ataque em teste",186,499,218,24,8,Rubi.Muted);PendingBox("EP",410,553,58);PendingBox("WF auto",476,553,72);
   mirrorStatus.SetBounds(186,577,362,19);mirrorStatus.Text="Aguardando clientes";mirrorStatus.Font=new Font("Segoe UI",8*UiScale.F);mirrorStatus.ForeColor=Rubi.Muted;mirrorStatus.BackColor=Color.Transparent;Controls.Add(mirrorStatus);
   follow.SetBounds(186,607,168,34);follow.Text="Seguir principal";((NeonButton)follow).Primary=true;
   sync.SetBounds(360,607,92,34);sync.Text="Sincronizar\npersonagens";sync.AccessibleName="Sincronizar personagens";
   var update=ButtonAt("Update",458,607,90,34);update.AccessibleName="Update";
   var auto=PendingBox("Auto Pot",184,662,78);auto.BackColor=Rubi.Background;ButtonAt("⚙",264,657,34,30);
   logs.SetBounds(458,657,90,30);logs.Text="Registros";logs.AccessibleName="Abrir registros";
   Controls.AddRange(new Control[]{principal,follow,sync,logs});hints.SetToolTip(logs,"Abrir pasta do log automático do The Classic");
   InitAttack();
   FormClosed+=(s,e)=>{hints.Dispose();output.Dispose();followers.Dispose();stop.Dispose();};
  }
  internal void BindAccounts(List<FollowAccount> accounts){
   syncingRows=true;try{principal.Items.Clear();followers.Items.Clear();foreach(var a in accounts){principal.Items.Add(a);followers.Items.Add(a,false);}foreach(var row in rows){row.Items.Clear();row.Items.Add("—");foreach(var a in accounts)row.Items.Add(a);row.SelectedIndex=0;}if(principal.Items.Count>0)principal.SelectedIndex=0;}finally{syncingRows=false;}SyncRowsToFollowers();mirrorStatus.Text="Personagens: "+accounts.Count+" · The Classic";
  }
  void SyncRowsToFollowers(){
   if(syncingRows)return;syncingRows=true;try{
    var leader=principal.SelectedItem as FollowAccount;var selected=new HashSet<int>();
    foreach(var row in rows){var account=row.SelectedItem as FollowAccount;if(account==null)continue;if(leader!=null&&account.Pid==leader.Pid||!selected.Add(account.Pid)){row.SelectedIndex=0;continue;}}
    for(int i=0;i<followers.Items.Count;i++)followers.SetItemChecked(i,selected.Contains(((FollowAccount)followers.Items[i]).Pid));
   }finally{syncingRows=false;}
   UpdateAttackRows();
  }
  void EnableRows(bool enabled){foreach(var row in rows)if(row!=null)row.Enabled=enabled;}
  void UpdateMirrorStatus(string text){
   if(text.StartsWith("STATE_CONFIRMED"))mirrorStatus.Text=text.Contains("followTarget=0 ")?"Seguimento parado":"Seguindo principal · confirmado";
   else if(text.StartsWith("COMMAND_ERROR")||text.StartsWith("ERROR"))mirrorStatus.Text="Não foi possível concluir. Consulte Registros.";
  }
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);FarmZillaFrame.Paint(e.Graphics,ClientSize.Width,ClientSize.Height,SideWidth,true);}
  public void PaintBehind(PaintEventArgs e){FarmZillaFrame.Paint(e.Graphics,ClientSize.Width,ClientSize.Height,SideWidth,true);}
 }
}
