using System;

// Short, bounded recovery inspired by RotaExecution.Escape (back, left, right).
// Do not use this to infer safe paths or run forever against an unknown wall.
static class AutoQuestUnstuck {
 public const int MaxAttempts=3;
 public const int NoGainSeconds=8;
 public const int ManeuverTimeoutSeconds=5;
 public const double ManeuverDistance=22.0;
 public const double MovementThreshold=6.0;
 public static AutoQuestGroundPoint Candidate(double x,double z,double targetX,double targetZ,int attempt) {
  if(attempt<1||attempt>MaxAttempts)throw new ArgumentOutOfRangeException("attempt");
  double dx=targetX-x,dz=targetZ-z;
  double len=Math.Sqrt(dx*dx+dz*dz);
  if(!AutoQuestBetaSession.ValidDestination(targetX,targetZ)||len<1.0)
   throw new ArgumentException("Destino inválido para tentativa de destravar.");
  double ux=dx/len,uz=dz/len,ex,ez;
  if(attempt==1){ex=-ux;ez=-uz;}
  else if(attempt==2){ex=-uz;ez=ux;}
  else{ex=uz;ez=-ux;}
  double tx=x+ex*ManeuverDistance,tz=z+ez*ManeuverDistance;
  if(!RotaAvalon.GameClient.ValidCoordinate(tx)||!RotaAvalon.GameClient.ValidCoordinate(tz))
   throw new ArgumentException("Ponto de recuperação fora do mapa validado.");
  return new AutoQuestGroundPoint{Index=attempt,X=tx,Z=tz};
 }
 public static bool Moved(double x,double z,double fromX,double fromZ) {
  double dx=x-fromX,dz=z-fromZ;
  return dx*dx+dz*dz>=MovementThreshold*MovementThreshold;
 }
}
