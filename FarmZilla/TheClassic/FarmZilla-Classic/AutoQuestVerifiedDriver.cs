using System;
using System.Collections.Generic;

// The Classic: executes only decisions backed by VERIFIED task stage and position data.
// No UI starts this driver yet. Raw 32-byte records/IDs must never be passed as verified tasks.
// The action sink is injectable to replay decisions without controlling a game client.
interface IAutoQuestGameCommands {
 void Takeoff(double targetX,double targetZ);
 void GroundPath(double x,double y,double z);
 void FlightPath(double x,double z);
 void Descend();
 void Cultivate(bool enabled);
 void Halt();
}
sealed class AutoQuestDriverResult {
 public AutoQuestMove Stage;
 public AutoQuestTravelCommand Travel;
 public bool Issued;
 public string Message;
 public static AutoQuestDriverResult Of(AutoQuestMove stage,AutoQuestTravelCommand travel,bool issued,string message) {
  return new AutoQuestDriverResult{Stage=stage,Travel=travel,Issued=issued,Message=message};
 }
}
sealed class AutoQuestVerifiedDriver : IDisposable {
 const double GroundLeg=120.0,FlightLeg=300.0,ReachedLeg=8.0,MinimumProgress=3.0;
 static readonly TimeSpan LegTimeout=TimeSpan.FromSeconds(20);
 static readonly TimeSpan LandingPulse=TimeSpan.FromSeconds(2);
 readonly IAutoQuestGameCommands commands;
 readonly AutoQuestTravelPolicy travel=new AutoQuestTravelPolicy();
 bool ownsF12,ownsNavigation,stopped,faulted;
 TimeSpan? legSentAt,landingSentAt;
 double legX,legZ,legStartX,legStartZ,destinationX,destinationZ;
 AutoQuestTravelCommand legMode;
 public bool Running {get{return !stopped&&!faulted;}}
 public bool CultivationOwned {get{return ownsF12;}}
 public AutoQuestVerifiedDriver(IAutoQuestGameCommands actions) {
  if(actions==null)throw new ArgumentNullException("actions");
  commands=actions;
 }
 static double Distance(double ax,double az,double bx,double bz) {
  double dx=ax-bx,dz=az-bz;return Math.Sqrt(dx*dx+dz*dz);
 }
 static AutoQuestActor Snapshot(AutoQuestActor s,bool owned){
  return new AutoQuestActor{X=s.X,Y=s.Y,Z=s.Z,Hp=s.Hp,CharacterVerified=s.CharacterVerified,
   ExclusiveLock=s.ExclusiveLock,FlightKnown=s.FlightKnown,IsFlying=s.IsFlying,F12Owned=owned};
 }
 AutoQuestDriverResult Answer(AutoQuestMove m,AutoQuestTravelCommand c,bool issued,string text){
  return AutoQuestDriverResult.Of(m,c,issued,text);
 }
 void ClearLeg(){legSentAt=null;}
 void SafeStop() {
  if(ownsF12){try{commands.Cultivate(false);}catch{}finally{ownsF12=false;}}
  if(ownsNavigation){try{commands.Halt();}catch{}finally{ownsNavigation=false;}}
  ClearLeg();
 }
 public void Stop(){if(stopped)return;stopped=true;SafeStop();}
 public void Dispose(){Stop();}
 AutoQuestDriverResult Fail(AutoQuestMove move,string message) {
  faulted=true;SafeStop();
  return Answer(move,AutoQuestTravelCommand.Pause,false,message);
 }
 public AutoQuestDriverResult Tick(AutoQuestActor actor,IEnumerable<AutoQuestObservedTask> tasks,
  TimeSpan sessionElapsed,bool? flightItemEquipped=null){
  if(stopped||faulted)return Answer(AutoQuestMove.Pause,AutoQuestTravelCommand.Pause,false,
   "Auto Quest interrompido; iniciar uma nova sessão para continuar.");
  if(actor==null||!actor.CharacterVerified||!actor.ExclusiveLock||actor.Hp<=0||
    sessionElapsed<TimeSpan.Zero||Double.IsNaN(actor.X)||Double.IsInfinity(actor.X)||
    Double.IsNaN(actor.Y)||Double.IsInfinity(actor.Y)||Double.IsNaN(actor.Z)||Double.IsInfinity(actor.Z))
   return Fail(AutoQuestMove.Pause,"Identidade, vida, posição ou exclusividade do personagem inválida.");
  var state=Snapshot(actor,ownsF12);
  AutoQuestDecision plan=AutoQuestWorkflow.Decide(state,tasks);
  try{
   if(plan.Move==AutoQuestMove.Pause)return Fail(plan.Move,plan.Reason);
   if(plan.Move==AutoQuestMove.Finished){Stop();return Answer(plan.Move,AutoQuestTravelCommand.None,false,"Fim confirmado pelo planejador.");}
   if(plan.Move==AutoQuestMove.AwaitVerifiedData){
    // Unknown quest state must not leave a previously enabled F12 running.
    if(ownsF12){commands.Cultivate(false);ownsF12=false;}
    return Answer(plan.Move,AutoQuestTravelCommand.None,false,plan.Reason);
   }
   if(plan.Move==AutoQuestMove.StopF12) {
    if(ownsF12){commands.Cultivate(false);ownsF12=false;ClearLeg();
     return Answer(plan.Move,AutoQuestTravelCommand.None,true,plan.Reason);}
    return Answer(plan.Move,AutoQuestTravelCommand.None,false,"Autocultivo já desligado.");
   }
   if(plan.Move==AutoQuestMove.StartF12) {
    if(!actor.FlightKnown||actor.IsFlying)return Answer(plan.Move,AutoQuestTravelCommand.Pause,false,"F12 exige pouso confirmado.");
    commands.Cultivate(true);ownsF12=true;ClearLeg();
    return Answer(plan.Move,AutoQuestTravelCommand.None,true,"F12 ligado para os objetivos confirmados da região.");
   }
   if(plan.Move==AutoQuestMove.KeepF12||plan.Move==AutoQuestMove.WaitForStageUpdate)
    return Answer(plan.Move,AutoQuestTravelCommand.None,false,plan.Reason);
   if(plan.Move==AutoQuestMove.AcceptQuest||plan.Move==AutoQuestMove.DeliverQuest||
      plan.Move==AutoQuestMove.InteractNpc||plan.Move==AutoQuestMove.ManualReward) {
    // No NPC packet is synthesized. The existing QuestService sends validated captured packets;
    // an NPC action will be plugged in only after a verified decoder exposes the correct packet.
    return Answer(plan.Move,AutoQuestTravelCommand.None,false,
     "Etapa NPC identificada no planejamento; pacote de interação ainda não validado.");
   }
   if(plan.Move!=AutoQuestMove.MoveToNpc&&plan.Move!=AutoQuestMove.MoveToRegion&&
     plan.Move!=AutoQuestMove.WaitForLanding)
    return Answer(plan.Move,AutoQuestTravelCommand.None,false,plan.Reason);

   var order=travel.Select(state,plan,sessionElapsed,flightItemEquipped);
   if(order.Command==AutoQuestTravelCommand.Pause)return Fail(plan.Move,order.Reason);
   if(order.Command==AutoQuestTravelCommand.Land){
    ClearLeg();
    if(landingSentAt.HasValue&&sessionElapsed-landingSentAt.Value<LandingPulse)
     return Answer(plan.Move,order.Command,false,"Aguardando confirmação do pouso.");
    commands.Descend();ownsNavigation=true;landingSentAt=sessionElapsed;
    return Answer(plan.Move,order.Command,true,"Solicitado pulso de descida; confirmar pouso no próximo ciclo.");
   }
   if(order.Command==AutoQuestTravelCommand.TryTakeoff){
    ClearLeg();commands.Takeoff(order.X,order.Z);ownsNavigation=true;
    return Answer(plan.Move,order.Command,true,"Tentativa única de voo enviada; observar estado antes de continuar.");
   }
   if(order.Command!=AutoQuestTravelCommand.GroundAutoPath&&order.Command!=AutoQuestTravelCommand.FlyToTarget)
    return Answer(plan.Move,order.Command,false,order.Reason);

   if(legSentAt.HasValue&&(legMode!=order.Command||destinationX!=order.X||destinationZ!=order.Z))
    ClearLeg();
   if(legSentAt.HasValue){
    if(sessionElapsed<legSentAt.Value)return Fail(plan.Move,"Relógio retrocedeu durante o deslocamento.");
    double remaining=Distance(state.X,state.Z,legX,legZ);
    if(remaining>ReachedLeg&&sessionElapsed-legSentAt.Value<LegTimeout)
     return Answer(plan.Move,order.Command,false,"Trecho já enviado; aguardando deslocamento.");
    if(remaining>ReachedLeg&&Distance(state.X,state.Z,legStartX,legStartZ)<MinimumProgress)
     return Fail(plan.Move,"Personagem sem progresso após 20 segundos; rota interrompida para evitar repetição.");
    ClearLeg();
   }
   double distance=Distance(state.X,state.Z,order.X,order.Z);
   if(distance<=ReachedLeg)return Answer(plan.Move,order.Command,false,"Destino próximo; aguardando nova etapa confirmada.");
   double size=order.Command==AutoQuestTravelCommand.FlyToTarget?FlightLeg:GroundLeg;
   double fraction=Math.Min(1.0,size/distance);
   double x=state.X+(order.X-state.X)*fraction;
   double z=state.Z+(order.Z-state.Z)*fraction;
   if(order.Command==AutoQuestTravelCommand.FlyToTarget)commands.FlightPath(x,z);
   else commands.GroundPath(x,state.Y,z);
   ownsNavigation=true;
   legSentAt=sessionElapsed;legMode=order.Command;legX=x;legZ=z;
   legStartX=state.X;legStartZ=state.Z;destinationX=order.X;destinationZ=order.Z;
   return Answer(plan.Move,order.Command,true,"Trecho de deslocamento enviado para o jogo.");
  }catch(Exception ex){return Fail(plan.Move,"Comando interrompido: "+ex.Message);}
 }
}
