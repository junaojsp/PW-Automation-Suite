using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace RotaAvalon {
    // Manual route run (0.47.0): flies the recorded path point by point at each point's recorded height. While flying it
    // looks around (every 0,6 s) for a chosen material within reach of the character; when one has spawned it stops there,
    // goes over it, comes straight down, collects and does the material quest (same Visit as the automatic farm), climbs
    // straight back to the height it was flying and resumes the same leg. Defense, unstick and recovery are the farm's.
    // Laps: a closed path goes round; an open one goes there and back, so it only ever flies over the recorded path.
    public sealed partial class RouteExecution {
        const double TrailArrive=6,LookSeconds=0.6;
        DateTime nextLook;double trailHeight=40;
        public void StartTrail(int pid,TrailRoute trail,ICollection<int> materials,ResourceCatalog catalog,string library){StartTrail(pid,trail,materials,catalog,library,40);}
        // height: flight height (as the game shows it) for points without height — routes from another program (0.47.3, log
        // 05/11 13:11–13:16: Limago started on the ground, "kept the current height" ~23 and bumped into houses and a mountain).
        public void StartTrail(int pid,TrailRoute trail,ICollection<int> materials,ResourceCatalog catalog,string library,double height){
            if(Running)throw new InvalidOperationException("Uma rota já está em execução.");
            if(trail==null||trail.Path.Count<2)throw new InvalidOperationException("Grave ou carregue um trajeto primeiro.");
            if(materials==null||materials.Count==0)throw new InvalidOperationException("Marque pelo menos um material na lista.");
            var path=trail.Path.Select(p=>new TrailPoint{X=p.X,Y=p.Y,Z=p.Z,Flying=p.Flying}).ToList();var chosen=new HashSet<int>(materials);
            double reach=Math.Max(TrailRoute.MinReach,Math.Min(TrailRoute.MaxReach,trail.Reach));bool closed=trail.Closed;string name=trail.Name;
            verticalBroken=verticalConfirmed=false;pureMisses=0;trailHeight=Math.Max(10,Math.Min(300,height));Begin(pid,"trajeto");
            var thread=new Thread(()=>RunTrail(pid,name,path,closed,chosen,reach,catalog,library));thread.IsBackground=true;thread.Start();
        }
        static int Nearest(List<TrailPoint> path,ClientSnapshot s){int best=0;double d=double.MaxValue;for(int i=0;i<path.Count;i++){double v=Math.Pow(path[i].X-s.X,2)+Math.Pow(path[i].Z-s.Z,2);if(v<d){d=v;best=i;}}return best;}
        // A chosen material that has spawned within reach (not collected or tried in the last 150 s).
        Waypoint Spot(GameClient client,ResourceCatalog catalog,ClientSnapshot s,HashSet<int> chosen,double reach){
            if(DateTime.Now<nextLook)return null;nextLook=DateTime.Now.AddSeconds(LookSeconds);
            ResourceFrame frame;try{frame=client.ReadResources(catalog,s);}catch{return null;}
            foreach(var r in frame.Resources.Where(r=>chosen.Contains(r.TemplateId)&&r.LevelAllowed&&r.HorizontalDistance<=reach).OrderBy(r=>r.HorizontalDistance)){
                var w=new Waypoint{ResourceId=r.TemplateId,ItemId=r.ItemId,X=r.X,Y=r.Y,Z=r.Z,Action="Coletar"};if(!RecentlyCollected(w))return w;}
            return null;}
        // One leg toward a path point; false = stopped because a material was spotted (look returned true).
        bool TrailLeg(GameClient client,TrailPoint w,string character,string library,Func<ClientSnapshot,bool> look){
            var s=Read(client,character);if(look(s))return false;
            // 0.47.1: a point without height (route from another program) keeps the height it is flying at.
            if(w.Flying&&!heightBroken){try{return FlyHigh(client,w.X,w.Z,Double.IsNaN(w.Y)?Math.Round(trailHeight):Math.Max(1,Math.Round(w.Y/10)),character,library,look,TrailArrive);}
                catch(System.ComponentModel.Win32Exception e){heightBroken=true;emit("altura_indisponivel",new{code=e.NativeErrorCode,message="Voo com altura recusado pelo cliente; seguindo na altura atual."});}}
            Fly(client,w.X,w.Z,character,library);return true;}
        void RunTrail(int pid,string name,List<TrailPoint> path,bool closed,HashSet<int> chosen,double reach,ResourceCatalog catalog,string library){bridgeLibrary=library;
            try{using(var log=new StreamWriter(LogPath,false)){log.AutoFlush=true;var json=new JavaScriptSerializer();
                emit=(phase,data)=>{string line=json.Serialize(new{utc=DateTime.UtcNow.ToString("o"),phase=phase,data=data});log.WriteLine(line);var handler=Message;if(handler!=null)handler(phase+" · "+json.Serialize(data));};
                try{using(var client=new GameClient(pid,new Dictionary<string,bool>())){
                    var initial=client.Read();string character=initial.Name;Read(client,character);
                    int index=Nearest(path,initial),dir=1,cycle=1,failures=0,total=0,collected=0;
                    emit("iniciada",new{mode="trajeto",name=name,character=character,points=path.Count,circular=closed,alcance=reach,inicio=index+1,altura=path.Any(p=>Double.IsNaN(p.Y))?(object)trailHeight:"gravada"});emit("ciclo",new{cycle=cycle,points=path.Count});
                    // No height in the file: take off and go straight up to the flight height before the first leg.
                    if(path.Any(p=>Double.IsNaN(p.Y))){CruiseHeight=trailHeight;ClimbHere(client,character,library);}
                    while(!stop){var w=path[index];
                        try{Waypoint found=null;Func<ClientSnapshot,bool> look=s=>{found=Spot(client,catalog,s,chosen,reach);return found!=null;};
                            for(int round=0;round<20&&!stop;round++){found=null;if(TrailLeg(client,w,character,library,look)||found==null)break;
                                var at=Read(client,character);CruiseHeight=Double.IsNaN(w.Y)?trailHeight:Math.Max(10,Math.Round(at.Y/10));
                                emit("material_no_caminho",new{point=index+1,resource=found.ResourceId,distancia=Math.Round(Math.Sqrt(Math.Pow(found.X-at.X,2)+Math.Pow(found.Z-at.Z,2)),1),altura=CruiseHeight});
                                bool got=false;try{got=Visit(client,found,index,cycle,catalog,character,library,true);}finally{lock(recent)recent.Add(Tuple.Create(found.ResourceId,found.X,found.Z,DateTime.Now));}
                                if(got){collected++;total++;}
                                ClimbHere(client,character,library);emit("voltando_trajeto",new{point=index+1,altura=CruiseHeight});}
                            failures=0;}
                        catch(OperationCanceledException){throw;}
                        catch(Exception e){if(!(e is TimeoutException||e is IOException||e is System.ComponentModel.Win32Exception||e is InvalidOperationException||e is InvalidDataException))throw;failures++;emit("ponto_falhou",new{point=index+1,message=e.Message,seguidas=failures});if(Recover(client,character,e,failures))failures=0;}
                        index+=dir;bool lap=false;
                        if(index>=path.Count){lap=true;if(closed)index=0;else{dir=-1;index=Math.Max(0,path.Count-2);}}
                        else if(index<0){lap=true;dir=1;index=Math.Min(1,path.Count-1);}
                        if(lap){emit("volta_concluida",new{cycle=cycle,collected=collected,total=total});collected=0;cycle++;Wait(500);emit("ciclo",new{cycle=cycle,points=path.Count});}
                    }
                    emit("encerrada",new{reason="Solicitado pelo usuário",total=total});
                }}catch(OperationCanceledException){emit("parada",new{reason="Solicitado pelo usuário; comando nativo já enviado pode terminar no jogo"});}catch(Exception e){LastError=e.Message;emit("erro",new{type=e.GetType().Name,message=e.Message});}
            }}catch(Exception e){LastError=e.Message;var handler=Message;if(handler!=null)handler("Falha no log: "+e.Message);}finally{Running=false;}
        }
    }
}
