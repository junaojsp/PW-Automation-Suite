using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
partial class AvalonInterno {
 string updateSummary="Atualizar aplicativo";Button updateSettingsButton;bool updateChecking;string pendingUpdate;string pendingControlUpdate;
 // The Classic: the update channels (FarmZilla Control and the private repository) publish the Avalon executable.
 static readonly bool UpdatesDisabled=true;
 void InitUpdates(){updateSummary="Atualização automática desativada (The Classic)";}
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
  if(UpdatesDisabled){if(interactive)MessageBox.Show(this,"Esta versão do FarmZilla The Classic não se atualiza sozinha; use o executável gerado pelo build.","FarmZilla");return;}
  if(LicenseGate.ControlMode){
   updateChecking=true;
   try{
    updateSummary="Consultando FarmZilla Control...";
    var available=await Task.Run(()=>FarmZillaControlUpdate.Check(LicenseGate.Identity));
    if(IsDisposed||closeInProgress)return;
    if(available==null){
     updateSummary="App atualizado · "+ReleaseInfo.Version;
     if(interactive)MessageBox.Show(this,"Nenhuma versão mais recente publicada no FarmZilla Control.","FarmZilla");
     return;
    }
    updateSummary="Disponível: "+available.version;
    updateSettingsButton.Text="Update\ndisponível";
    Note("Nova versão publicada no FarmZilla Control: "+available.version+".");
    if(!interactive)return; // Never interrupt a farm with the automatic periodic check.
    if(busy||teleport.Working||missions.Working||MatFarmRunning()||closeInProgress){
     MessageBox.Show(this,"Finalize as operações de farm, ataque, missões e teleporte antes da atualização.","FarmZilla");
     return;
    }
    if(MessageBox.Show(this,"Baixar e instalar FarmZilla "+available.version+" pelo FarmZilla Control?\n"+
       "O aplicativo será encerrado e reaberto. Configurações e licença serão preservadas.",
       "Atualizar FarmZilla",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
    updateSummary="Baixando e verificando...";
    string request=await Task.Run(()=>FarmZillaControlUpdate.Stage(available,LicenseGate.Identity,Application.ExecutablePath));
    if(IsDisposed||closeInProgress)return;
    if(busy||teleport.Working||missions.Working||MatFarmRunning()){
     MessageBox.Show(this,"Uma operação começou durante o download. Tente novamente quando terminar.","FarmZilla");
     return;
    }
    pendingControlUpdate=request;
    Note("Download conferido. Encerrando o FarmZilla com segurança para instalar.");
    Close();
   }catch(Exception ex){
    if(!IsDisposed){updateSummary="Não foi possível atualizar";Note("Update FarmZilla Control: "+ex.Message);
     if(interactive)MessageBox.Show(this,"Atualização não concluída: "+ex.Message,"FarmZilla",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   }finally{updateChecking=false;}
   return;
  }
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

