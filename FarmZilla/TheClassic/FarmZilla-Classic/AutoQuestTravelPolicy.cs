using System;

// Pure transport policy for The Classic Auto Quest. This module DOES NOT move the player.
// The live driver must later connect these instructions to the existing Rota native bridge:
// TryTakeoff -> FlyTo/FlightHeight (one bounded attempt); FlyToTarget -> flying path;
// GroundAutoPath -> Navigate (the game's own auto-path); Land -> verified landing.
// A grounded character is NOT proof that a flight item is missing. When equipment
// availability is unknown, try once, then use the ground path if takeoff never succeeds.
// No quest stage, NPC, or destination may be inferred from the raw internal task IDs.
enum AutoQuestTravelCommand {
 None, WaitForFlightState, TryTakeoff, WaitForTakeoff,
 FlyToTarget, GroundAutoPath, Land, Pause
}
sealed class AutoQuestTravelOrder {
 public AutoQuestTravelCommand Command;
 public string Reason;
 public double X,Y,Z;
 public static AutoQuestTravelOrder Of(AutoQuestTravelCommand command,string reason,AutoQuestDecision destination=null){
  return new AutoQuestTravelOrder{Command=command,Reason=reason,
   X=destination==null?0:destination.X,
   Y=destination==null?0:destination.Y,
   Z=destination==null?0:destination.Z};
 }
}

// Keep one policy instance for an Auto Quest session, even as its objective/NPC changes.
// If takeoff fails, don't repeatedly attempt to fly on each new leg or each new quest.
// Reset() is explicit, for example after an equipment change or a new Auto Quest session.
sealed class AutoQuestTravelPolicy {
 public const int TakeoffTimeoutSeconds=6;
 TimeSpan? takeoffRequestedAt;
 bool takeoffUnavailable;
 public void Reset(){takeoffRequestedAt=null;takeoffUnavailable=false;}
 static bool Finite(double value){return !Double.IsNaN(value)&&!Double.IsInfinity(value)&&Math.Abs(value)<=100000;}
 public AutoQuestTravelOrder Select(AutoQuestActor actor,AutoQuestDecision plan,TimeSpan sessionElapsed,
  bool? flightItemEquipped=null){
  if(actor==null||plan==null||!actor.CharacterVerified||!actor.ExclusiveLock||actor.Hp<=0||
    !Finite(actor.X)||!Finite(actor.Y)||!Finite(actor.Z)||sessionElapsed<TimeSpan.Zero)
   return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.Pause,"Sem personagem validado, vida, exclusividade ou tempo consistente.");
  if(plan.Move==AutoQuestMove.WaitForLanding){
   if(!actor.FlightKnown)
    return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.WaitForFlightState,"Aguardar leitura confiável do estado de voo.",plan);
   return actor.IsFlying
    ?AutoQuestTravelOrder.Of(AutoQuestTravelCommand.Land,"Pousar antes do NPC ou F12.",plan)
    :AutoQuestTravelOrder.Of(AutoQuestTravelCommand.None,"Já está no solo; o planejador pode reavaliar a missão.",plan);
  }
  bool navigation=plan.Move==AutoQuestMove.MoveToRegion||plan.Move==AutoQuestMove.MoveToNpc;
  if(!navigation)
   return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.None,"Nenhum deslocamento solicitado.");
  if(plan.QuestIds==null||plan.QuestIds.Length==0||
    !Finite(plan.X)||!Finite(plan.Y)||!Finite(plan.Z))
   return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.Pause,"Destino de missão não validado; não navegar.");
  if(!actor.FlightKnown)
   return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.WaitForFlightState,"Estado de voo transitório; não enviar comandos.",plan);
  if(actor.IsFlying){
   takeoffRequestedAt=null;takeoffUnavailable=false;
   return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.FlyToTarget,"Voo confirmado; viajar pelo ar usando rota existente.",plan);
  }
  // Only a VERIFIED equipment check can skip takeoff without trying.
  if(flightItemEquipped.HasValue&&!flightItemEquipped.Value){
   takeoffRequestedAt=null;
   return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.GroundAutoPath,"Sem item de voo equipado; usar auto caminho do jogo.",plan);
  }
  if(takeoffUnavailable)
   return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.GroundAutoPath,"Decolagem já falhou nesta sessão; usar auto caminho.",plan);
  if(!takeoffRequestedAt.HasValue){
   takeoffRequestedAt=sessionElapsed;
   return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.TryTakeoff,"Tentar decolar uma vez; confirmar o voo pelo estado real.",plan);
  }
  if(sessionElapsed<takeoffRequestedAt.Value)
   return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.Pause,"Relógio da sessão retrocedeu; não repetir decolagem.",plan);
  if(sessionElapsed-takeoffRequestedAt.Value>=TimeSpan.FromSeconds(TakeoffTimeoutSeconds)){
   takeoffRequestedAt=null;takeoffUnavailable=true;
   return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.GroundAutoPath,"Não decolou em 6 segundos; seguir pelo auto caminho.",plan);
  }
  return AutoQuestTravelOrder.Of(AutoQuestTravelCommand.WaitForTakeoff,"Aguardar confirmação de voo sem reenviar decolagem.",plan);
 }
}
