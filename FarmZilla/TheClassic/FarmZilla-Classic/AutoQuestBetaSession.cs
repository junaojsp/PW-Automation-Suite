using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// 0.6.6: FIRST OPERATOR-GUIDED, ACTUATING BETA. This is NOT a decoded Auto Quest.
// The operator explicitly supplies the objective X/Z and (optionally) a raw task ID.
// Only movement and optional operator-requested F12 are allowed. NPC packets, collection,
// inferred task completion and automatic rewards are deliberately prohibited.
sealed class AutoQuestBetaSession : IDisposable {
 public const int MaxMinutes=15;
 public const double MaxStepGround=110,MaxStepFlight=280,Arrival=14;
 public static bool ValidDestination(double x,double z){
  return RotaAvalon.GameClient.ValidCoordinate(x)&&RotaAvalon.GameClient.ValidCoordinate(z)&&
   Math.Abs(x)>0.001&&Math.Abs(z)>0.001;
 }
 public static double Step(double current,double target,double otherDelta,double maxLeg){
  double dx=target-current,distance=Math.Sqrt(dx*dx+otherDelta*otherDelta);
  return current+dx*Math.Min(1,maxLeg/Math.Max(distance,0.001));
 }
 public event Action<string> Message;
 public event Action Finished;
 public readonly string FilePath;
 public bool Running{get{return job!=null&&!job.IsCompleted;}}
 public int Phase{get{return phase;}} // 0: outbound; 1: at objective; 2: returning; 3: at NPC; 4: ended
 readonly int pid;readonly uint characterId;readonly string characterName;
 readonly double goalX,goalZ;readonly ushort internalTaskId;readonly bool allowFlight;
 readonly CancellationTokenSource cancel=new CancellationTokenSource();
 readonly object fileGate=new object();
 Task job;volatile int phase,returnRequested,combatRequested;
 int lines;bool disposed;
 public AutoQuestBetaSession(int processId,uint id,string name,double x,double z,ushort watchId,bool tryFlight){
  if(processId<=0||id==0||String.IsNullOrWhiteSpace(name)||!ValidDestination(x,z))
   throw new ArgumentException("Identidade ou destino do teste inválido.");
  pid=processId;characterId=id;characterName=name;goalX=x;goalZ=z;
  internalTaskId=watchId;allowFlight=tryFlight;
  var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
   "FarmZillaClassic","diagnosticos");
  Directory.CreateDirectory(folder);
  FilePath=Path.Combine(folder,"autoquest-beta-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+pid+".txt");
 }
 void Log(string eventName,string details){
  string line=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+" | "+eventName+"; "+details;
  lock(fileGate)if(lines<25000){File.AppendAllText(FilePath,line+Environment.NewLine,Encoding.UTF8);lines++;}
  var handler=Message;if(handler!=null)try{handler(line);}catch{}
 }
 public void Start(){
  if(job!=null)throw new InvalidOperationException("Teste já iniciado.");
  Log("BETA INICIO","versão="+ReleaseInfo.Version+"; personagem="+characterName+
   "; PID="+pid+"; id="+characterId+"; destino explícito X="+goalX.ToString("0.0")+
   " Z="+goalZ.ToString("0.0")+"; id interno monitorado="+internalTaskId+
   "; tentar voo="+allowFlight+"; máximo="+MaxMinutes+" min");
  Log("REGRAS","Sem identificação de objetivo/mob, sem aceite/entrega automática, sem dedução de missão concluída a partir de bytes.");
  job=Task.Run(()=>Run(cancel.Token));
 }
 public void RequestReturn(){
  if(!Running)return;
  Interlocked.Exchange(ref returnRequested,1);
  Log("SOLICITAÇÃO","Retornar ao ponto inicial (NPC deve ser acionado manualmente).");
 }
 public void RequestCombat(){
  if(!Running)return;
  Interlocked.Exchange(ref combatRequested,1);
  Log("SOLICITAÇÃO","Ativar F12 apenas ao chegar no destino, no solo e com ID interno monitorado ativo.");
 }
 public async Task StopAsync(){
  cancel.Cancel();
  var current=job;if(current!=null)await current;
 }
 sealed class Sample {
  public double X,Y,Z;public int Hp,MaxHp;public bool FlightKnown,IsFlying;
  public string Activity;
  public uint Npc,Target;public byte NpcBusy;
  public List<AutoQuestRawTaskRecord> Records;
 }
 Sample Read(){
  var sample=new Sample();
  using(var m=new CharacterMemory(pid)){
   if(m.Id!=characterId||m.Name!=characterName)throw new IOException("Personagem foi alterado durante o teste.");
   sample.Records=m.ReadAutoQuestRawTaskRecords();
   try{sample.Npc=m.U(m.Host+ClassicLayout.HostNpcTalk);}catch{}
   try{sample.Target=m.U(m.Host+ClassicLayout.HostTarget);}catch{}
   try{sample.NpcBusy=m.Read(m.Host+ClassicLayout.HostNpcBusy,1)[0];}catch{}
  }
  using(var client=new RotaAvalon.GameClient(pid,new Dictionary<string,bool>())){
   var s=client.Read();if(s.Name!=characterName)throw new IOException("Nome do personagem mudou.");
   sample.X=s.X;sample.Y=s.Y;sample.Z=s.Z;sample.Hp=s.Hp;sample.MaxHp=s.MaxHp;
   sample.FlightKnown=s.FlightStateKnown;sample.IsFlying=s.IsFlying;
   try{sample.Activity=client.ReadActivity().Kind.ToString();}catch{sample.Activity="Indisponivel";}
  }
  if(sample.Hp<=0)throw new IOException("Personagem sem vida.");
  return sample;
 }
 static double Distance(double x,double z,double tx,double tz){
  double dx=x-tx,dz=z-tz;return Math.Sqrt(dx*dx+dz*dz);
 }
 static string Position(Sample s){
  return "XYZ="+s.X.ToString("0.0")+","+s.Y.ToString("0.0")+","+s.Z.ToString("0.0")+
   "; HP="+s.Hp+"/"+s.MaxHp+"; voo="+(s.FlightKnown?(s.IsFlying?"sim":"não"):"desconhecido")+
   "; ação="+s.Activity+"; alvo=0x"+s.Target.ToString("X8");
 }
 void Observe(Sample before,Sample now){
  if(before==null){
   Log("AMOSTRA INICIAL",Position(now)+"; IDs internos="+String.Join(",",
    now.Records.Select(x=>x.Id).Take(150)));
   if(internalTaskId!=0){
    var item=now.Records.FirstOrDefault(x=>x.Id==internalTaskId);
    Log("MONITOR","ID "+internalTaskId+(item==null?" AUSENTE":(" presente; bytes32="+item.Hex)));
   }
   return;
  }
  if(before.Npc!=now.Npc||before.NpcBusy!=now.NpcBusy)
   Log("INTERAÇÃO NPC","contexto 0x"+before.Npc.ToString("X8")+" -> 0x"+now.Npc.ToString("X8")+
    "; ocupado="+before.NpcBusy+" -> "+now.NpcBusy+"; "+Position(now));
  if(before.IsFlying!=now.IsFlying||before.FlightKnown!=now.FlightKnown)
   Log("VOO","mudança; "+Position(now));
  if(before.Activity!=now.Activity)Log("AÇÃO",before.Activity+" -> "+now.Activity+"; "+Position(now));
  var changed=AutoQuestRawTaskDiff.Changes(before.Records,now.Records);
  foreach(var entry in changed.Take(32))
   Log("REGISTRO BRUTO",entry.Describe()+"; "+Position(now));
  if(changed.Count>32)Log("REGISTRO BRUTO","mais "+(changed.Count-32)+" alterações na amostra");
 }
 // This helper is offline-testable; it does not fabricate stages or objectives.
 public static bool CanCombat(bool atDestination,bool flightKnown,bool flying,
  string activity,ushort monitoredId,bool recordPresent){
  return atDestination&&flightKnown&&!flying&&activity=="Idle"&&
   monitoredId!=0&&recordPresent;
 }
 void Run(CancellationToken token){
  var clock=Stopwatch.StartNew();AutoQuestNativeCommands commands=null;
  bool ownsF12=false,ownsMovement=false;string conclusion="encerrado";
  double originX=0,originZ=0,legX=0,legZ=0,bestToLeg=0;
  double recoverX=0,recoverZ=0,recoverStartX=0,recoverStartZ=0;
  bool legGuided=false,recovering=false,pendingRecovery=false;
  int recoveryAttempts=0;
  TimeSpan? legSince=null,lastGainAt=null,recoverySentAt=null,landingSince=null,lastDescent=null;
  AutoQuestGroundCorridor learnedWalk=null;
  Sample previous=null;
  var policy=new AutoQuestTravelPolicy();
  string lastDecision="";int readErrors=0;long lastPulseMs=-10000;
  try{
   var first=Read();
   originX=first.X;originZ=first.Z;
   if(Distance(originX,originZ,goalX,goalZ)<20)
    throw new IOException("Destino já está próximo do personagem; escolha um ponto de teste distante.");
   if(internalTaskId!=0&&!first.Records.Any(x=>x.Id==internalTaskId))
    throw new IOException("ID interno "+internalTaskId+" não está ativo; limpe o campo para testar só a rota.");
   // The previous 0.6.5 walk is evidence of the correct city exit.
   learnedWalk=AutoQuestGroundCorridor.For(characterName,originX,originZ,goalX,goalZ);
   if(learnedWalk!=null)
    Log("ROTA APRENDIDA","Usando "+AutoQuestGroundCorridor.Count+
     " pontos do log 0.6.5 via saída OESTE da cidade; retorno pelo mesmo corredor.");
   else Log("ROTA NÃO MAPEADA",
    "Destino sem trilha terrestre validada. Trechos diretos monitorados; "+
    "destravamento limitado a 3 manobras antes de interromper.");
   commands=new AutoQuestNativeCommands(pid,characterId,characterName);
   Log("ORIGEM",Position(first)+"; retorno gravado: "+originX.ToString("0.0")+","+originZ.ToString("0.0"));
   previous=first;Observe(null,first);
   while(!token.IsCancellationRequested&&clock.Elapsed<TimeSpan.FromMinutes(MaxMinutes)){
    Sample s;
    try{s=Read();readErrors=0;}catch(Exception ex){
     if(++readErrors==1||readErrors==3)Log("ERRO LEITURA",ex.Message+"; tentativas="+readErrors);
     if(readErrors>=3)throw new IOException("Leituras repetidamente indisponíveis: "+ex.Message);
     if(token.WaitHandle.WaitOne(1200))break;continue;
    }
    Observe(previous,s);previous=s;
    if(clock.ElapsedMilliseconds-lastPulseMs>=5000){
     Log("PULSO","fase="+phase+"; "+Position(s)+"; destino="+
      (phase==2?originX.ToString("0.0")+","+originZ.ToString("0.0"):goalX.ToString("0.0")+","+goalZ.ToString("0.0")));
     lastPulseMs=clock.ElapsedMilliseconds;
    }
    if(internalTaskId!=0&&!s.Records.Any(x=>x.Id==internalTaskId)&&phase<=2){
     throw new IOException("ID interno monitorado "+internalTaskId+" desapareceu. O teste foi interrompido; isso não comprova entrega.");
    }
    if(Interlocked.CompareExchange(ref returnRequested,0,0)!=0&&phase<2){
     if(ownsF12){commands.Cultivate(false);ownsF12=false;Log("F12","Desligado antes de voltar");}
     if(ownsMovement){commands.Halt();ownsMovement=false;Log("NAVEGAÇÃO","Comando de parada antes de retornar");}
     phase=2;legSince=null;lastGainAt=null;recovering=false;pendingRecovery=false;landingSince=null;
     Log("FASE","Retornando à origem registrada pelo teste; entrega manual no NPC");
    }
    if(phase==0||phase==2){
     double tx=phase==0?goalX:originX,tz=phase==0?goalZ:originZ;
     double dist=Distance(s.X,s.Z,tx,tz);
     if(dist<=Arrival){
      if(!s.FlightKnown){
       if(token.WaitHandle.WaitOne(1100))break;continue;
      }
      if(s.IsFlying){
       if(!landingSince.HasValue)landingSince=clock.Elapsed;
       if(clock.Elapsed-landingSince.Value>TimeSpan.FromSeconds(50))
        throw new IOException("Pouso não confirmado após 50 segundos.");
       if(!lastDescent.HasValue||clock.Elapsed-lastDescent.Value>=TimeSpan.FromSeconds(3)){
        commands.Descend();ownsMovement=true;lastDescent=clock.Elapsed;
        Log("COMANDO","DESCER; "+Position(s));
       }
       if(token.WaitHandle.WaitOne(1100))break;continue;
      }
      // "not flying" may still be falling; don't start F12 during FlightTransition.
      if(s.Activity=="FlightTransition"){
       if(!landingSince.HasValue)landingSince=clock.Elapsed;
       if(clock.Elapsed-landingSince.Value>TimeSpan.FromSeconds(50))
        throw new IOException("Transição de pouso não terminou em 50 segundos.");
       if(token.WaitHandle.WaitOne(1100))break;continue;
      }
      if(ownsMovement){commands.Halt();ownsMovement=false;Log("COMANDO","PARAR AO CHEGAR; "+Position(s));}
      phase=phase==0?1:3;legSince=null;
      Log("FASE",phase==1?"Região alcançada. F12 só será acionado pelo operador.":
       "Retorno concluído; conversar/entregar no NPC manualmente.");
     }else{
      var actor=new AutoQuestActor{X=s.X,Y=s.Y,Z=s.Z,Hp=s.Hp,
       CharacterVerified=true,ExclusiveLock=true,FlightKnown=s.FlightKnown,IsFlying=s.IsFlying};
      var plan=new AutoQuestDecision{Move=phase==0?AutoQuestMove.MoveToRegion:AutoQuestMove.MoveToNpc,
       QuestIds=new ushort[]{internalTaskId!=0?internalTaskId:(ushort)65535},X=tx,Y=s.Y,Z=tz};
      var order=policy.Select(actor,plan,clock.Elapsed,allowFlight?(bool?)null:false);
      if(order.Command==AutoQuestTravelCommand.Pause)throw new IOException(order.Reason);
      if(order.Command==AutoQuestTravelCommand.TryTakeoff){
       try{
        commands.Takeoff(tx,tz);ownsMovement=true;legSince=null;lastGainAt=null;
        Log("COMANDO","TENTAR DECOLAR UMA VEZ; "+Position(s));
       }catch(Exception ex){
        // A missing flight item may cause the native client to reject takeoff.
        // Do NOT retry: the travel policy will choose ground after its six-second timeout.
        Log("FALLBACK VOO","Decolagem não aceita: "+ex.Message+
         "; aguardando fallback automático para caminho pelo chão.");
       }
      }else if(order.Command==AutoQuestTravelCommand.GroundAutoPath||
       order.Command==AutoQuestTravelCommand.FlyToTarget){
       if(recovering){
        if(order.Command!=AutoQuestTravelCommand.GroundAutoPath)
         throw new IOException("Modo de viagem mudou durante o destravamento; interromper por segurança.");
        if(AutoQuestUnstuck.Moved(s.X,s.Z,recoverStartX,recoverStartZ)){
         commands.Halt();
         Log("DESTRAVAMENTO","Movimento confirmado após manobra "+recoveryAttempts+
          "; refazendo ponto da rota; "+Position(s));
         recovering=false;recoverySentAt=null;legSince=null;lastGainAt=null;
        }else if(clock.Elapsed-recoverySentAt.Value>=
          TimeSpan.FromSeconds(AutoQuestUnstuck.ManeuverTimeoutSeconds)){
         commands.Halt();
         Log("DESTRAVAMENTO","Manobra "+recoveryAttempts+
          " sem movimento suficiente após 5 segundos; "+Position(s));
         recovering=false;recoverySentAt=null;
         if(recoveryAttempts>=AutoQuestUnstuck.MaxAttempts)
          throw new IOException("Três tentativas de destravamento sem resultado; Auto Quest parado.");
         pendingRecovery=true;
        }
       }
       if(legSince.HasValue&&!recovering){
        double remaining=Distance(s.X,s.Z,legX,legZ);
        double threshold=legGuided?AutoQuestGroundCorridor.WaypointReach:Arrival;
        if(remaining<=threshold){legSince=null;lastGainAt=null;}
        else{
         // Position changed isn't enough: require progress toward the waypoint.
         if(remaining<bestToLeg-1.5){bestToLeg=remaining;lastGainAt=clock.Elapsed;}
         TimeSpan noGain=clock.Elapsed-(lastGainAt??legSince.Value);
         if(noGain>=TimeSpan.FromSeconds(AutoQuestUnstuck.NoGainSeconds)||
            clock.Elapsed-legSince.Value>=TimeSpan.FromSeconds(60)){
          Log("OBSTACULO DETECTADO","sem aproximação ao waypoint por "+
           noGain.TotalSeconds.ToString("0")+" s; waypoint="+
           legX.ToString("0.0")+","+legZ.ToString("0.0")+
           "; distância="+remaining.ToString("0.0")+
           "; modo="+(legGuided?"ROTA APRENDIDA":"direto")+
           "; "+Position(s));
          commands.Halt();ownsMovement=false;
          recoverX=legX;recoverZ=legZ;
          legSince=null;lastGainAt=null;
          if(order.Command==AutoQuestTravelCommand.FlyToTarget)
           throw new IOException("Voo sem progresso confirmado; sessão pausada sem desvio não validado.");
          if(recoveryAttempts>=AutoQuestUnstuck.MaxAttempts)
           throw new IOException("Limite de três tentativas de destravamento atingido.");
          pendingRecovery=true;
         }
        }
       }
       if(pendingRecovery){
        if(!s.FlightKnown||s.IsFlying)
         throw new IOException("Destravamento por terra exige personagem no solo.");
        var escape=AutoQuestUnstuck.Candidate(s.X,s.Z,recoverX,recoverZ,
         recoveryAttempts+1);
        recoveryAttempts++;
        commands.GroundPath(escape.X,s.Y,escape.Z);
        ownsMovement=true;
        recoverStartX=s.X;recoverStartZ=s.Z;recoverySentAt=clock.Elapsed;
        recovering=true;pendingRecovery=false;
        Log("DESTRAVAMENTO","tentativa="+recoveryAttempts+"/"+AutoQuestUnstuck.MaxAttempts+
         "; método="+(recoveryAttempts==1?"RECUAR":recoveryAttempts==2?"ESQUERDA":"DIREITA")+
         "; alvo="+escape.X.ToString("0.0")+","+escape.Z.ToString("0.0")+
         "; "+Position(s));
       }else if(!legSince.HasValue&&!recovering){
        var size=order.Command==AutoQuestTravelCommand.FlyToTarget?MaxStepFlight:MaxStepGround;
        double targetX=tx,targetZ=tz;
        int waypoint=-1;bool guided=false;
        if(order.Command==AutoQuestTravelCommand.GroundAutoPath&&learnedWalk!=null){
         var point=learnedWalk.Next(s.X,s.Z,phase==2);
         targetX=point.X;targetZ=point.Z;waypoint=point.Index;guided=true;
        }
        double x=Step(s.X,targetX,targetZ-s.Z,size),z=Step(s.Z,targetZ,targetX-s.X,size);
        if(order.Command==AutoQuestTravelCommand.FlyToTarget)commands.FlightPath(x,z);
        else commands.GroundPath(x,s.Y,z);
        ownsMovement=true;legSince=clock.Elapsed;legGuided=guided;
        legX=x;legZ=z;bestToLeg=Distance(s.X,s.Z,x,z);lastGainAt=clock.Elapsed;
        Log("COMANDO",order.Command+"; trecho="+x.ToString("0.0")+","+z.ToString("0.0")+
         "; waypoint="+waypoint+"; modo="+(guided?"ROTA APRENDIDA VIA OESTE":"direto")+
         "; restante até objetivo="+dist.ToString("0.0")+"; "+Position(s));
       }
      }else if(order.Command.ToString()!=lastDecision){
       Log("PLANEJADOR",order.Command+"; "+order.Reason);
      }
      lastDecision=order.Command.ToString();
     }
    }
    if(phase==1&&Interlocked.Exchange(ref combatRequested,0)!=0){
     bool present=internalTaskId!=0&&s.Records.Any(x=>x.Id==internalTaskId);
     if(ownsF12){Log("F12","Já ligado neste teste; comando não repetido.");}
     else if(CanCombat(true,s.FlightKnown,s.IsFlying,s.Activity,internalTaskId,present)){
      commands.Cultivate(true);ownsF12=true;Log("COMANDO","F12 LIGADO por solicitação explícita; monitor ID "+internalTaskId);
     }else Log("F12 BLOQUEADO","É necessário estar no solo, sem transição de voo e com o ID monitorado ativo.");
    }
    if(phase==3&&internalTaskId!=0&&!s.Records.Any(x=>x.Id==internalTaskId)){
     Log("REGISTRO","ID "+internalTaskId+" removido perto da origem; entrega/recompensa não comprovadas.");
     conclusion="registro removido após retorno; verificar no jogo";break;
    }
    if(token.WaitHandle.WaitOne(1200))break;
   }
   if(token.IsCancellationRequested)conclusion="interrompido pelo usuário";
   else if(clock.Elapsed>=TimeSpan.FromMinutes(MaxMinutes))conclusion="limite de 15 minutos";
  }catch(Exception ex){
   conclusion="ERRO: "+ex.Message;Log("ERRO FATAL",ex.GetType().Name+": "+ex.Message);
  }finally{
   if(commands!=null){
    if(ownsF12)try{commands.Cultivate(false);Log("LIMPEZA","F12 desligado.");}catch(Exception ex){Log("ERRO LIMPEZA","Desligar F12: "+ex.Message);}
    if(ownsMovement)try{commands.Halt();Log("LIMPEZA","Navegação interrompida.");}catch(Exception ex){Log("ERRO LIMPEZA","Parar navegação: "+ex.Message);}
    commands.Dispose();
   }
   phase=4;
   Log("BETA FIM","resultado="+conclusion+"; duração="+clock.Elapsed.TotalSeconds.ToString("0")+" s; linhas="+lines);
   var done=Finished;if(done!=null)try{done();}catch{}
  }
 }
 public void Dispose(){if(disposed)return;disposed=true;cancel.Cancel();cancel.Dispose();}
}
