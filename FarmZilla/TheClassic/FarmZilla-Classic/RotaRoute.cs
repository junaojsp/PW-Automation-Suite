using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace RotaAvalon {
    public sealed class Waypoint {
        public int ResourceId {get;set;} public int ItemId {get;set;}
        public double X {get;set;} public double Y {get;set;} public double Z {get;set;}
        public int WaitMs {get;set;} public string Action {get;set;}
        // Area point from the embedded material map (0.36.0): center of a spawn area, height unknown until found.
        public bool Area {get;set;}
        public Waypoint(){Action="Passar";}
        public static Waypoint From(ClientSnapshot s){return new Waypoint{X=s.X,Y=s.Y,Z=s.Z};}
        public double DistanceTo(Waypoint other){double x=X-other.X,y=Y-other.Y,z=Z-other.Z;return Math.Sqrt(x*x+y*y+z*z);}
    }
    public sealed class RouteDocument {
        public List<int> ResourceIds {get;set;}
        public int Schema {get;set;} public string Name {get;set;} public string ClientHash {get;set;}
        public string Character {get;set;} public bool Loop {get;set;} public List<Waypoint> Points {get;set;}public double FlightHeight {get;set;}public int RewardOption {get;set;}public string Region {get;set;}public double SearchRadius {get;set;}
        public RouteDocument(){Schema=1;Name="Nova rota";ClientHash=GameClient.SupportedHash;Character="";Points=new List<Waypoint>();ResourceIds=new List<int>();FlightHeight=60;RewardOption=2;Region="";SearchRadius=1000;}
        public void Validate(){
            if(ResourceIds==null)ResourceIds=new List<int>();if(ResourceIds.Count>100000)throw new InvalidDataException("Lista de materiais excessiva.");var ids=new HashSet<int>();foreach(int id in ResourceIds)if(id<1||id>1000000||!ids.Add(id))throw new InvalidDataException("ID de material inválido ou duplicado.");
            if(Schema!=1)throw new InvalidDataException("Formato de rota não suportado.");
            if(Region==null)Region="";if(Region.Length>80)throw new InvalidDataException("Região deve ter até 80 caracteres.");foreach(char c in Region)if(Char.IsControl(c))throw new InvalidDataException("Nome de região inválido.");
            if(Double.IsNaN(SearchRadius)||Double.IsInfinity(SearchRadius)||SearchRadius<10||SearchRadius>10000)throw new InvalidDataException("Raio de busca deve estar entre 10 e 10000.");
            if(RewardOption!=2)throw new InvalidDataException("Este perfil usa a segunda opção de recompensa, conforme configurado para o servidor.");
            if(Double.IsNaN(FlightHeight)||Double.IsInfinity(FlightHeight)||FlightHeight<1||FlightHeight>1000)throw new InvalidDataException("Altura de voo deve estar entre 1 e 1000.");
            if(ClientHash!=GameClient.SupportedHash)throw new InvalidDataException("Rota criada para outro perfil de cliente.");
            if(String.IsNullOrWhiteSpace(Name)||Name.Length>120)throw new InvalidDataException("Nome da rota deve ter de 1 a 120 caracteres.");
            if(Points==null || Points.Count>20000)throw new InvalidDataException("Limite de 20 mil pontos por rota.");
            foreach(var p in Points){if(p==null || !GameClient.ValidCoordinate(p.X)||!GameClient.ValidCoordinate(p.Y)||!GameClient.ValidCoordinate(p.Z)||p.WaitMs<0||p.WaitMs>3600000||p.ResourceId<0||p.ResourceId>1000000||p.ItemId<0||p.ItemId>1000000 || (p.Action!="Passar"&&p.Action!="Coletar"&&p.Action!="Aguardar"))throw new InvalidDataException("A rota contém um ponto inválido.");}
        }
        public static RouteDocument Load(string path){var info=new FileInfo(path);if(info.Length>8*1024*1024)throw new InvalidDataException("Arquivo de rota muito grande.");var r=new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.Deserialize<RouteDocument>(File.ReadAllText(path));if(r==null)throw new InvalidDataException("Rota vazia.");r.Validate();return r;}
        public void Save(string path){Validate();string full=Path.GetFullPath(path),temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.Serialize(this));if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);}finally{if(File.Exists(temp))File.Delete(temp);}}
        public double Length(){double length=0;for(int i=1;i<Points.Count;i++)length+=Points[i-1].DistanceTo(Points[i]);if(Loop&&Points.Count>1)length+=Points[Points.Count-1].DistanceTo(Points[0]);return length;}
    }
}
