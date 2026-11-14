using System;using System.Drawing;using System.IO;using System.Linq;using System.Threading.Tasks;using System.Windows.Forms;
// 0.46.0 (owner): one key toggles the game's own auto-cultivation ("alto cultivo", what F12 does in the game) on the
// principal and the marked secondaries. On: follow pauses, FarmZilla's own attack stays off (otherwise the secondaries
// would attack the principal's target) and each account cultivates by itself. Off again: cultivation stops in all of
// them and they follow the principal again. Native bridge operation 8 (Rota bridge), the same steps as the game's button.
// Hotkey 7; default F11 (Windows reserves F12 for debuggers); saved in alto-cultivo.txt; changed in Ajustes.
partial class AvalonInterno {
 Keys altoChoice=Keys.F11;bool altoCtrl,altoAlt,altoShift,altoOn,altoBusy,altoResume=true;
 string AltoFile{get{return Path.Combine(Path.GetDirectoryName(settings),"alto-cultivo.txt");}}
 string AltoKeyText(){return (altoCtrl?"Ctrl+":"")+(altoAlt?"Alt+":"")+(altoShift?"Shift+":"")+altoChoice;}
 void LoadAlto(){try{if(!File.Exists(AltoFile))return;var p=File.ReadAllText(AltoFile).Trim().Split(',');altoChoice=(Keys)int.Parse(p[0]);altoCtrl=bool.Parse(p[1]);altoAlt=bool.Parse(p[2]);altoShift=bool.Parse(p[3]);}catch{altoChoice=Keys.F11;altoCtrl=altoAlt=altoShift=false;}}
 void SaveAlto(){try{Directory.CreateDirectory(Path.GetDirectoryName(AltoFile));File.WriteAllText(AltoFile,((int)altoChoice)+","+altoCtrl+","+altoAlt+","+altoShift);}catch{}}
 void BindAlto(){if(altoKey!=null)altoKey.Text=AltoKeyText();Win.UnregisterHotKey(Handle,7);if(!shortcutActive||!gameInFront||controlLocked||altoChoice==Keys.None)return;uint mods=0x4000|(altoCtrl?2u:0u)|(altoAlt?1u:0u)|(altoShift?4u:0u);Note(Win.RegisterHotKey(Handle,7,mods,(uint)altoChoice)?"Alto cultivo: atalho "+AltoKeyText():"Tecla do Alto cultivo ocupada; escolha outra em Ajustes.");}
 // Key field in the Comandos card (0.46.1): click and press the key, like the other keys.
 TextBox altoKey;
 TextBox AltoKeyField(){var box=new TextBox{ReadOnly=true,TextAlign=HorizontalAlignment.Center,Text=AltoKeyText()};
  box.KeyDown+=(o,e)=>{e.SuppressKeyPress=true;if(e.KeyCode==Keys.ControlKey||e.KeyCode==Keys.ShiftKey||e.KeyCode==Keys.Menu)return;altoChoice=e.KeyCode;altoCtrl=e.Control;altoAlt=e.Alt;altoShift=e.Shift;box.Text=AltoKeyText();SaveAlto();BindAlto();};
  hints.SetToolTip(box,"Clique e pressione a tecla. Liga o alto cultivo do jogo no principal e nos secundários marcados (param de seguir); apertando de novo desliga e todos voltam a seguir. Padrão: F11.");Controls.Add(box);return box;}
 void AddAltoKey(Form d,int y){
  d.Controls.Add(new Label{Text="Alto cultivo (liga/desliga em todas as contas)",Left=18,Top=y,Width=210,Height=32,ForeColor=Rubi.Text});
  var box=new TextBox{Left=232,Top=y+4,Width=78,ReadOnly=true,TextAlign=HorizontalAlignment.Center,Text=AltoKeyText(),BackColor=Rubi.Field,ForeColor=Rubi.Accent,BorderStyle=BorderStyle.FixedSingle};
  box.KeyDown+=(o,e)=>{e.SuppressKeyPress=true;if(e.KeyCode==Keys.ControlKey||e.KeyCode==Keys.ShiftKey||e.KeyCode==Keys.Menu)return;altoChoice=e.KeyCode;altoCtrl=e.Control;altoAlt=e.Alt;altoShift=e.Shift;box.Text=AltoKeyText();SaveAlto();BindAlto();};
  hints.SetToolTip(box,"Clique e pressione a tecla. Liga o alto cultivo do jogo no principal e nos secundários marcados (param de seguir); apertando de novo desliga e todos voltam a seguir.");d.Controls.Add(box);
 }
 async Task ToggleAlto(){
  if(closeInProgress||altoBusy)return;await YieldFollowRepair();if(busy||teleport.Working||missions.Working){Note("Aguarde a operação atual para o Alto cultivo.");return;}
  var primary=main.SelectedItem as Entry;if(primary==null){Note("Escolha o principal.");return;}
  var targets=new[]{primary}.Concat(followers.CheckedItems.Cast<Entry>().Where(e=>e.Pid!=primary.Pid)).Where(e=>!RotaLock.Busy(e.Pid)&&ProcessAlive(e.Pid)).ToArray();
  bool on=!altoOn;altoBusy=true;
  try{
   if(on){RequestAttackStop();CancelParty();StopAttackRepeat(null);SyncFollowIntent();var secondaries=targets.Where(e=>e.Pid!=primary.Pid).ToArray();
    if(secondaries.Any(e=>followIntents.ContainsKey(e.Pid)&&followIntents[e.Pid].Enabled))await Toggle();}
   altoOn=on;string library=RotaAvalon.RotaPaths.Bridge();int ok=0;
   foreach(var e in targets){var entry=e;try{await Task.Run(()=>RotaAvalon.NativeBridge.AutoPolicy(entry.Pid,on,library));ok++;}catch(Exception ex){Note(entry.Name+": alto cultivo: "+ex.Message);}}
   status.Text=on?"ALTO CULTIVO LIGADO · "+ok+"/"+targets.Length+" contas":"ALTO CULTIVO DESLIGADO · seguindo o principal";
   Note(on?"Alto cultivo ligado em "+ok+"/"+targets.Length+" conta(s); seguir pausado.":"Alto cultivo desligado em "+ok+"/"+targets.Length+" conta(s); voltando a seguir.");
   if(!on&&altoResume){SyncFollowIntent();var secondaries=followers.CheckedItems.Cast<Entry>().Where(e=>e.Pid!=primary.Pid&&!RotaLock.Busy(e.Pid)).ToArray();if(secondaries.Length>0&&secondaries.All(e=>followIntents.ContainsKey(e.Pid))&&!secondaries.Any(e=>followIntents[e.Pid].Enabled))await Toggle();}
  }finally{altoBusy=false;}
 }
}
