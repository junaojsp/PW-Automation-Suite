using System;using System.Threading;
class QuestTurninPolicyTest {
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static int Main(){try{
  int sends=0,closes=0;bool active=true;
  var done=QuestDelivery.Execute(()=>!active,()=>{},()=>true,()=>sends++,()=>{active=false;return !active;},()=>closes++,CancellationToken.None);
  Check(done.Confirmed&&sends==1&&closes==1,"entrega confirmada não é repetida e fecha a interação");
  sends=0;closes=0;active=true;
  var retry=QuestDelivery.Execute(()=>!active,()=>{},()=>true,()=>sends++,()=>{if(sends==3)active=false;return !active;},()=>closes++,CancellationToken.None);
  Check(retry.Confirmed&&sends==3&&closes==1,"entrega pendente repete até três vezes somente no cliente afetado");
  sends=0;closes=0;
  var failure=QuestDelivery.Execute(()=>false,()=>{},()=>true,()=>sends++,()=>false,()=>closes++,CancellationToken.None);
  Check(!failure.Confirmed&&sends==3&&closes==1,"falha de entrega fecha o NPC sem anunciar sucesso");
  return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
