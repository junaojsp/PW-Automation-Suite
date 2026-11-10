using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class CharacterRef {
 public int Pid;public uint Id;public string Name;
 public string Key {get{return Pid+":"+Id;}}
}
sealed partial class CharacterMemory : IDisposable {
 IntPtr handle;public uint Module,Host,Id;public GameProfile G=GameProfile.Current();public string Name;public float X,Y,Z;
 public CharacterMemory(int pid){try{using(var p=Process.GetProcessById(pid)){G=GameProfile.Require(p);Module=InternalBridge.Addr(p.MainModule.BaseAddress);handle=Win.OpenProcess(0x410,false,pid);if(handle==IntPtr.Zero)throw new Exception("Acesso ao personagem indisponível.");uint root=U(Module+G.Root),run=U(root+0x1C);Host=U(run+0x34);Id=U(Host+0x4F0);if(Id<2)throw new Exception("Personagem ainda não entrou no mundo.");uint name=U(Host+0x748);Name=Encoding.Unicode.GetString(Read(name,64)).Split('\0')[0];if(Name.Length==0||Name.Any(c=>char.IsControl(c)))throw new Exception("Nome do personagem indisponível.");X=F(Host+0x3C);Y=F(Host+0x40);Z=F(Host+0x44);}}catch{Dispose();throw;}}
 // Class (profession) of the character, host+0x74C in the validated Avalon profile; -1 when unreadable.
 public int Profession {get{try{int v=(int)U(Host+0x74C);return v>=0&&v<16?v:-1;}catch{return -1;}}}
 public byte[] Read(uint at,int size){byte[] b=new byte[size];UIntPtr got;if(!Win.ReadProcessMemory(handle,new IntPtr(unchecked((int)at)),b,new UIntPtr((uint)size),out got)||got.ToUInt32()!=size)throw new Exception("Personagem carregando ou desconectado.");return b;}
 public uint U(uint at){return BitConverter.ToUInt32(Read(at,4),0);}float F(uint at){return BitConverter.ToSingle(Read(at,4),0);}
 public Stone FindStone(){return FindStone(36905);}
 // 0.51.18 (registro Esdras 09/11 10:03–10:04: gorlami "Sem Pedra de Teleporte 36905" com o principal usando a 36905): the same
 // stone first, otherwise any of the four teleport stones the character has.
 public Stone FindAnyStone(uint preferred){try{return FindStone(preferred);}catch{}foreach(var id in TeleportService.StoneIds){if(id==preferred)continue;try{return FindStone(id);}catch{}}throw new Exception("Sem Pedra de Teleporte no inventário ("+string.Join(", ",Array.ConvertAll(TeleportService.StoneIds,x=>x.ToString()))+").");}
 public Stone FindStone(uint stoneId){uint inv=U(Host+0x10F8),count=U(inv+0x10),items=U(inv+0xC);if(count>256)throw new Exception("Inventário indisponível.");for(uint i=0;i<count;i++){uint item=U(items+4*i);if(item!=0&&U(item+0xC)==stoneId&&U(item)==Module+G.ItemTransmitScroll){uint n=U(item+0x14);if(n>0)return new Stone{Slot=(ushort)i,Count=n,Id=stoneId};}}throw new Exception("Sem Pedra de Teleporte "+stoneId+" no inventário.");}
 public string DescribeSlot(int slot){uint inv=U(Host+0x10F8),count=U(inv+0x10),items=U(inv+0xC);if(slot<0||slot>=count||count>256)return "slot fora do inventário";uint item=U(items+4*(uint)slot);if(item==0)return "slot vazio";return "item "+U(item+0xC)+", tipo +"+(U(item)-Module).ToString("X")+", campo "+U(item+0x14);}
 public ushort FindSpecialTeleport(){uint inv=U(Host+0x10F8),count=U(inv+0x10),items=U(inv+0xC);if(count>256)throw new Exception("Inventário indisponível.");for(uint i=0;i<count;i++){uint item=U(items+4*i);if(item!=0&&U(item+0xC)==52256&&U(item)==Module+G.ItemTaskDice)return (ushort)i;}throw new Exception("Sem Teleporte Especial no inventário.");}
 public Destination FindDestination(uint id){uint ptr=U(Module+G.TeleportTable),count=U(Module+G.TeleportCount);if(count>4096)throw new Exception("Tabela de destinos inválida.");for(uint i=0;i<count;i++){byte[] b=Read(ptr+i*0x58,0x58);if(BitConverter.ToUInt32(b,0)!=id)continue;var d=new Destination{Id=id,Name=Encoding.Unicode.GetString(b,4,64).Split('\0')[0],X=BitConverter.ToSingle(b,0x48),Y=BitConverter.ToSingle(b,0x4C),Z=BitConverter.ToSingle(b,0x50)};if(float.IsNaN(d.X)||float.IsInfinity(d.X)||float.IsNaN(d.Y)||float.IsInfinity(d.Y)||float.IsNaN(d.Z)||float.IsInfinity(d.Z))throw new Exception("Coordenadas inválidas.");return d;}throw new Exception("Destino não encontrado na tabela deste cliente.");}
 public bool At(Destination d){return Math.Abs(X-d.X)<10&&Math.Abs(Z-d.Z)<10&&Math.Abs(Y-d.Y)<20;}
 public void Dispose(){if(handle!=IntPtr.Zero){Win.CloseHandle(handle);handle=IntPtr.Zero;}}
}
sealed class Stone {public ushort Slot;public uint Count,Id;}
sealed class Destination {public uint Id;public string Name;public float X,Y,Z;}
sealed class QuestChoice {public byte[] Packet;public ushort Quest,Next;public byte Kind;public bool WasActive;public HashSet<ushort> Before;}
sealed class SpecialRequest {public byte[] Use,Point;public bool HasStart;public float X,Y,Z;public ushort PointId {get{return BitConverter.ToUInt16(Point,7);}}}
sealed class TeleportService {
 readonly ConcurrentDictionary<int,byte> activeClients=new ConcurrentDictionary<int,byte>(),unsafeClients=new ConcurrentDictionary<int,byte>();
 public bool IsClientBusy(int pid){return pumping||pending!=null||pendingSpecial!=null||activeClients.ContainsKey(pid)||unsafeClients.ContainsKey(pid);}
 readonly Action<string> note;readonly Action<string> status;readonly HashSet<int> blocked;
 TeleportCapture capture;string selection="",fault="";int capturePid;uint captureId;bool pumping,stopping;int revision;CancellationTokenSource cancel=new CancellationTokenSource();Task job=Task.FromResult(0);byte[] pending;
 public Action<byte[]> OnNpcCommand;public Func<int,bool> ShouldMirror;public Func<bool> QuestJobRunning;
 readonly Queue<QuestChoice> questChoices=new Queue<QuestChoice>();HashSet<ushort> questSnapshot;public Action OnTeleportCaptured;public Func<Task> BeforeTeleport;public long CapturePollVersion {get;private set;}
 public bool Working {get{return !job.IsCompleted||pending!=null||pendingSpecial!=null||pumping;}}
 // Window choices wait in their own queue until the quest service (delivery/acceptance) is idle, so they never run ahead of it.
 public bool QuestChoicePending {get{return questChoices.Count>0;}}
 public Func<bool> QuestQueueBusy;
 SpecialRequest pendingSpecial,specialUse;DateTime specialUseTime;
 public TeleportService(Action<string> log,Action<string> label,HashSet<int> blockedClients){note=log;status=label;blocked=blockedClients;}
 public void SelectionChanged(){revision++;cancel.Cancel();pending=null;pendingSpecial=specialUse=null;questChoices.Clear();questSnapshot=null;}
 public async Task Pump(CharacterRef main,CharacterRef[] secondaries,bool followBusy){if(pumping||stopping)return;pumping=true;int observedRevision=revision;try{
  string key=main==null?"":main.Key+"/"+string.Join(",",secondaries.Select(s=>s.Key).OrderBy(s=>s).ToArray());
  if(key!=selection){selection=key;fault="";cancel.Cancel();pending=null;pendingSpecial=specialUse=null;questChoices.Clear();questSnapshot=null;if(capture!=null)capture.Poll();}
  bool wanted=main!=null&&secondaries.Length>0;
  if(capture!=null&&(!wanted||capturePid!=main.Pid||captureId!=main.Id)){await job;var old=capture;await Task.Run(()=>old.Dispose());capture=null;note("Captura automática removida.");}
  if(wanted&&fault==selection){status("Teleporte indisponível\nConsulte o registro");return;}
  if(!wanted){status("Teleporte e missões\nSelecione os secundários");return;}
  if(capture==null){if(fault==selection){status("Teleporte indisponível\nConsulte o registro");return;}if(followBusy)return;
   try{using(var state=new CharacterMemory(main.Pid))if(state.Id!=main.Id)throw new Exception("O personagem principal mudou. Atualize a lista.");bool all=DiagnosticAll();capture=await Task.Run(()=>new TeleportCapture(main.Pid,0,all));if(all)note("Diagnóstico ampliado ativo: tipos de comando do principal serão registrados (arquivo diagnostico-comandos.txt).");capturePid=main.Pid;captureId=main.Id;if(capture.RecoveredOrphan)note("Captura deixada por uma execução anterior do FarmZilla foi removida do principal.");capture.Poll();note("Teleporte e missões ativos: principal "+main.Name+".");}
   catch(Exception e){fault=selection;note("Captura não iniciada: "+e.Message);status("Teleporte indisponível\nConsulte o registro");return;}
  }
  if(observedRevision!=revision)return;var events=capture.Poll();CapturePollVersion++;foreach(var command in events){if(command.FullLength!=command.Packet.Length){DiagnoseCommand(command);continue;}if(IsStone(command.Packet)){pending=command.Packet;cancel.Cancel();if(OnTeleportCaptured!=null)OnTeleportCaptured();}else if(IsSpecialUse(command.Packet))RememberSpecialUse(main,command.Packet);else if(IsSpecialPoint(command.Packet)){if(specialUse!=null&&(DateTime.UtcNow-specialUseTime).TotalSeconds<=120){specialUse.Point=command.Packet;pendingSpecial=specialUse;specialUse=null;note("Teleporte Especial: destino "+pendingSpecial.PointId+" capturado; aguardando disponibilidade do espelhamento.");cancel.Cancel();if(OnTeleportCaptured!=null)OnTeleportCaptured();}else note("Teleporte Especial: ponto "+BitConverter.ToUInt16(command.Packet,7)+" recebido "+(specialUse==null?"sem uso prévio do item 52256 registrado.":"mais de 120 segundos após o uso do item 52256.")+" Verifique se ambos os comandos foram capturados no principal.");}else if(IsQuestChoice(command.Packet)){ushort quest=BitConverter.ToUInt16(command.Packet,7);if(questSnapshot==null)note("Diagnóstico: escolha em janela (0x31) "+BitConverter.ToString(command.Packet)+" sem lista de missões do principal; não espelhada.");else if(questChoices.Count<4)questChoices.Enqueue(new QuestChoice{Packet=command.Packet,Quest=quest,Kind=command.Packet[6],WasActive=questSnapshot.Contains(quest),Before=new HashSet<ushort>(questSnapshot)});}else if(IsDialogChoice(command.Packet))note("Diagnóstico: escolha em janela no principal (0x31): "+BitConverter.ToString(command.Packet)+". Nada foi espelhado.");else if(IsItemUse(command.Packet))DiagnoseItemUse(main,command.Packet);else if(IsNpcOrQuest(command.Packet)){if(OnNpcCommand!=null)OnNpcCommand(command.Packet);}else DiagnoseCommand(command);}
  RefreshQuestSnapshot(main);
  status("Teleporte e missões\nAtivo · "+secondaries.Length+" secundário(s)");
  if(pending!=null&&!followBusy){byte[] packet=pending;pending=null;var recipients=Recipients(secondaries);if(recipients.Length==0)return;await job;if(BeforeTeleport!=null)await BeforeTeleport();if(observedRevision!=revision||stopping)return;cancel.Dispose();cancel=new CancellationTokenSource();var token=cancel.Token;job=RunBatch(main,recipients,packet,token);}
  else if(pendingSpecial!=null&&!followBusy){var request=pendingSpecial;pendingSpecial=null;var recipients=Recipients(secondaries);if(recipients.Length==0){note("Teleporte Especial: nenhum secundário elegível para espelhamento (verifique Seguir e bloqueios).");return;}note("Teleporte Especial: preparando "+recipients.Length+" secundário(s) para ponto "+request.PointId+".");await job;if(BeforeTeleport!=null)await BeforeTeleport();if(observedRevision!=revision||stopping)return;cancel.Dispose();cancel=new CancellationTokenSource();var token=cancel.Token;job=RunSpecialBatch(main,recipients,request,token);}
  else if(questChoices.Count>0&&!followBusy&&!(QuestQueueBusy!=null&&QuestQueueBusy())){var choice=questChoices.Dequeue();var recipients=secondaries.Where(s=>!blocked.Contains(s.Pid)).ToArray();if(recipients.Length==0)return;await job;if(observedRevision!=revision||stopping)return;cancel.Dispose();cancel=new CancellationTokenSource();var token=cancel.Token;job=RunQuestChoice(main,recipients,choice,token);}
 }catch(Exception e){fault=selection;note("Teleporte: "+e.Message);status("Teleporte interrompido\nConsulte o registro");cancel.Cancel();if(capture!=null){var old=capture;try{old.Dispose();capture=null;}catch(Exception ex){note("Reinicie o cliente principal: restauração não confirmada. "+ex.Message);}}}
 finally{pumping=false;}}
 // Secondaries that take part in a teleport; with "teleportar só quem está seguindo", those with follow off stay.
 CharacterRef[] Recipients(CharacterRef[] secondaries){var available=secondaries.Where(s=>!blocked.Contains(s.Pid)).ToArray();if(ShouldMirror==null)return available;var stay=available.Where(s=>!ShouldMirror(s.Pid)).ToArray();var go=available.Where(s=>ShouldMirror(s.Pid)).ToArray();
  if(stay.Length>0)note(go.Length==0?"Teleporte não espelhado: Seguir desativado nos secundários.":"Teleporte: "+string.Join(", ",stay.Select(s=>s.Name).ToArray())+" ficou (Seguir desativado).");return go;}
 static bool IsQuestChoice(byte[] p){return p.Length==9&&BitConverter.ToUInt16(p,0)==0x31&&BitConverter.ToUInt32(p,2)==3&&(p[6]==3||p[6]==4);}
 // Principal's active quests as of the previous tick: tells whether a window choice really added a new quest.
 void RefreshQuestSnapshot(CharacterRef main){try{using(var s=new CharacterMemory(main.Pid))if(s.Id==main.Id)questSnapshot=new HashSet<ushort>(s.ActiveQuestIds());}catch{}}
 static bool HasQuest(CharacterRef c,ushort quest){using(var s=new CharacterMemory(c.Pid)){if(s.Id!=c.Id)throw new InvalidOperationException("O personagem mudou.");return s.ActiveQuestIds().Contains(quest);}}
 static bool WaitQuest(CharacterRef c,ushort quest,int timeout,CancellationToken token){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<timeout){token.ThrowIfCancellationRequested();try{if(HasQuest(c,quest))return true;}catch(InvalidOperationException){throw;}catch{}if(token.WaitHandle.WaitOne(250))token.ThrowIfCancellationRequested();}return false;}
 // Automatic quest window (chained quests): mirrored only when the quest became active on the principal; each secondary is confirmed by its own quest list.
 async Task RunQuestChoice(CharacterRef main,CharacterRef[] recipients,QuestChoice choice,CancellationToken token){foreach(var r in recipients)activeClients[r.Pid]=0;try{
  string bytes=BitConverter.ToString(choice.Packet);
  if(!choice.WasActive){if(!await Task.Run(()=>WaitQuest(main,choice.Quest,4000,token))){note("Missão "+choice.Quest+": escolha na janela do principal ("+bytes+") não virou missão ativa; não espelhada.");return;}}
  else{ushort next=await Task.Run(()=>WaitAdvance(main,choice,4000,token));if(next==0){note("Missão "+choice.Quest+": escolha na janela do principal ("+bytes+") não avançou para outra etapa; não espelhada (pode ser abandono).");return;}choice.Next=next;}
  var clock=Stopwatch.StartNew();while(QuestJobRunning!=null&&QuestJobRunning()&&clock.ElapsedMilliseconds<20000)await Task.Delay(250,token);
  note(choice.WasActive?"Missão "+choice.Quest+": etapa concluída pela janela no principal (seguinte "+choice.Next+", "+bytes+"); espelhando nos secundários.":"Missão "+choice.Quest+": aceita pela janela automática no principal ("+bytes+"); espelhando nos secundários.");
  var progress=new Progress<string>(s=>note(s));var results=await Task.WhenAll(recipients.Select(s=>Task.Run(()=>{try{var result=MirrorQuestChoice(s,choice,token,(IProgress<string>)progress);if(result.Blocked)unsafeClients[s.Pid]=0;return result;}finally{byte unused;activeClients.TryRemove(s.Pid,out unused);}})));
  int confirmed=0;foreach(var result in results){if(result.Blocked)blocked.Add(result.Pid);if(result.Confirmed)confirmed++;note(result.Message);}note("CHECK missão "+choice.Quest+" (janela automática): "+confirmed+"/"+results.Length+" secundário(s) confirmados.");
 }catch(OperationCanceledException){note("Missão "+choice.Quest+": espelhamento da janela automática cancelado.");}catch(Exception e){note("Missão "+choice.Quest+": "+e.Message);}finally{foreach(var r in recipients){byte unused;activeClients.TryRemove(r.Pid,out unused);}}}
 // Stage finished on the principal: the quest left its list and a quest that was not there before appeared.
 static ushort WaitAdvance(CharacterRef c,QuestChoice choice,int timeout,CancellationToken token){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<timeout){token.ThrowIfCancellationRequested();try{using(var s=new CharacterMemory(c.Pid)){if(s.Id!=c.Id)throw new InvalidOperationException("O personagem mudou.");var now=s.ActiveQuestIds();if(!now.Contains(choice.Quest)){var added=now.Where(q=>!choice.Before.Contains(q)).ToArray();if(added.Length>0)return added[0];}}}catch(InvalidOperationException){throw;}catch{}if(token.WaitHandle.WaitOne(250))token.ThrowIfCancellationRequested();}return 0;}
 static bool WaitGone(CharacterRef c,ushort quest,int timeout,CancellationToken token){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<timeout){token.ThrowIfCancellationRequested();try{if(!HasQuest(c,quest))return true;}catch(InvalidOperationException){throw;}catch{}if(token.WaitHandle.WaitOne(250))token.ThrowIfCancellationRequested();}return false;}
 // The secondary's popup stays open when the command is sent directly; close it once the result is confirmed.
 static void CloseQuestWindow(CharacterRef character,Result result,CancellationToken token){bool blocked;string error=QuestService.CloseWindows(character,token,out blocked);if(blocked)result.Blocked=true;if(error!=null)result.Message+=" Não foi possível fechar a janela: "+error;else result.Message+=" Janela fechada.";}
 static Result MirrorQuestAdvance(CharacterRef character,QuestChoice choice,CancellationToken token,IProgress<string> progress){var result=new Result{Pid=character.Pid};try{
  if(!HasQuest(character,choice.Quest)){result.Confirmed=HasQuest(character,choice.Next);result.Message=character.Name+(result.Confirmed?": já está na etapa "+choice.Next+".":": não tem a missão "+choice.Quest+"; nada enviado.");if(result.Confirmed)CloseQuestWindow(character,result,token);return result;}
  for(int attempt=1;attempt<=3;attempt++){token.ThrowIfCancellationRequested();progress.Report(character.Name+": missão "+choice.Quest+" (janela automática, avançar etapa) · tentativa "+attempt+"/3.");TeleportBridge bridge=null;
   try{bridge=new TeleportBridge(character.Pid);if(bridge.CharacterId!=character.Id)throw new Exception("Personagem mudou durante a operação.");bridge.SendQuestChoice(choice.Packet);}
   finally{if(bridge!=null){try{bridge.Dispose();}finally{if(!bridge.RestoreSucceeded)result.Blocked=true;}}}
   if(result.Blocked)throw new Exception("Ponte não confirmou remoção; reinicie esse cliente.");
   if(WaitGone(character,choice.Quest,3000,token)){result.Confirmed=true;result.Message=character.Name+": missão "+choice.Quest+" concluída pela janela"+(HasQuest(character,choice.Next)?"; etapa "+choice.Next+" ativa.":".");CloseQuestWindow(character,result,token);return result;}
   if(attempt<3&&token.WaitHandle.WaitOne(1000))token.ThrowIfCancellationRequested();}
  result.Message=character.Name+": missão "+choice.Quest+" não avançou após 3 tentativas.";
 }catch(OperationCanceledException){result.Message=character.Name+": tentativa pendente cancelada.";}catch(Exception e){result.Message=character.Name+": "+e.Message;}return result;}
 static Result MirrorQuestChoice(CharacterRef character,QuestChoice choice,CancellationToken token,IProgress<string> progress){var result=new Result{Pid=character.Pid};try{
  if(choice.WasActive)return MirrorQuestAdvance(character,choice,token,progress);
  for(int attempt=1;attempt<=3;attempt++){token.ThrowIfCancellationRequested();if(HasQuest(character,choice.Quest)){result.Confirmed=true;result.Message=character.Name+": missão "+choice.Quest+(attempt==1?" já estava ativa.":" aceita e confirmada.");CloseQuestWindow(character,result,token);return result;}
   progress.Report(character.Name+": missão "+choice.Quest+" (janela automática) · tentativa "+attempt+"/3.");TeleportBridge bridge=null;
   try{bridge=new TeleportBridge(character.Pid);if(bridge.CharacterId!=character.Id)throw new Exception("Personagem mudou durante a operação.");bridge.SendQuestChoice(choice.Packet);}
   finally{if(bridge!=null){try{bridge.Dispose();}finally{if(!bridge.RestoreSucceeded)result.Blocked=true;}}}
   if(result.Blocked)throw new Exception("Ponte não confirmou remoção; reinicie esse cliente.");
   if(WaitQuest(character,choice.Quest,3000,token)){result.Confirmed=true;result.Message=character.Name+": missão "+choice.Quest+" aceita e confirmada.";CloseQuestWindow(character,result,token);return result;}
   if(attempt<3&&token.WaitHandle.WaitOne(1000))token.ThrowIfCancellationRequested();
  }result.Message=character.Name+": missão "+choice.Quest+" não confirmada após 3 tentativas (pode faltar requisito ou a missão anterior).";
 }catch(OperationCanceledException){result.Message=character.Name+": tentativa pendente cancelada.";}catch(Exception e){result.Message=character.Name+": "+e.Message;}return result;}
 static bool IsSpecialUse(byte[] p){return p.Length==10&&BitConverter.ToUInt32(p,0)==0x01000028&&BitConverter.ToUInt32(p,6)==52256;}
 // Only the observed map-point form (31 00 03 00 00 00 01 + point); other 0x31 commands (e.g. quest dialogs) are never treated as teleport.
 static bool IsSpecialPoint(byte[] p){return p.Length==9&&BitConverter.ToUInt16(p,0)==0x31&&BitConverter.ToUInt32(p,2)==3&&p[6]==1;}
 static bool IsDialogChoice(byte[] p){return p.Length>=2&&BitConverter.ToUInt16(p,0)==0x31;}
 void RememberSpecialUse(CharacterRef main,byte[] packet){var r=new SpecialRequest{Use=packet};try{using(var s=new CharacterMemory(main.Pid))if(s.Id==main.Id){r.HasStart=true;r.X=s.X;r.Y=s.Y;r.Z=s.Z;}}catch(Exception e){note("Teleporte Especial: não foi possível registrar posição inicial: "+e.Message);}specialUse=r;specialUseTime=DateTime.UtcNow;note("Teleporte Especial: uso do item 52256 capturado no principal (slot "+BitConverter.ToUInt16(packet,4)+"); aguardando escolha do ponto.");}
 // Teleporte Especial: the point table is not mapped, so the principal's arrival position is the destination for the secondaries.
 async Task RunSpecialBatch(CharacterRef main,CharacterRef[] recipients,SpecialRequest request,CancellationToken token){foreach(var recipient in recipients)activeClients[recipient.Pid]=0;try{
  var destination=await Task.Run(()=>WaitMoved(main,request,12000,token));if(destination==null){note("Teleporte Especial: principal não confirmou deslocamento e estabilização em 12 s para o ponto "+request.PointId+"; nenhum comando enviado aos secundários. Verifique distância do destino e tempo do carregamento.");return;}
  note("Teleporte Especial: principal chegou ao ponto "+request.PointId+".");
  var progress=new Progress<string>(s=>note(s));var results=await Task.WhenAll(recipients.Select(s=>Task.Run(()=>{try{var result=MirrorSpecial(s,destination,request,token,(IProgress<string>)progress);if(result.Blocked)unsafeClients[s.Pid]=0;return result;}finally{byte unused;activeClients.TryRemove(s.Pid,out unused);}})));foreach(var result in results){if(result.Blocked)blocked.Add(result.Pid);note(result.Message);}
 }catch(OperationCanceledException){note("Teleporte Especial: tentativas anteriores canceladas por mudança de seleção ou destino.");}catch(Exception e){note("Teleporte Especial: "+e.Message);}finally{foreach(var recipient in recipients){byte unused;activeClients.TryRemove(recipient.Pid,out unused);}}}
 static Destination WaitMoved(CharacterRef character,SpecialRequest request,int timeout,CancellationToken token){var clock=Stopwatch.StartNew();float lx=float.NaN,ly=0,lz=0;while(clock.ElapsedMilliseconds<timeout){token.ThrowIfCancellationRequested();try{using(var s=new CharacterMemory(character.Pid)){if(s.Id!=character.Id)throw new InvalidOperationException("O personagem mudou.");
    bool moved=!request.HasStart||Math.Abs(s.X-request.X)>30||Math.Abs(s.Z-request.Z)>30;bool steady=!float.IsNaN(lx)&&Math.Abs(s.X-lx)<1&&Math.Abs(s.Z-lz)<1&&Math.Abs(s.Y-ly)<2;
    if(moved&&steady)return new Destination{Id=request.PointId,Name="ponto "+request.PointId,X=s.X,Y=s.Y,Z=s.Z};lx=s.X;ly=s.Y;lz=s.Z;}}catch(InvalidOperationException){throw;}catch{}if(token.WaitHandle.WaitOne(250))token.ThrowIfCancellationRequested();}return null;}
 static bool Near(CharacterMemory s,Destination d){return Math.Abs(s.X-d.X)<20&&Math.Abs(s.Z-d.Z)<20&&Math.Abs(s.Y-d.Y)<30;}
 static bool WaitNear(CharacterRef character,Destination destination,int timeout,CancellationToken token){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<timeout){token.ThrowIfCancellationRequested();try{using(var s=new CharacterMemory(character.Pid)){if(s.Id!=character.Id)throw new InvalidOperationException("O personagem mudou.");if(Near(s,destination))return true;}}catch(InvalidOperationException){throw;}catch{}if(token.WaitHandle.WaitOne(250))token.ThrowIfCancellationRequested();}return false;}
 static Result MirrorSpecial(CharacterRef character,Destination destination,SpecialRequest request,CancellationToken token,IProgress<string> progress){var result=new Result{Pid=character.Pid};try{
  int lastSlot=-1;
  for(int attempt=1;attempt<=3;attempt++){token.ThrowIfCancellationRequested();ushort slot=0;bool found=false;string seen=null;var clock=Stopwatch.StartNew();
   // After a refused point the game holds the item for a few seconds; wait up to 5 s for it to come back.
   while(true){using(var s=new CharacterMemory(character.Pid)){if(s.Id!=character.Id)throw new Exception("Personagem mudou; atualize a lista.");if(Near(s,destination)){result.Message=character.Name+": chegada confirmada ao "+destination.Name+".";return result;}try{slot=s.FindSpecialTeleport();found=true;}catch(Exception){if(lastSlot>=0)seen=s.DescribeSlot(lastSlot);}}
    if(found)break;if(clock.ElapsedMilliseconds>=5000)throw new Exception("Teleporte Especial indisponível no inventário"+(seen!=null?" (slot "+lastSlot+": "+seen+")":"")+".");if(token.WaitHandle.WaitOne(250))token.ThrowIfCancellationRequested();}
   lastSlot=slot;
   progress.Report(character.Name+": Teleporte Especial para o "+destination.Name+" · tentativa "+attempt+"/3.");byte[] use=(byte[])request.Use.Clone();Buffer.BlockCopy(BitConverter.GetBytes(slot),0,use,4,2);TeleportBridge bridge=null;
   try{bridge=new TeleportBridge(character.Pid);if(bridge.CharacterId!=character.Id)throw new Exception("Personagem mudou durante a operação.");bridge.SendSpecial(use);if(token.WaitHandle.WaitOne(1500))token.ThrowIfCancellationRequested();bridge.SendSpecial(request.Point);}
   finally{if(bridge!=null){try{bridge.Dispose();}finally{if(!bridge.RestoreSucceeded)result.Blocked=true;}}}
   if(result.Blocked)throw new Exception("Ponte não confirmou remoção; reinicie esse cliente.");
   if(WaitNear(character,destination,5000,token)){result.Message=character.Name+": chegou ao "+destination.Name+".";return result;}
   if(attempt<3&&token.WaitHandle.WaitOne(500))token.ThrowIfCancellationRequested();
  }result.Message=character.Name+": não chegou ao "+destination.Name+" após 3 tentativas; confira se o ponto está liberado para esse personagem.";
 }catch(OperationCanceledException){result.Message=character.Name+": tentativa pendente cancelada.";}catch(Exception e){result.Message=character.Name+": "+e.Message;}return result;}
 static bool IsNpcOrQuest(byte[] p){return p.Length==6&&BitConverter.ToUInt16(p,0)==0x23||(p.Length==18||p.Length==22)&&BitConverter.ToUInt16(p,0)==0x25;}
 static bool DiagnosticAll(){try{return System.IO.File.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AvalonInterno","diagnostico-comandos.txt"));}catch{return false;}}
 // Diagnostic mode only: one line per command type and size every 30 s, at most 40 lines a minute.
 readonly Dictionary<string,DateTime> lastCommandLog=new Dictionary<string,DateTime>();DateTime commandMinute;int commandLines;
 void DiagnoseCommand(TeleportCapture.CapturedCommand c){if(c.Packet.Length<2)return;ushort type=BitConverter.ToUInt16(c.Packet,0);string key=type+"/"+c.FullLength;DateTime last;if(lastCommandLog.TryGetValue(key,out last)&&(DateTime.UtcNow-last).TotalSeconds<30)return;if((DateTime.UtcNow-commandMinute).TotalSeconds>=60){commandMinute=DateTime.UtcNow;commandLines=0;}if(++commandLines>40)return;lastCommandLog[key]=DateTime.UtcNow;
  byte[] head=new byte[Math.Min(c.Packet.Length,40)];Buffer.BlockCopy(c.Packet,0,head,0,head.Length);note("Diagnóstico comando: tipo 0x"+type.ToString("X2")+", "+c.FullLength+" bytes: "+BitConverter.ToString(head)+(c.FullLength>head.Length?"…":"")+". Nada foi espelhado.");}
 static bool IsItemUse(byte[] p){return p.Length>=10&&BitConverter.ToUInt32(p,0)==0x0100006D;}
 // 0.51.12 (owner): two teleport stones — 36905 and 26651 (same item class, seen in Lima's bag 09/11); each secondary uses the same one the principal used.
 public static readonly uint[] StoneIds={36905,26651,14351,33916};// 0.51.15: as quatro "Pedra de Teleporte" da lista do dono
 static bool IsStone(byte[] p){return p.Length==14&&IsItemUse(p)&&Array.IndexOf(StoneIds,BitConverter.ToUInt32(p,6))>=0;}
 // Diagnosis for items other than the stone (e.g. Teleporte Especial): log the command and the principal's position before/after. Sends nothing.
 readonly Dictionary<string,DateTime> lastItemLog=new Dictionary<string,DateTime>();
 void DiagnoseItemUse(CharacterRef main,byte[] packet){string hex=BitConverter.ToString(packet);DateTime last;if(lastItemLog.TryGetValue(hex,out last)&&(DateTime.UtcNow-last).TotalSeconds<5)return;lastItemLog[hex]=DateTime.UtcNow;
  string where="";try{using(var s=new CharacterMemory(main.Pid))where=" · posição "+Pos(s);}catch{}
  note("Diagnóstico: item "+BitConverter.ToUInt32(packet,6)+" usado no principal, slot "+BitConverter.ToUInt16(packet,4)+", "+packet.Length+" bytes: "+hex+where+". Nada foi espelhado.");
  var progress=new Progress<string>(note);var who=main;Task.Run(()=>{Thread.Sleep(4000);try{using(var s=new CharacterMemory(who.Pid))if(s.Id==who.Id)((IProgress<string>)progress).Report("Diagnóstico: principal 4 s depois do item "+BitConverter.ToUInt32(packet,6)+" · posição "+Pos(s)+".");}catch{}});}
 static string Pos(CharacterMemory s){return s.X.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+", "+s.Y.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+", "+s.Z.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);}
 async Task RunBatch(CharacterRef main,CharacterRef[] recipients,byte[] packet,CancellationToken token){foreach(var recipient in recipients)activeClients[recipient.Pid]=0;try{
  Destination destination;using(var state=new CharacterMemory(main.Pid)){if(state.Id!=main.Id)throw new Exception("Principal mudou. Atualize a lista.");destination=state.FindDestination(BitConverter.ToUInt32(packet,10)&65535);}
  bool primaryArrived=await Task.Run(()=>WaitArrival(main,destination,12000,token));if(!primaryArrived){note("Principal não confirmou chegada a "+destination.Name+"; secundários não receberam comando.");return;}
  note("Destino do principal confirmado: "+destination.Name+".");
  var progress=new Progress<string>(s=>note(s));var results=await Task.WhenAll(recipients.Select(s=>Task.Run(()=>{try{var result=Mirror(s,destination,packet,token,(IProgress<string>)progress);if(result.Blocked)unsafeClients[s.Pid]=0;return result;}finally{byte unused;activeClients.TryRemove(s.Pid,out unused);}})));foreach(var result in results){if(result.Blocked)blocked.Add(result.Pid);note(result.Message);}
 }catch(OperationCanceledException){note("Tentativas anteriores canceladas por mudança de seleção ou destino.");}catch(Exception e){note("Teleporte: "+e.Message);}finally{foreach(var recipient in recipients){byte unused;activeClients.TryRemove(recipient.Pid,out unused);}}}
 static bool WaitArrival(CharacterRef character,Destination destination,int timeout,CancellationToken token){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<timeout){token.ThrowIfCancellationRequested();try{using(var state=new CharacterMemory(character.Pid)){if(state.Id!=character.Id)throw new InvalidOperationException("O personagem mudou.");if(state.At(destination))return true;}}catch(InvalidOperationException){throw;}catch{}if(token.WaitHandle.WaitOne(250))token.ThrowIfCancellationRequested();}return false;}
 sealed class Result {public int Pid;public bool Blocked,Confirmed;public string Message;}
 static Result Mirror(CharacterRef character,Destination destination,byte[] original,CancellationToken token,IProgress<string> progress){var result=new Result{Pid=character.Pid};try{
  for(int attempt=1;attempt<=3;attempt++){token.ThrowIfCancellationRequested();Stone stone;using(var state=new CharacterMemory(character.Pid)){if(state.Id!=character.Id)throw new Exception("Personagem mudou; atualize a lista.");if(state.At(destination)){result.Message=character.Name+": chegada confirmada a "+destination.Name+".";return result;}stone=state.FindAnyStone(BitConverter.ToUInt32(original,6));}
   progress.Report(character.Name+": teleporte para "+destination.Name+" · tentativa "+attempt+"/3.");byte[] packet=(byte[])original.Clone();Buffer.BlockCopy(BitConverter.GetBytes(stone.Slot),0,packet,4,2);Buffer.BlockCopy(BitConverter.GetBytes(stone.Id),0,packet,6,4);token.ThrowIfCancellationRequested();TeleportBridge bridge=null;
   try{bridge=new TeleportBridge(character.Pid);if(bridge.CharacterId!=character.Id)throw new Exception("Personagem mudou durante a operação.");bridge.SendTeleport(packet);}
   finally{if(bridge!=null){try{bridge.Dispose();}finally{if(!bridge.RestoreSucceeded)result.Blocked=true;}}}
   if(result.Blocked)throw new Exception("Ponte não confirmou remoção; reinicie esse cliente.");
   if(WaitArrival(character,destination,8000,token)){result.Message=character.Name+": chegou a "+destination.Name+".";return result;}
   using(var state=new CharacterMemory(character.Pid)){if(state.Id!=character.Id)throw new Exception("Personagem mudou.");Stone after;try{after=state.FindStone(stone.Id);}catch{after=new Stone{Count=0,Id=stone.Id};}if(after.Count<stone.Count){if(WaitArrival(character,destination,10000,token)){result.Message=character.Name+": chegou a "+destination.Name+".";return result;}throw new Exception("Pedra consumida, mas chegada não confirmada. Nova tentativa automática suspensa.");}}
   if(attempt<3&&token.WaitHandle.WaitOne(1500))token.ThrowIfCancellationRequested();
  }result.Message=character.Name+": não chegou após 3 tentativas; confira se o ponto está liberado e se pode teleportar.";
 }catch(OperationCanceledException){result.Message=character.Name+": tentativa pendente cancelada.";}catch(Exception e){result.Message=character.Name+": "+e.Message;}return result;}
 public async Task Stop(){stopping=true;cancel.Cancel();pending=null;pendingSpecial=specialUse=null;questChoices.Clear();while(pumping)await Task.Delay(50);await job;if(capture!=null){var old=capture;await Task.Run(()=>old.Dispose());capture=null;note("Captura automática removida ao fechar o aplicativo.");}cancel.Dispose();}
}
