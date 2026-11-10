using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace RotaAvalon {
    // Embedded material map (0.36.0): spawn-area centers of the collectible materials of the main world, from PW Helper
    // (use authorized by its developer on 03/11/2026). Shown and planned like scanned points; a real scan of the same
    // material within 40 wins (exact position and height).
    public static class MaterialMap {
        sealed class File {public string fonte;public List<double[]> pontos;}
        static List<double[]> points;static readonly object gate=new object();
        public static string Source="";
        static List<double[]> Points(){lock(gate){if(points!=null)return points;points=new List<double[]>();
            try{using(var stream=typeof(MaterialMap).Assembly.GetManifestResourceStream("RotaAvalon.PontosMapa")){if(stream!=null)using(var reader=new StreamReader(stream)){var f=new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.Deserialize<File>(reader.ReadToEnd());if(f!=null&&f.pontos!=null){points=f.pontos.Where(p=>p!=null&&p.Length>=3).ToList();Source=f.fonte??"";}}}}catch{points=new List<double[]>();}
            return points;}}
        public static int Count{get{return Points().Count;}}
        // Adds the map points not covered by a real observation of the same material (40 around) to the observed list.
        public static List<ResourceMarker> Merge(List<ResourceMarker> observed,ResourceCatalog catalog,ClientSnapshot player,ResourceFrame frame){
            if(player==null||frame==null||frame.Terrain==null||!frame.Terrain.IsMainWorld)return observed;
            var cells=new HashSet<string>();foreach(var r in observed){int cx=(int)Math.Floor(r.X/40),cz=(int)Math.Floor(r.Z/40);for(int i=-1;i<=1;i++)for(int j=-1;j<=1;j++)cells.Add(r.TemplateId+":"+(cx+i)+":"+(cz+j));}
            var result=new List<ResourceMarker>(observed);
            foreach(var p in Points()){int t=(int)p[0];double x=p[1],z=p[2];if(cells.Contains(t+":"+(int)Math.Floor(x/40)+":"+(int)Math.Floor(z/40)))continue;ResourceDefinition d;if(!catalog.Definitions.TryGetValue(t,out d))continue;
                double dx=x-player.X,dz=z-player.Z,h=Math.Sqrt(dx*dx+dz*dz);
                result.Add(new ResourceMarker{InstanceId=0,TemplateId=t,Name=String.IsNullOrWhiteSpace(d.nome)?"Recurso "+t:d.nome,RequiredLevel=d.nivelMinimo,ItemId=d.item1Id,X=x,Y=0,Z=z,Map=ResourceHistory.World,Distance=h,HorizontalDistance=h,LevelAllowed=player.Level>=d.nivelMinimo,Historical=true,Seed=true});}
            return result;}
    }
}
