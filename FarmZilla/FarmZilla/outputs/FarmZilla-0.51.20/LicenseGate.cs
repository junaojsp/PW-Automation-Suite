using System;using System.Drawing;using System.Threading.Tasks;using System.Windows.Forms;
static class LicenseGate {
 internal static string CurrentToken;internal static OnlineIdentity Identity;internal static string ServiceUrl;
 internal static bool Ensure(){
  // Authorized PCs open straight away: the check runs before any window. The dialog only appears when the PC needs attention.
  string reason=null;
  try{Identity=OnlineIdentity.Load();ServiceUrl=OnlineApi.Service;if(!String.IsNullOrEmpty(ServiceUrl)){var access=OnlineLicense.Evaluate(Identity,ServiceUrl,()=>OnlineApi.Acquire(Identity,ServiceUrl));if(access.Allowed){CurrentToken=access.Token;AuthorizationSession.Begin(access.Token);return true;}reason=access.Reason;}}
  catch(Exception ex){reason=ex.Message;}
  using(var dialog=new Form{Text="FarmZilla · autorização do computador",ClientSize=new Size(590,370),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterScreen,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(12,16,19),ForeColor=Color.White,Font=new Font("Segoe UI",(float)(9)*global::UiScale.F)}){
   dialog.Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
   var title=new Label{Text="Liberação pelo gerenciador FarmZilla",Left=20,Top=17,Width=550,Height=28,Font=new Font("Segoe UI",(float)(13)*global::UiScale.F,FontStyle.Bold)};
   var info=new Label{Text="Copie o código do PC e envie ao administrador.\nDepois de ele ativar, clique em Verificar liberação.",Left=20,Top=56,Width=550,Height=43};
   var code=new TextBox{Left=20,Top=108,Width=420,Height=48,ReadOnly=true,Multiline=true,Text="Identificando hardware…",AccessibleName="Código do PC"};
   var copy=new Button{Text="Copiar código",Left=450,Top=108,Width=120,Height=48,Enabled=false};copy.Click+=(s,e)=>{try{Clipboard.SetText(code.Text);}catch{}};
   var label=new Label{Text="Servidor de ativação fornecido pelo administrador",Left=20,Top=174,Width=550,Height=23};
   var service=new TextBox{Left=20,Top=199,Width=550,Text=OnlineApi.Service,AccessibleName="Servidor de ativação"};
   var status=new Label{Left=20,Top=237,Width=550,Height=53,ForeColor=Color.Cyan,Text=reason??"Aguarde a identificação do computador."};
   var check=new Button{Text="Verificar liberação",Left=380,Top=316,Width=190,Height=34,Enabled=false};
   var detail=new Label{Text="Licença vitalícia: depois de ativado, o FarmZilla funciona também sem internet.",Left=20,Top=315,Width=340,Height=40,Font=new Font("Segoe UI",(float)(8)*global::UiScale.F)};
   bool checking=false;
   Func<Task> verify=async()=>{if(checking||Identity==null)return;checking=true;check.Enabled=false;try{OnlineApi.Service=service.Text.Trim();ServiceUrl=OnlineApi.Service;status.Text="Consultando autorização…";var access=await Task.Run(()=>OnlineLicense.Evaluate(Identity,ServiceUrl,()=>OnlineApi.Acquire(Identity,ServiceUrl)));if(dialog.IsDisposed)return;status.Text=access.Reason;if(access.Allowed){CurrentToken=access.Token;AuthorizationSession.Begin(access.Token);dialog.DialogResult=DialogResult.OK;}}catch(Exception ex){status.Text=ex.Message;}finally{checking=false;if(!dialog.IsDisposed)check.Enabled=true;}};
   check.Click+=async(s,e)=>await verify();
   dialog.Shown+=async(s,e)=>{try{Identity=await Task.Run(()=>OnlineIdentity.Load());if(dialog.IsDisposed)return;code.Text=Identity.Code;copy.Enabled=check.Enabled=true;status.Text=reason??"Envie este código ao administrador para liberar este PC.";}catch(Exception ex){status.Text=ex.Message;code.Text="Identificação indisponível";}};
   dialog.Controls.AddRange(new Control[]{title,info,code,copy,label,service,status,check,detail});return dialog.ShowDialog()==DialogResult.OK;
  }
 }
}

