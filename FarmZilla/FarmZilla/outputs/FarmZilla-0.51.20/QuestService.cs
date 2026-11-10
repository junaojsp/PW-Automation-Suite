using System;using System.Collections.Generic;using System.Collections.Concurrent;using System.Diagnostics;using System.Linq;using System.Threading;using System.Threading.Tasks;
sealed class QuestService {
 readonly Action<string> note;readonly HashSet<int> blocked;readonly Queue<Request> queue=new Queue<Request>();readonly HashSet<string> queued=new HashSet<string>();
 readonly ConcurrentDictionary<int,byte> active=new ConcurrentDictionary<int,byte>(),unsafeClients=new ConcurrentDictionary<int,byte>();
 CharacterRef source;CharacterRef[] recipients=new CharacterRef[0];HashSet<ushort> known;string selection="";byte[] npc;float npcX,npcZ;DateTime npcTime;CancellationTokenSource cancel=new CancellationTokenSource();Task job=Task.FromResult(0);bool stopping;int generation;
 sealed class Request {public CharacterRef Source;public CharacterRef[] Recipients;public byte[] Npc,Packet;public ushort Quest;public bool Finish;public string Key;public float X,Z;public DateTime Created;}
 sealed class Result {public int Pid;public bool Blocked,Confirmed;public string Message;}
 public QuestService(Action<string> log,HashSet<int> blockedClients){note=log;blocked=blockedClients;}
 public Action<ushort,int,int> ReportCheck;
 public bool Working {get{return !job.IsCompleted||queue.Count>0;}}
 public bool Running {get{return !job.IsCompleted;}}
 public bool Pending {get{return queue.Count>0;}}
 public bool IsClientBusy(int pid){return active.ContainsKey(pid)||unsafeClients.ContainsKey(pid);}
 public void CancelAll(){generation++;cancel.Cancel();queue.Clear();queued.Clear();npc=null;}
 public void UpdateSelection(CharacterRef main,CharacterRef[] selected){string key=main==null?"":main.Key+"/"+string.Join(",",selected.Select(s=>s.Key).OrderBy(s=>s).ToArray());if(key==selection)return;CancelAll();selection=key;source=main;recipients=selected;known=null;RefreshPrimarySnapshot();}
 public void RefreshPrimarySnapshot(){if(source==null)return;try{using(var state=Open(source))known=new HashSet<ushort>(state.ActiveQuestIds());}catch{known=null;}}
 static CharacterMemory Open(CharacterRef character){var memory=new CharacterMemory(character.Pid);if(memory.Id!=character.Id){memory.Dispose();throw new InvalidOperationException("O personagem mudou. Atualize a lista.");}return memory;}
 public void Observe(byte[] packet){if(stopping||source==null||recipients.Length==0)return;
  if(packet.Length==6&&BitConverter.ToUInt16(packet,0)==0x23){try{using(var state=Open(source)){npc=(byte[])packet.Clone();npcX=state.X;npcZ=state.Z;npcTime=DateTime.UtcNow;}}catch{npc=null;}return;}
  bool finish=packet.Length==18&&BitConverter.ToUInt16(packet,0)==0x25&&BitConverter.ToUInt32(packet,2)==6&&BitConverter.ToUInt32(packet,6)==8;bool accept=packet.Length==22&&BitConverter.ToUInt16(packet,0)==0x25&&BitConverter.ToUInt32(packet,2)==7&&BitConverter.ToUInt32(packet,6)==12;if(!finish&&!accept)return;if(finish&&BitConverter.ToUInt32(packet,14)!=0){note("Entrega com escolha de recompensa ainda não validada; confira os secundários.");return;}
  uint id=BitConverter.ToUInt32(packet,10);if(id==0||id>65535){note("Missão: identificador fora do perfil validado.");return;}
  if(known==null){note("Missão "+id+": leitura do principal indisponível; não espelhada.");return;}
  if(finish?!known.Contains((ushort)id):known.Contains((ushort)id))return;
  if(npc==null||(DateTime.UtcNow-npcTime).TotalMinutes>5){note("Missão "+id+": reabra o NPC no principal com o app ativo antes de aceitar ou entregar.");return;}
  string key=selection+":"+generation+":"+BitConverter.ToUInt32(npc,2)+":"+id+":"+finish;if(queued.Contains(key))return;if(queue.Count>=16){note("Fila de missões cheia; missão "+id+" não adicionada.");return;}
  queued.Add(key);queue.Enqueue(new Request{Source=source,Recipients=recipients.ToArray(),Npc=(byte[])npc.Clone(),Packet=(byte[])packet.Clone(),Quest=(ushort)id,Finish=finish,Key=key,X=npcX,Z=npcZ,Created=DateTime.UtcNow});note("Missão "+id+(finish?": entrega capturada, aguardando confirmação do principal.":": aceitação capturada, aguardando confirmação do principal."));
 }
 public void Pump(bool otherBusy){if(stopping||otherBusy||!job.IsCompleted||queue.Count==0)return;var request=queue.Dequeue();if((DateTime.UtcNow-request.Created).TotalSeconds>120){queued.Remove(request.Key);note("Missão "+request.Quest+": solicitação expirada; confira os secundários.");return;}cancel.Dispose();cancel=new CancellationTokenSource();job=Run(request,cancel.Token);}
 public async Task CancelAndWait(){CancelAll();await job;}
 static bool WaitQuest(CharacterRef character,ushort quest,int timeout,CancellationToken token,bool present=true){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<timeout){token.ThrowIfCancellationRequested();try{using(var state=Open(character))if(state.ActiveQuestIds().Contains(quest)==present)return true;}catch(InvalidOperationException){throw;}catch{}if(token.WaitHandle.WaitOne(50))token.ThrowIfCancellationRequested();}return false;}
 async Task Run(Request request,CancellationToken token){foreach(var character in request.Recipients)active[character.Pid]=0;try{
  bool confirmed=await Task.Run(()=>WaitQuest(request.Source,request.Quest,8000,token,!request.Finish));if(!confirmed){note("Missão "+request.Quest+(request.Finish?": entrega não confirmada no principal; nenhum comando enviado aos secundários.":": não entrou na lista ativa do principal; nenhum comando enviado aos secundários."));return;}
  var progress=new Progress<string>(s=>note(s));var selected=request.Recipients.Where(s=>!blocked.Contains(s.Pid)&&!unsafeClients.ContainsKey(s.Pid)).ToArray();
  var results=await Task.WhenAll(selected.Select(character=>Task.Run(()=>{try{var result=Mirror(character,request,token,(IProgress<string>)progress);if(result.Blocked)unsafeClients[character.Pid]=0;return result;}finally{byte unused;active.TryRemove(character.Pid,out unused);}})));
  foreach(var result in results){if(result.Blocked)blocked.Add(result.Pid);note(result.Message);}int confirmedCount=results.Count(r=>r.Confirmed);note("CHECK missão "+request.Quest+": "+confirmedCount+"/"+request.Recipients.Length+" secundário(s) confirmados ("+(DateTime.UtcNow-request.Created).TotalSeconds.ToString("0.0")+" s desde o principal).");if(ReportCheck!=null)ReportCheck(request.Quest,confirmedCount,request.Recipients.Length);
 }catch(OperationCanceledException){note("Missão "+request.Quest+": tentativas pendentes canceladas.");}catch(Exception e){note("Missão "+request.Quest+": "+e.Message);}finally{queued.Remove(request.Key);foreach(var character in request.Recipients){byte unused;active.TryRemove(character.Pid,out unused);}}}
 static void Send(CharacterRef character,byte[] packet,Result result){NpcBridge bridge=null;try{bridge=new NpcBridge(character.Pid);if(bridge.CharacterId!=character.Id)throw new Exception("Personagem mudou.");bridge.SendNpc(packet);if(bridge.LastSendResult!=1)throw new Exception("Envio não aceito pelo cliente.");}finally{if(bridge!=null){try{bridge.Dispose();}finally{if(!bridge.RestoreSucceeded)result.Blocked=true;}}if(result.Blocked)throw new Exception("Ponte não confirmou remoção; reinicie esse cliente.");}}
 // Closes the visible NPC/quest windows of a character (e.g. the automatic quest popup on a secondary). Null on success, else the reason.
 // Popup = Win_NPC visible with no NPC interaction (checked on the real client: interaction flags all zero).
 public static bool PopupOpen(CharacterRef character){using(var state=Open(character))return state.NpcDialogs(CancellationToken.None).Any(d=>state.NpcVisible(d)&&!state.NpcInteractionActive(d));}
 // Closes only popups (never an NPC conversation in progress). True when something was closed.
 public static bool ClosePopup(CharacterRef character,CancellationToken token,out bool blocked){blocked=false;uint[] popups;using(var state=Open(character))popups=state.NpcDialogs(token).Where(d=>state.NpcVisible(d)&&!state.NpcInteractionActive(d)).Take(4).ToArray();if(popups.Length==0)return false;NpcBridge bridge=null;
  try{bridge=new NpcBridge(character.Pid);if(bridge.CharacterId!=character.Id)throw new Exception("Personagem mudou antes de fechar janela");bridge.CloseDialogs(popups);}finally{if(bridge!=null){try{bridge.Dispose();}finally{blocked=!bridge.RestoreSucceeded;}}}
  for(int i=0;i<20;i++){using(var state=Open(character))if(popups.All(d=>!state.NpcVisible(d)))return true;Thread.Sleep(50);}throw new Exception("Janela ainda aberta no cliente");}
 public static string CloseWindows(CharacterRef character,CancellationToken token,out bool blocked){var r=new Result{Pid=character.Pid};try{CloseInteraction(character,0,r,token);blocked=r.Blocked;return null;}catch(OperationCanceledException){throw;}catch(Exception e){blocked=r.Blocked;return e.Message;}}
 static void CloseInteraction(CharacterRef character,ushort quest,Result result,CancellationToken token){uint[] dialogs;using(var state=Open(character)){dialogs=state.QuestWindows(token).Where(d=>state.IsNpcDialog(d)?state.NpcVisible(d)||state.NpcInteractionActive(d):state.QuestWindowVisible(d)).OrderBy(d=>state.IsNpcDialog(d)?1:0).Take(16).ToArray();}if(dialogs.Length==0)return;NpcBridge bridge=null;try{bridge=new NpcBridge(character.Pid);if(bridge.CharacterId!=character.Id)throw new Exception("Personagem mudou antes de fechar janela");bridge.CloseDialogs(dialogs);}finally{if(bridge!=null){try{bridge.Dispose();}finally{if(!bridge.RestoreSucceeded)result.Blocked=true;}}if(result.Blocked)throw new Exception("Ponte de fechamento não confirmou remoção; reinicie esse cliente");}for(int i=0;i<20;i++){using(var state=Open(character))if(dialogs.All(d=>!state.QuestWindowVisible(d)&&(!state.IsNpcDialog(d)||!state.NpcInteractionActive(d))))return;Thread.Sleep(50);}throw new Exception("Janela ou interação com NPC ainda ativa no cliente");}
 static Result Mirror(CharacterRef character,Request request,CancellationToken token,IProgress<string> progress){var result=new Result{Pid=character.Pid};var took=Stopwatch.StartNew();try{
  using(var state=Open(character)){bool present=state.ActiveQuestIds().Contains(request.Quest);if(request.Finish&&!present){result.Message=character.Name+": não possui a missão "+request.Quest+" ativa; entrega não enviada nem confirmada.";return result;}if(!request.Finish&&present){result.Confirmed=true;result.Message=character.Name+": já possui a missão "+request.Quest+"; não repetida.";return result;}if(Math.Abs(state.X-request.X)>30||Math.Abs(state.Z-request.Z)>30)throw new Exception("aproxime o personagem do mesmo NPC antes de aceitar a missão no principal.");}
  List<uint> dialogs;using(var state=Open(character))dialogs=state.NpcDialogs(token);if(dialogs.Count==0)throw new Exception("janela interna do NPC indisponível.");int attempt=0;
  var delivered=QuestDelivery.Execute(
   ()=>{using(var state=Open(character))return request.Finish?!state.ActiveQuestIds().Contains(request.Quest):state.ActiveQuestIds().Contains(request.Quest);},
   ()=>{attempt++;progress.Report(character.Name+": missão "+request.Quest+" · tentativa "+attempt+"/3.");Send(character,request.Npc,result);if(token.WaitHandle.WaitOne(50))token.ThrowIfCancellationRequested();},
   ()=>{var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<6000){token.ThrowIfCancellationRequested();using(var state=Open(character))if(dialogs.Any(d=>state.NpcVisible(d)))return true;if(token.WaitHandle.WaitOne(50))token.ThrowIfCancellationRequested();}return false;},
   ()=>Send(character,request.Packet,result),
   ()=>WaitQuest(character,request.Quest,8000,token,!request.Finish),
   ()=>{if(!result.Blocked)CloseInteraction(character,request.Quest,result,CancellationToken.None);},token,()=>!result.Blocked);
  result.Confirmed=delivered.Confirmed;
  if(delivered.Confirmed)result.Message=character.Name+": missão "+request.Quest+(request.Finish?" entregue e confirmada.":" recebida e confirmada.")+(delivered.CloseError==null?(delivered.AlreadyPresent?"":" Janelas fechadas."):" Não foi possível fechar a janela: "+delivered.CloseError)+" ("+took.Elapsed.TotalSeconds.ToString("0.0")+" s)";
  else result.Message=character.Name+": missão "+request.Quest+" não confirmada após 3 tentativas. Confira disponibilidade da missão e proximidade do NPC.";
 }catch(OperationCanceledException){result.Message=character.Name+": missão pendente cancelada.";}catch(Exception e){result.Message=character.Name+": "+e.Message;}return result;} public async Task Stop(){stopping=true;CancelAll();await job;cancel.Dispose();}
}


