using System;

// Read-only decision planner. It never touches the game process.
// The driver reports observed player positions and executes only explicit
// short ground waypoints; a few metres of movement are NOT proof of escape.
enum AutoQuestDetourKind { Issue, Wait, SegmentReached, SegmentBlocked, Cleared, Abort }
sealed class AutoQuestDetourUpdate {
 public AutoQuestDetourKind Kind;
 public double X,Z;
 public int Attempt;
 public string Direction,Details;
 public static AutoQuestDetourUpdate Of(AutoQuestDetourKind kind,string details){
  return new AutoQuestDetourUpdate{Kind=kind,Details=details};
 }
}

sealed class AutoQuestAdaptiveDetour {
 public const int MaxCommands=16,MaxFailures=6,MaxSeconds=110;
 public const int NoGainSeconds=8,LegTimeoutSeconds=18;
 public const double SideDistance=28,ForwardDistance=32,BackDistance=25;
 public const double ArrivalRadius=8,MinMovement=16;
 public const double ClearanceFromWall=48,RequiredGoalGain=25;

 readonly double blockedX,blockedZ,goalX,goalZ,forwardX,forwardZ;
 readonly double initialGoalDistance;
 readonly TimeSpan begun;
 int direction=1,sideMoves,forwardMoves,issuedCount,failures;
 bool triedLeft,triedRight,issued,finished,failed;
 string mode="LATERAL",issuedMode,issuedDirection;
 double fromX,fromZ,stepX,stepZ,bestRemaining;
 TimeSpan sentAt,lastGainAt;
 public int IssuedCount{get{return issuedCount;}}
 public int Failures{get{return failures;}}
 public bool Active{get{return !finished&&!failed;}}
 public double BlockedX{get{return blockedX;}}
 public double BlockedZ{get{return blockedZ;}}
 static double Dist(double x,double z,double a,double b){
  double dx=x-a,dz=z-b;return Math.Sqrt(dx*dx+dz*dz);
 }
 public AutoQuestAdaptiveDetour(double x,double z,double goalWaypointX,double goalWaypointZ,
  double finalX,double finalZ,TimeSpan current){
  if(!AutoQuestBetaSession.ValidDestination(goalWaypointX,goalWaypointZ)||
   !AutoQuestBetaSession.ValidDestination(finalX,finalZ)||
   !RotaAvalon.GameClient.ValidCoordinate(x)||!RotaAvalon.GameClient.ValidCoordinate(z)||
   current<TimeSpan.Zero)throw new ArgumentException("Coordenadas ou relógio inválidos para o desvio.");
  double dx=finalX-x,dz=finalZ-z;
  double d=Math.Sqrt(dx*dx+dz*dz);
  if(d<15)throw new ArgumentException("Destino perto demais para iniciar busca lateral.");
  blockedX=x;blockedZ=z;goalX=finalX;goalZ=finalZ;
  forwardX=dx/d;forwardZ=dz/d;initialGoalDistance=d;begun=current;
 }
 AutoQuestDetourUpdate Abort(string reason){
  failed=true;
  return AutoQuestDetourUpdate.Of(AutoQuestDetourKind.Abort,
   reason+"; comandos="+issuedCount+"/"+MaxCommands+"; falhas="+failures+"/"+MaxFailures);
 }
 public AutoQuestDetourUpdate Poll(double x,double z,TimeSpan now){
  if(failed)return Abort("Desvio já interrompido.");
  if(finished)return AutoQuestDetourUpdate.Of(AutoQuestDetourKind.Cleared,"Corredor de desvio concluído.");
  if(!RotaAvalon.GameClient.ValidCoordinate(x)||!RotaAvalon.GameClient.ValidCoordinate(z)||
   now<begun) return Abort("Posição ou tempo inválidos.");
  if(now-begun>=TimeSpan.FromSeconds(MaxSeconds))
   return Abort("Limite de 110 segundos tentando contornar o obstáculo.");
  if(issued){
   double distance=Dist(x,z,stepX,stepZ);
   if(distance<=ArrivalRadius&&Dist(x,z,fromX,fromZ)>=MinMovement){
    issued=false;
    if(issuedMode=="LATERAL"){
     sideMoves++;triedLeft=triedRight=false;
     if(sideMoves>=2){mode="AVANCAR";forwardMoves=0;}
    }else if(issuedMode=="RECUAR"){
     mode="LATERAL";sideMoves=0;forwardMoves=0;
     triedLeft=triedRight=false;
    }else{
     forwardMoves++;
     double gain=initialGoalDistance-Dist(x,z,goalX,goalZ);
     double clearance=Dist(x,z,blockedX,blockedZ);
     // Only release the original goal after two FULL forward steps away from
     // the obstacle, with confirmed net improvement toward the final goal.
     if(forwardMoves>=2&&gain>=RequiredGoalGain&&clearance>=ClearanceFromWall){
      finished=true;
      return AutoQuestDetourUpdate.Of(AutoQuestDetourKind.Cleared,
       "Dois passos de avanço confirmados, distância ao destino reduzida "+gain.ToString("0.0")+
       " e afastamento do bloqueio "+clearance.ToString("0.0")+
       ". Liberado para recalcular caminho principal.");
     }
     if(forwardMoves>=2){
      mode="LATERAL";sideMoves=0;forwardMoves=0;
     }
    }
    return AutoQuestDetourUpdate.Of(AutoQuestDetourKind.SegmentReached,
     "Etapa "+issuedCount+" "+issuedDirection+" alcançada; não retomar destino principal ainda"+
     "; deslocamento observado dX="+(x-fromX).ToString("0.0")+
     " dZ="+(z-fromZ).ToString("0.0")+" total="+
      Dist(x,z,fromX,fromZ).ToString("0.0")+
     "; distância real do bloqueio="+Dist(x,z,blockedX,blockedZ).ToString("0.0"));
   }
   if(distance<bestRemaining-6.0){bestRemaining=distance;lastGainAt=now;}
   if(now-lastGainAt>=TimeSpan.FromSeconds(NoGainSeconds)||
      now-sentAt>=TimeSpan.FromSeconds(LegTimeoutSeconds)){
    issued=false;failures++;
    string kind=issuedDirection;
    if(failures>=MaxFailures)return Abort("Falhas repetidas em desvios curtos; nenhum caminho confirmado.");
    if(issuedMode=="LATERAL"){
     if(direction==1)triedLeft=true;else triedRight=true;
     if(triedLeft&&triedRight){
      mode="RECUAR";triedLeft=triedRight=false;
     }else{direction=-direction;mode="LATERAL";}
     sideMoves=0;forwardMoves=0;
    }else if(issuedMode=="AVANCAR"){
     // Forward hit a wall: keep following the SAME flank for more distance,
     // rather than immediately retrying the blocked original waypoint.
     mode="LATERAL";sideMoves=0;forwardMoves=0;
    }else{
     mode="LATERAL";sideMoves=0;forwardMoves=0;direction=-direction;
    }
    return AutoQuestDetourUpdate.Of(AutoQuestDetourKind.SegmentBlocked,
     "Etapa "+issuedCount+" "+kind+" bloqueada; aprox. restantes="+distance.ToString("0.0")+
     "; deslocamento observado dX="+(x-fromX).ToString("0.0")+
     " dZ="+(z-fromZ).ToString("0.0")+" total="+
      Dist(x,z,fromX,fromZ).ToString("0.0")+
     "; próximo modo="+mode+"; falhas="+failures);
   }
   return AutoQuestDetourUpdate.Of(AutoQuestDetourKind.Wait,"Aguardando avanço real no desvio.");
  }
  if(issuedCount>=MaxCommands)
   return Abort("Limite de "+MaxCommands+" comandos de desvio atingido.");
  double destX,destZ;
  string label;
  if(mode=="LATERAL"){
   // Perpendicular to current goal vector, not fixed world east/west.
   // A side which cannot be traversed is rejected and the other is probed.
   destX=x-direction*forwardZ*SideDistance;
   destZ=z+direction*forwardX*SideDistance;
   label=direction==1?"ESQUERDA":"DIREITA";
  }else if(mode=="RECUAR"){
   destX=x-forwardX*BackDistance;destZ=z-forwardZ*BackDistance;
   label="RECUAR PARA REAVALIAR";
  }else{
   destX=x+forwardX*ForwardDistance;destZ=z+forwardZ*ForwardDistance;
   label="AVANÇAR PELO DESVIO";
  }
  if(!RotaAvalon.GameClient.ValidCoordinate(destX)||
     !RotaAvalon.GameClient.ValidCoordinate(destZ))
   return Abort("Destino de manobra fora do mapa válido.");
  fromX=x;fromZ=z;stepX=destX;stepZ=destZ;bestRemaining=Dist(x,z,destX,destZ);
  sentAt=lastGainAt=now;issued=true;issuedCount++;
  issuedMode=mode;issuedDirection=label;
  return new AutoQuestDetourUpdate{Kind=AutoQuestDetourKind.Issue,
   X=destX,Z=destZ,Attempt=issuedCount,Direction=label,
   Details="etapa "+issuedCount+"/"+MaxCommands+"; lado="+label+
    "; waypoint="+destX.ToString("0.0")+","+destZ.ToString("0.0")+
    "; afastamento do bloqueio="+Dist(x,z,blockedX,blockedZ).ToString("0.0")};
 }
}
