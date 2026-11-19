using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// The game's follow walks on the ground. When the leader is in the air, the follower opens the same flyer the Mat Farm uses.
static class FollowFlight {
 public static bool ShouldTakeoff(bool following,bool leaderFlying,bool selfFlying){return following&&leaderFlying&&!selfFlying;}
 public static bool ShouldMatchHeight(bool following,bool leaderFlying,bool selfFlying,double leaderY,double selfY){
  return following&&leaderFlying&&selfFlying&&RotaAvalon.GameClient.ValidCoordinate(leaderY)&&
   RotaAvalon.GameClient.ValidCoordinate(selfY)&&Math.Abs(leaderY-selfY)>=25;
 }
 public static bool ShouldDescend(bool following,bool leaderFlying,bool selfFlying){
  return following&&!leaderFlying&&selfFlying;
 }
 public static int Height(double worldY){return Math.Max(1,Math.Min(1000,(int)Math.Round(worldY/10.0)));}
}

// Mantem a intencao da tecla separada do trabalho de seguir que o jogo remove no teleporte.
sealed class FollowIntent {
 public readonly int Pid;public readonly uint Id,Target;public bool Enabled,StopPending;public DateTime NextAttempt;
 public FollowIntent(int pid,uint id,uint target,bool enabled){Pid=pid;Id=id;Target=target;Enabled=enabled;}
 public void ToggleTo(bool enabled){Enabled=enabled;StopPending=!enabled;NextAttempt=DateTime.MinValue;}
}
partial class AvalonInterno {
 readonly Dictionary<int,FollowIntent> followIntents=new Dictionary<int,FollowIntent>();
 bool repairingFollow;
 void SyncFollowIntent(){
  var primary=main.SelectedItem as Entry;
  var selected=followers.CheckedItems.Cast<Entry>().Where(rz=>!RotaLock.Busy(rz.Pid)).Where(e=>primary!=null&&e.Pid!=primary.Pid).ToArray();
  var keep=new HashSet<int>(selected.Select(e=>e.Pid));foreach(int pid in followIntents.Keys.Where(pid=>!keep.Contains(pid)).ToArray())followIntents.Remove(pid);
  if(primary==null)return;
  foreach(var e in selected){FollowIntent intent;if(followIntents.TryGetValue(e.Pid,out intent)&&intent.Id==e.Id&&intent.Target==primary.Id)continue;
   try{using(var state=new State(e.Pid)){if(state.Id!=e.Id)continue;followIntents[e.Pid]=new FollowIntent(e.Pid,e.Id,primary.Id,state.Following&&state.Target==primary.Id);}}
   catch{} // Loading: retry discovery on the next tick without changing the requested mode.
  }
 }
 Task Toggle(){
  if(closeInProgress)return Task.FromResult(0);CancelBuffWait();
  if(busy&&!repairingFollow&&partyCancel==null){Note("Aguarde a operação de configuração terminar.");return Task.FromResult(0);}
  SyncFollowIntent();var primary=main.SelectedItem as Entry;if(primary==null){Note("Escolha o principal.");return Task.FromResult(0);}
  var selected=followers.CheckedItems.Cast<Entry>().Where(rz=>!RotaLock.Busy(rz.Pid)).Where(e=>e.Pid!=primary.Pid).ToArray();if(selected.Length==0){Note("Marque um secundário.");return Task.FromResult(0);}
  if(selected.Any(e=>!followIntents.ContainsKey(e.Pid))){Note("Personagem carregando; tente a tecla novamente após entrar no mundo.");return Task.FromResult(0);}
  bool enable=!selected.Any(e=>followIntents[e.Pid].Enabled);followWanted=enable;
  // 0.46.6 (log 05/11 08:05:07: the key turned follow on but nothing moved until two more presses): a key press clears
  // the waiting time left by earlier failed tries and runs at once, ahead of a follow repair already running.
  foreach(var e in selected){followIntents[e.Pid].ToggleTo(enable);followIntents[e.Pid].NextAttempt=DateTime.MinValue;followFails.Remove(e.Pid);}
  status.Text=enable?"SEGUIR ATIVO · mantido após teleporte":"SEGUIR DESATIVADO · pela tecla";
  Note(enable?"Seguir ativado pela tecla; retomada automática após teleporte.":"Seguir desativado pela tecla; retomada automática cancelada.");
  // During teleport only update intent. The existing bridge runs after teleport releases the client.
  return RunFollowNow();
 }
 readonly System.Collections.Generic.Dictionary<int,int> followFails=new System.Collections.Generic.Dictionary<int,int>();readonly Dictionary<int,DateTime> liftNext=new Dictionary<int,DateTime>();bool followYield;
 // The user's commands come first: a running follow repair stops at the next account (up to 4 s) and lets them run.
 async Task YieldFollowRepair(){for(int i=0;i<40&&busy&&attackTicking;i++)await Task.Delay(50);if(!(busy&&repairingFollow))return;followYield=true;try{for(int i=0;i<40&&busy&&repairingFollow;i++)await Task.Delay(100);}finally{followYield=false;}}
 async Task RunFollowNow(){await YieldFollowRepair();await MaintainFollowing();}
 // 0.47.7 (owner: "tem que seguir a milhão"): the TG on yourself sends everyone to follow at once; failed tries (account far
 // away) are retried every 2 s instead of growing up to 30 s, so an account that comes near follows again in 1–3 s.
 const double FollowRetrySeconds=1;
 async Task FollowNowAll(){foreach(var intent in followIntents.Values)if(intent.Enabled)intent.NextAttempt=DateTime.MinValue;followFails.Clear();await MaintainFollowing();}
 sealed class FollowJob {public Entry Entry;public FollowIntent Intent;public bool Stop,Lift,Height,Descend;public double LeaderY;public string Message;public Exception Error;}
 // All accounts at the same time (each command waits up to 1,5 s for the game to confirm), not one after the other.
 async Task MaintainFollowing(){
  if(closeInProgress||busy||buffing||missions.Working)return;// 0.51.13: missão primeiro
  var primary=main.SelectedItem as Entry;if(primary==null)return;
  var selected=followers.CheckedItems.Cast<Entry>().Where(rz=>!RotaLock.Busy(rz.Pid)).Where(e=>e.Pid!=primary.Pid).ToArray();
  var jobs=new System.Collections.Generic.List<FollowJob>();
  // Snapshot do lider apenas uma vez por ciclo: nao confundir modo de voo desconhecido com pouso.
  bool leaderFlying=false,leaderKnown=false;double leaderY=0;
  try{using(var lead=new State(primary.Pid)){if(lead.Id==primary.Id&&lead.FlightKnown){leaderKnown=true;leaderFlying=lead.Flying;leaderY=lead.Y;}}}catch{}
  foreach(var e in selected){if(teleport.IsClientBusy(e.Pid)||missions.IsClientBusy(e.Pid)||AttackEngaged(e.Pid)||MeditationEngaged(e.Pid)||ResBusy(e.Pid)||IsDead(e.Pid)||FoxBusy(e.Pid))continue;FollowIntent intent;
   if(!followIntents.TryGetValue(e.Pid,out intent)||intent.Target!=primary.Id||intent.Id!=e.Id||blocked.Contains(e.Pid)||DateTime.UtcNow<intent.NextAttempt)continue;
   try{bool following,selfFlying,selfKnown;double selfY;using(var state=new State(e.Pid)){if(state.Id!=intent.Id)continue;following=state.Following&&state.Target==intent.Target;selfFlying=state.Flying;selfKnown=state.FlightKnown;selfY=state.Y;}
    if(intent.Enabled){
     // A decolagem no jogo pode cancelar temporariamente a acao Seguir.
     // A INTENCAO do usuario (intent.Enabled), nao o work de seguir atual,
     // governa o espelhamento do voo; o follow sera reparado em outro ciclo.
     if(leaderKnown&&selfKnown){
      DateTime again;bool cooldown=liftNext.TryGetValue(e.Pid,out again)&&DateTime.UtcNow<again;
      if(!cooldown){
       if(FollowFlight.ShouldTakeoff(true,leaderFlying,selfFlying)){
        jobs.Add(new FollowJob{Entry=e,Intent=intent,Lift=true,LeaderY=leaderY});continue;
       }
       if(FollowFlight.ShouldDescend(true,leaderFlying,selfFlying)){
        jobs.Add(new FollowJob{Entry=e,Intent=intent,Descend=true,LeaderY=leaderY});continue;
       }
       if(FollowFlight.ShouldMatchHeight(true,leaderFlying,selfFlying,leaderY,selfY)){
        jobs.Add(new FollowJob{Entry=e,Intent=intent,Height=true,LeaderY=leaderY});continue;
       }
      }
     }
     if(!following)jobs.Add(new FollowJob{Entry=e,Intent=intent,Stop=false});
    }else if(intent.StopPending){if(!following){intent.StopPending=false;continue;}jobs.Add(new FollowJob{Entry=e,Intent=intent,Stop=true});}
   }catch{}}
  if(jobs.Count==0)return;
  busy=true;repairingFollow=true;action.Enabled=false;
  try{
   foreach(var j in jobs)j.Intent.NextAttempt=DateTime.UtcNow.AddSeconds(FollowRetrySeconds);
   await Task.WhenAll(jobs.Select(j=>Task.Run(()=>{try{j.Message=j.Lift?Takeoff(j.Entry.Pid,j.Intent.Target,j.LeaderY):j.Height?MatchFlightHeight(j.Entry.Pid,j.LeaderY):j.Descend?FollowLanding(j.Entry.Pid,j.LeaderY):Command(j.Entry.Pid,j.Intent.Target,j.Stop);}catch(Exception ex){j.Error=ex;}})));
   foreach(var j in jobs){var e=j.Entry;var intent=j.Intent;
    if(j.Lift||j.Height||j.Descend){
     liftNext[e.Pid]=DateTime.UtcNow.AddSeconds(j.Descend?3:2);
     // Nunca impedir que o Seguir se recupere apenas porque ajustamos o voo.
     intent.NextAttempt=DateTime.MinValue;
     if(j.Error==null){if(!j.Descend||j.Message.Contains("confirmado"))Note(e.Name+": voo sincronizado: "+j.Message);
     }else Note(e.Name+": voo sincronizado indisponível: "+j.Error.Message);
     continue;
    }
    if(j.Error==null){intent.NextAttempt=DateTime.MinValue;
     if(j.Stop){Note(e.Name+": seguimento parado pela tecla. "+j.Message);if(!intent.Enabled)intent.StopPending=false;}
     else{followFails.Remove(e.Pid);Note(e.Name+": seguimento retomado. "+j.Message);}continue;}
    int fails;followFails.TryGetValue(e.Pid,out fails);fails++;followFails[e.Pid]=fails;intent.NextAttempt=DateTime.UtcNow.AddSeconds(FollowRetrySeconds);
    if(fails==1||fails%30==0)Note(e.Name+": não foi possível confirmar seguimento"+(fails>1?" ("+fails+" tentativas; tentando a cada "+FollowRetrySeconds+" s — aproxime do principal)":"")+". "+j.Error.Message);}
  }finally{repairingFollow=false;busy=false;action.Enabled=true;}
 }
 // The route's take-off (a short move plus a height) is what opens the flyer. Follow is posted again only after the
 // character is in the air, so the game's follow keeps them with the leader instead of walking underneath.
 string Takeoff(int pid,uint target,double leaderY){
  using(var client=new RotaAvalon.GameClient(pid,new Dictionary<string,bool>())){
   var s=client.Read();if(s.Hp<=0)throw new InvalidOperationException("Personagem sem vida.");
   if(!s.FlightStateKnown)throw new InvalidOperationException("Estado de voo desconhecido; decolagem não enviada.");
   if(s.IsFlying)return "já está no ar.";
   string library=RotaAvalon.RotaPaths.Bridge();
   int lift=FollowFlight.Height(Math.Max(s.Y+50,leaderY));
   double x=s.X+15;if(!RotaAvalon.GameClient.ValidCoordinate(x))x=s.X-15;
   RotaAvalon.NativeBridge.FlyTo(client,x,s.Z,library);
   RotaAvalon.NativeBridge.FlightHeight(client,lift,library);
   var clock=System.Diagnostics.Stopwatch.StartNew();
   while(clock.Elapsed.TotalSeconds<4){System.Threading.Thread.Sleep(200);try{s=client.Read();}catch{continue;}
    if(s.FlightStateKnown&&s.IsFlying){try{Command(pid,target,false);}catch{}return "decolagem confirmada; ajuste à altura do líder pendente.";}}
  }
  return "decolagem enviada; estado de voo ainda não confirmado.";
 }
 string MatchFlightHeight(int pid,double leaderY){
  using(var client=new RotaAvalon.GameClient(pid,new Dictionary<string,bool>())){
   var s=client.Read();if(!s.FlightStateKnown||!s.IsFlying)throw new InvalidOperationException("Secundário não está em voo confirmado.");
   int target=FollowFlight.Height(leaderY);string library=RotaAvalon.RotaPaths.Bridge();
   RotaAvalon.NativeBridge.FlightHeight(client,target,library);
   return "altura-alvo do líder ajustada para "+target+" (Y/10); movimento solicitado.";
  }
 }
 // Usa somente os comandos de descida já suportados; não inventa pacote de fechar asas.
 // Se o cliente não pousar, informa o estado real em vez de afirmar sucesso.
 string FollowLanding(int pid,double leaderY){
  using(var client=new RotaAvalon.GameClient(pid,new Dictionary<string,bool>())){
   var s=client.Read();if(!s.FlightStateKnown)throw new InvalidOperationException("Estado de voo desconhecido.");
   if(!s.IsFlying)return "secundário já está no chão.";
   string library=RotaAvalon.RotaPaths.Bridge();
   // Mesma sequencia de pouso ja usada pelo AutoQuestNativeCommands.Descend:
   // desabilitar o piloto de altitude ANTES de pressionar Z, para nao subir sozinho.
   RotaAvalon.NativeBridge.FlightHeightOff(client,library);
   RotaAvalon.NativeBridge.KeyMove(client,false,library);
   var clock=System.Diagnostics.Stopwatch.StartNew();
   try{while(clock.ElapsedMilliseconds<3000){
    s=client.Read();if(s.FlightStateKnown&&!s.IsFlying)return "pouso confirmado junto ao líder.";
    // Não continuar descendo abaixo da altura lida do líder.
    if(!s.FlightStateKnown||s.Y<=leaderY+12)break;
    client.Vertical(-1);System.Threading.Thread.Sleep(150);
   }}finally{try{client.Vertical(0);}catch{}}
   return "líder pousou; descida solicitada, mas pouso do secundário ainda não confirmado.";
  }
 }
}
