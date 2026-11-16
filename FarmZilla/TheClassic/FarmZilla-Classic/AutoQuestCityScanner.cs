using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

// Scans the NPC objects currently LOADED in the player's map region.
// Does not open NPC windows, walk, accept quests, or claim those NPCs offer
// any specific mission. More complete world coverage needs game data/map files.
sealed class AutoQuestNpcMarker {
 public uint InstanceId;public string Type;
 public double X,Y,Z,Distance;
 public string NameHeaderHex; // Raw research evidence only, NOT a decoded NPC name.
}
sealed class AutoQuestNpcFrame {
 public uint WorldInstance;
 public readonly List<AutoQuestNpcMarker> Npcs=new List<AutoQuestNpcMarker>();
 public int ObjectsSeen,MonstersSkipped;public string ScanError;
}
static class AutoQuestNpcProbe {
 const int MaxObjects=8000,MaxNearNpcs=384;
 public static bool IsNpcClass(string type){
  if(String.IsNullOrEmpty(type))return false;
  return type.StartsWith(".?AVCEC",StringComparison.Ordinal)&&
   type.IndexOf("NPC",StringComparison.Ordinal)>=0&&
   type.IndexOf("NPCMan",StringComparison.Ordinal)<0&&
   type.IndexOf("Monster",StringComparison.Ordinal)<0;
 }
 public static AutoQuestNpcFrame Read(RotaAvalon.GameClient client,RotaAvalon.ClientSnapshot actor){
  var result=new AutoQuestNpcFrame();
  using(var process=Process.GetProcessById(client.Pid))
  using(var memory=new RotaAvalon.ProcessMemory(client.Pid)){
   uint module=unchecked((uint)process.MainModule.BaseAddress.ToInt32());
   uint end=module+(uint)process.MainModule.ModuleMemorySize;
   uint root=memory.UInt32(checked(module+client.RootRva));
   uint world=memory.UInt32(root+ClassicLayout.RunWorld);
   if(world<0x10000)throw new IOException("Mundo ainda não carregado.");
   result.WorldInstance=memory.UInt32(world+ClassicLayout.WorldInstance);
   uint manager=memory.UInt32(world+ClassicLayout.WorldNpcs);
   if(manager<0x10000||
    RotaAvalon.NearbyResources.NativeType(memory,manager,module,end)!=".?AVCECNPCMan@@")
    throw new IOException("Gerenciador de NPCs ainda não reconhecido.");
   uint n=memory.UInt32(manager+0x1C),table=memory.UInt32(manager+0x20);
   if(n==0||table==0)return result;
   if(n>65536||table<0x10000)throw new IOException("Tabela de NPCs inválida.");
   byte[] buckets=memory.Read(table,checked((int)n*4));
   if(buckets.Length!=(int)n*4)throw new IOException("Tabela de NPCs incompleta.");
   var seenNodes=new HashSet<uint>();var ids=new HashSet<uint>();
   for(int i=0;i<buckets.Length;i+=4){
    uint node=BitConverter.ToUInt32(buckets,i);int chain=0;
    while(node!=0&&chain++<1000&&seenNodes.Count<MaxObjects&&seenNodes.Add(node)){
     byte[] link=memory.Read(node,12);if(link.Length!=12)break;
     node=BitConverter.ToUInt32(link,0);
     uint obj=BitConverter.ToUInt32(link,4),key=BitConverter.ToUInt32(link,8);
     if(obj<0x10000||(obj&3)!=0||!ids.Add(obj))continue;
     byte[] data=memory.Read(obj,0x11C);
     if(data.Length!=0x11C||BitConverter.ToUInt32(data,0x118)!=key)continue;
     result.ObjectsSeen++;
     string type=RotaAvalon.NearbyResources.NativeType(memory,obj,module,end);
     if(type==".?AVCECMonster@@"){result.MonstersSkipped++;continue;}
     if(!IsNpcClass(type))continue;
     double x=BitConverter.ToSingle(data,0x3c),y=BitConverter.ToSingle(data,0x40),z=BitConverter.ToSingle(data,0x44);
     if(!RotaAvalon.GameClient.ValidCoordinate(x)||!RotaAvalon.GameClient.ValidCoordinate(y)||
      !RotaAvalon.GameClient.ValidCoordinate(z))continue;
     double dx=x-actor.X,dz=z-actor.Z;
     double distance=Math.Sqrt(dx*dx+dz*dz);
     if(distance>900)continue; // Nearby loaded NPCs only.
     byte[] nameHeader=memory.Read(obj+ClassicLayout.NpcName,16);
     result.Npcs.Add(new AutoQuestNpcMarker{InstanceId=key,Type=type,X=x,Y=y,Z=z,
      Distance=distance,NameHeaderHex=nameHeader.Length==16?
       BitConverter.ToString(nameHeader).Replace("-",""):"indisponível"});
     if(result.Npcs.Count>=MaxNearNpcs)break;
    }
    if(result.Npcs.Count>=MaxNearNpcs||seenNodes.Count>=MaxObjects)break;
   }
   if(memory.UInt32(root+ClassicLayout.RunWorld)!=world||
      memory.UInt32(world+ClassicLayout.WorldNpcs)!=manager)
    throw new IOException("O mapa mudou durante o escaneamento.");
  }
  result.Npcs.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
  return result;
 }
}
sealed class AutoQuestCityScan {
 public readonly string FilePath;
 public readonly AutoQuestTaskCatalog Catalog;
 public readonly AutoQuestNpcFrame Frame;
 public readonly int ActiveCount,MatchedTitles,DeliveryRows;
 public readonly string ZoneKey;
 AutoQuestCityScan(string path,AutoQuestTaskCatalog cat,AutoQuestNpcFrame frame,
  int active,int matched,int rows,string zone){
  FilePath=path;Catalog=cat;Frame=frame;ActiveCount=active;MatchedTitles=matched;
  DeliveryRows=rows;ZoneKey=zone;
 }
 public static string Sector(uint instance,double x,double z){
  return "instância="+instance+"/setor="+Math.Floor(x/400)+","+Math.Floor(z/400);
 }
 public static AutoQuestCityScan Capture(int pid,uint expectedId,string expectedName){
  ushort[] active;uint npc=0;byte busy=0;
  using(var memory=new CharacterMemory(pid)){
   if(memory.Id!=expectedId||memory.Name!=expectedName)
    throw new IOException("Identidade mudou antes do escaneamento.");
   active=memory.ActiveQuestIds();
   npc=memory.U(memory.Host+ClassicLayout.HostNpcTalk);
   busy=memory.Read(memory.Host+ClassicLayout.HostNpcBusy,1)[0];
  }
  string directory,zone;RotaAvalon.ClientSnapshot actor;AutoQuestNpcFrame npcs;
  uint[] delivery;
  using(var client=new RotaAvalon.GameClient(pid,new Dictionary<string,bool>())){
   actor=client.Read();
   if(actor.Name!=expectedName)throw new IOException("Cliente alterado.");
   directory=client.GameDirectory;
   try{npcs=AutoQuestNpcProbe.Read(client,actor);}
   catch(Exception ex){npcs=new AutoQuestNpcFrame{ScanError=ex.Message};}
   delivery=client.ReadConfirmRows().Distinct().Take(24).ToArray();
   zone=Sector(npcs.WorldInstance,actor.X,actor.Z);
  }
  var catalog=AutoQuestTaskCatalog.FromGame(directory);
  var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
   "FarmZillaClassic","diagnosticos");
  Directory.CreateDirectory(folder);
  var file=Path.Combine(folder,"autoquest-cityscan-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+pid+".txt");
  var output=new StringBuilder();
  output.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" | AUTO QUEST CIDADE - scanner somente leitura; versão="+ReleaseInfo.Version);
  output.AppendLine("PERSONAGEM; nome="+actor.Name+"; PID="+pid+"; nivel="+actor.Level+
   "; HP="+actor.Hp+"/"+actor.MaxHp+"; XYZ="+actor.X.ToString("0.0")+","+
   actor.Y.ToString("0.0")+","+actor.Z.ToString("0.0")+"; "+zone);
  output.AppendLine("LIMITES; setor é agrupamento por coordenadas, NÃO nome de cidade; NPCs somente carregados perto do personagem.");
  output.AppendLine("CATALOGO; arquivo="+(catalog.Path??"não localizado")+"; versão="+catalog.Version+
   "; cabeçalhos="+catalog.HeaderCount+"; títulos="+catalog.Titles.Count+
   "; rejeitados="+catalog.Rejected+"; erro="+(catalog.Reason??"nenhum"));
  int matches=0;
  foreach(var id in active.Distinct().OrderBy(x=>x)){
   string name=catalog.Title(id);
   if(name!=null)matches++;
   output.AppendLine("MISSAO ATIVA INTERNA; id="+id+"; título="+(name??"NÃO DECODIFICADO")+
    "; estágio/objetivo/progresso/NPC=DESCONHECIDOS");
  }
  output.AppendLine("NPCs CARREGADOS; "+npcs.Npcs.Count+" NPCs candidatos; itens varridos="+
   npcs.ObjectsSeen+"; monstros excluídos="+npcs.MonstersSkipped+
   "; leitura="+(npcs.ScanError??"ok"));
  foreach(var item in npcs.Npcs)
   output.AppendLine("NPC CANDIDATO; id interno="+item.InstanceId+"; tipo="+item.Type+
    "; XYZ="+item.X.ToString("0.0")+","+item.Y.ToString("0.0")+","+item.Z.ToString("0.0")+
    "; distância="+item.Distance.ToString("0.0")+
    "; bytes brutos de nome="+item.NameHeaderHex+
    "; missões oferecidas=AINDA NÃO LIDAS");
  output.AppendLine("CONTEXTO NPC; id="+npc+"; ocupado="+busy);
  foreach(var row in delivery)
   output.AppendLine("LINHA DE TELA DE ENTREGA; id="+row+
    "; título de catálogo="+(catalog.Title((int)row)??"NÃO DECODIFICADO")+
    "; requer confirmação na interface (não é missão disponível automaticamente).");
  output.AppendLine("PENDENCIA; leitura de títulos de missões filhas, lista de ofertas por NPC, requisitos, coordenadas e alvos depende do decodificador de tasks.data + NPC; nenhum comando enviado.");
  File.WriteAllText(file,output.ToString(),Encoding.UTF8);
  return new AutoQuestCityScan(file,catalog,npcs,active.Length,matches,delivery.Length,zone);
 }
}
