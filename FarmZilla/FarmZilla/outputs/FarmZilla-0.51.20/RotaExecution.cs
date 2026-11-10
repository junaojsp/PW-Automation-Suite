using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace RotaAvalon {
    // Runs a manual route (fixed copy; UI edits apply on the next start) or the automatic farm (FarmZilla 0.26.0): points from
    // the scanned history of the chosen materials, nearest-next, rebuilt every lap, repeated until stopped.
    // Per point: go there with the game's own auto-path (native message 0x14A; it keeps the current height), find the material
    // loaded there, collect, do the material quest when it appears (second reward), defend with macro 0 when hit.
    // Field facts: a purely vertical target is ignored and the native move has no height control (disassembly 02/11/2026);
    // collect, quest and defense confirmed in game on 02/11/2026 19:04.
    public sealed partial class RouteExecution {
        volatile bool stop;
        public volatile bool Running;
        public string LastError {get;private set;}
        public string LogPath {get;private set;}
        public event Action<string> Message;
        // Defense through FarmZilla's attack bridge (macro 0 on a monster): (pid, monster) -> error or null, and "still attacking".
        public Func<int,uint,string> StartAttack;public Func<int,uint,bool> AttackRunning;
        // 0.50.4 (owner): a dead character waits a little for a resurrection, then revives at the nearest town and goes on.
        public Func<int,string> ReviveInTown;
        // Hop: back-and-forth around the point while climbing/descending (the game ignores a purely vertical move); 8 keeps the
        // character practically over the material's coordinate (0.29.0; was 25).
        // ProbeRange/VisibleBelow: measured 02/11/2026, the client keeps materials loaded up to ~120 away and ~41 below.
        const double SeedRadius=60;
        const double TakeoffStep=12,KeepMoving=8,QuietSeconds=5,StuckSeconds=2.5,StuckGain=2,EscapeStep=28,EscapeClimb=8;const int MaxEscapes=4;
        const double Leg=120,ArriveXZ=3,ArriveY=8,FindRadius=25,HighLeg=300,Approach=400,Hop=8,ShortHop=170,SettleSpan=30,ProbeRange=90,VisibleBelow=36;
        // Flight height in game units (Y/10). 0 = keep the current height (old behaviour, used by scans).
        public double CruiseHeight;bool heightBroken;
        const int MaxFailuresInARow=6,RecentSeconds=150;
        readonly Dictionary<uint,DateTime> ignored=new Dictionary<uint,DateTime>();
        int pid,defenses;double lastHp=-1;bool defending,flightWarned;Action<string,object> emit;
        readonly List<Tuple<int,double,double,DateTime>> recent=new List<Tuple<int,double,double,DateTime>>();
        public void Stop(){stop=true;}
        void Begin(int pid,string prefix){if(Running)throw new InvalidOperationException("Uma rota já está em execução.");
            LogPath=Path.Combine(RotaPaths.Logs,prefix+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".jsonl");
            stop=false;LastError=null;Running=true;this.pid=pid;lastHp=-1;defenses=0;flightWarned=false;heightBroken=false;lock(recent)recent.Clear();}
        public void Start(int pid,RouteDocument document,ResourceCatalog catalog,string library){
            if(Running)throw new InvalidOperationException("Uma rota já está em execução.");
            document.Validate();if(document.Points.Count==0)throw new InvalidOperationException("Adicione pontos ao mapa.");
            var json=new JavaScriptSerializer{MaxJsonLength=8*1024*1024};var route=json.Deserialize<RouteDocument>(json.Serialize(document));
            Begin(pid,"rota");CruiseHeight=route.FlightHeight;var thread=new Thread(()=>Run(pid,route,null,catalog,library));thread.IsBackground=true;thread.Start();
        }
        // plan: called at the start of every lap (on the caller's UI thread through Invoke); null or empty = wait and ask again.
        public void StartFarm(int pid,ResourceCatalog catalog,string library,Func<RouteDocument> plan,double height){verticalBroken=verticalConfirmed=false;pureMisses=0;
            if(plan==null)throw new ArgumentNullException("plan");Begin(pid,"farm");CruiseHeight=height;var thread=new Thread(()=>Run(pid,null,plan,catalog,library));thread.IsBackground=true;thread.Start();
        }
        void Wait(int milliseconds){for(int elapsed=0;elapsed<milliseconds;elapsed+=100){if(stop)throw new OperationCanceledException();Thread.Sleep(Math.Min(100,milliseconds-elapsed));}}
        ClientSnapshot Raw(GameClient client,string character){if(stop)throw new OperationCanceledException();var s=client.Read();if(s.Name!=character)throw new IOException("Personagem mudou.");if(s.Hp<=0)throw new IOException("Personagem sem vida; rota parada.");
            bool flying=s.FlightStateKnown&&s.IsFlying;if(!flying&&!flightWarned){flightWarned=true;emit("sem_voo",new{message="Voo desligado; seguindo pelo caminho automático do jogo."});}else if(flying)flightWarned=false;return s;}
        // Every read checks for damage: life going down = something is hitting the character, so it defends first.
        // 0.48.0 (owner: "priorizar sempre a missão, ainda que tiver um bicho batendo"): while the material quest is being done
        // (missionFirst) damage does not start the defense and low life does not stop; a monster still hitting afterwards is
        // fought right after (the next read sees the life going down).
        bool missionFirst;
        ClientSnapshot Read(GameClient client,string character){var s=Raw(client,character);
            if(missionFirst){if(lastHp>=0&&s.Hp<lastHp)hitDuringMission=true;lastHp=s.Hp;return s;}
            if(!defending&&(hitDuringMission||lastHp>=0&&s.Hp<lastHp&&s.Hp<s.MaxHp)){hitDuringMission=false;defending=true;try{Defend(client,character);}finally{defending=false;}s=Raw(client,character);}
            lastHp=s.Hp;if(s.Hp*10L<s.MaxHp*3L)throw new IOException("Vida abaixo de 30%; parado por segurança.");return s;}
        bool hitDuringMission;
        // 0.47.0 (owner: decide fast; more than 1 minute on one monster = give up; turn to the next one that is hitting until
        // nobody is): one monster at a time with macro 0 restarted 1 s after it ends. A monster ends by dying (gone from the list),
        // by 60 s without dying (ignored for 2 min) or by 5 s without damage (nobody hitting: back to the route). After a kill or
        // a give-up, damage in the last 2 s (or within the next 1 s) means someone else is hitting: the nearest other monster is
        // taken at once. At most 6 monsters / 4 minutes per defense so the route never stays stuck.
        const double GiveUpSeconds=60,StillHitSeconds=2;
        void Defend(GameClient client,string character){
            var s=Raw(client,character);lock(ignored){foreach(var key in ignored.Where(kv=>kv.Value<DateTime.Now).Select(kv=>kv.Key).ToList())ignored.Remove(key);}
            if(StartAttack==null||AttackRunning==null)throw new IOException("Personagem atacado e a defesa (macro 0) não está disponível.");
            // The move in progress is stopped first (it kept flying away from the monster).
            try{if(bridgeLibrary!=null)NativeBridge.Navigate(client,s.X,s.Y,s.Z,bridgeLibrary);}catch{}
            var done=new HashSet<uint>();var whole=Stopwatch.StartNew();DateTime lastHit=DateTime.Now;int seenHp=s.Hp;
            for(int fight=0;fight<6&&whole.Elapsed.TotalMinutes<4;fight++){
                s=Raw(client,character);
                var candidates=client.ReadMonsters(s).Where(m=>!done.Contains(m.Id)).Where(m=>{lock(ignored)return !ignored.ContainsKey(m.Id);}).ToList();
                var monster=candidates.Where(m=>m.Distance<=25).OrderBy(m=>m.Distance).FirstOrDefault()??candidates.Where(m=>m.Distance<=60).OrderBy(m=>m.Distance).FirstOrDefault();
                if(monster==null){if(fight==0)emit("dano_sem_monstro",new{hp=s.Hp,max=s.MaxHp});lastHp=s.Hp;return;}
                done.Add(monster.Id);defenses++;emit(fight==0?"defesa":"defesa_proximo",new{monster=monster.Id.ToString("X8"),distance=Math.Round(monster.Distance,1),hp=s.Hp,max=s.MaxHp,luta=fight+1});
                var clock=Stopwatch.StartNew();DateTime next=DateTime.MinValue;string end=null;bool unavailable=false;
                while(end==null){s=Raw(client,character);
                    if(s.Hp<seenHp)lastHit=DateTime.Now;seenHp=s.Hp;
                    if(!client.ReadMonsters(s).Any(m=>m.Id==monster.Id)){end="morto";emit("monstro_derrotado",new{monster=monster.Id.ToString("X8"),seconds=Math.Round(clock.Elapsed.TotalSeconds,1)});break;}
                    // 0.47.3 (log 13:12–13:14: the same monster was "finished" 4 times while alive and hit again 2 s later): no damage
                    // only ends the fight when the monster cannot be attacked any more (dead/gone: the start fails) or ran off beyond 40.
                    var still=client.ReadMonsters(s).FirstOrDefault(m=>m.Id==monster.Id);if(still!=null&&still.Distance>40&&(DateTime.Now-lastHit).TotalSeconds>=QuietSeconds){emit("defesa_concluida",new{monster=monster.Id.ToString("X8"),seconds=Math.Round(clock.Elapsed.TotalSeconds,1),motivo="monstro longe ("+Math.Round(still.Distance)+")"});lastHp=s.Hp;return;}
                    if(clock.Elapsed.TotalSeconds>=3&&unavailable&&(DateTime.Now-lastHit).TotalSeconds>=QuietSeconds){emit("defesa_concluida",new{monster=monster.Id.ToString("X8"),seconds=Math.Round(clock.Elapsed.TotalSeconds,1),motivo="sem dano e alvo indisponível"});lastHp=s.Hp;return;}
                    if(clock.Elapsed.TotalSeconds>=GiveUpSeconds){end="desistiu";lock(ignored)ignored[monster.Id]=DateTime.Now.AddMinutes(2);emit("defesa_desistiu",new{monster=monster.Id.ToString("X8"),message="Monstro não morreu em "+GiveUpSeconds+" s; trocando de alvo."});break;}
                    if(DateTime.Now>=next&&!AttackRunning(pid,monster.Id)){string error=StartAttack(pid,monster.Id);bool failed=error!=null&&error.IndexOf("atacando",StringComparison.OrdinalIgnoreCase)<0;unavailable=failed;if(failed)emit("defesa_aviso",new{message=error});next=DateTime.Now.AddSeconds(failed?2.0:1.0);}
                    Wait(150);}
                // Someone else still hitting? Damage in the last 2 s, or within the next second.
                if((DateTime.Now-lastHit).TotalSeconds>StillHitSeconds){var look=Stopwatch.StartNew();while(look.Elapsed.TotalSeconds<1){Wait(150);s=Raw(client,character);if(s.Hp<seenHp){lastHit=DateTime.Now;seenHp=s.Hp;break;}seenHp=s.Hp;}}
                if((DateTime.Now-lastHit).TotalSeconds>StillHitSeconds){lastHp=Raw(client,character).Hp;return;}
            }
            s=Raw(client,character);lastHp=s.Hp;emit("defesa_limite",new{message="Defesa longa demais; voltando à rotina."});
        }
        // Goes to (x,z) in legs of up to 120 with the game's auto-path, keeping the current height.
        void Fly(GameClient client,double tx,double tz,string character,string library){
            var total=Stopwatch.StartNew();int escapes=0;
            while(true){var s=Read(client,character);double dx=tx-s.X,dz=tz-s.Z,distance=Math.Sqrt(dx*dx+dz*dz);if(distance<=ArriveXZ)return;
                if(total.Elapsed.TotalMinutes>5)throw new TimeoutException("Tempo de deslocamento esgotado.");
                double fraction=Math.Min(1,Leg/distance),x=s.X+dx*fraction,z=s.Z+dz*fraction;NativeBridge.Navigate(client,x,s.Y,z,library);
                var leg=Stopwatch.StartNew();bool arrived=false,retry=false;int fights=defenses;var progress=new Progress(x,z,s);
                while(leg.Elapsed.TotalSeconds<35){s=Read(client,character);if(defenses!=fights){retry=true;break;}double ex=x-s.X,ez=z-s.Z;if(ex*ex+ez*ez<=ArriveXZ*ArriveXZ){arrived=true;break;}
                    if(progress.Stuck(s)){if(++escapes>MaxEscapes)throw new TimeoutException("Travado: "+MaxEscapes+" tentativas de desviar sem sair do lugar (altura "+(s.Y/10).ToString("F1")+").");Escape(client,x,z,-1,character,library);retry=true;break;}Wait(150);}
                if(!arrived&&!retry)throw new TimeoutException("Ponto não alcançado; possível obstáculo.");
            }
        }
        // FarmZilla 0.27.0: the game's own auto-move with height control (native operation 6). Climbs to CruiseHeight while
        // going; for collect points the last stretch targets the mapped point's height, then short back-and-forth hops finish
        // the descent (the game ignores a purely vertical move). Any failure of the height path falls back to the old move.
        // 0.34.0 (owner: too much time in the air): never climbs standing still — the height changes while it moves to the
        // next point; the descent starts 400 before the point; on the way down, once the point is in sight (≤90 away, ≤36
        // above the mapped ground) it checks the material and, if it is not there, goes to the next point still high up.
        // Returns false when the material was seen missing.
        bool Travel(GameClient client,Waypoint p,ResourceCatalog catalog,string character,string library){
            bool collect=p.Action=="Coletar";
            if(CruiseHeight<=0||heightBroken){Fly(client,p.X,p.Z,character,library);return true;}
            double ground=Math.Max(1,Math.Round(p.Y/10+1));
            try{var s=Read(client,character);double dx=p.X-s.X,dz=p.Z-s.Z,distance=Math.Sqrt(dx*dx+dz*dz);
                if(!collect){FlyHigh(client,p.X,p.Z,CruiseHeight,character,library);return true;}
                if(p.Area&&p.Y<=0)return FindFromAbove(client,p,catalog,character,library,distance);
                // 0.44.0 (owner: it flew low between near materials and bumped into things): always climb straight up to the
                // programmed height, fly high to right above the point, come straight down and collect; the next point starts by
                // climbing straight up again. On the way down the material is checked; if it is gone, it goes on still high.
                var probe=new AbsenceProbe(this,client,catalog,p);
                if(verticalBroken){
                    // No straight vertical in this client: one straight inclined line — high until 400 before the point, then a
                    // straight glide down onto it (no back and forth); the climb happens on the straight line to the next point.
                    if(distance>Approach+ArriveXZ){emit("indo_alto",new{alvo=CruiseHeight,distancia=Math.Round(distance,0)});FlyHigh(client,p.X-dx/distance*Approach,p.Z-dz/distance*Approach,CruiseHeight,character,library);}
                    emit("descendo",new{alvo=ground,modo="rampa"});if(!FlyHigh(client,p.X,p.Z,ground,character,library,probe.Check))return false;
                    var low=Read(client,character);if(Math.Abs(low.Y/10-ground)>2&&!Settle(client,p.X,p.Z,ground,character,library,probe.Check))return false;}
                else{
                    if(distance>ArriveXZ+1){ClimbHere(client,character,library);emit("indo_alto",new{alvo=CruiseHeight,distancia=Math.Round(distance,0)});FlyHigh(client,p.X,p.Z,CruiseHeight,character,library);}
                    emit("descendo",new{alvo=ground});if(!Settle(client,p.X,p.Z,ground,character,library,probe.Check))return false;}
                for(int i=0;i<3&&!probe.Seen;i++){var now=Read(client,character);if((now.Y-p.Y)/10>VisibleBelow)break;if(probe.Force(now))return false;Wait(600);}
                return true;}
            catch(System.ComponentModel.Win32Exception e){heightBroken=true;emit("altura_indisponivel",new{code=e.NativeErrorCode,message="Voo com altura recusado pelo cliente; seguindo na altura atual."});Fly(client,p.X,p.Z,character,library);return true;}
        }
        // Area point with unknown height (embedded material map): fly over the center, look for the material within 60, and
        // step down 20 at a time until it shows up or the ground is near. The height found is kept on the point (next laps).
        bool FindFromAbove(GameClient client,Waypoint p,ResourceCatalog catalog,string character,string library,double distance){
            var s=Read(client,character);ClimbHere(client,character,library);FlyHigh(client,p.X,p.Z,CruiseHeight,character,library);
            for(int step=0;step<6;step++){var now=Read(client,character);ResourceMarker found=null;
                try{found=client.ReadResources(catalog,now).Resources.Where(r=>r.TemplateId==p.ResourceId&&r.LevelAllowed&&Math.Pow(r.X-p.X,2)+Math.Pow(r.Z-p.Z,2)<=SeedRadius*SeedRadius).OrderBy(r=>Math.Pow(r.X-p.X,2)+Math.Pow(r.Z-p.Z,2)).FirstOrDefault();}catch{}
                if(found!=null){p.Y=found.Y;double g=Math.Max(1,Math.Round(found.Y/10+1));emit("achado",new{altura=Math.Round(found.Y/10,1),distancia=Math.Round(Math.Sqrt(Math.Pow(found.X-p.X,2)+Math.Pow(found.Z-p.Z,2)),0)});emit("descendo",new{alvo=g});Settle(client,found.X,found.Z,g,character,library);return true;}
                double next=Math.Round(now.Y/10-20);if(next<8)break;emit("procurando",new{altura=next});Settle(client,p.X,p.Z,next,character,library);}
            return false;}
        // Looks for the point's material from above; two misses (0,7 s apart) in sight = absent. Once seen, it stops looking.
        sealed class AbsenceProbe {
            readonly RouteExecution owner;readonly GameClient client;readonly ResourceCatalog catalog;readonly Waypoint p;int misses;DateTime next=DateTime.MinValue;
            public bool Seen;
            public AbsenceProbe(RouteExecution owner,GameClient client,ResourceCatalog catalog,Waypoint p){this.owner=owner;this.client=client;this.catalog=catalog;this.p=p;}
            public bool Check(ClientSnapshot s){return Look(s,false);}
            public bool Force(ClientSnapshot s){return Look(s,true);}
            bool Look(ClientSnapshot s,bool force){if(Seen)return false;double dx=p.X-s.X,dz=p.Z-s.Z;if(dx*dx+dz*dz>ProbeRange*ProbeRange)return false;if((s.Y-p.Y)/10>VisibleBelow)return false;
                if(!force&&DateTime.Now<next)return false;next=DateTime.Now.AddMilliseconds(700);
                ResourceFrame frame;try{frame=client.ReadResources(catalog,s);}catch{return false;}
                if(frame.Resources.Any(r=>r.TemplateId==p.ResourceId&&r.LevelAllowed&&Math.Pow(r.X-p.X,2)+Math.Pow(r.Z-p.Z,2)<=(p.Area?SeedRadius:FindRadius)*(p.Area?SeedRadius:FindRadius))){Seen=true;return false;}
                misses++;return misses>=2;}
        }
        bool FlyHigh(GameClient client,double tx,double tz,double height,string character,string library,Func<ClientSnapshot,bool> absent=null,double arrive=ArriveXZ+1){
            var total=Stopwatch.StartNew();int h=(int)Math.Round(height);int escapes=0;
            while(true){var s=Read(client,character);if(absent!=null&&absent(s))return false;double dx=tx-s.X,dz=tz-s.Z,distance=Math.Sqrt(dx*dx+dz*dz);if(distance<=arrive)return true;
                if(total.Elapsed.TotalMinutes>6)throw new TimeoutException("Tempo de deslocamento esgotado.");
                double fraction=Math.Min(1,HighLeg/distance),x=s.X+dx*fraction,z=s.Z+dz*fraction;
                NativeBridge.FlyTo(client,x,z,library);Wait(80);NativeBridge.FlightHeight(client,h,library);
                var leg=Stopwatch.StartNew();bool arrived=false,retry=false;int fights=defenses;var progress=new Progress(x,z,s);
                while(leg.Elapsed.TotalSeconds<60){s=Read(client,character);if(absent!=null&&absent(s))return false;if(defenses!=fights){retry=true;break;}double ex=x-s.X,ez=z-s.Z;if(ex*ex+ez*ez<=arrive*arrive){arrived=true;escapes=0;break;}
                    if(progress.Stuck(s)){if(++escapes>MaxEscapes){emit("voo_reto_parado",new{altura=Math.Round(s.Y/10,1)});Fly(client,x,z,character,library);arrived=true;escapes=0;break;}Escape(client,x,z,h,character,library);retry=true;break;}Wait(150);}
                if(!arrived&&!retry)throw new TimeoutException("Ponto não alcançado; possível obstáculo.");}
        }
        // 0.44.0 (owner: it kept pushing into walls): progress = getting closer to the leg target. No gain of 2 in 2.5 s =
        // stuck, then Escape tries another way out: back, then left, then right (and a bit higher when flying), each until
        // it actually moves; the leg is then sent again from the new spot.
        sealed class Progress {
            readonly double x,z;double best;readonly Stopwatch since=Stopwatch.StartNew();
            public Progress(double x,double z,ClientSnapshot s){this.x=x;this.z=z;best=Distance(s);}
            double Distance(ClientSnapshot s){double dx=x-s.X,dz=z-s.Z;return Math.Sqrt(dx*dx+dz*dz);}
            public bool Stuck(ClientSnapshot s){double d=Distance(s);if(d<best-StuckGain){best=d;since.Restart();return false;}return since.Elapsed.TotalSeconds>=StuckSeconds;}
        }
        void Escape(GameClient client,double tx,double tz,int height,string character,string library){
            var s=Read(client,character);double dx=tx-s.X,dz=tz-s.Z,len=Math.Max(1,Math.Sqrt(dx*dx+dz*dz)),ux=dx/len,uz=dz/len;
            var ways=new[]{Tuple.Create(-ux,-uz,"trás"),Tuple.Create(-uz,ux,"esquerda"),Tuple.Create(uz,-ux,"direita")};
            foreach(var way in ways){double ex=s.X+way.Item1*EscapeStep,ez=s.Z+way.Item2*EscapeStep;emit("destravando",new{lado=way.Item3,altura=Math.Round(s.Y/10,1)});
                if(height>0){NativeBridge.FlyTo(client,ex,ez,library);Wait(60);NativeBridge.FlightHeight(client,height+(int)EscapeClimb,library);}else NativeBridge.Navigate(client,ex,s.Y,ez,library);
                double sx=s.X,sz=s.Z;var clock=Stopwatch.StartNew();
                while(clock.Elapsed.TotalSeconds<2.5){var now=Read(client,character);if(Math.Abs(now.X-sx)+Math.Abs(now.Z-sz)>=8){emit("destravado",new{lado=way.Item3});return;}Wait(120);}
                s=Read(client,character);}
            emit("destravar_falhou",new{x=Math.Round(s.X,1),z=Math.Round(s.Z,1)});}
        // Straight up/down like holding Space / Z (0.45.0): holds the vertical direction, stops at the height, always releases.
        // null = the client did not move vertically in 1,5 s (not supported here; never tried again this session), true = done,
        // false = the material was seen missing on the way down.
        bool verticalBroken,verticalConfirmed;int pureMisses;
        bool? Straight(GameClient client,double height,string character,string library,Func<ClientSnapshot,bool> absent){
            if(verticalBroken)return null;var s=Read(client,character);double target=height*10;if(Math.Abs(s.Y-target)<=10)return true;int dir=s.Y<target?1:-1;
            // 0.45.5 (log 18:40–18:44: 6 of 14 climbs/descents fell back to the side passes, mostly the climb right after
            // collecting, because the character was on the ground): on the ground it asks the game's take-off (height a bit
            // above) and then holds Space the same way; the side passes stay only for a client where nothing vertical works.
            if(!(s.FlightStateKnown&&s.IsFlying)){if(dir<0)return true;
                // 0.45.8 (owner: on the ground, open the flight): the game's own flight command opens the flyer already set on the
                // character (log 20:55: Lima took ~3 s or more), so it waits up to 7 s, asking again at 3,5 s, then climbs straight.
                // 0.45.9 (log 21:07:05: the height command alone did not open the flight in 7 s; the route opened it right after): the
                // flight opens with a short move forward (like a normal take-off) and the straight climb starts as soon as it flies.
                emit("decolando",new{altura=Math.Round(s.Y/10,1)});int lift=(int)Math.Round(s.Y/10)+5;double fx=s.X,fz=s.Z;var takeoff=Stopwatch.StartNew();int pushes=0;
                while(takeoff.Elapsed.TotalSeconds<6){if(takeoff.Elapsed.TotalSeconds>=pushes*1.5){pushes++;try{NativeBridge.FlyTo(client,fx+TakeoffStep*pushes,fz,library);Wait(60);NativeBridge.FlightHeight(client,lift,library);}catch(System.ComponentModel.Win32Exception){}}
                    Wait(150);s=Read(client,character);if(s.FlightStateKnown&&s.IsFlying){emit("decolou",new{segundos=Math.Round(takeoff.Elapsed.TotalSeconds,1)});try{NativeBridge.FlyTo(client,s.X,s.Z,library);}catch{}break;}}
                // 0.45.7 (log 20:55: Lima started on the ground, did not take off in time and the straight climb was switched off
                // for the whole farm): still on the ground = only this climb goes with the route (which takes off by itself);
                // the straight climb stays on and is used again as soon as it is flying.
                if(!(s.FlightStateKnown&&s.IsFlying)){emit("sem_decolar",new{altura=Math.Round(s.Y/10,1)});return null;}}
            // 0.45.4 (owner: the little forward/back steps look wrong): first only Space/Z held, no route move at all — the route
            // starts only after the height is reached. If the height does not change in 1,2 s with the key alone, it continues
            // with the 0.45.2 steps of 8 (live test: still in the air needed them); after 3 such misses in a row the key-only
            // try is skipped for the rest of the session so it does not cost 1,2 s every time.
            double anchorX=s.X,anchorZ=s.Z;int step=0;bool steps=pureMisses>=3;double pureFrom=s.Y;var key=Stopwatch.StartNew();try{if(!steps)NativeBridge.KeyMove(client,dir>0,library);}catch(System.ComponentModel.Win32Exception){}
            double startY=s.Y,limit=30+Math.Abs(s.Y/10-height)*1.6;var clock=Stopwatch.StartNew();int lastHp=s.Hp;
            try{
                while(clock.Elapsed.TotalSeconds<limit){
                    if(steps)NativeBridge.FlyTo(client,anchorX+(step++%2==0?KeepMoving:-KeepMoving),anchorZ,library);
                    // 0.45.6: "presses" Space/Z through the game's own keyboard move (no route, no side steps) about 4 times a
                    // second, while the direction bit is held between frames.
                    else if(key.ElapsedMilliseconds>=250){key.Restart();try{NativeBridge.KeyMove(client,dir>0,library);}catch(System.ComponentModel.Win32Exception){}}
                    client.Vertical(dir);Wait(150);s=Raw(client,character);
                    if(s.Hp<lastHp){client.Vertical(0);s=Read(client,character);lastHp=s.Hp;continue;}lastHp=s.Hp;
                    if(absent!=null&&absent(s))return false;
                    if(dir>0?s.Y>=target-8:s.Y<=target+8){if(!steps)pureMisses=0;return true;}
                    if(!steps&&clock.Elapsed.TotalSeconds>=1.2&&Math.Abs(s.Y-pureFrom)<2){steps=true;pureMisses++;emit("vertical_passos",new{altura=Math.Round(s.Y/10,1),message=pureMisses>=3?"Espaço/Z sozinho não move parado no ar; usando passos curtos nesta sessão.":"Espaço/Z sozinho não moveu; usando passos curtos nesta subida/descida."});startY=s.Y;clock.Restart();}
                    else if(!steps&&!verticalConfirmed&&clock.Elapsed.TotalSeconds>=1.2){verticalConfirmed=true;emit("vertical_ok",new{altura=Math.Round(s.Y/10,1),modo="espaço/Z"});}
                    if(steps&&!verticalConfirmed&&clock.Elapsed.TotalSeconds>=2){if(Math.Abs(s.Y-startY)<2){if(!(s.FlightStateKnown&&s.IsFlying))return null;verticalBroken=true;emit("vertical_indisponivel",new{message="Subida/descida reta não aceita por este cliente; usando linha reta inclinada."});return null;}verticalConfirmed=true;emit("vertical_ok",new{altura=Math.Round(s.Y/10,1),modo="passos"});}
                }
            }
            catch(OperationCanceledException){throw;}
            catch(Exception e){verticalBroken=true;emit("vertical_indisponivel",new{message=e.Message});return null;}
            finally{try{client.Vertical(0);}catch{}if(steps)try{var at=client.Read();NativeBridge.FlyTo(client,at.X,at.Z,library);}catch{}}
            return null;}
        // Straight up over the current spot to the programmed height (skipped when already there).
        void ClimbHere(GameClient client,string character,string library){if(verticalBroken)return;var s=Read(client,character);if(s.Y/10>=CruiseHeight-2)return;
            // On the ground: the game's auto take-off first (height target a little above), so the climb itself can be straight.
emit("subindo",new{alvo=CruiseHeight,atual=Math.Round(s.Y/10,1)});
            // 0.45.7: straight first; if the character could not take off here, the route climbs on its way (no side passes).
            var straight=Straight(client,CruiseHeight,character,library,null);if(straight==true){var at=Read(client,character);emit("altura",new{alvo=CruiseHeight,atual=Math.Round(at.Y/10,1),modo="reto"});return;}
            var now=Read(client,character);if(!(now.FlightStateKnown&&now.IsFlying))return;
            Settle(client,now.X,now.Z,CruiseHeight,character,library);}
        // Reaches an exact height over (x,z). Height only changes while moving horizontally, so it makes clean straight passes
        // over the point (not tiny side-to-side hops, which spun the character), descends/climbs along each pass and stops the
        // moment the height is reached; a stuck pass switches axis and widens. Ends with a precise landing on (x,z). (0.33.0)
        bool Settle(GameClient client,double x,double z,double height,string character,string library,Func<ClientSnapshot,bool> absent=null){
            int h=(int)Math.Round(height);var clock=Stopwatch.StartNew();int side=1,axis=0;double span=SettleSpan;
            double limit;{var first=Read(client,character);limit=Math.Max(60,30+Math.Abs(first.Y/10-height)*1.6);
                double off=Math.Sqrt(Math.Pow(first.X-x,2)+Math.Pow(first.Z-z,2));
                // 0.45.3 (log 18:26:58: a material found off the area center was approached with the side passes and the character
                // stopped 3,6 too high, so the collect failed): first fly level to right above it, then go straight down.
                if(off>ArriveXZ+2&&!verticalBroken&&first.FlightStateKnown&&first.IsFlying){FlyHigh(client,x,z,Math.Round(first.Y/10),character,library);var over=Read(client,character);off=Math.Sqrt(Math.Pow(over.X-x,2)+Math.Pow(over.Z-z,2));}
                if(off<=ArriveXZ+2){bool? straight=Straight(client,height,character,library,absent);if(straight==false)return false;if(straight==true){var at=Read(client,character);emit("altura",new{alvo=height,atual=Math.Round(at.Y/10,1),modo="reto"});return true;}}}

            while(clock.Elapsed.TotalSeconds<limit){var s=Read(client,character);if(absent!=null&&absent(s))return false;if(Math.Abs(s.Y/10-height)<=1.0)break;
                double tx=x+(axis==0?span*side:0),tz=z+(axis==0?0:span*side);
                NativeBridge.FlyTo(client,tx,tz,library);Wait(60);NativeBridge.FlightHeight(client,h,library);
                double sx=s.X,sz=s.Z,sy=s.Y;int fights=defenses;var leg=Stopwatch.StartNew();
                while(leg.Elapsed.TotalSeconds<5){s=Read(client,character);if(absent!=null&&absent(s))return false;if(defenses!=fights)break;if(Math.Abs(s.Y/10-height)<=1.0)break;double ex=tx-s.X,ez=tz-s.Z;if(ex*ex+ez*ez<=9)break;Wait(140);}
                if(Math.Abs(s.X-sx)+Math.Abs(s.Z-sz)<1&&Math.Abs(s.Y-sy)<1){axis^=1;span=Math.Min(span+10,70);}
                side=-side;}
            FlyHigh(client,x,z,h,character,library);
            var now=Read(client,character);emit("altura",new{alvo=height,atual=Math.Round(now.Y/10,1)});return true;
        }
        // Farm recovery: low life waits for it to come back, other blocks wait and retry; true = failure counter restarts.
        bool Recover(GameClient client,string character,Exception e,int failures){string m=e.Message??"";
            if(m.Contains("Vida abaixo")){RecoverLife(client,character);return true;}
            if(m.Contains("Mochila cheia")){emit("aguardando",new{motivo=m,segundos=60});Wait(60000);return false;}
            if(m.Contains("sem vida")){DeathRecovery(client,character);return true;}
            if(m.Contains("Personagem mudou")||m.Contains("fechado")){emit("aguardando",new{motivo=m,segundos=15});Wait(15000);return false;}
            if(failures>=MaxFailuresInARow){emit("pausa",new{motivo=failures+" falhas seguidas",segundos=30});Wait(30000);return true;}
            Wait(2000);return false;}
        const double ResurrectWaitSeconds=12;
        void DeathRecovery(GameClient client,string character){
            var clock=Stopwatch.StartNew();emit("morto",new{espera_prece=ResurrectWaitSeconds});
            while(clock.Elapsed.TotalSeconds<ResurrectWaitSeconds){Wait(500);var s=client.Read();if(s.Name!=character)throw new IOException("Personagem mudou.");if(s.Hp>0){lastHp=s.Hp;emit("ressuscitado",new{segundos=Math.Round(clock.Elapsed.TotalSeconds,1)});return;}}
            if(ReviveInTown==null){emit("aguardando",new{motivo="Personagem sem vida; reviver na cidade indisponível.",segundos=15});Wait(15000);return;}
            for(int attempt=1;attempt<=3;attempt++){string error=ReviveInTown(pid);emit("reviver_cidade",new{tentativa=attempt,erro=error});
                var t=Stopwatch.StartNew();while(t.Elapsed.TotalSeconds<10){Wait(500);var s=client.Read();if(s.Hp>0){lastHp=s.Hp;emit("na_cidade",new{x=Math.Round(s.X,1),z=Math.Round(s.Z,1),hp=s.Hp,max=s.MaxHp});Wait(3000);return;}}}
            emit("reviver_falhou",new{message="Não reviveu na cidade em 3 tentativas; tentando de novo."});Wait(5000);}
        void RecoverLife(GameClient client,string character){emit("recuperando_vida",new{alvo="70%"});var clock=Stopwatch.StartNew();double previous=-1;
            while(clock.Elapsed.TotalMinutes<5){try{var s=Raw(client,character);if(s.Hp*10L>=s.MaxHp*7L){lastHp=s.Hp;emit("vida_recuperada",new{hp=s.Hp,max=s.MaxHp});return;}
                    if(previous>=0&&s.Hp<previous&&!defending){defending=true;try{Defend(client,character);}catch(OperationCanceledException){throw;}catch(Exception){}finally{defending=false;}}previous=s.Hp;}
                catch(OperationCanceledException){throw;}catch(Exception e){if((e.Message??"").Contains("sem vida")){DeathRecovery(client,character);return;}}Wait(2000);}
            emit("vida_nao_recuperou",new{minutos=5});}
        bool RecentlyCollected(Waypoint p){lock(recent){recent.RemoveAll(r=>(DateTime.Now-r.Item4).TotalSeconds>RecentSeconds);return recent.Any(r=>r.Item1==p.ResourceId&&Math.Abs(r.Item2-p.X)<10&&Math.Abs(r.Item3-p.Z)<10);}}
        static Dictionary<int,int> Totals(InventorySnapshot bag){var totals=new Dictionary<int,int>();foreach(var item in bag.Items){int n;totals.TryGetValue(item.Id,out n);totals[item.Id]=n+item.Quantity;}return totals;}
        static bool Gained(InventorySnapshot before,InventorySnapshot after){var a=Totals(before);foreach(var kv in Totals(after)){int n;a.TryGetValue(kv.Key,out n);if(kv.Value>n)return true;}return false;}
        static bool GainedExcept(InventorySnapshot before,InventorySnapshot after,int exceptId){var a=Totals(before);foreach(var kv in Totals(after)){if(kv.Key==exceptId)continue;int n;a.TryGetValue(kv.Key,out n);if(kv.Value>n)return true;}return false;}
        // A material quest is still waiting to be confirmed (true) or there is none (false: done).
        bool MissionPending(GameClient client,string library){try{NativeBridge.InspectMaterialReward(client,library);return true;}catch(System.ComponentModel.Win32Exception e){if(e.NativeErrorCode==259)return false;return true;}catch{return true;}}
        // One point. Returns true when something was collected. Throws only for what must stop everything.
        bool Visit(GameClient client,Waypoint p,int index,int cycle,ResourceCatalog catalog,string character,string library,bool farm){
            if(farm&&RecentlyCollected(p)){emit("pulado_recente",new{point=index+1,resource=p.ResourceId});return false;}
            emit("deslocando",new{cycle=cycle,point=index+1,resource=p.ResourceId,x=Math.Round(p.X,1),z=Math.Round(p.Z,1),altura=CruiseHeight});if(!Travel(client,p,catalog,character,library)){emit("pulado_ausente",new{point=index+1,resource=p.ResourceId,visto_de_cima=true});return false;}
            var s=Read(client,character);emit("chegou",new{point=index+1,altura=Math.Round(s.Y/10,1)});
            if(p.Action!="Coletar"){Wait(p.WaitMs);return false;}
            var frame=client.ReadResources(catalog,s);
            double search=p.Area?SeedRadius:FindRadius;var target=frame.Resources.Where(r=>r.TemplateId==p.ResourceId&&r.LevelAllowed&&Math.Pow(r.X-p.X,2)+Math.Pow(r.Z-p.Z,2)<=search*search).OrderBy(r=>r.HorizontalDistance).FirstOrDefault();
            if(target==null){emit("pulado_ausente",new{point=index+1,resource=p.ResourceId,altura=Math.Round(s.Y/10,1)});return false;}
            if(Math.Pow(target.X-s.X,2)+Math.Pow(target.Z-s.Z,2)>ArriveXZ*ArriveXZ)Fly(client,target.X,target.Z,character,library);
            var reward=VerifiedRewards.Find(p.ResourceId);uint expected=LearnedMissions.Find(p.ResourceId);if(expected==0)expected=MissionFor(p.ResourceId);
            var bag=client.ReadInventory();if(bag.Items.Count>=bag.Slots)throw new IOException("Mochila cheia: libere espaço e inicie de novo.");
            FinishPendingMission(client,character,library,index);var rowsBefore=client.ReadConfirmRows();
            bag=client.ReadInventory();int temporary=reward==null?0:bag.QuantityOf(reward.TemporaryItemId);
            // A fight can interrupt the gathering: ask again (up to 3 times) while the material is still there.
            bool mined=false;for(int attempt=1;attempt<=3&&!mined;attempt++){s=Read(client,character);frame=client.ReadResources(catalog,s);if(!frame.Resources.Any(r=>r.InstanceId==target.InstanceId))break;
                NativeBridge.Collect(client,catalog,target.InstanceId,library);emit("coleta_solicitada",new{point=index+1,resource=p.ResourceId,attempt=attempt});
                var harvest=Stopwatch.StartNew();while(harvest.Elapsed.TotalSeconds<15){s=Read(client,character);var now=client.ReadInventory();if(reward!=null?now.QuantityOf(reward.TemporaryItemId)>temporary:Gained(bag,now)){mined=true;break;}frame=client.ReadResources(catalog,s);if(!frame.Resources.Any(r=>r.InstanceId==target.InstanceId)){Wait(600);mined=reward!=null?client.ReadInventory().QuantityOf(reward.TemporaryItemId)>temporary:Gained(bag,client.ReadInventory());break;}Wait(100);}}
            if(!mined){emit("coleta_falhou",new{point=index+1,resource=p.ResourceId});return false;}
            lock(recent)recent.Add(Tuple.Create(p.ResourceId,p.X,p.Z,DateTime.Now));
            emit(reward!=null?"temporario_recebido":"coletado",new{point=index+1,resource=p.ResourceId,item=reward==null?p.ItemId:reward.TemporaryItemId});
            // Material quest (0.32.0): the reward shows up as a NEW item (any id other than the temporary), which works even for
            // materials with no reward registered (e.g. 3081). Success = a new item arrived OR the material quest is gone; while
            // it is still pending, the second option is chosen again. Shorter waits so collecting a material does not drag.
            int temporaryId=reward!=null?reward.TemporaryItemId:p.ItemId;
            // 0.48.0: temporary item first, then the quest, then (a bit later) the final item — calmer waits and no fighting in between.
            missionFirst=true;try{Wait(1500);
            var before=client.ReadInventory();int baseline=reward==null?0:before.QuantityOf(reward.FinalItemId);
            for(int attempt=1;attempt<=3;attempt++){
                // 0.48.1 (log 05/11: herbs/flowers never showed a quest and each collect waited 12 s): no known quest = wait only 4 s.
                uint quest=0;var ready=Stopwatch.StartNew();
                while(ready.Elapsed.TotalSeconds<(expected==0?4:12)){Read(client,character);try{quest=NativeBridge.PrepareMaterialReward(client,library,expected).Reserved;break;}catch(System.ComponentModel.Win32Exception e){if(e.NativeErrorCode!=259&&e.NativeErrorCode!=13)throw;}Wait(250);}
                if(quest==0){if(GainedExcept(before,client.ReadInventory(),temporaryId)&&!MissionPending(client,library)){emit("recompensa_recebida",new{point=index+1,quest=0,attempt=attempt});return true;}emit("missao_nao_apareceu",new{point=index+1,resource=p.ResourceId,expected=expected,attempt=attempt});
                    // 0.51.7 (log Esdras 08/11: Sulfeto de Arsênico 3535 coletado 3×, missão 61991 nunca apareceu — material+58456 só vale
                    // para os minérios): register what the window shows; exactly one row new since before collecting = this material's quest.
                    var rows=client.ReadConfirmRows();var fresh=rows.Where(r=>!rowsBefore.Contains(r)).ToList();
                    emit("missao_janela",new{point=index+1,resource=p.ResourceId,expected=expected,linhas=rows,antes=rowsBefore});
                    if(fresh.Count==1&&fresh[0]!=expected&&!(p.ResourceId>=3074&&p.ResourceId<=3092)&&attempt<3){expected=fresh[0];LearnedMissions.Save(p.ResourceId,expected);emit("missao_aprendida",new{point=index+1,resource=p.ResourceId,quest=expected});continue;}
                    return true;}
                emit("missao_confirmada",new{point=index+1,quest=quest,attempt=attempt});Wait(1200);
                bool chosen=false;var award=Stopwatch.StartNew();while(award.Elapsed.TotalSeconds<5&&!chosen){Read(client,character);try{NativeBridge.ConfirmSecondReward(client,quest,library);chosen=true;}catch(System.ComponentModel.Win32Exception e){if(e.NativeErrorCode!=259)throw;Wait(300);}}
                if(!chosen){emit("recompensa_sem_janela",new{point=index+1,quest=quest,attempt=attempt});Wait(1000);continue;}
                emit("segunda_opcao",new{point=index+1,quest=quest,attempt=attempt});Wait(1500);
                var confirmation=Stopwatch.StartNew();DateTime again=DateTime.Now.AddSeconds(4);int repeats=0;
                while(confirmation.Elapsed.TotalSeconds<14){Read(client,character);var now=client.ReadInventory();
                    // 0.51.19 (Esdras: CheirosoWR "pega a missão, não completa e sai voando"; 5 "missao_pendente_concluida" depois): any new
                    // item (a monster drop) counted as the reward while the quest was still open; now a new item only counts once the quest
                    // is gone, the known final item still counts at once.
                    bool pending=MissionPending(client,library);
                    bool rewarded=(reward!=null&&now.QuantityOf(reward.FinalItemId)>baseline)||(!pending&&GainedExcept(before,now,temporaryId));
                    if(rewarded){emit("recompensa_recebida",new{point=index+1,quest=quest,item=reward==null?0:reward.FinalItemId,before=baseline,after=reward==null?0:now.QuantityOf(reward.FinalItemId),attempt=attempt,repeticoes=repeats});return true;}
                    if(!pending&&confirmation.Elapsed.TotalSeconds>3)break;
                    if(DateTime.Now>=again&&repeats<3&&pending){repeats++;try{NativeBridge.ConfirmSecondReward(client,quest,library);emit("segunda_opcao_repetida",new{point=index+1,quest=quest,vez=repeats});}catch(System.ComponentModel.Win32Exception e){if(e.NativeErrorCode!=259&&e.NativeErrorCode!=13&&e.NativeErrorCode!=50)throw;}again=DateTime.Now.AddSeconds(4);}
                    Wait(250);}
                if(GainedExcept(before,client.ReadInventory(),temporaryId)&&!MissionPending(client,library)){emit("recompensa_recebida",new{point=index+1,quest=quest,attempt=attempt});return true;}
                emit("missao_sem_confirmacao",new{point=index+1,quest=quest,attempt=attempt});Wait(1000);}
            // 0.51.19: still open after 3 rounds = try once more to finish it before leaving.
            FinishPendingMission(client,character,library,index);emit("recompensa_nao_confirmada",new{point=index+1,resource=p.ResourceId});return true;
            }finally{missionFirst=false;if(hitDuringMission)emit("missao_antes_da_defesa",new{point=index+1,message="Apanhou durante a missão; defendendo agora."});}
        }
        // A material quest left ready (e.g. interrupted run) is finished before collecting again, instead of stopping.
        void FinishPendingMission(GameClient client,string character,string library,int index){
            try{NativeBridge.InspectMaterialReward(client,library);}catch(System.ComponentModel.Win32Exception e){if(e.NativeErrorCode==259)return;throw;}
            try{uint quest=NativeBridge.PrepareMaterialReward(client,library,0).Reserved;if(quest==0)return;NativeBridge.ConfirmSecondReward(client,quest,library);emit("missao_pendente_concluida",new{point=index+1,quest=quest});Wait(1500);}catch(System.ComponentModel.Win32Exception e){emit("missao_pendente_falhou",new{code=e.NativeErrorCode});}
        }
        string bridgeLibrary;
        void Run(int pid,RouteDocument route,Func<RouteDocument> plan,ResourceCatalog catalog,string library){bridgeLibrary=library;
            try{using(var log=new StreamWriter(LogPath,false)){log.AutoFlush=true;var json=new JavaScriptSerializer();
                emit=(phase,data)=>{string line=json.Serialize(new{utc=DateTime.UtcNow.ToString("o"),phase=phase,data=data});log.WriteLine(line);var handler=Message;if(handler!=null)handler(phase+" · "+json.Serialize(data));};
                try{using(var client=new GameClient(pid,new Dictionary<string,bool>())){
                    var initial=client.Read();string character=initial.Name;if(route!=null&&!String.IsNullOrEmpty(route.Character)&&route.Character!=character)throw new IOException("Rota associada a outro personagem.");Read(client,character);
                    bool farm=plan!=null;emit("iniciada",new{mode=farm?"farm":"rota",name=route==null?"Farm automático":route.Name,character=character,points=route==null?0:route.Points.Count,loop=farm||route.Loop});
                    int cycle=0,failures=0,total=0;
                    // Farm (0.30.0, owner: "never stops unless Parar"): every failure is logged, waited out and retried.
                    do{cycle++;
                        try{
                        if(farm){route=plan();if(route==null||route.Points.Count==0){emit("sem_pontos",new{cycle=cycle});Wait(10000);continue;}emit("ciclo",new{cycle=cycle,points=route.Points.Count});}
                        int collected=0;
                        for(int index=0;index<route.Points.Count;index++){var p=route.Points[index];
                            try{Read(client,character);
                                if(p.Action=="Coletar"&&!route.ResourceIds.Contains(p.ResourceId)){emit("pulado_filtro",new{point=index+1,resource=p.ResourceId});continue;}
                                if(Visit(client,p,index,cycle,catalog,character,library,farm)){collected++;total++;}failures=0;}
                            catch(OperationCanceledException){throw;}
                            catch(Exception e){if(!farm||!(e is TimeoutException||e is IOException||e is System.ComponentModel.Win32Exception||e is InvalidOperationException||e is InvalidDataException))throw;failures++;emit("ponto_falhou",new{point=index+1,message=e.Message,seguidas=failures});if(Recover(client,character,e,failures))failures=0;}
                        }
                        emit("volta_concluida",new{cycle=cycle,collected=collected,total=total});if(farm||route.Loop)Wait(farm?2000:5000);
                        }catch(OperationCanceledException){throw;}
                        catch(Exception e){if(!farm)throw;failures++;emit("falha_recuperada",new{message=e.Message,seguidas=failures});if(Recover(client,character,e,failures))failures=0;}
                    }while((farm||route.Loop)&&!stop);
                    emit("encerrada",new{reason=stop?"Solicitado pelo usuário":"Fim da rota",total=total});
                }}catch(OperationCanceledException){emit("parada",new{reason="Solicitado pelo usuário; comando nativo já enviado pode terminar no jogo"});}catch(Exception e){LastError=e.Message;emit("erro",new{type=e.GetType().Name,message=e.Message});}
            }}catch(Exception e){LastError=e.Message;var handler=Message;if(handler!=null)handler("Falha no log: "+e.Message);}finally{Running=false;}
        }
        // Confirmed in game: 3076→61532, 3090→61546, 3091→61547 — the 3074..3092 mining family follows material + 58456.
        // 0.48.2 (owner had to take the Verme Bastão 3538 quest by hand): the window only accepts a row with the exact quest id and the
        // herbs had none. Assumed the same numbering for every ground material (3074–3103 and herbs/flowers 3535–3553 → +58456);
        // confirmed so far for 3076, 3077, 3081, 3082, 3086, 3090, 3091. A wrong guess just finds no row (as before).
        public static uint MissionFor(int resource){return resource>=3074&&resource<=3103||resource>=3535&&resource<=3553?(uint)(resource+58456):0u;}
    }
    // 0.51.7: material → quest learned in game (missao_aprendida), kept in %LOCALAPPDATA%\AvalonInterno\Rota\missoes-aprendidas.txt.
    static class LearnedMissions {
        static readonly object gate=new object();static Dictionary<int,uint> map;
        static string File_{get{return Path.Combine(RotaPaths.Root,"missoes-aprendidas.txt");}}
        static void Load(){if(map!=null)return;map=new Dictionary<int,uint>();try{if(File.Exists(File_))foreach(var line in File.ReadAllLines(File_)){var f=line.Split('=');int r;uint q;if(f.Length==2&&int.TryParse(f[0].Trim(),out r)&&uint.TryParse(f[1].Trim(),out q)&&q!=0)map[r]=q;}}catch{}}
        public static uint Find(int resource){lock(gate){Load();uint q;return map.TryGetValue(resource,out q)?q:0u;}}
        public static void Save(int resource,uint quest){lock(gate){Load();map[resource]=quest;try{Directory.CreateDirectory(RotaPaths.Root);File.WriteAllLines(File_,map.OrderBy(k=>k.Key).Select(k=>k.Key+"="+k.Value).ToArray());}catch{}}}
    }
}
