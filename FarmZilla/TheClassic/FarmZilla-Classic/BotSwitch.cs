using System;using System.Linq;using System.Threading.Tasks;
// 0.46.3 (owner): the Ativar/Desativar button turns the whole bot on and off. Off: follow stops on every account, Alto
// cultivo is turned off, attack/repeat, PT and missions are cancelled, teleport/mission mirroring pauses and the keys
// are released. On: everything comes back and every account follows the principal again (AutoFollow).
partial class AvalonInterno {
 bool switching;
 async Task SetBotActive(bool on){
  if(switching||closeInProgress)return;await YieldFollowRepair();if(busy){Note("Aguarde a operação atual para "+(on?"ativar":"desativar")+".");return;}
  switching=true;
  try{
   if(!on){
    followWanted=false;StopAttackRepeat(null);RequestAttackStop();CancelParty();missions.CancelAll();
    if(altoOn){altoResume=false;try{await ToggleAlto();}finally{altoResume=true;}}
    var primary=main.SelectedItem as Entry;
    if(primary!=null){SyncFollowIntent();var following=followers.CheckedItems.Cast<Entry>().Where(e=>e.Pid!=primary.Pid&&followIntents.ContainsKey(e.Pid)&&followIntents[e.Pid].Enabled).ToArray();if(following.Length>0)await Toggle();}
    followWanted=false;shortcutActive=false;Bind();
    action.Text="Ativar";status.Text="BOT DESATIVADO · nada é espelhado";Note("Bot desativado: seguir, ataque, alto cultivo, teleporte e missões parados; teclas liberadas.");
   }else{
    shortcutActive=true;followWanted=true;Bind();
    action.Text="Desativar";status.Text="BOT ATIVO · todas as contas seguem o principal";Note("Bot ativado: teclas ligadas e todas as contas voltam a seguir.");
    EnsureEveryoneFollows();
   }
  }finally{switching=false;}
 }
}
