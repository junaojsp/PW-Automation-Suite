using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// New automation options are explicitly opt-in and are initially in diagnostic-only mode.
// Never emit an unverified client packet for potion usage, buff effects or recall acceptance.
partial class AvalonInterno {
 readonly CheckBox autoPotOption=new CheckBox();
 readonly CheckBox recallOption=new CheckBox();
 readonly CheckBox epBuffOption=new CheckBox();
 bool autoPotWanted,recallWanted,epBuffWanted;

 string AutomationFlagPath(string name){return Path.Combine(Path.GetDirectoryName(settings),"farmzilla-"+name+".txt");}
 bool ReadAutomationFlag(string name){try{return File.Exists(AutomationFlagPath(name))&&File.ReadAllText(AutomationFlagPath(name)).Trim()=="1";}catch{return false;}}
 void WriteAutomationFlag(string name,bool enabled){
  try{Directory.CreateDirectory(Path.GetDirectoryName(settings));File.WriteAllText(AutomationFlagPath(name),enabled?"1":"0");}
  catch(Exception ex){Note("Não foi possível salvar "+name+": "+ex.Message);}
 }
 CheckBox AutomationBox(CheckBox box,string title,string name,int y,string hint,Action<bool> change){
  box.SetBounds(17,y,148,24);
  box.Text=title;box.Font=new Font("Segoe UI",(float)(9)*global::UiScale.F);
  box.ForeColor=Rubi.Muted;box.BackColor=Color.Transparent;
  box.FlatStyle=FlatStyle.Flat;
  box.AccessibleName=title;box.Checked=ReadAutomationFlag(name);
  change(box.Checked);
  box.CheckedChanged+=(s,e)=>{
   change(box.Checked);WriteAutomationFlag(name,box.Checked);
   Note(title+(box.Checked?": opção marcada; aguardando validação do protocolo do jogo (sem ações automáticas).":": desligado."));
  };
  Controls.Add(box);hints.SetToolTip(box,hint);return box;
 }
 void InitAutomationOptions(){
  RubiLabel("AUTOMAÇÕES",17,197,148,17,8,FontStyle.Bold,Rubi.Text);
  AutomationBox(autoPotOption,"Auto Pot","auto-pot",220,
   "Configuração preservada. Uso de poções ainda não habilitado: falta confirmar slots/atalhos e comandos do cliente.",
   enabled=>autoPotWanted=enabled);
  AutomationBox(recallOption,"Aceitar Recall","aceitar-recall",250,
   "Somente convites de Recall enviados pelo líder aos secundários. Aceite automático depende de identificação confirmada do convite.",
   enabled=>recallWanted=enabled);
  AutomationBox(epBuffOption,"EP buff PT","ep-buff-pt",280,
   "Apenas membros da PT sem buffs. Identificação de efeitos e comando de buff ainda dependem de validação do cliente.",
   enabled=>epBuffWanted=enabled);
 }
}
