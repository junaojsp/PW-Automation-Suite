using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
namespace RotaAvalon {
    // Mat Farm route as a plain .txt (0.42.0, owner): materials, flight height, farm radius and the numbered points in the
    // farm order. Loading it makes the farm follow exactly these points (lap after lap) instead of building its own plan.
    // One "chave=valor" per line; points as "ponto=material;item;x;y;z;area" with dot decimals. Lines starting with # are notes.
    public sealed class FarmRouteText {
        public string Name="",Character="";public double Height=60,Radius=1500;public List<int> Materials=new List<int>();public List<Waypoint> Points=new List<Waypoint>();
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        static string N(double v){return Math.Round(v,2).ToString("0.##",Inv);}
        public void Write(string path){
            var lines=new List<string>{"# FarmZilla · rota do Mat Farm","# Carregue no Mat Farm em \"Carregar rota\". Pontos na ordem do farm: material;item;x;y;z;area (area=1: centro da área, altura achada na hora).",
                "nome="+Name.Replace("\r"," ").Replace("\n"," "),"personagem="+Character,"altura="+N(Height),"raio="+N(Radius),"materiais="+String.Join(",",Materials)};
            foreach(var p in Points)lines.Add("ponto="+p.ResourceId+";"+p.ItemId+";"+N(p.X)+";"+N(p.Y)+";"+N(p.Z)+";"+(p.Area?1:0));
            File.WriteAllLines(path,lines,new UTF8Encoding(false));
        }
        public static FarmRouteText Read(string path){
            var info=new FileInfo(path);if(!info.Exists)throw new FileNotFoundException("Arquivo da rota não encontrado.");if(info.Length>2*1024*1024)throw new InvalidDataException("Arquivo grande demais para uma rota.");
            var r=new FarmRouteText{Name=Path.GetFileNameWithoutExtension(path)};int number=0;
            foreach(var raw in File.ReadAllLines(path,Encoding.UTF8)){number++;var line=raw.Trim();if(line.Length==0||line.StartsWith("#"))continue;int eq=line.IndexOf('=');if(eq<=0)throw new InvalidDataException("Linha "+number+" sem \"=\".");
                string key=line.Substring(0,eq).Trim().ToLowerInvariant(),value=line.Substring(eq+1).Trim();
                switch(key){
                    case "nome":if(value.Length>0)r.Name=value.Length>80?value.Substring(0,80):value;break;
                    case "personagem":r.Character=value;break;
                    case "altura":r.Height=Clamp(Number(value,number),10,300);break;
                    case "raio":r.Radius=Clamp(Number(value,number),100,30000);break;
                    case "materiais":r.Materials=value.Split(new[]{',',';',' '},StringSplitOptions.RemoveEmptyEntries).Select(v=>Int(v,number)).Distinct().ToList();break;
                    case "ponto":{var f=value.Split(';');if(f.Length<5)throw new InvalidDataException("Linha "+number+": ponto precisa de material;item;x;y;z.");
                        r.Points.Add(new Waypoint{ResourceId=Int(f[0],number),ItemId=Int(f[1],number),X=Number(f[2],number),Y=Number(f[3],number),Z=Number(f[4],number),Area=f.Length>5&&f[5].Trim()=="1",Action="Coletar",WaitMs=0});
                        if(r.Points.Count>5000)throw new InvalidDataException("Rota com mais de 5000 pontos.");break;}
                    default:break;}}
            if(r.Points.Count==0)throw new InvalidDataException("A rota não tem pontos.");
            foreach(var id in r.Points.Select(p=>p.ResourceId))if(!r.Materials.Contains(id))r.Materials.Add(id);
            return r;
        }
        static double Clamp(double v,double low,double high){return Math.Max(low,Math.Min(high,v));}
        static double Number(string s,int line){double v;if(!Double.TryParse(s.Trim().Replace(',','.'),NumberStyles.Float,Inv,out v)||Double.IsNaN(v)||Double.IsInfinity(v))throw new InvalidDataException("Linha "+line+": número inválido \""+s+"\".");return v;}
        static int Int(string s,int line){int v;if(!Int32.TryParse(s.Trim(),NumberStyles.Integer,Inv,out v)||v<0)throw new InvalidDataException("Linha "+line+": número inválido \""+s+"\".");return v;}
        public RouteDocument ToPlan(){var plan=new RouteDocument{Name=Name,Character=Character,Loop=false,ResourceIds=Materials.ToList(),FlightHeight=Height};foreach(var p in Points)plan.Points.Add(p);return plan;}
    }
}
