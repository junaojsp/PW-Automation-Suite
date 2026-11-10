using System;using System.Collections.Generic;using System.Threading;
// Autoteste 0.46.8 sem jogo: "Suporte em" escolhe quem recebe a macro 2 do sacerdote; padrão = líder; trocar reinicia o suporte.
class SupportTargetTest {
 sealed class Fake:IAttackRuntime {public uint Current=0x80000101;public bool Valid=true,Alive=true,Fail;public int Starts,Stops;public uint Target(CharacterRef c){return Current;}public bool ValidTarget(CharacterRef c,uint t){return Valid;}public bool Running(CharacterRef c,uint combo,uint t){return Alive;}public AttackResult Start(CharacterRef c,uint t){int n=Interlocked.Increment(ref Starts);return Fail?new AttackResult{Message="falha"}:new AttackResult{Combo=(uint)(100+n),Message="atacando"};}public AttackResult Stop(CharacterRef c,uint combo,uint t){Interlocked.Increment(ref Stops);return new AttackResult();}public int Releases;public AttackResult Release(CharacterRef c){Releases++;return new AttackResult();}public int StepValue=1;public int Step(CharacterRef c,uint combo){return StepValue;}public int SupStarts,SupStops,SupReleases;public uint SupLeader;public AttackResult StartSupport(CharacterRef c,CharacterRef leader){int n=Interlocked.Increment(ref SupStarts);SupLeader=leader.Id;return new AttackResult{Combo=(uint)(500+n),Message="suporte"};}public bool RunningSupport(CharacterRef c,uint combo){return Alive;}public AttackResult StopSupport(CharacterRef c){SupStops++;return new AttackResult();}public AttackResult ReleaseSupport(CharacterRef c){SupReleases++;return new AttackResult();}}
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static int Main(){try{var p=new CharacterRef{Pid=1,Id=1,Name="principal"};var ep=new CharacterRef{Pid=2,Id=2,Name="ep"};var war=new CharacterRef{Pid=3,Id=3,Name="guerreiro"};
  var rt=new Fake();var c=new AttackController(rt,new HashSet<int>(),s=>{});var sup=new HashSet<int>{2};
  c.Tick(p,new[]{ep,war},true,true,false,sup,null);Check(rt.SupStarts==1&&rt.SupLeader==1,"sem escolha o suporte vai no líder");
  c.Tick(p,new[]{ep,war},true,true,false,sup,war);Check(rt.SupStops==1&&rt.SupStarts==2&&rt.SupLeader==3,"escolher outra conta reinicia o suporte nela");
  c.Tick(p,new[]{ep,war},true,true,false,sup,war);Check(rt.SupStarts==2,"mesma escolha não reinicia");
  c.Tick(p,new[]{ep,war},true,true,false,sup,ep);Check(rt.SupLeader==1,"escolher o próprio sacerdote volta para o líder");
  Console.WriteLine("Suporte em aprovado.");return 0;}catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}
}
