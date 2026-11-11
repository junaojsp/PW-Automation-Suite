using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

// Preserva o layout da tela de liberacao original e usa a API do FarmZilla Control.
// O codigo exibido e o mesmo deviceHash que o painel aceita ao criar uma licenca.
static class FarmZillaControlActivationDialog {
 internal static bool Ensure(OnlineIdentity identity) {
  using(var dialog=new Form {
   Text="FarmZilla · autorização do computador",ClientSize=new Size(590,370),
   FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterScreen,
   MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(12,16,19),
   ForeColor=Color.White,Font=new Font("Segoe UI",(float)(9)*global::UiScale.F)
  }) {
   dialog.Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
   var title=new Label {Text="Liberação pelo gerenciador FarmZilla",Left=20,Top=17,Width=550,Height=28,Font=new Font("Segoe UI",(float)(13)*global::UiScale.F,FontStyle.Bold)};
   var info=new Label {Text="Copie o código deste computador e cadastre no painel.\nInforme a chave autorizada para abrir o FarmZilla.",Left=20,Top=56,Width=550,Height=43};
   var code=new TextBox {Left=20,Top=108,Width=420,Height=48,ReadOnly=true,Multiline=true,Text=FarmZillaControlLicense.DeviceHash(identity),AccessibleName="Código do PC"};
   var copy=new Button {Text="Copiar código",Left=450,Top=108,Width=120,Height=48};
   copy.Click+=(s,e)=>{try{Clipboard.SetText(code.Text);}catch{}};
   var label=new Label {Text="Chave de licença fornecida pelo administrador",Left=20,Top=174,Width=550,Height=23};
   var key=new TextBox {Left=20,Top=199,Width=550,MaxLength=128,Text="",AccessibleName="Chave de licença"};
   var status=new Label {Left=20,Top=237,Width=550,Height=53,ForeColor=Color.Cyan,Text="Informe sua chave para autorizar esta abertura."};
   var detail=new Label {Text="A licença é verificada online em toda abertura.",Left=20,Top=315,Width=335,Height=40,Font=new Font("Segoe UI",(float)(8)*global::UiScale.F)};
   var activate=new Button {Text="Ativar licença",Left=380,Top=316,Width=190,Height=34};
   bool working=false;
   activate.Click+=async(s,e)=>{
    if(working)return;
    string value=key.Text.Trim();
    if(value.Length==0){status.Text="Informe a chave de licença.";return;}
    working=true;activate.Enabled=false;status.Text="Consultando autorização...";
    try {
     var response=await Task.Run(()=>{string message;bool ok=FarmZillaControlLicense.Activate(value,identity,out message);return Tuple.Create(ok,message);});
     if(dialog.IsDisposed)return;
     if(response.Item1){FarmZillaControlLicense.SaveKey(value);dialog.DialogResult=DialogResult.OK;}
     else status.Text=response.Item2;
    }catch(Exception ex){if(!dialog.IsDisposed)status.Text="Falha na validação: "+ex.Message;}
    finally{working=false;if(!dialog.IsDisposed)activate.Enabled=true;}
   };
   dialog.Controls.AddRange(new Control[]{title,info,code,copy,label,key,status,detail,activate});
   dialog.AcceptButton=activate;
   return dialog.ShowDialog()==DialogResult.OK;
  }
 }
}
