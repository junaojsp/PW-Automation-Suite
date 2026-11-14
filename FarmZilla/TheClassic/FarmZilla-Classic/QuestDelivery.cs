using System;using System.Threading;
// Per-character delivery policy. A confirmed recipient never receives another acceptance command.
static class QuestDelivery {
 internal sealed class Outcome {public bool Confirmed,AlreadyPresent;public int Attempts;public string CloseError;}
 internal static Outcome Execute(Func<bool> hasQuest,Action openNpc,Func<bool> npcReady,Action acceptQuest,Func<bool> waitQuest,Action closeWindows,CancellationToken token,Func<bool> canRetryError=null){var result=new Outcome();token.ThrowIfCancellationRequested();if(hasQuest()){result.Confirmed=true;result.AlreadyPresent=true;return result;}bool opened=false;
  try{for(int attempt=1;attempt<=3;attempt++){token.ThrowIfCancellationRequested();try{if(hasQuest()){result.Confirmed=true;break;}result.Attempts=attempt;opened=true;openNpc();if(npcReady()){token.ThrowIfCancellationRequested();acceptQuest();if(waitQuest()){result.Confirmed=true;break;}}}catch(OperationCanceledException){throw;}catch(Exception){if(attempt==3||canRetryError==null||!canRetryError())throw;}if(attempt<3&&token.WaitHandle.WaitOne(250))token.ThrowIfCancellationRequested();}
   if(!result.Confirmed)result.Confirmed=hasQuest();return result;
  }finally{if(opened){try{closeWindows();}catch(Exception e){result.CloseError=e.Message;}}}
 }
}


