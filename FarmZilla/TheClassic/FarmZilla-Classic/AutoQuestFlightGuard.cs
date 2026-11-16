using System;

// Pure flight-plan guard. The game client exposes world Y in units 10x the
// on-screen altitude; the native auto-flight height command expects Y/10.
// The first verified in-flight sample is the ONLY cruise reference for this
// session: never chase the rising Y as the player climbs unexpectedly.
sealed class AutoQuestFlightGuard {
 public const double MaxRiseWorld=18.0,MaxFallWorld=18.0;
 public const double MaxCourseDetour=45.0;
 public const double MaxFlightLeg=90.0;
 public readonly double ReferenceY,StartX,StartZ,GoalX,GoalZ;
 public readonly int FixedHeight;
 public AutoQuestFlightGuard(double initialY,double x,double z,double destinationX,double destinationZ){
  if(!RotaAvalon.GameClient.ValidCoordinate(initialY)||initialY<10||
   !RotaAvalon.GameClient.ValidCoordinate(x)||!RotaAvalon.GameClient.ValidCoordinate(z)||
   !AutoQuestBetaSession.ValidDestination(destinationX,destinationZ))
   throw new ArgumentException("Amostra inicial de voo ou destino inválidos.");
  ReferenceY=initialY;StartX=x;StartZ=z;GoalX=destinationX;GoalZ=destinationZ;
  FixedHeight=(int)Math.Round(initialY/10.0,MidpointRounding.AwayFromZero);
  if(FixedHeight<1||FixedHeight>1000)
   throw new ArgumentException("Altura de referência fora do comando nativo.");
 }
 static double Distance(double x,double z,double tx,double tz){
  double dx=x-tx,dz=z-tz;return Math.Sqrt(dx*dx+dz*dz);
 }
 public double DeltaY(double y){return y-ReferenceY;}
 public double Remaining(double x,double z){return Distance(x,z,GoalX,GoalZ);}
 public string Validate(double x,double y,double z){
  if(!RotaAvalon.GameClient.ValidCoordinate(x)||!RotaAvalon.GameClient.ValidCoordinate(y)||
   !RotaAvalon.GameClient.ValidCoordinate(z))
   return "Coordenadas de voo ilegíveis.";
  double delta=DeltaY(y);
  if(delta>MaxRiseWorld)
   return "Subida não controlada: Y atual="+y.ToString("0.0")+
    ", referência="+ReferenceY.ToString("0.0")+", acima "+delta.ToString("0.0")+
    " unidades. Navegação aérea parada automaticamente.";
  if(delta< -MaxFallWorld)
   return "Queda não prevista em voo: Y atual="+y.ToString("0.0")+
    ", referência="+ReferenceY.ToString("0.0")+", diferença="+delta.ToString("0.0")+
    ". Navegação aérea parada automaticamente.";
  double initial=Distance(StartX,StartZ,GoalX,GoalZ);
  double actual=Remaining(x,z);
  if(actual>initial+MaxCourseDetour)
   return "Deslocamento aéreo afastou-se do objetivo em "+
    (actual-initial).ToString("0.0")+" unidades; navegação interrompida.";
  return null;
 }
}
