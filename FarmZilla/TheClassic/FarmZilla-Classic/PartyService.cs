using System;using System.Collections.Generic;using System.Diagnostics;using System.Linq;using System.Threading;
static partial class PartyService {
 public sealed class Outcome {public bool Blocked;public int Pid;public string Message;}
 static void Send(CharacterRef character,byte[] packet,Outcome result){PartyBridge bridge=null;try{bridge=new PartyBridge(character.Pid);if(bridge.CharacterId!=character.Id)throw new Exception("Personagem mudou. Atualize a lista.");bridge.SendParty(packet);if(bridge.LastSendResult!=1)throw new Exception("Envio da PT não foi aceito pelo cliente.");}finally{if(bridge!=null){try{bridge.Dispose();}finally{if(!bridge.RestoreSucceeded){result.Blocked=true;result.Pid=character.Pid;}}}if(result.Blocked)throw new Exception("Remoção da ponte não confirmada. Reinicie esse cliente.");}}
 static CharacterMemory Open(CharacterRef character){var memory=new CharacterMemory(character.Pid);if(memory.Id!=character.Id){memory.Dispose();throw new Exception("Personagem mudou. Atualize a lista.");}return memory;}
 static bool Together(CharacterRef main,CharacterRef secondary){using(var a=Open(main))using(var b=Open(secondary)){var first=a.ReadParty();var second=b.ReadParty();return first!=null&&second!=null&&first.Leader==main.Id&&second.Leader==main.Id&&first.Members.Contains(secondary.Id)&&second.Members.Contains(main.Id);}}
 public static Outcome Join(CharacterRef main,CharacterRef secondary,CancellationToken token,IProgress<string> progress){var result=new Outcome();try{
  for(int attempt=1;attempt<=2;attempt++){token.ThrowIfCancellationRequested();if(Together(main,secondary)){result.Message=secondary.Name+": já está na PT do principal.";return result;}
   using(var a=Open(main))using(var b=Open(secondary)){var first=a.ReadParty();var second=b.ReadParty();if(first!=null&&first.Leader!=main.Id)throw new Exception("O principal precisa ser líder para convidar.");if(second!=null)throw new Exception("O secundário já está em outra PT; não foi removido.");}
   byte[] invite=new byte[6];invite[0]=0x1B;Buffer.BlockCopy(BitConverter.GetBytes(secondary.Id),0,invite,2,4);progress.Report("Convidando "+secondary.Name+" · tentativa "+attempt+"/2.");token.ThrowIfCancellationRequested();Send(main,invite,result);
   bool accepted=false;var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<12000){token.ThrowIfCancellationRequested();if(Together(main,secondary)){result.Message=secondary.Name+": entrada na PT confirmada.";return result;}
    if(!accepted){using(var memory=Open(secondary)){var invitation=memory.FindPartyInvitation(secondary.Pid,main.Id);if(invitation!=null){token.ThrowIfCancellationRequested();byte[] answer=PartyInvite.Accept(invitation);Send(secondary,answer,result);accepted=true;progress.Report(secondary.Name+": convite do principal aceito pelo app.");}}}
    if(token.WaitHandle.WaitOne(50))token.ThrowIfCancellationRequested();
   }
  }result.Message=secondary.Name+": não entrou na PT. Confira distância, limite do grupo e restrições de convite.";
 }catch(OperationCanceledException){result.Message=secondary.Name+": montagem da PT cancelada pela mudança de seleção.";}catch(Exception e){result.Message=secondary.Name+": "+e.Message;}return result;}
 // 0.39.0 (owner: Montar PT too slow): the principal invites every secondary in one pass and the app answers each
 // invitation as it arrives, instead of waiting for one secondary to join before inviting the next. Each entry is still
 // confirmed in both clients; the ones missing after 10 s get one more invitation.
 public static List<Outcome> JoinAll(CharacterRef main,IList<CharacterRef> secondaries,CancellationToken token,IProgress<string> progress){
  var results=secondaries.Select(s=>new Outcome()).ToList();var pending=new List<int>();
  try{
   for(int i=0;i<secondaries.Count;i++){var secondary=secondaries[i];try{if(Together(main,secondary)){results[i].Message=secondary.Name+": já está na PT do principal.";continue;}
     using(var a=Open(main))using(var b=Open(secondary)){var first=a.ReadParty();var second=b.ReadParty();if(first!=null&&first.Leader!=main.Id)throw new Exception("O principal precisa ser líder para convidar.");if(second!=null)throw new Exception("O secundário já está em outra PT; não foi removido.");}
     pending.Add(i);}catch(Exception e){results[i].Message=secondary.Name+": "+e.Message;}}
   for(int attempt=1;attempt<=2&&pending.Count>0;attempt++){
    var accepted=new HashSet<int>();
    foreach(int i in pending){token.ThrowIfCancellationRequested();var secondary=secondaries[i];byte[] invite=new byte[6];invite[0]=0x1B;Buffer.BlockCopy(BitConverter.GetBytes(secondary.Id),0,invite,2,4);
     try{Send(main,invite,results[i]);}catch(Exception e){results[i].Message=secondary.Name+": "+e.Message;if(results[i].Blocked)return results;accepted.Add(i);}}
    progress.Report("Convites enviados para "+pending.Count+" secundário(s) · tentativa "+attempt+"/2.");
    var clock=Stopwatch.StartNew();var waiting=new List<int>(pending.Where(i=>!accepted.Contains(i)||results[i].Message==null));
    while(waiting.Count>0&&clock.ElapsedMilliseconds<10000){token.ThrowIfCancellationRequested();
     foreach(int i in waiting.ToArray()){var secondary=secondaries[i];
      if(Together(main,secondary)){results[i].Message=secondary.Name+": entrada na PT confirmada.";waiting.Remove(i);continue;}
      if(accepted.Contains(i))continue;
      using(var memory=Open(secondary)){var invitation=memory.FindPartyInvitation(secondary.Pid,main.Id);if(invitation==null)continue;
       byte[] answer=PartyInvite.Accept(invitation);
       try{Send(secondary,answer,results[i]);accepted.Add(i);progress.Report(secondary.Name+": convite do principal aceito pelo app.");}catch(Exception e){results[i].Message=secondary.Name+": "+e.Message;waiting.Remove(i);if(results[i].Blocked)return results;}}}
     if(waiting.Count>0&&token.WaitHandle.WaitOne(40))token.ThrowIfCancellationRequested();}
    pending=waiting;}
   foreach(int i in pending)results[i].Message=secondaries[i].Name+": não entrou na PT. Confira distância, limite do grupo e restrições de convite.";
  }catch(OperationCanceledException){for(int i=0;i<results.Count;i++)if(results[i].Message==null)results[i].Message=secondaries[i].Name+": montagem da PT cancelada pela mudança de seleção.";}
  return results;}
}
