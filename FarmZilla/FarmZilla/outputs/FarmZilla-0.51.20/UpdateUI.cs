using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
partial class AvalonInterno {
 string updateSummary="Atualizar aplicativo";Button updateSettingsButton;System.Windows.Forms.Timer updateTimer;bool updateChecking;string pendingUpdate;
 void InitUpdates(){
  updateTimer=new System.Windows.Forms.Timer{Interval=30*60*1000};updateTimer.Tick+=(s,e)=>{var task=CheckUpdate(false);};
#if FARMZILLA_STANDALONE
  // No startup request to the legacy activation/update service in portable builds.
  Shown+=(s,e)=>{updateSummary="Atualizacao manual disponivel";};
#else
  Shown+=async(s,e)=>{updateTimer.Start();await Task.Delay(3000);if(!IsDisposed)await CheckUpdate(false);};
#endif
  FormClosed+=(s,e)=>{updateTimer.Dispose();if(pendingUpdate!=null)try{AppUpdate.LaunchInstaller(pendingUpdate,Application.ExecutablePath);}catch(Exception ex){MessageBox.Show("Atualização não iniciada: "+ex.Message+"\nAbra o aplicativo e tente novamente.","FarmZilla");}};
 }
 bool ConfigureRepository(){
  using(var dialog=new Form{Text="Atualizações do aplicativo",ClientSize=new Size(350,160),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false}){
   var label=new Label{Text="Endereço HTTPS do servidor privado de atualizações",Left=12,Top=12,Width=325,Height=35};
   var input=new TextBox{Left=12,Top=51,Width=325,Text=AppUpdate.Repository};
   var save=new Button{Text="Salvar",Left=240,Top=99,Width=97};save.Click+=(s,e)=>{try{AppUpdate.Repository=input.Text.Trim();dialog.DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(dialog,ex.Message);}};
   dialog.Controls.AddRange(new Control[]{label,input,save});dialog.AcceptButton=save;return dialog.ShowDialog(this)==DialogResult.OK;
  }
 }
 bool MatFarmRunning(){foreach(var w in rotaWindows)if(!w.IsDisposed&&w.FarmRunning)return true;return false;}
 async Task CheckUpdate(bool interactive){
  if(updateChecking||closeInProgress)return;
  string repository=AppUpdate.Repository;
  if(repository==""){updateSummary="Atualizações privadas ainda não configuradas";if(!interactive||!ConfigureRepository())return;repository=AppUpdate.Repository;}
  updateChecking=true;
  try {
   updateSummary="Consultando versão…";
   string envelope=await Task.Run(()=>Encoding.UTF8.GetString(AppUpdate.Download(AppUpdate.ManifestUrl(repository),AppUpdate.MaxManifest)));
   var manifest=AppUpdate.Verify(envelope,repository,ReleaseInfo.PublicKey);
   if(IsDisposed||closeInProgress)return;
   if(new Version(manifest.version)<=new Version(ReleaseInfo.Version)){updateSummary="App atualizado · "+ReleaseInfo.Version;if(interactive)MessageBox.Show(this,"Você já está usando a versão mais recente.","FarmZilla");return;}
   updateSummary="Disponível: "+manifest.version;updateSettingsButton.Text="Update\ndisponível";Note("Nova versão disponível: "+manifest.version+". Clique em Update.");
   // 0.47.4 (owner: "do nada volta para o Espelho e para"): the automatic check only announces while a Mat Farm route runs;
   // it installs by itself only when nothing is running (it closed the Mat Farm mid-route on 05/11 09:43 and 10:32).
   // 0.47.5 (owner): installing happens only when the user clicks Update; the automatic check just announces it.
   if(!interactive)return;
   if(busy||teleport.Working||missions.Working){MessageBox.Show(this,"Aguarde a operação atual terminar e clique em Atualizar aplicativo novamente.","FarmZilla");return;}
   if(interactive&&MessageBox.Show(this,"Instalar a versão "+manifest.version+" e reiniciar o aplicativo?\nAs teclas salvas serão mantidas. Selecione novamente os personagens após reiniciar.","Atualizar FarmZilla",MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
   updateSummary="Baixando e conferindo…";
   string request=await Task.Run(()=>AppUpdate.Stage(repository,envelope,manifest,Application.ExecutablePath));
   if(IsDisposed||closeInProgress)return;
   if(!interactive&&(busy||teleport.Working||missions.Working||followers.CheckedItems.Count>0||MatFarmRunning()))return;pendingUpdate=request;Note("Atualização verificada. Encerrando capturas antes de reiniciar.");Close();
  }catch(Exception ex){if(!IsDisposed){updateSummary="Não foi possível consultar";Note("Atualizações: "+ex.Message);if(interactive)MessageBox.Show(this,"Atualização não concluída: "+ex.Message,"FarmZilla");}}
  finally{updateChecking=false;}
 }
}

