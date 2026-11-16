using System;
using System.Collections.Generic;
using System.Linq;

// Never turn a nearby NPC, raw task ID or catalog title into a route.
// A destination is eligible only when the quest stage and its location were
// independently verified by the game. The existing flight routine is reused
// only AFTER this gate, never with the old ProFile/CheirosoWB test coordinates.
static class AutoQuestScanPlanner {
 public static AutoQuestDecision Select(AutoQuestActor actor,IEnumerable<AutoQuestObservedTask> tasks){
  var result=AutoQuestExistingQuestPlanner.Select(actor,tasks);
  if(result.Move!=AutoQuestMove.MoveToNpc&&
     result.Move!=AutoQuestMove.MoveToRegion)
   return AutoQuestDecision.Of(AutoQuestMove.AwaitVerifiedData,
    "Scanner não encontrou uma etapa com NPC/objetivo e coordenadas validados. "+
    "Não enviar voo ou caminhada para NPC candidato nem para destino fixo.");
  if(result.QuestIds==null||result.QuestIds.Length==0||
    !RotaAvalon.GameClient.ValidCoordinate(result.X)||
    !RotaAvalon.GameClient.ValidCoordinate(result.Z))
   return AutoQuestDecision.Of(AutoQuestMove.AwaitVerifiedData,
    "Destino inválido ou sem missão associada.");
  return result;
 }
 public static string Describe(AutoQuestDecision decision){
  if(decision==null)return "Sem decisão de missão.";
  if(decision.Move==AutoQuestMove.AwaitVerifiedData)return decision.Reason;
  return "Ação="+decision.Move+"; IDs="+String.Join(",",decision.QuestIds)+
   "; X="+decision.X.ToString("0.0")+"; Z="+decision.Z.ToString("0.0")+
   "; NPC="+decision.NpcInstanceId+"; "+decision.Reason;
 }
}
