using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace RotaAvalon {
    // Remembered locations of ground collectibles (materials that give an item), per map: the main world and any other map
    // with its own terrain (Céu, Inferno), told apart by the terrain bounds the client reports. Quest objects and unnamed
    // records still show while loaded, but are not remembered (FarmZilla 0.21.0, owner request).
    public sealed class ResourceHistory {
        public const string World="mundo";
        readonly List<ResourceMarker> known=new List<ResourceMarker>();readonly string path;readonly ResourceCatalog catalog;DateTime saved=DateTime.MinValue;bool dirty;
        readonly HashSet<string> maps=new HashSet<string>();
        public event Action<string> NewMap;
        public ResourceHistory(string path,ResourceCatalog catalog){this.path=path;this.catalog=catalog;try{if(File.Exists(path)&&new FileInfo(path).Length<8*1024*1024){var records=new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.Deserialize<List<ResourceMarker>>(File.ReadAllText(path));if(records!=null)foreach(var r in records.Take(20000)){if(r!=null&&Collectible(r.TemplateId)&&GameClient.ValidCoordinate(r.X)&&GameClient.ValidCoordinate(r.Y)&&GameClient.ValidCoordinate(r.Z)){if(r.Map==null)r.Map=World;known.Add(r);maps.Add(r.Map);}else dirty=true;}}}catch{/* Keep live readings usable if history is corrupt. */}}
        public bool Collectible(int template){ResourceDefinition d;return catalog.Definitions.TryGetValue(template,out d)&&d.item1Id>0&&!String.IsNullOrWhiteSpace(d.nome);}
        public static string MapKey(TerrainBounds t){return t.IsMainWorld?World:String.Format(CultureInfo.InvariantCulture,"terreno {0:F0},{1:F0},{2:F0},{3:F0}",t.Left,t.Top,t.Right,t.Bottom);}
        public List<ResourceMarker> Merge(ClientSnapshot player,ResourceFrame frame){
            if(player==null||frame==null||frame.Terrain==null)return frame==null?new List<ResourceMarker>():frame.Resources;
            string map=MapKey(frame.Terrain);if(maps.Add(map)&&map!=World&&NewMap!=null)NewMap(map);
            var here=known.Where(v=>v.Map==map).ToList();
            // Indexed by instance and by material (FarmZilla 0.21.0: was a full scan of the history per live resource).
            var byInstance=new Dictionary<uint,List<ResourceMarker>>();var byTemplate=new Dictionary<int,List<ResourceMarker>>();foreach(var v in here){Index(byInstance,v.InstanceId,v);Index(byTemplate,v.TemplateId,v);}
            var liveIds=new HashSet<uint>(frame.Resources.Select(current=>current.InstanceId));
            var live=new HashSet<ResourceMarker>();var passing=new List<ResourceMarker>();
            foreach(var r in frame.Resources){if(!Collectible(r.TemplateId)){passing.Add(r);continue;}List<ResourceMarker> bucket;ResourceMarker old=null;if(byInstance.TryGetValue(r.InstanceId,out bucket))old=bucket.Where(v=>v.TemplateId==r.TemplateId&&v.InstanceId==r.InstanceId&&Distance(v,r)<=25).OrderBy(v=>Distance(v,r)).FirstOrDefault();if(old==null&&byTemplate.TryGetValue(r.TemplateId,out bucket))old=bucket.Where(v=>v.TemplateId==r.TemplateId&&Distance(v,r)<=2&&!liveIds.Contains(v.InstanceId)).OrderBy(v=>Distance(v,r)).FirstOrDefault();if(old==null){if(known.Count>=20000){passing.Add(r);continue;}old=new ResourceMarker{Map=map};known.Add(old);here.Add(old);Index(byTemplate,r.TemplateId,old);}if(old.InstanceId!=r.InstanceId)Index(byInstance,r.InstanceId,old);old.InstanceId=r.InstanceId;old.TemplateId=r.TemplateId;old.RequiredLevel=r.RequiredLevel;old.ItemId=r.ItemId;old.Name=r.Name;old.X=r.X;old.Y=r.Y;old.Z=r.Z;old.LastSeenUtc=DateTime.UtcNow;live.Add(old);dirty=true;}
            var result=new List<ResourceMarker>();foreach(var r in here){int need=Need(r);double dx=r.X-player.X,dy=r.Y-player.Y,dz=r.Z-player.Z;result.Add(new ResourceMarker{InstanceId=r.InstanceId,TemplateId=r.TemplateId,RequiredLevel=need,ItemId=r.ItemId,Name=r.Name,X=r.X,Y=r.Y,Z=r.Z,Map=r.Map,Distance=Math.Sqrt(dx*dx+dy*dy+dz*dz),HorizontalDistance=Math.Sqrt(dx*dx+dz*dz),LevelAllowed=player.Level>=need,Historical=!live.Contains(r),LastSeenUtc=r.LastSeenUtc});}
            result.AddRange(passing);if(dirty&&(DateTime.UtcNow-saved).TotalSeconds>=15)Save();return result;
        }
        public int Count(string map){return known.Count(v=>v.Map==map);}
        // A point saved before the tier fix still says 80. The catalog tier replaces it, and the file is rewritten with the rest.
        int Need(ResourceMarker r){ResourceDefinition d;if(!catalog.Definitions.TryGetValue(r.TemplateId,out d))return r.RequiredLevel;int gather=catalog.GatherLevel(d);if(r.RequiredLevel!=gather){r.RequiredLevel=gather;dirty=true;}return gather;}
        static void Index<K>(Dictionary<K,List<ResourceMarker>> index,K key,ResourceMarker value){List<ResourceMarker> list;if(!index.TryGetValue(key,out list))index[key]=list=new List<ResourceMarker>();list.Add(value);}
        static double Distance(ResourceMarker a,ResourceMarker b){double x=a.X-b.X,z=a.Z-b.Z;return Math.Sqrt(x*x+z*z);}
        public void Save(){if(!dirty)return;Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string temporary=path+".tmp";File.WriteAllText(temporary,new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.Serialize(known));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);saved=DateTime.UtcNow;dirty=false;}
    }
}
