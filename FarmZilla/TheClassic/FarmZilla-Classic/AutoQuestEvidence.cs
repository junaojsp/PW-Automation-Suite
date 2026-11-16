using System;
using System.Collections.Generic;
using System.Linq;

// Read-only evidence layer for 32-byte INTERNAL task records.
// A changed byte, an added/removed record, an NPC dialog, and a combat-looking
// activity are observations, NOT decoded quest objectives, reward or completion.
// Never turn an AutoQuestEvidence into AutoQuestObservedTask.StageVerified.
enum AutoQuestEvidenceKind {
 AddedNearNpc, Offset06ChangedDuringActivity, RemovedNearNpc, CorrelatedRawCycle
}
sealed class AutoQuestEvidence {
 public ushort InternalId;
 public int Occurrence;
 public AutoQuestEvidenceKind Kind;
 public string Detail;
 public string Describe(){
  return "id interno="+InternalId+"; ocorrência="+Occurrence+"; evidência="+Kind+
   "; "+Detail+"; ESTÁGIO/NPC/PROGRESSO NÃO VALIDADOS";
 }
}
sealed class AutoQuestEvidenceTracker {
 sealed class Track {
  public bool AddedNearNpc,ChangedOffset06OutsideNpc;
  public byte FirstOffset06,LastOffset06;
 }
 readonly Dictionary<string,Track> state=new Dictionary<string,Track>(StringComparer.Ordinal);
 static string Key(AutoQuestRawTaskChange d){return d.Id+":"+d.Occurrence;}
 static Dictionary<string,AutoQuestRawTaskRecord> Index(IEnumerable<AutoQuestRawTaskRecord> items){
  var result=new Dictionary<string,AutoQuestRawTaskRecord>(StringComparer.Ordinal);
  if(items!=null)foreach(var item in items) {
   if(item==null||item.Data==null||item.Data.Length!=32)throw new ArgumentException("Registro interno inválido.");
   if(result.ContainsKey(item.Key))throw new ArgumentException("Chave interna duplicada.");
   result[item.Key]=item;
  }
  return result;
 }
 public void Reset(){state.Clear();}
 // "npcNearby" means the context/busy state or dialog was observed in either
 // snapshot; it is not proof that the character accepted/delivered a quest.
 // "activityOutsideNpc" means the action reader observed a non-idle, non-walking
 // state (for example Other) away from NPCs. It is NOT necessarily a kill.
 public List<AutoQuestEvidence> Observe(
  IEnumerable<AutoQuestRawTaskRecord> previous,
  IEnumerable<AutoQuestRawTaskRecord> current,
  IEnumerable<AutoQuestRawTaskChange> changes,
  bool npcNearby,bool activityOutsideNpc){
  var old=Index(previous);
  var now=Index(current);
  var result=new List<AutoQuestEvidence>();
  foreach(var change in changes??Enumerable.Empty<AutoQuestRawTaskChange>()){
   if(change==null)continue;
   var key=Key(change);Track t;
   if(change.Kind=="ADICIONADO"){
    t=new Track{AddedNearNpc=npcNearby};
    state[key]=t;
    if(npcNearby)result.Add(new AutoQuestEvidence{InternalId=change.Id,Occurrence=change.Occurrence,
     Kind=AutoQuestEvidenceKind.AddedNearNpc,Detail="registro apareceu durante ou logo após contato NPC"});
    continue;
   }
   if(change.Kind=="ALTERADO"){
    AutoQuestRawTaskRecord from,to;
    if(!old.TryGetValue(key,out from)||!now.TryGetValue(key,out to))continue;
    if(!state.TryGetValue(key,out t))state[key]=t=new Track();
    if(from.Data[6]!=to.Data[6]&&!npcNearby&&activityOutsideNpc){
     t.ChangedOffset06OutsideNpc=true;
     t.FirstOffset06=from.Data[6];t.LastOffset06=to.Data[6];
     result.Add(new AutoQuestEvidence{InternalId=change.Id,Occurrence=change.Occurrence,
      Kind=AutoQuestEvidenceKind.Offset06ChangedDuringActivity,
      Detail="byte +06: "+from.Data[6].ToString("X2")+" -> "+to.Data[6].ToString("X2")+
       " após atividade fora do NPC (sem afirmar contador ou conclusão)"});
    }
    continue;
   }
   if(change.Kind=="REMOVIDO"){
    if(npcNearby)result.Add(new AutoQuestEvidence{InternalId=change.Id,Occurrence=change.Occurrence,
     Kind=AutoQuestEvidenceKind.RemovedNearNpc,Detail="registro saiu enquanto havia contexto de NPC"});
    if(npcNearby&&state.TryGetValue(key,out t)&&t.AddedNearNpc&&t.ChangedOffset06OutsideNpc)
     result.Add(new AutoQuestEvidence{InternalId=change.Id,Occurrence=change.Occurrence,
      Kind=AutoQuestEvidenceKind.CorrelatedRawCycle,
      Detail="sequência observada: entrou perto do NPC -> byte +06 mudou "+
       t.FirstOffset06.ToString("X2")+">"+t.LastOffset06.ToString("X2")+
       " fora do NPC -> saiu perto do NPC; NÃO comprova entrega/recompensa"});
    state.Remove(key);
   }
  }
  return result;
 }
}
