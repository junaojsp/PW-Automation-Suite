using System;using System.Drawing;using System.IO;using System.Linq;using System.Windows.Forms;
// 0.46.8 (owner): choose who receives the support (macro 2) of the sacerdotes marked "Suporte": the leader (principal, default)
// or any character in the lists. Saved by character name in suporte-alvo.txt; a name that is not open falls back to the leader.
partial class AvalonInterno {
 const string LeaderChoice="Líder (principal)";
 readonly ComboBox supportTarget=new CyanCombo();string supportChoice="";bool supportFilling;
 string SupportFile{get{return Path.Combine(Path.GetDirectoryName(settings),"suporte-alvo.txt");}}
 void SupportTargetField(int x,int y){
  try{if(File.Exists(SupportFile))supportChoice=File.ReadAllText(SupportFile).Trim();}catch{}
  RubiLabel("Suporte em",x,y+4,66,18,8,FontStyle.Regular,SupportColor);
  supportTarget.SetBounds(x+68,y,137,26);supportTarget.DropDownStyle=ComboBoxStyle.DropDownList;supportTarget.AccessibleName="Quem recebe o suporte";
  supportTarget.SelectedIndexChanged+=(o,e)=>{if(supportFilling)return;string pick=supportTarget.SelectedItem as string;supportChoice=pick==null||pick==LeaderChoice?"":pick;try{Directory.CreateDirectory(Path.GetDirectoryName(SupportFile));File.WriteAllText(SupportFile,supportChoice);}catch{}RequestAttackStop();Note("Suporte (macro 2) em: "+(supportChoice==""?"líder (principal)":supportChoice)+".");};
  Controls.Add(supportTarget);hints.SetToolTip(supportTarget,"Em quem o sacerdote marcado em Suporte usa a macro 2 durante o combate. Padrão: o líder (principal).");FillSupportTargets();}
 void FillSupportTargets(){if(supportTarget==null)return;var names=main.Items.Cast<object>().OfType<Entry>().Select(e=>e.Name).Where(n=>!string.IsNullOrEmpty(n)).Distinct().OrderBy(n=>n,StringComparer.CurrentCultureIgnoreCase).ToList();
  supportFilling=true;try{supportTarget.BeginUpdate();supportTarget.Items.Clear();supportTarget.Items.Add(LeaderChoice);if(supportChoice!=""&&!names.Contains(supportChoice))names.Add(supportChoice);foreach(var n in names)supportTarget.Items.Add(n);supportTarget.SelectedItem=supportChoice==""?LeaderChoice:supportChoice;supportTarget.EndUpdate();}finally{supportFilling=false;}}
 CharacterRef SupportTargetRef(Entry primary){if(supportChoice==""||primary==null)return null;var e=main.Items.Cast<object>().OfType<Entry>().FirstOrDefault(x=>x.Name==supportChoice);return e==null||e.Pid==primary.Pid?null:e.Ref();}
}
