using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace RotaAvalon {
    public sealed class ResourceDefinition {
        public int id;public string nome,icone,itemNome;public int nivelMinimo,nivelRecurso,item1Id,idTipo;
    }
    public sealed class ResourceCatalogFile {public int quantidade;public List<ResourceDefinition> registros;}
    public sealed class ResourceCatalog {
        public readonly Dictionary<int,ResourceDefinition> Definitions=new Dictionary<int,ResourceDefinition>();
        // 0.46.4 (owner: only what is gathered on the ground): the game's five production resource types — wood 2861, iron
        // ore 2862, stone 2863, coal 2864, herbs/plants 2865. Chests, portals, event and quest objects are other types.
        static readonly HashSet<int> GroundTypes=new HashSet<int>{2861,2862,2863,2864,2865};
        // 0.47.9 (owner): the Mat Farm list shows every material by the item it gives ("Liga de Aço", "Tábua Polida"); the
        // ground name stays available (MaterialName) for the search and the tooltip. All 49 ground items have distinct names.
        public string FarmName(int template){ResourceDefinition d;if(!Definitions.TryGetValue(template,out d))return "Recurso "+template;
            if(!String.IsNullOrWhiteSpace(d.itemNome))return d.itemNome.Trim();return String.IsNullOrWhiteSpace(d.nome)?"Recurso "+template:d.nome.Trim();}
        public string MaterialName(int template){ResourceDefinition d;return Definitions.TryGetValue(template,out d)&&!String.IsNullOrWhiteSpace(d.nome)?d.nome.Trim():"";}
        public bool Ground(int template){ResourceDefinition d;return Definitions.TryGetValue(template,out d)&&GroundTypes.Contains(d.idTipo)&&d.item1Id>0;}
        public static ResourceCatalog Load(string path){
            var info=new FileInfo(path);if(info.Length>8*1024*1024)throw new InvalidDataException("Catálogo muito grande.");
            return Parse(File.ReadAllText(path));
        }
        public static ResourceCatalog LoadEmbedded(){using(var stream=typeof(ResourceCatalog).Assembly.GetManifestResourceStream("RotaAvalon.Catalogo")){if(stream==null)throw new IOException("Catálogo embutido ausente.");using(var reader=new StreamReader(stream))return Parse(reader.ReadToEnd());}}
        static ResourceCatalog Parse(string json){
            var file=new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.Deserialize<ResourceCatalogFile>(json);
            if(file==null||file.registros==null||file.registros.Count!=file.quantidade||file.quantidade>100000)throw new InvalidDataException("Catálogo inválido.");
            var catalog=new ResourceCatalog();foreach(var d in file.registros){if(d==null||d.id<1||d.id>1000000||d.nome==null||d.nome.Length>128||d.nivelMinimo<0||d.nivelMinimo>10000||catalog.Definitions.ContainsKey(d.id))throw new InvalidDataException("Registro de recurso inválido.");catalog.Definitions.Add(d.id,d);}return catalog;
        }
    }
    public sealed class ResourceMarker {
        public uint InstanceId;public int TemplateId,RequiredLevel,ItemId;public string Name,Map;
        public double X,Y,Z,Distance,HorizontalDistance;public bool LevelAllowed;public bool Historical;public DateTime LastSeenUtc;public bool Seed;
    }
    public sealed class TerrainBounds {
        public double Left,Top,Right,Bottom;
        public bool IsMainWorld {get{return Math.Abs(Left+4096)<0.1&&Math.Abs(Right-4096)<0.1&&Math.Abs(Top-5632)<0.1&&Math.Abs(Bottom+5632)<0.1;}}
    }
    public sealed class ResourceFrame {public DateTime ReadAt;public uint World;public TerrainBounds Terrain;public List<ResourceMarker> Resources=new List<ResourceMarker>();public int ObjectsInspected;}
    public sealed class MonsterMarker {public uint Id;public double X,Y,Z,Distance;}
    // Read-only, mapped in the Avalon client of 02/11/2026: same hash-table layout as the material manager.
    public static class NearbyMonsters {
        public static List<MonsterMarker> Read(ProcessMemory memory,uint root,uint imageBase,uint imageEnd,ClientSnapshot player){
            var result=new List<MonsterMarker>();uint world=memory.UInt32(root+ClassicLayout.RunWorld);if(world<0x10000||(world&3)!=0)return result;
            uint manager=memory.UInt32(world+ClassicLayout.WorldNpcs);if(manager<0x10000||NearbyResources.NativeType(memory,manager,imageBase,imageEnd)!=".?AVCECNPCMan@@")throw new IOException("Gerenciador de monstros não reconhecido.");
            uint count=memory.UInt32(manager+0x1C),array=memory.UInt32(manager+0x20);if(count<1||count>65536||array<0x10000||(array&3)!=0)throw new IOException("Tabela de monstros inconsistente.");
            byte[] buckets=memory.Read(array,checked((int)count*4));if(buckets.Length!=count*4)throw new IOException("Tabela de monstros incompleta.");var seen=new HashSet<uint>();
            for(int bucket=0;bucket<buckets.Length;bucket+=4){uint node=BitConverter.ToUInt32(buckets,bucket);int chain=0;while(node!=0&&chain++<1000&&seen.Count<8000&&seen.Add(node)){byte[] link=memory.Read(node,12);if(link.Length!=12)break;node=BitConverter.ToUInt32(link,0);uint obj=BitConverter.ToUInt32(link,4),key=BitConverter.ToUInt32(link,8);if(obj<0x10000||(obj&3)!=0)continue;
                byte[] record=memory.Read(obj,0x11C);if(record.Length!=0x11C||BitConverter.ToUInt32(record,0x118)!=key)continue;if(NearbyResources.NativeType(memory,obj,imageBase,imageEnd)!=".?AVCECMonster@@")continue;
                double x=BitConverter.ToSingle(record,0x3c),y=BitConverter.ToSingle(record,0x40),z=BitConverter.ToSingle(record,0x44);if(!GameClient.ValidCoordinate(x)||!GameClient.ValidCoordinate(y)||!GameClient.ValidCoordinate(z))continue;
                double dx=x-player.X,dy=y-player.Y,dz=z-player.Z;result.Add(new MonsterMarker{Id=key,X=x,Y=y,Z=z,Distance=Math.Sqrt(dx*dx+dy*dy+dz*dz)});}}
            return result;
        }
    }
    public static class NearbyResources {
        static uint U(byte[] bytes,int at){return BitConverter.ToUInt32(bytes,at);}
        static double F(byte[] bytes,int at){return BitConverter.ToSingle(bytes,at);}
        static void Pointer(uint p){if(p<0x10000||(p&3)!=0)throw new IOException("Mundo ainda indisponível.");}
        public static string NativeType(ProcessMemory memory,uint address,uint imageBase,uint imageEnd){
            try{uint vt=memory.UInt32(address);if(vt<imageBase+4||vt>=imageEnd)return null;uint col=memory.UInt32(vt-4);if(col<imageBase||col>=imageEnd)return null;uint desc=memory.UInt32(col+12);if(desc<imageBase||desc>=imageEnd)return null;byte[] bytes=memory.Read(desc+8,96);string text=Encoding.ASCII.GetString(bytes);int zero=text.IndexOf('\0');return zero>=0?text.Substring(0,zero):null;}catch{return null;}
        }
        public static ResourceFrame Read(ProcessMemory memory,uint root,uint imageBase,uint imageEnd,ResourceCatalog catalog,ClientSnapshot player){
            uint world=memory.UInt32(root+ClassicLayout.RunWorld);Pointer(world);
            if(NativeType(memory,world,imageBase,imageEnd)!=".?AVCECWorld@@")throw new IOException("Perfil do mundo não reconhecido.");
            uint manager=memory.UInt32(world+ClassicLayout.WorldMatters);Pointer(manager);
            if(NativeType(memory,manager,imageBase,imageEnd)!=".?AVCECMatterMan@@")throw new IOException("Gerenciador de objetos não reconhecido.");
            var frame=new ResourceFrame{ReadAt=DateTime.Now,World=world};uint terrain=memory.UInt32(world+0x0c);Pointer(terrain);byte[] geometry=memory.Read(terrain+0x204,16);
            if(geometry.Length==16)frame.Terrain=new TerrainBounds{Left=F(geometry,0),Top=F(geometry,4),Right=F(geometry,8),Bottom=F(geometry,12)};
            uint count=memory.UInt32(manager+ClassicLayout.MatterHashCount),array=memory.UInt32(manager+ClassicLayout.MatterHashBuckets);
            // 0.46.5 (owner 05/11: map and materials vanished in town): with no object loaded nearby the game leaves the table
            // empty (array 0) — that is "no materials here", not "world unavailable", so map and list still show.
            if(array==0)return frame;
            Pointer(array);if(count<1||count>65536)throw new IOException("Tabela de objetos inconsistente.");
            byte[] buckets=memory.Read(array,checked((int)count*4));if(buckets.Length!=count*4)throw new IOException("Tabela de objetos incompleta.");var seenNodes=new HashSet<uint>();var seenObjects=new HashSet<uint>();
            for(int bucket=0;bucket<buckets.Length;bucket+=4){uint node=U(buckets,bucket);int chain=0;while(node!=0){Pointer(node);if(!seenNodes.Add(node)||chain++>10000||seenNodes.Count>20000)throw new IOException("Tabela de objetos mudou durante a leitura.");byte[] link=memory.Read(node,12);if(link.Length!=12)break;node=U(link,0);uint obj=U(link,4),key=U(link,8);if(!seenObjects.Add(obj)||obj<0x10000||(obj&3)!=0)continue;frame.ObjectsInspected++;
                byte[] record=memory.Read(obj,0x118);if(record.Length!=0x118)continue;uint idOnObject=U(record,(int)ClassicLayout.MatterId);if(!MatterNode.Same(key,idOnObject))continue;int id=unchecked((int)U(record,(int)ClassicLayout.MatterTemplate));ResourceDefinition definition;if(!catalog.Definitions.TryGetValue(id,out definition))continue;
                string type=NativeType(memory,obj,imageBase,imageEnd);if(type!=".?AVCECMatter@@"&&type!=".?AVCECTracedMatter@@")continue;
                double x=F(record,0x3c),y=F(record,0x40),z=F(record,0x44);if(!GameClient.ValidCoordinate(x)||!GameClient.ValidCoordinate(y)||!GameClient.ValidCoordinate(z))continue;
                double dx=x-player.X,dy=y-player.Y,dz=z-player.Z;frame.Resources.Add(new ResourceMarker{InstanceId=idOnObject,TemplateId=id,Name=String.IsNullOrWhiteSpace(definition.nome)?"Recurso "+id:definition.nome,RequiredLevel=definition.nivelMinimo,ItemId=definition.item1Id,X=x,Y=y,Z=z,Distance=Math.Sqrt(dx*dx+dy*dy+dz*dz),HorizontalDistance=Math.Sqrt(dx*dx+dz*dz),LevelAllowed=player.Level>=definition.nivelMinimo});
            }}
            if(memory.UInt32(root+ClassicLayout.RunWorld)!=world||memory.UInt32(world+ClassicLayout.WorldMatters)!=manager)throw new IOException("O mapa mudou durante a leitura.");
            frame.Resources=frame.Resources.OrderBy(r=>r.Distance).ToList();return frame;
        }
    }
}
