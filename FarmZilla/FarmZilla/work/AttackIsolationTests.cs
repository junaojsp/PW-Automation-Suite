using System;
using System.Collections.Generic;
using System.Linq;
// Test the controller without connecting to the game.
class AttackIsolationTests {
 sealed class Fake : IAttackRuntime {
  public uint Current=0x80000101;public bool LeaderCanAttack=true;
  public readonly HashSet<int> RunningPids=new HashSet<int>();
  public readonly List<int> Started=new List<int>(),Stopped=new List<int>();
  public uint Target(CharacterRef c){return Current;}
  public bool ValidTarget(CharacterRef c,uint t){return LeaderCanAttack;}
  public bool Running(CharacterRef c,uint combo,uint target){return RunningPids.Contains(c.Pid);}
  public AttackResult Start(CharacterRef c,uint target){Started.Add(c.Pid);RunningPids.Add(c.Pid);return new AttackResult{Combo=(uint)(100+c.Pid)};}
  public AttackResult Stop(CharacterRef c,uint combo,uint target){Stopped.Add(c.Pid);RunningPids.Remove(c.Pid);return new AttackResult();}
  public AttackResult Release(CharacterRef c){return new AttackResult();}
  public int Step(CharacterRef c,uint combo){return 1;}
  public AttackResult StartSupport(CharacterRef c,CharacterRef leader){Started.Add(c.Pid);RunningPids.Add(c.Pid);return new AttackResult{Combo=(uint)(100+c.Pid)};}
  public bool RunningSupport(CharacterRef c,uint combo){return RunningPids.Contains(c.Pid);}
  public AttackResult StopSupport(CharacterRef c){Stopped.Add(c.Pid);RunningPids.Remove(c.Pid);return new AttackResult();}
  public AttackResult ReleaseSupport(CharacterRef c){return new AttackResult();}
 }
 static void Expect(bool condition,string message){if(!condition)throw new Exception("FAIL: "+message);Console.WriteLine("PASS "+message);}
 static CharacterRef C(int pid,string name){return new CharacterRef{Pid=pid,Id=(uint)(100+pid),Name=name};}
 static void LeaderCannotAttackFromStart(){
  var fake=new Fake{LeaderCanAttack=false};var leader=C(1,"lider");var sec=C(2,"secundario");
  var controller=new AttackController(fake,new HashSet<int>(),s=>{});
  controller.Tick(leader,new[]{leader,sec},true,true,false);
  Expect(!fake.Started.Contains(1),"lider bloqueado nao inicia macro");
  Expect(fake.Started.Contains(2)&&controller.Engaged(2),"secundario inicia independentemente do lider");
  controller.Tick(leader,new[]{leader,sec},true,true,false);
  Expect(controller.Engaged(2)&&!fake.Stopped.Contains(2),"falha do lider nao encerra secundaria ativa");
  fake.Current=0;controller.Tick(leader,new[]{leader,sec},true,true,false);
  Expect(fake.Stopped.Contains(2)&&!controller.Engaged(2),"sem alvo: parar secundaria e liberar seguimento");
 }
 static void LeaderFailsAfterStarting(){
  var fake=new Fake();var leader=C(11,"lider");var sec=C(12,"secundario");
  var controller=new AttackController(fake,new HashSet<int>(),s=>{});
  controller.Tick(leader,new[]{leader,sec},true,true,false);
  Expect(fake.Started.Contains(11)&&fake.Started.Contains(12),"inicio normal ativa ambos");
  fake.LeaderCanAttack=false;
  // The old controller globally stopped all clients after this periodic validation.
  for(int i=0;i<20;i++){controller.Tick(leader,new[]{leader,sec},true,true,false);System.Threading.Thread.Sleep(60);}
  Expect(controller.Engaged(12)&&!fake.Stopped.Contains(12),"incapacidade posterior do lider preserva secundaria");
  Expect(fake.Stopped.Contains(11)&&!controller.Engaged(11),"macro do lider eh interrompida isoladamente");
  fake.Current=0x80000102;controller.Tick(leader,new[]{leader,sec},true,true,false);
  Expect(controller.Engaged(12),"novo alvo valido permanece coordenado");
 }
 static int Main(){
  try{LeaderCannotAttackFromStart();LeaderFailsAfterStarting();Console.WriteLine("Ataque independente: testes aprovados.");return 0;}
  catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
}
