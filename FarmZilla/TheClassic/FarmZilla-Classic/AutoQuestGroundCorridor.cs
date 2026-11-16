using System;
using System.IO;

// The ONLY vetted on-foot route in the current Auto Quest beta.
// From the user's successful 0.6.5 walking log, 2026-11-16 09:06:53..09:10:13.
// It follows the city's WEST exit (X~1539 at Z~1300), not the direct
// X~1750 path which trapped the 0.6.6 beta against the city wall.
// Observations are sampled positions (not a game navmesh), so stay near this
// corridor and stop if the client deviates. Never apply to other missions/areas.
sealed class AutoQuestGroundPoint {
 public double X,Z;
 public int Index;
}
sealed class AutoQuestGroundCorridor {
 public const int Samples=40;
 public const double WaypointReach=10.0,MaxDeviation=62.0;
 public const double StartRadius=52.0,TargetRadius=10.0;
 static readonly AutoQuestGroundPoint[] WestCityWalk=new AutoQuestGroundPoint[] {
   new AutoQuestGroundPoint{X=1710.2,Z=977.9,Index=0},
   new AutoQuestGroundPoint{X=1715.7,Z=973.3,Index=1},
   new AutoQuestGroundPoint{X=1731.3,Z=989.3,Index=2},
   new AutoQuestGroundPoint{X=1736.5,Z=1005.1,Index=3},
   new AutoQuestGroundPoint{X=1729.5,Z=1017.6,Index=4},
   new AutoQuestGroundPoint{X=1694.0,Z=1022.6,Index=5},
   new AutoQuestGroundPoint{X=1662.3,Z=1026.0,Index=6},
   new AutoQuestGroundPoint{X=1631.7,Z=1031.9,Index=7},
   new AutoQuestGroundPoint{X=1614.2,Z=1035.5,Index=8},
   new AutoQuestGroundPoint{X=1602.4,Z=1046.7,Index=9},
   new AutoQuestGroundPoint{X=1596.0,Z=1078.3,Index=10},
   new AutoQuestGroundPoint{X=1578.8,Z=1083.5,Index=11},
   new AutoQuestGroundPoint{X=1573.6,Z=1100.4,Index=12},
   new AutoQuestGroundPoint{X=1542.3,Z=1113.2,Index=13},
   new AutoQuestGroundPoint{X=1534.1,Z=1125.5,Index=14},
   new AutoQuestGroundPoint{X=1536.0,Z=1158.4,Index=15},
   new AutoQuestGroundPoint{X=1537.4,Z=1188.1,Index=16},
   new AutoQuestGroundPoint{X=1538.4,Z=1224.0,Index=17},
   new AutoQuestGroundPoint{X=1538.9,Z=1259.8,Index=18},
   new AutoQuestGroundPoint{X=1539.2,Z=1277.9,Index=19},
   new AutoQuestGroundPoint{X=1539.1,Z=1313.7,Index=20},
   new AutoQuestGroundPoint{X=1538.1,Z=1349.6,Index=21},
   new AutoQuestGroundPoint{X=1537.5,Z=1385.0,Index=22},
   new AutoQuestGroundPoint{X=1537.7,Z=1402.6,Index=23},
   new AutoQuestGroundPoint{X=1540.5,Z=1437.9,Index=24},
   new AutoQuestGroundPoint{X=1545.1,Z=1453.7,Index=25},
   new AutoQuestGroundPoint{X=1574.6,Z=1474.2,Index=26},
   new AutoQuestGroundPoint{X=1607.6,Z=1495.7,Index=27},
   new AutoQuestGroundPoint{X=1610.9,Z=1497.6,Index=28},
   new AutoQuestGroundPoint{X=1618.3,Z=1534.3,Index=29},
   new AutoQuestGroundPoint{X=1632.6,Z=1562.1,Index=30},
   new AutoQuestGroundPoint{X=1650.7,Z=1588.3,Index=31},
   new AutoQuestGroundPoint{X=1672.0,Z=1613.8,Index=32},
   new AutoQuestGroundPoint{X=1683.5,Z=1626.8,Index=33},
   new AutoQuestGroundPoint{X=1709.4,Z=1651.4,Index=34},
   new AutoQuestGroundPoint{X=1733.9,Z=1674.9,Index=35},
   new AutoQuestGroundPoint{X=1757.5,Z=1697.3,Index=36},
   new AutoQuestGroundPoint{X=1780.4,Z=1719.4,Index=37},
   new AutoQuestGroundPoint{X=1761.7,Z=1754.0,Index=38},
   new AutoQuestGroundPoint{X=1750.0,Z=1773.4,Index=39}
 };
 readonly double originX,originZ;
 int next,previous;
 bool returning;
 AutoQuestGroundCorridor(double x,double z){
  originX=x;originZ=z;
  next=0;previous=-1;
  // A different starting position near the same NPC may already be near
  // the first few sampled points. Never skip an unobserved far-away part.
  for(int i=0;i<4;i++)
   if(Distance(x,z,WestCityWalk[i].X,WestCityWalk[i].Z)<=16.0)
    next=i;
 }
 public int NextIndex{get{return returning?previous:next;}}
 public static int Count{get{return WestCityWalk.Length;}}
 public static AutoQuestGroundPoint Sample(int index){
  if(index<0||index>=Count)throw new ArgumentOutOfRangeException("index");
  return WestCityWalk[index];
 }
 static double Distance(double x,double z,double px,double pz){
  double dx=x-px,dz=z-pz;return Math.Sqrt(dx*dx+dz*dz);
 }
 public static bool IsKnownDestination(string name,double x,double z){
  return String.Equals(name,"CheirosoWB",StringComparison.OrdinalIgnoreCase)&&
   Distance(x,z,1750.0,1773.4)<=TargetRadius;
 }
 public static AutoQuestGroundCorridor For(string name,double originX,double originZ,double destinationX,double destinationZ){
  if(!IsKnownDestination(name,destinationX,destinationZ))return null;
  // A recorded corridor is an optional optimization, never a prerequisite
  // for starting the scanner/beta in a different city or after moving away.
  // The generic beta remains supervised and its anti-stuck timeout applies.
  if(Distance(originX,originZ,1710.2,977.9)>StartRadius)return null;
  return new AutoQuestGroundCorridor(originX,originZ);
 }
 void CheckNear(double x,double z,int cursor){
  // Check only the CURRENT short section: don't leap to a distant part of
  // the walk just because the route crosses itself or is geographically close.
  double nearest=Double.MaxValue;
  for(int i=Math.Max(0,cursor-3);i<=Math.Min(Count-1,cursor+2);i++)
   nearest=Math.Min(nearest,Distance(x,z,WestCityWalk[i].X,WestCityWalk[i].Z));
  if(nearest>MaxDeviation)
   throw new IOException("O personagem saiu "+nearest.ToString("0.0")+
    " unidades da trilha segura conhecida, no ponto "+cursor+
    ". Navegação interrompida; não tentar atravessar a parede.");
 }
 public AutoQuestGroundPoint Next(double x,double z,bool reverse){
  if(reverse&&!returning){
   returning=true;
   // The first trip may have been flown, so near the recorded destination
   // return from the FAR end instead of jumping straight over city walls.
   previous=Distance(x,z,1750.0,1773.4)<=35.0?
    Count-2:Math.Max(0,Math.Min(Count-2,next-1));
  }
  if(!returning){
   while(next<Count-1&&
    Distance(x,z,WestCityWalk[next].X,WestCityWalk[next].Z)<=WaypointReach)
    next++;
   CheckNear(x,z,next);
   return WestCityWalk[next];
  }
  while(previous>=0&&
   Distance(x,z,WestCityWalk[previous].X,WestCityWalk[previous].Z)<=WaypointReach)
   previous--;
  CheckNear(x,z,Math.Max(0,previous));
  if(previous<0)
   return new AutoQuestGroundPoint{X=originX,Z=originZ,Index=-1};
  return WestCityWalk[previous];
 }
 public static bool ValidSpacing(){
  if(Count!=Samples)return false;
  for(int i=1;i<Count;i++){
   double d=Distance(WestCityWalk[i].X,WestCityWalk[i].Z,WestCityWalk[i-1].X,WestCityWalk[i-1].Z);
   if(d<0.1||d>45)return false;
  }
  return Distance(WestCityWalk[0].X,WestCityWalk[0].Z,1710.2,977.9)<1&&
   Distance(WestCityWalk[Count-1].X,WestCityWalk[Count-1].Z,1750,1773.4)<1;
 }
}
