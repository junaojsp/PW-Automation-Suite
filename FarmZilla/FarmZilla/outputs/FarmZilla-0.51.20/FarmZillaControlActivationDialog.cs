using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

// Ativacao padrao pelo FarmZilla Control. Codigo do PC pode ser copiado para o painel.
static class FarmZillaControlActivationDialog {
 internal static bool Ensure(OnlineIdentity identity) {
  string saved=null;
  try { saved=FarmZillaControlLicense.LoadKey(); } catch {}
  if(!String.IsNullOrWhiteSpace(saved)) {
   string result;
   if(FarmZillaControlLicense.Activate(saved,identity,out result))return true;
  }
  using(var form=new Form {
   Text="FarmZilla · ativar licença",ClientSize=new Size(460,288),
   StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,
   MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(12,16,19),
   ForeColor=Color.White,Font=new Font("Segoe UI",9)
  }) {
   var title=new Label {Text="Informe sua licença do FarmZilla",Left=18,Top=16,Width=420,Height=28,Font=new Font("Segoe UI",12,FontStyle.Bold)};
   var info=new Label {Text="A licença será vinculada a este computador.",Left=18,Top=48,Width=420,Height=23};
   var deviceCode=new TextBox {Left=18,Top=96,Width=320,ReadOnly=true,Text=FarmZillaControlLicense.DeviceHash(identity),AccessibleName="Código do computador"};
   var copyCode=new Button {Text="Copiar",Left=348,Top=94,Width=90,Height=29};
   copyCode.Click+=(s,e)=>{try{Clipboard.SetText(deviceCode.Text);}catch{}};
   var codeInfo=new Label {Text="Código deste PC — copie para o painel FarmZilla Control",Left=18,Top=73,Width=420,Height=21};
   var keyLabel=new Label {Text="Chave de licença fornecida pelo administrador",Left=18,Top=140,Width=420,Height=21};
   var key=new TextBox {Left=18,Top=164,Width=420,MaxLength=128,Text=saved??"",AccessibleName="Chave de licença"};
   var status=new Label {Left=18,Top=201,Width=420,Height=35,ForeColor=Color.Cyan};
   var activate=new Button {Text="Ativar licença",Left=295,Top=245,Width=143,Height=30};
   bool working=false;
   activate.Click+=async(s,e)=>{
    if(working)return;
    string value=key.Text.Trim();
    if(value.Length==0){status.Text="Informe a chave.";return;}
    working=true;activate.Enabled=false;status.Text="Validando licença…";
    try {
     var response=await Task.Run(()=>{string message;bool ok=FarmZillaControlLicense.Activate(value,identity,out message);return Tuple.Create(ok,message);});
     if(form.IsDisposed)return;
     if(response.Item1){FarmZillaControlLicense.SaveKey(value);form.DialogResult=DialogResult.OK;}
     else status.Text=response.Item2;
    }catch(Exception ex){if(!form.IsDisposed)status.Text="Falha na validação: "+ex.Message;}
    finally{working=false;if(!form.IsDisposed)activate.Enabled=true;}
   };
   form.Controls.AddRange(new Control[]{title,info,codeInfo,deviceCode,copyCode,keyLabel,key,status,activate});
   form.AcceptButton=activate;
   return form.ShowDialog()==DialogResult.OK;
  }
 }
}
