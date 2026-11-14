using System;using System.Drawing;using System.Linq;using System.Threading;using System.Threading.Tasks;using System.Windows.Forms;
partial class AvalonInterno {
 TextBox buffKey=new TextBox();Keys buffChoice=Keys.D5;bool buffCtrl,buffAlt,buffShift;
 string BuffKeyText(){return(buffCtrl?"Ctrl+":"")+(buffAlt?"Alt+":"")+(buffShift?"Shift+":"")+buffChoice;}
 void BuffKeyEditor(int x,int y){buffKey.SetBounds(x,y,58,16);buffKey.ReadOnly=true;buffKey.BorderStyle=BorderStyle.None;buffKey.Text=BuffKeyText();buffKey.TextAlign=HorizontalAlignment.Center;buffKey.BackColor=Color.Black;buffKey.ForeColor=Color.Cyan;buffKey.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);buffKey.KeyDown+=(o,e)=>{e.SuppressKeyPress=true;if(busy)return;if(e.KeyCode==Keys.ControlKey||e.KeyCode==Keys.ShiftKey||e.KeyCode==Keys.Menu)return;buffChoice=e.KeyCode;buffCtrl=e.Control;buffAlt=e.Alt;buffShift=e.Shift;buffKey.Text=BuffKeyText();Save();};Controls.Add(buffKey);hints.SetToolTip(buffKey,"Escolha a tecla de Bufar. Executa a macro 1 (segundo slot da janela R) uma vez no principal e nos secundários selecionados. Padrão: 5.");}
 void BindBuff(){buffKey.Text=BuffKeyText();Win.UnregisterHotKey(Handle,4);if(!shortcutActive||!gameInFront||controlLocked)return;uint mods=0x4000|(buffCtrl?2u:0u)|(buffAlt?1u:0u)|(buffShift?4u:0u);Note(Win.RegisterHotKey(Handle,4,mods,(uint)buffChoice)?"Bufar: atalho "+BuffKeyText()+" · macro 1, uma vez":"Tecla de Bufar ocupada; escolha outra.");}
 CancellationTokenSource buffCancel;bool buffing;
 // Ends the post-buff wait so another command (or a new Bufar) takes over immediately.
 void CancelBuffWait(){var c=buffCancel;if(c==null)return;buffCancel=null;buffing=false;try{c.Cancel();}catch(ObjectDisposedException){}}
 async Task BuffParty(){if(closeInProgress)return;CancelBuffWait();await YieldFollowRepair();if(busy||teleport.Working||missions.Working){Note("Aguarde a operação atual para bufar.");return;}var primary=main.SelectedItem as Entry;if(primary==null){Note("Escolha o principal.");return;}var selected=new[]{primary}.Where(rz=>!RotaLock.Busy(rz.Pid)).Concat(followers.CheckedItems.Cast<Entry>().Where(rz=>!RotaLock.Busy(rz.Pid)).Where(e=>e.Pid!=primary.Pid)).GroupBy(e=>e.Pid).Select(g=>g.First()).ToArray();
  Entry[] started;busy=true;action.Enabled=false;
  try{await StopAttacks();var results=await Task.WhenAll(selected.Select(e=>Task.Run(()=>blocked.Contains(e.Pid)?new BuffResult{Message=e.Name+": reinicie esse cliente antes de bufar."}:BuffService.Start(e.Ref()))));for(int i=0;i<results.Length;i++){if(results[i].Blocked)blocked.Add(selected[i].Pid);Note(results[i].Message);}started=selected.Where((e,i)=>results[i].Started&&!results[i].Blocked).ToArray();status.Text="Bufar · macro 1 acionada em "+started.Length+"/"+selected.Length;}
  catch(Exception e){Note("Bufar: "+e.Message);return;}finally{busy=false;action.Enabled=true;}
  // While macros run, automatic follow/attack/meditation wait; a key press (any function or Bufar again) ends this wait.
  var cts=new CancellationTokenSource();buffCancel=cts;buffing=true;var token=cts.Token;
  try{await Task.Delay(400,token);while(started.Length>0){token.ThrowIfCancellationRequested();started=await Task.Run(()=>started.Where(e=>{try{return BuffService.Running(e.Ref());}catch{return false;}}).ToArray());if(started.Length>0)await Task.Delay(300,token);}Note("Bufar: execução encerrada. Cada habilidade depende de alcance, alvo e recursos do personagem.");}
  catch(OperationCanceledException){Note("Bufar: espera encerrada; o novo comando assumiu.");}
  finally{if(buffCancel==cts){buffCancel=null;buffing=false;}cts.Dispose();}
 }
}
