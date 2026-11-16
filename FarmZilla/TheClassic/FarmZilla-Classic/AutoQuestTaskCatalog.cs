using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// READ-ONLY catalog of quest HEADERS from the running The Classic client's
// own tasks.data. IDs/names are not NPC offers, active objective stages,
// counters, rewards or validated quest destinations. Never drive an action
// solely from this index; child quest IDs may be absent from top-level headers.
sealed class AutoQuestTaskCatalog {
 public const uint ExpectedMagic=0x93858361;
 public const int MaxQuestHeaders=100000;
 public const long MaxFileBytes=512L*1024*1024;
 public readonly Dictionary<int,string> Titles=new Dictionary<int,string>();
 public string Path,Reason;
 public int Version,HeaderCount,Rejected;
 public bool Loaded{get{return String.IsNullOrEmpty(Reason);}}
 public static string Find(string gameDirectory){
  if(String.IsNullOrWhiteSpace(gameDirectory)||!Directory.Exists(gameDirectory))return null;
  var dirs=new List<string>{gameDirectory,System.IO.Path.Combine(gameDirectory,"data"),
   System.IO.Path.Combine(gameDirectory,"element","data")};
  var parent=Directory.GetParent(gameDirectory);
  if(parent!=null){
   dirs.Add(System.IO.Path.Combine(parent.FullName,"element","data"));
   dirs.Add(System.IO.Path.Combine(parent.FullName,"data"));
  }
  foreach(var dir in dirs.Distinct(StringComparer.OrdinalIgnoreCase)){
   var candidate=System.IO.Path.Combine(dir,"tasks.data");
   if(File.Exists(candidate))return candidate;
  }
  return null;
 }
 public string Title(int id){string value;return Titles.TryGetValue(id,out value)?value:null;}
 public static AutoQuestTaskCatalog FromGame(string gameDirectory){
  var path=Find(gameDirectory);
  if(path==null)return new AutoQuestTaskCatalog{Reason="Arquivo tasks.data não localizado na instalação do jogo."};
  try{return Load(path);}
  catch(Exception ex){return new AutoQuestTaskCatalog{Path=path,Reason=ex.Message};}
 }
 public static AutoQuestTaskCatalog Load(string path){
  var catalog=new AutoQuestTaskCatalog{Path=path};
  var info=new FileInfo(path);
  if(!info.Exists)throw new FileNotFoundException("tasks.data não localizado.",path);
  if(info.Length<12||info.Length>MaxFileBytes)
   throw new InvalidDataException("Tamanho de tasks.data fora do limite de leitura.");
  using(var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
  using(var reader=new BinaryReader(stream,Encoding.Unicode)){
   uint magic=reader.ReadUInt32();
   if(magic!=ExpectedMagic)throw new InvalidDataException("Assinatura de tasks.data incompatível.");
   catalog.Version=reader.ReadInt32();
   int count=reader.ReadInt32();
   if(catalog.Version<1||catalog.Version>350||count<0||count>MaxQuestHeaders||
    (long)count*4>stream.Length-12)
    throw new InvalidDataException("Versão/quantidade de tarefas incompatível.");
   catalog.HeaderCount=count;
   int[] offsets=new int[count];
   for(int i=0;i<count;i++)offsets[i]=reader.ReadInt32();
   long endHeader=12L+4L*count;
   int idOffset=catalog.Version>=153?2:0;
   long needed=idOffset+4+30*2;
   var seenOffsets=new HashSet<int>();
   for(int i=0;i<count;i++){
    int off=offsets[i];
    if(off<endHeader||off>stream.Length-needed||!seenOffsets.Add(off)){
     catalog.Rejected++;continue;
    }
    stream.Position=off+idOffset;
    int id=reader.ReadInt32();
    if(id<=0||id>10000000){catalog.Rejected++;continue;}
    ushort key=unchecked((ushort)id);
    var nameBytes=reader.ReadBytes(30*2);
    if(nameBytes.Length!=60){catalog.Rejected++;continue;}
    var value=new StringBuilder();
    for(int p=0;p<30;p++){
     char decoded=(char)(BitConverter.ToUInt16(nameBytes,p*2)^key);
     if(decoded=='\0')break;
     if(Char.IsControl(decoded)){value.Length=0;break;}
     value.Append(decoded);
    }
    string name=value.ToString().Trim();
    if(name.Length==0||catalog.Titles.ContainsKey(id)){catalog.Rejected++;continue;}
    catalog.Titles[id]=name;
   }
  }
  return catalog;
 }
}
