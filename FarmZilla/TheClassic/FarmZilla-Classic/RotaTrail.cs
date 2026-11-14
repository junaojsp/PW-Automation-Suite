using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
namespace RotaAvalon {
    // Manual Mat Farm route (0.47.0, owner): the player flies the path once while FarmZilla records it; the farm then flies the
    // same path over and over and, wherever a chosen material has spawned within reach of the path, it stops, comes straight
    // down, collects (and does the material quest), climbs straight back to the path height and goes on from where it stopped.
    public sealed class TrailPoint {public double X,Y,Z;public bool Flying;}
    public sealed partial class TrailRoute {
        public string Name="",Character="";public double Reach=50;public List<int> Materials=new List<int>();
        public List<TrailPoint> Path=new List<TrailPoint>();public List<Waypoint> Seen=new List<Waypoint>();
        public const double MinReach=10,MaxReach=120;
        // Closed when the end is back near the start: laps go round; otherwise they go there and back (only over the known path).
        public bool Closed{get{if(Path.Count<3)return false;var a=Path[0];var b=Path[Path.Count-1];double limit=60;if(Imported)for(int i=1;i<Path.Count;i++)limit=Math.Max(limit,Math.Sqrt(Math.Pow(Path[i].X-Path[i-1].X,2)+Math.Pow(Path[i].Z-Path[i-1].Z,2)));return Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Z-b.Z,2))<=limit;}}
        // 0.47.1: a route from another program (points hundreds apart) closes when going back to the start is no longer than its longest leg.
        public double Length(){double total=0;for(int i=1;i<Path.Count;i++)total+=Math.Sqrt(Math.Pow(Path[i].X-Path[i-1].X,2)+Math.Pow(Path[i].Z-Path[i-1].Z,2));return total;}
        // A material seen near the path while recording (same spot within 5 = once).
        public bool AddSeen(ResourceMarker r){if(Seen.Any(w=>w.ResourceId==r.TemplateId&&Math.Abs(w.X-r.X)<5&&Math.Abs(w.Z-r.Z)<5))return false;if(Seen.Count>=5000)return false;
            Seen.Add(new Waypoint{ResourceId=r.TemplateId,ItemId=r.ItemId,X=r.X,Y=r.Y,Z=r.Z,Action="Coletar"});return true;}
        // For the Mat Farm map: the path as a line (the "Coletar" points seen are drawn by the map's own material layer).
        public RouteDocument ToMap(){var d=new RouteDocument{Name=Name,Character=Character,Loop=Closed,ResourceIds=Materials.ToList()};foreach(var p in Path)d.Points.Add(new Waypoint{X=p.X,Y=Double.IsNaN(p.Y)?0:p.Y,Z=p.Z});return d;}

        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        static string N(double v){return Math.Round(v,2).ToString("0.##",Inv);}
        // Plain text, one "chave=valor" per line, dot decimals, # = note: easy to read in Notepad or another program.
        public void Write(string path){
            var lines=new List<string>{"# FarmZilla · trajeto manual do Mat Farm","# trajeto=x;y;z;voo (voo 1 = voando; y ? = sem altura, voa na altura atual) na ordem gravada · coleta=material;item;x;y;z vistos perto do caminho · alcance = distância máxima do caminho para descer e coletar",
                "tipo=trajeto","nome="+Name.Replace("\r"," ").Replace("\n"," "),"personagem="+Character,"alcance="+N(Reach),"materiais="+String.Join(",",Materials)};
            foreach(var p in Path)lines.Add("trajeto="+N(p.X)+";"+(Double.IsNaN(p.Y)?"?":N(p.Y))+";"+N(p.Z)+";"+(p.Flying?1:0));
            foreach(var w in Seen)lines.Add("coleta="+w.ResourceId+";"+w.ItemId+";"+N(w.X)+";"+N(w.Y)+";"+N(w.Z));
            File.WriteAllLines(path,lines,new UTF8Encoding(false));
        }
        public static bool IsTrail(string path){try{foreach(var line in File.ReadLines(path).Take(40)){var t=line.Trim().ToLowerInvariant();if(t.StartsWith("tipo=trajeto")||t.StartsWith("trajeto="))return true;}}catch{}return false;}
        public static TrailRoute Read(string path){
            var info=new FileInfo(path);if(!info.Exists)throw new FileNotFoundException("Arquivo do trajeto não encontrado.");if(info.Length>4*1024*1024)throw new InvalidDataException("Arquivo grande demais para um trajeto.");
            var r=new TrailRoute{Name=System.IO.Path.GetFileNameWithoutExtension(path)};int number=0;
            foreach(var raw in File.ReadAllLines(path,Encoding.UTF8)){number++;var line=raw.Trim();if(line.Length==0||line.StartsWith("#"))continue;int eq=line.IndexOf('=');if(eq<=0)throw new InvalidDataException("Linha "+number+" sem \"=\".");
                string key=line.Substring(0,eq).Trim().ToLowerInvariant(),value=line.Substring(eq+1).Trim();
                switch(key){
                    case "nome":if(value.Length>0)r.Name=value.Length>80?value.Substring(0,80):value;break;
                    case "personagem":r.Character=value;break;
                    case "alcance":r.Reach=Math.Max(MinReach,Math.Min(MaxReach,Number(value,number)));break;
                    case "materiais":r.Materials=value.Split(new[]{',',';',' '},StringSplitOptions.RemoveEmptyEntries).Select(v=>Int(v,number)).Distinct().ToList();break;
                    case "trajeto":{var f=value.Split(';');if(f.Length<3)throw new InvalidDataException("Linha "+number+": trajeto precisa de x;y;z.");
                        var p=new TrailPoint{X=Number(f[0],number),Y=f[1].Trim()=="?"?Double.NaN:Number(f[1],number),Z=Number(f[2],number),Flying=f.Length<4||f[3].Trim()!="0"};
                        if(!GameClient.ValidCoordinate(p.X)||!GameClient.ValidCoordinate(p.Z))throw new InvalidDataException("Linha "+number+": coordenada fora do mapa.");
                        r.Path.Add(p);if(r.Path.Count>20000)throw new InvalidDataException("Trajeto com mais de 20 000 pontos.");break;}
                    case "coleta":{var f=value.Split(';');if(f.Length<5)throw new InvalidDataException("Linha "+number+": coleta precisa de material;item;x;y;z.");
                        if(r.Seen.Count<5000)r.Seen.Add(new Waypoint{ResourceId=Int(f[0],number),ItemId=Int(f[1],number),X=Number(f[2],number),Y=Number(f[3],number),Z=Number(f[4],number),Action="Coletar"});break;}
                    default:break;}}
            if(r.Path.Count<2)throw new InvalidDataException("O trajeto precisa de pelo menos 2 pontos (linhas trajeto=).");
            return r;
        }
        static double Number(string s,int line){double v;if(!Double.TryParse(s.Trim().Replace(',','.'),NumberStyles.Float,Inv,out v)||Double.IsNaN(v)||Double.IsInfinity(v))throw new InvalidDataException("Linha "+line+": número inválido \""+s+"\".");return v;}
        static int Int(string s,int line){int v;if(!Int32.TryParse(s.Trim(),NumberStyles.Integer,Inv,out v)||v<0)throw new InvalidDataException("Linha "+line+": número inválido \""+s+"\".");return v;}
    }
    // Keeps the recorded path light: a new point every 20 of travel, or after 8 when the direction turns more than 25°, or when
    // the height changes by 3 (Y/10) or flight turns on/off; standing still adds nothing.
    public sealed class TrailRecorder {
        public const double Spacing=20,TurnSpacing=8,TurnDegrees=25,HeightStep=30;
        readonly TrailRoute trail;public TrailRecorder(TrailRoute trail){this.trail=trail;}
        public bool Offer(double x,double y,double z,bool flying){var path=trail.Path;
            if(path.Count>=20000)return false;
            var p=new TrailPoint{X=x,Y=y,Z=z,Flying=flying};if(path.Count==0){path.Add(p);return true;}
            var last=path[path.Count-1];double dx=x-last.X,dz=z-last.Z,d=Math.Sqrt(dx*dx+dz*dz);
            bool add=d>=Spacing||last.Flying!=flying&&d>=2||Math.Abs(y-last.Y)>=HeightStep&&d>=2;
            if(!add&&d>=TurnSpacing&&path.Count>=2){var prev=path[path.Count-2];double ax=last.X-prev.X,az=last.Z-prev.Z,la=Math.Sqrt(ax*ax+az*az);
                if(la>0.5){double cos=(ax*dx+az*dz)/(la*d);add=Math.Acos(Math.Max(-1,Math.Min(1,cos)))*180/Math.PI>TurnDegrees;}}
            if(!add)return false;path.Add(p);return true;}
    }
}
