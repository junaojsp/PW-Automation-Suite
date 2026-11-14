using System;using System.Drawing;using System.IO;using System.Windows.Forms;
// 0.50.2 (owner): "Reviver" box next to Meditação — on (default) = a dead account accepts a resurrection by itself; saved in reviver.txt.
partial class AvalonInterno {
 readonly CheckBox reviveAuto=new CheckBox();volatile bool reviveOn=true;
 string ReviveFile{get{return Path.Combine(Path.GetDirectoryName(settings),"reviver.txt");}}
 void ReviveField(int x,int y){try{if(File.Exists(ReviveFile))reviveOn=File.ReadAllText(ReviveFile).Trim()!="0";}catch{}
  reviveAuto.SetBounds(x,y,62,20);reviveAuto.Text="Reviver";reviveAuto.Checked=reviveOn;reviveAuto.ForeColor=Rubi.Muted;reviveAuto.FlatStyle=FlatStyle.Flat;reviveAuto.BackColor=CardTone(137,268,390);reviveAuto.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);
  reviveAuto.CheckedChanged+=(o,e)=>{reviveOn=reviveAuto.Checked;try{Directory.CreateDirectory(Path.GetDirectoryName(ReviveFile));File.WriteAllText(ReviveFile,reviveOn?"1":"0");}catch{}Note(reviveOn?"Reviver: contas mortas aceitam ressurreição sozinhas.":"Reviver desligado: aceite a ressurreição no jogo.");};
  Controls.Add(reviveAuto);hints.SetToolTip(reviveAuto,"Marcado: conta morta aceita sozinha a ressurreição (Prece da Vida) de outro jogador. Nunca volta à cidade.");}
}
