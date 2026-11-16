using System;
using System.IO;
using System.Threading;

// Concrete adapter for the existing The Classic native bridge, used ONLY by
// AutoQuestVerifiedDriver after all task-stage/location guards have passed.
// Not enabled by any UI button until the real task/NPC decoder is validated.
sealed class AutoQuestNativeCommands : IAutoQuestGameCommands, IDisposable {
 readonly int pid;
 readonly uint id;
 readonly string name,lockName,library;
 bool disposed;
 public AutoQuestNativeCommands(int processId,uint characterId,string characterName){
  if(IntPtr.Size!=4)throw new InvalidOperationException("Auto Quest exige executável x86.");
  if(processId<=0||characterId==0||String.IsNullOrWhiteSpace(characterName))
   throw new ArgumentException("Identidade do personagem inválida.");
  pid=processId;id=characterId;name=characterName;
  lockName="Auto Quest "+Guid.NewGuid().ToString("N");
  if(!RotaLock.TrySet(pid,lockName))
   throw new InvalidOperationException("Personagem ocupado com Espelho, Mat Farm ou Frost.");
  try{library=RotaAvalon.RotaPaths.Bridge();}
  catch{RotaLock.ClearIf(pid,lockName);throw;}
 }
 RotaAvalon.GameClient Client(){
  if(disposed)throw new ObjectDisposedException("AutoQuestNativeCommands");
  // Both independent readers must refer to the exact same character.
  using(var memory=new CharacterMemory(pid))
   if(memory.Id!=id||memory.Name!=name)throw new IOException("Identidade mudou durante o Auto Quest.");
  var client=new RotaAvalon.GameClient(pid,new System.Collections.Generic.Dictionary<string,bool>());
  try{
   var state=client.Read();
   if(state.Name!=name||state.Hp<=0)throw new IOException("Personagem alterado ou sem vida.");
   return client;
  }catch{client.Dispose();throw;}
 }
 public void GroundPath(double x,double y,double z){
  using(var client=Client()){
   var state=client.Read();
   if(!state.FlightStateKnown||state.IsFlying)throw new IOException("Auto caminho exige personagem no chão.");
   // The native auto-path keeps the character's CURRENT height, never the mission's stale height.
   RotaAvalon.NativeBridge.Navigate(client,x,state.Y,z,library);
  }
 }
 public void FlightPath(double x,double z){
  // Keep compatibility with the verified driver, which is not yet active
  // in the Auto Quest UI. The beta uses the fixed-height overload below.
  using(var client=Client()){
   var state=client.Read();
   if(!state.FlightStateKnown||!state.IsFlying)
    throw new IOException("Deslocamento aéreo exige voo confirmado.");
   int height=(int)Math.Round(state.Y/10.0,MidpointRounding.AwayFromZero);
   SendFlightLeg(client,x,z,height);
  }
 }
 public void FlightPath(double x,double z,int fixedHeight){
  using(var client=Client()){
   var state=client.Read();
   if(!state.FlightStateKnown||!state.IsFlying)
    throw new IOException("Deslocamento aéreo exige voo confirmado.");
   SendFlightLeg(client,x,z,fixedHeight);
  }
 }
 void SendFlightLeg(RotaAvalon.GameClient client,double x,double z,int fixedHeight){
  if(fixedHeight<1||fixedHeight>1000)
   throw new ArgumentOutOfRangeException("fixedHeight","Altitude de cruzeiro inválida.");
  // The host processes auto-move messages asynchronously. Mirror the
  // 80-ms ordering already validated by RotaExecution.FlyHigh, rather
  // than immediately racing the height message behind the direction.
  RotaAvalon.NativeBridge.FlyTo(client,x,z,library);
  Thread.Sleep(80);
  RotaAvalon.NativeBridge.FlightHeight(client,fixedHeight,library);
 }
 public void Takeoff(double targetX,double targetZ){
  using(var client=Client()){
   var state=client.Read();
   if(!state.FlightStateKnown)throw new IOException("Estado de voo transitório.");
   if(state.IsFlying)return;
   // Same bounded takeoff style as RotaExecution: a short movement toward the VERIFIED target.
   double dx=targetX-state.X,dz=targetZ-state.Z;
   double distance=Math.Sqrt(dx*dx+dz*dz);
   if(distance<1)throw new IOException("Decolagem sem destino válido.");
   double step=Math.Min(12,distance);
   RotaAvalon.NativeBridge.FlyTo(client,state.X+step*dx/distance,state.Z+step*dz/distance,library);
   int height=(int)Math.Min(300,Math.Max(1,Math.Ceiling(state.Y/10.0)+2));
   RotaAvalon.NativeBridge.FlightHeight(client,height,library);
  }
 }
 public void Descend(){
  using(var client=Client()){
   var state=client.Read();
   if(!state.FlightStateKnown)throw new IOException("Estado de voo transitório.");
   if(!state.IsFlying)return;
   // A bounded descent pulse, re-evaluated against the REAL flight state by the driver.
   try{
    RotaAvalon.NativeBridge.KeyMove(client,false,library);
    client.Vertical(-1);
    Thread.Sleep(300);
   }finally{client.Vertical(0);}
  }
 }
 public void Cultivate(bool enabled){
  using(var client=Client())RotaAvalon.NativeBridge.AutoPolicy(pid,enabled,library);
 }
 public void Halt(){
  using(var client=Client()){
   var state=client.Read();
   if(state.FlightStateKnown&&state.IsFlying){
    try{RotaAvalon.NativeBridge.FlyTo(client,state.X,state.Z,library);}
    finally{RotaAvalon.NativeBridge.FlightHeightOff(client,library);}
   }else if(state.FlightStateKnown)
    RotaAvalon.NativeBridge.Navigate(client,state.X,state.Y,state.Z,library);
  }
 }
 public void Dispose(){
  if(disposed)return;
  disposed=true;
  RotaLock.ClearIf(pid,lockName);
 }
}
