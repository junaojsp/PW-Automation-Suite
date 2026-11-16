using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Diagnostic only. The 32-byte records are internal C++ task entries, NOT one record per journal quest.
// Their fields have not been decoded into task names, objectives, counters or delivery NPCs.
sealed class AutoQuestRawTaskRecord {
 public ushort Id;
 public int Occurrence,Position;
 public byte[] Data;
 public string Key {get{return Id.ToString()+":"+Occurrence;}}
 public string Hex {get{return BitConverter.ToString(Data).Replace("-","");}}
}
sealed class AutoQuestRawTaskChange {
 public ushort Id;public int Occurrence;
 public string Kind,Detail;
 public string Describe(){return "id="+Id+"; ocorrência="+Occurrence+"; tipo="+Kind+"; "+Detail;}
}
static class AutoQuestRawTaskDiff {
 static Dictionary<string,AutoQuestRawTaskRecord> Index(IEnumerable<AutoQuestRawTaskRecord> records) {
  var index=new Dictionary<string,AutoQuestRawTaskRecord>(StringComparer.Ordinal);
  if(records==null)return index;
  foreach(var rec in records){
   if(rec==null||rec.Data==null||rec.Data.Length!=32)throw new ArgumentException("Registro bruto inválido.");
   if(index.ContainsKey(rec.Key))throw new ArgumentException("Registro bruto duplicado com a mesma ocorrência.");
   index.Add(rec.Key,rec);
  }
  return index;
 }
 public static List<AutoQuestRawTaskChange> Changes(IEnumerable<AutoQuestRawTaskRecord> previous,IEnumerable<AutoQuestRawTaskRecord> current){
  Dictionary<string,AutoQuestRawTaskRecord> old=Index(previous),now=Index(current);
  var changes=new List<AutoQuestRawTaskChange>();
  foreach(var item in now.OrderBy(x=>x.Value.Id).ThenBy(x=>x.Value.Occurrence)){
   AutoQuestRawTaskRecord before;
   if(!old.TryGetValue(item.Key,out before)) {
    changes.Add(new AutoQuestRawTaskChange{Id=item.Value.Id,Occurrence=item.Value.Occurrence,
     Kind="ADICIONADO",Detail="bytes32="+item.Value.Hex});
    continue;
   }
   var offsets=new List<string>();
   for(int i=0;i<32;i++)if(before.Data[i]!=item.Value.Data[i])
    offsets.Add(i.ToString("X2")+":"+before.Data[i].ToString("X2")+">"+item.Value.Data[i].ToString("X2"));
   if(offsets.Count>0)changes.Add(new AutoQuestRawTaskChange{
    Id=item.Value.Id,Occurrence=item.Value.Occurrence,Kind="ALTERADO",
    Detail="offsets="+String.Join(",",offsets.Take(20))+(offsets.Count>20?"...":"")+
     "; bytes32="+item.Value.Hex
   });
  }
  foreach(var item in old.OrderBy(x=>x.Value.Id).ThenBy(x=>x.Value.Occurrence))
   if(!now.ContainsKey(item.Key))
    changes.Add(new AutoQuestRawTaskChange{Id=item.Value.Id,Occurrence=item.Value.Occurrence,
     Kind="REMOVIDO",Detail="bytes32="+item.Value.Hex});
  return changes;
 }
}
sealed partial class CharacterMemory {
 // Parallel reader: leaves ActiveQuestIds() unchanged so existing Espelho/Frost/QuestService retain their behavior.
 // Validates both the current task interface and stable list pointer/count across the sample.
 public List<AutoQuestRawTaskRecord> ReadAutoQuestRawTaskRecords(){
  uint task=U(Host+ClassicLayout.HostTasks);
  if(U(task)!=Module+G.TaskInterface||U(task+4)!=Host)throw new IOException("Interface interna de missões temporariamente indisponível.");
  uint list=U(task+8);
  byte[] header=Read(list,8);
  int count=header[0];
  if(count>175)throw new IOException("Quantidade de registros internos temporariamente inválida.");
  byte[] records=Read(list,8+32*count);
  if(Read(list,1)[0]!=count||U(task+8)!=list||U(task)!=Module+G.TaskInterface)
   throw new IOException("Lista de missões mudou durante a leitura.");
  var occurrences=new Dictionary<ushort,int>();
  var result=new List<AutoQuestRawTaskRecord>();
  for(int i=0;i<count;i++){
   int at=8+i*32;
   ushort id=BitConverter.ToUInt16(records,at);
   int occurrence;occurrences.TryGetValue(id,out occurrence);occurrences[id]=occurrence+1;
   var raw=new byte[32];Buffer.BlockCopy(records,at,raw,0,32);
   result.Add(new AutoQuestRawTaskRecord{Id=id,Occurrence=occurrence,Position=i,Data=raw});
  }
  return result;
 }
}
