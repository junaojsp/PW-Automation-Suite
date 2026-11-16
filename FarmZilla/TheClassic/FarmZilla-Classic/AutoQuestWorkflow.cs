using System;
using System.Collections.Generic;
using System.Linq;

// The Classic: verified quest-stage planner. NO game writes or automation in this module.
// The raw IDs from CharacterMemory.ActiveQuestIds() are NOT verified objective status.
// A decoder must populate these observations with game-confirmed stage/NPC/progress data.
enum AutoQuestStage { Unknown, AcceptAtNpc, TalkToNpc, KillMonsters, CollectItems, DeliverToNpc, Finished }
enum AutoQuestMove { AwaitVerifiedData, MoveToNpc, InteractNpc, AcceptQuest, DeliverQuest, MoveToRegion, StartF12, KeepF12, StopF12, WaitForLanding, WaitForStageUpdate, ManualReward, Finished, Pause }

sealed class AutoQuestObservedTask {
 public ushort QuestId;
 public string Title,RegionKey;
 public AutoQuestStage Stage;
 public int StageRevision;
 public bool StageVerified,LocationVerified,AvailableVerified,AcceptedVerified,ReadyForDeliveryVerified,HasRewardChoice;
 public uint NpcInstanceId;
 public double X,Y,Z;
 public int Progress,Required;
 // The quest may start at NPC A and need NPC B later. This field is for the CURRENT stage, not the original giver.
}
sealed class AutoQuestActor {
 public double X,Y,Z;
 public bool CharacterVerified,ExclusiveLock,FlightKnown,IsFlying,F12Owned;
 public int Hp;
}
sealed class AutoQuestDecision {
 public AutoQuestMove Move;
 public string Reason,RegionKey;
 public uint NpcInstanceId;
 public ushort[] QuestIds=new ushort[0];
 public double X,Y,Z;
 public static AutoQuestDecision Of(AutoQuestMove move,string reason,IEnumerable<AutoQuestObservedTask> tasks=null) {
  return new AutoQuestDecision{Move=move,Reason=reason,QuestIds=tasks==null?new ushort[0]:tasks.Select(t=>t.QuestId).Distinct().OrderBy(id=>id).ToArray()};
 }
}

static class AutoQuestWorkflow {
 const double TalkRange=12.0,ArrivalRange=25.0;
 static bool Finite(double value){return !Double.IsNaN(value)&&!Double.IsInfinity(value)&&Math.Abs(value)<=100000;}
 static bool Location(AutoQuestObservedTask t){return t.LocationVerified&&Finite(t.X)&&Finite(t.Y)&&Finite(t.Z);}
 static double Distance(AutoQuestActor p,AutoQuestObservedTask t){
  double dx=p.X-t.X,dz=p.Z-t.Z;return Math.Sqrt(dx*dx+dz*dz);
 }
 static bool NpcStage(AutoQuestStage stage){
  return stage==AutoQuestStage.AcceptAtNpc||stage==AutoQuestStage.TalkToNpc||stage==AutoQuestStage.DeliverToNpc;
 }
 static bool Ready(AutoQuestObservedTask t){
  if(t.QuestId==0||t.StageRevision<=0||!t.StageVerified||!Location(t))return false;
  switch(t.Stage){
   case AutoQuestStage.AcceptAtNpc:return t.NpcInstanceId!=0&&t.AvailableVerified&&!t.AcceptedVerified;
   case AutoQuestStage.TalkToNpc:return t.NpcInstanceId!=0&&t.AcceptedVerified;
   case AutoQuestStage.DeliverToNpc:return t.NpcInstanceId!=0&&t.ReadyForDeliveryVerified;
   case AutoQuestStage.KillMonsters:return t.AcceptedVerified&&!String.IsNullOrWhiteSpace(t.RegionKey)&&t.Required>0&&t.Progress>=0&&t.Progress<=t.Required;
   case AutoQuestStage.CollectItems:return t.AcceptedVerified&&!String.IsNullOrWhiteSpace(t.RegionKey)&&t.Required>0&&t.Progress>=0&&t.Progress<=t.Required;
   default:return false;
  }
 }
 static AutoQuestDecision Destination(AutoQuestMove move,string why,List<AutoQuestObservedTask> tasks){
  var first=tasks[0];var decision=AutoQuestDecision.Of(move,why,tasks);
  decision.X=first.X;decision.Y=first.Y;decision.Z=first.Z;
  decision.NpcInstanceId=first.NpcInstanceId;decision.RegionKey=first.RegionKey;
  return decision;
 }
 // Re-evaluate after EVERY observed stage transition. Do not advance a quest because an action was merely sent.
 public static AutoQuestDecision Decide(AutoQuestActor actor,IEnumerable<AutoQuestObservedTask> observed){
  if(actor==null||observed==null)return AutoQuestDecision.Of(AutoQuestMove.AwaitVerifiedData,"Sem observação validada.");
  if(!actor.CharacterVerified||!actor.ExclusiveLock||!Finite(actor.X)||!Finite(actor.Y)||!Finite(actor.Z)||actor.Hp<=0)
   return AutoQuestDecision.Of(actor.F12Owned?AutoQuestMove.StopF12:AutoQuestMove.Pause,"Personagem, vida ou exclusividade indisponível.");
  var all=observed.Where(t=>t!=null&&t.QuestId!=0).ToList();
  var candidates=all.Where(Ready).ToList();
  var fights=candidates.Where(t=>t.Stage==AutoQuestStage.KillMonsters&&t.Progress<t.Required).ToList();
  // A region label alone is not enough: separate far-apart objectives even if they share a region name.
  var groups=new List<List<AutoQuestObservedTask>>();
  foreach(var label in fights.GroupBy(t=>t.RegionKey,StringComparer.OrdinalIgnoreCase)){
   var remaining=label.OrderBy(t=>t.QuestId).ToList();
   while(remaining.Count>0){
    var anchor=remaining[0];
    var together=remaining.Where(t=>Math.Sqrt(Math.Pow(t.X-anchor.X,2)+Math.Pow(t.Z-anchor.Z,2))<=ArrivalRange).ToList();
    groups.Add(together);
    foreach(var task in together)remaining.Remove(task);
   }
  }
  groups=groups.OrderByDescending(g=>g.Count).ThenBy(g=>Distance(actor,g[0])).ToList();
  // Keep F12 on until ALL verified kill objectives at the current region are complete.
  var current=groups.FirstOrDefault(g=>g.All(t=>Distance(actor,t)<=ArrivalRange));
  if(actor.F12Owned){
   if(current!=null)return Destination(AutoQuestMove.KeepF12,"Outras missões de combate nesta região ainda estão pendentes.",current);
   return AutoQuestDecision.Of(AutoQuestMove.StopF12,"Encerrar o F12 antes de conversar, viajar ou entregar.");
  }
  // Delivering an already-completed mission must be possible even if it was accepted from another NPC.
  var npc=candidates.Where(t=>NpcStage(t.Stage)).Where(t=>Distance(actor,t)<=TalkRange)
    .OrderBy(t=>t.Stage==AutoQuestStage.DeliverToNpc?0:t.Stage==AutoQuestStage.TalkToNpc?1:2)
    .ThenBy(t=>t.QuestId).FirstOrDefault();
  if(npc!=null){
   var same=candidates.Where(t=>NpcStage(t.Stage)&&t.NpcInstanceId==npc.NpcInstanceId&&Distance(actor,t)<=TalkRange)
    .Where(t=>t.Stage==npc.Stage).ToList();
   if(npc.Stage==AutoQuestStage.DeliverToNpc){
    var uncomplicated=same.Where(t=>!t.HasRewardChoice).ToList();
    if(uncomplicated.Count>0)same=uncomplicated;
    else return Destination(AutoQuestMove.ManualReward,"Entrega com escolha de recompensa ainda não validada.",same);
   }
   if(!actor.FlightKnown||actor.IsFlying)return Destination(AutoQuestMove.WaitForLanding,"Esperar voo terminar antes do NPC.",same);
   var move=npc.Stage==AutoQuestStage.DeliverToNpc?AutoQuestMove.DeliverQuest:
    npc.Stage==AutoQuestStage.AcceptAtNpc?AutoQuestMove.AcceptQuest:AutoQuestMove.InteractNpc;
   return Destination(move,"Interagir com o NPC validado da etapa atual.",same);
  }
  if(current!=null){
   if(!actor.FlightKnown||actor.IsFlying)return Destination(AutoQuestMove.WaitForLanding,"Aguardar pouso antes do autocultivo.",current);
   return Destination(AutoQuestMove.StartF12,"Ativar F12 somente após chegar à região, para todas as missões agrupadas.",current);
  }
  // Already-ready deliveries take priority before a new cross-map combat trip.
  var delivery=candidates.Where(t=>t.Stage==AutoQuestStage.DeliverToNpc)
   .OrderBy(t=>Distance(actor,t)).FirstOrDefault();
  if(delivery!=null){
   var same=candidates.Where(t=>t.Stage==AutoQuestStage.DeliverToNpc&&t.NpcInstanceId==delivery.NpcInstanceId).ToList();
   return Destination(AutoQuestMove.MoveToNpc,"Ir ao NPC de entrega, mesmo que não seja o NPC que ofereceu a missão.",same);
  }
  // Group every remaining combat quest for the same region into one trip.
  if(groups.Count>0)return Destination(AutoQuestMove.MoveToRegion,"Uma viagem para todas as missões de combate próximas.",groups[0]);
  var talks=candidates.Where(t=>NpcStage(t.Stage)).OrderBy(t=>Distance(actor,t))
   .ThenBy(t=>t.QuestId).ToList();
  if(talks.Count>0){
   var first=talks[0];
   return Destination(AutoQuestMove.MoveToNpc,"Próximo NPC da cadeia de missões.",talks.Where(t=>t.NpcInstanceId==first.NpcInstanceId).ToList());
  }
  // Collect-items is not the F12 kill policy; a separate verified collection routine is required.
  if(candidates.Any(t=>t.Stage==AutoQuestStage.CollectItems))
   return AutoQuestDecision.Of(AutoQuestMove.AwaitVerifiedData,"Coleta requer rotina específica; F12 não coleta itens.");
  if(all.Count>0&&all.All(t=>t.Stage==AutoQuestStage.Finished&&t.StageVerified))
   return AutoQuestDecision.Of(AutoQuestMove.Finished,"Todas as missões observadas estão confirmadas como concluídas.");
  if(all.Any(t=>t.StageVerified&&t.Progress>=t.Required&&t.Required>0
    &&(t.Stage==AutoQuestStage.KillMonsters||t.Stage==AutoQuestStage.CollectItems)))
   return AutoQuestDecision.Of(AutoQuestMove.WaitForStageUpdate,"Objetivo cumprido; aguardar estado do jogo antes de prosseguir.");
  return AutoQuestDecision.Of(AutoQuestMove.AwaitVerifiedData,"Faltam estágio, NPC, localização ou progresso de missão confirmados.");
 }
}

// Prevent NPC interactions from being sent repeatedly while the game has not confirmed a new quest stage.
sealed class AutoQuestActionGuard {
 readonly HashSet<string> sent=new HashSet<string>(StringComparer.Ordinal);
 // One task at a time: NPC packets cannot deliver a group atomically.
 public bool TryIssue(AutoQuestDecision action,AutoQuestObservedTask task){
  if(action==null||task==null||!task.StageVerified||task.StageRevision<=0||task.NpcInstanceId==0)return false;
  if(!action.QuestIds.Contains(task.QuestId)||action.NpcInstanceId!=task.NpcInstanceId)return false;
  bool allowed=action.Move==AutoQuestMove.AcceptQuest&&task.Stage==AutoQuestStage.AcceptAtNpc||
   action.Move==AutoQuestMove.DeliverQuest&&task.Stage==AutoQuestStage.DeliverToNpc&&!task.HasRewardChoice||
   action.Move==AutoQuestMove.InteractNpc&&task.Stage==AutoQuestStage.TalkToNpc;
  if(!allowed)return false;
  string key=task.QuestId+":"+task.Stage+":"+task.StageRevision+":"+task.NpcInstanceId;
  return sent.Add(key);
 }
 public void Clear(){sent.Clear();}
}
