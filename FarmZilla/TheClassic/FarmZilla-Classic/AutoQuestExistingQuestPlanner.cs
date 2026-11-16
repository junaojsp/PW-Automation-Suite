using System;
using System.Collections.Generic;
using System.Linq;

// Existing accepted quests always take precedence over new NPC offers.
// Raw task records are not accepted-quest evidence; this selector requires
// an independently decoded and verified accepted state for each task.
static class AutoQuestExistingQuestPlanner {
 public static AutoQuestDecision Select(AutoQuestActor actor,IEnumerable<AutoQuestObservedTask> observed){
  if(observed==null)return AutoQuestDecision.Of(AutoQuestMove.AwaitVerifiedData,
   "Diário de missões indisponível; não navegar.");
  var tasks=observed.Where(t=>t!=null&&t.QuestId!=0).ToList();
  var owned=tasks.Where(t=>t.AcceptedVerified&&t.StageVerified&&
   t.StageRevision>0&&t.Stage!=AutoQuestStage.Finished).ToList();
  if(owned.Count>0){
   // Do not start a new quest while an accepted task still needs attention.
   // Incomplete accepted data must stop the workflow rather than letting
   // the planner skip ahead to a new NPC offer.
   var result=AutoQuestWorkflow.Decide(actor,owned);
   if(result.Move==AutoQuestMove.AwaitVerifiedData||
      result.Move==AutoQuestMove.WaitForStageUpdate)
    return AutoQuestDecision.Of(AutoQuestMove.AwaitVerifiedData,
     "Há missões já aceitas, mas faltam objetivos, contadores ou destinos "+
     "validados. Não aceitar outra missão e não iniciar voo.");
   return result;
  }
  // A raw ID, a title, or an unverified accepted flag does not prove
  // that the character has an active quest. Never infer acceptance.
  if(tasks.Any(t=>!t.StageVerified||t.StageRevision<=0))
   return AutoQuestDecision.Of(AutoQuestMove.AwaitVerifiedData,
    "Registros internos presentes, mas estado das missões não decodificado. "+
    "Não presumir que são missões aceitas nem buscar novas automaticamente.");
  return AutoQuestWorkflow.Decide(actor,tasks);
 }
 public static string Summary(IEnumerable<AutoQuestObservedTask> tasks){
  var all=(tasks??Enumerable.Empty<AutoQuestObservedTask>())
   .Where(t=>t!=null&&t.QuestId!=0).ToList();
  int accepted=all.Count(t=>t.AcceptedVerified&&t.StageVerified&&t.StageRevision>0&&
   t.Stage!=AutoQuestStage.Finished);
  return "missões aceitas validadas="+accepted+
   "; registros observados="+all.Count+
   "; prioridade=concluir aceitas antes de procurar novas";
 }
}
