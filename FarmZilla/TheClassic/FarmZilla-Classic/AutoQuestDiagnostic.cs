using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// The Classic: read-only, explicitly started quest tracing. No injected hooks or game commands.
// Diagnostic files stay local and are never added to the general telemetry upload queue.
sealed class AutoQuestDiagnostic : IDisposable {
 readonly int pid;readonly uint characterId;readonly string characterName;
 readonly object fileLock=new object();readonly CancellationTokenSource stop=new CancellationTokenSource();
 Task worker;int lines;bool disposed;
 public string FilePath {get;private set;}
 public bool Running {get{return worker!=null&&!worker.IsCompleted;}}
 public AutoQuestDiagnostic(int processId,uint id,string name){
  pid=processId;characterId=id;characterName=name;
  string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FarmZillaClassic","diagnosticos");
  Directory.CreateDirectory(folder);
  FilePath=Path.Combine(folder,"autoquest-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+pid+".txt");
 }
 void Log(string message){
  lock(fileLock){if(lines>=1600)return;
   File.AppendAllText(FilePath,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+" | "+message+Environment.NewLine,Encoding.UTF8);
   lines++;
  }
 }
 public void Start(){
  if(worker!=null)throw new InvalidOperationException("Diagnóstico já iniciado.");
  Log("AUTO QUEST THE CLASSIC - diagnóstico SOMENTE LEITURA - FarmZilla "+ReleaseInfo.Version);
  Log("INICIO; personagem="+characterName+"; PID="+pid+"; id="+characterId+"; duração máxima 240 s; amostragem 1 s");
  Log("Contagem dos IDs internos NÃO representa o contador de missões iniciadas da interface.");
  Log("Eventos observados: posição XYZ, voo, estado de movimento, alvo selecionado, interação NPC, janelas de missão e variação dos IDs internos.");
  worker=Task.Run(()=>Run(stop.Token));
 }
 sealed class Snapshot {
  public string Name,Flight,Action;
  public uint Target,NpcContext;public byte NpcBusy;
  public float X,Y,Z;public int Hp,MaxHp,Level;
  public bool NpcWindow,TaskConfirmWindow;
  public ushort[] RawQuestIds;public uint[] ConfirmRows;
 }
 static bool VisibleDialog(CharacterMemory m,uint run,uint uiOffset,uint expectedVtable){
  try{
   uint ui=m.U(run+ClassicLayout.RunUi);if(ui<0x10000)return false;
   uint manager=m.U(ui+8);if(manager<0x10000)return false;
   uint dlg=m.U(manager+uiOffset);
   return dlg>=0x10000&&m.U(dlg)==m.Module+expectedVtable&&m.Read(dlg+0x90,1)[0]!=0;
  }catch{return false;}
 }
 static uint ReadU(CharacterMemory m,uint at){try{return m.U(at);}catch{return 0;}}
 static byte ReadB(CharacterMemory m,uint at){try{return m.Read(at,1)[0];}catch{return 0;}}
 Snapshot Read(){
  ushort[] ids;uint target,context;byte npcBusy;bool dialog,confirm;
  using(var m=new CharacterMemory(pid)){
   if(m.Id!=characterId||m.Name!=characterName)throw new InvalidOperationException("Personagem alterado: interromper diagnóstico.");
   ids=m.ActiveQuestIds();target=ReadU(m,m.Host+ClassicLayout.HostTarget);
   context=ReadU(m,m.Host+ClassicLayout.HostNpcTalk);npcBusy=ReadB(m,m.Host+ClassicLayout.HostNpcBusy);
   uint root=m.U(m.Module+m.G.Root),run=m.U(root+0x1C);
   dialog=VisibleDialog(m,run,ClassicLayout.UiNpcTalk,m.G.DlgNpc);
   confirm=VisibleDialog(m,run,ClassicLayout.UiTaskConfirm,m.G.DlgTaskConfirm);
  }
  using(var game=new RotaAvalon.GameClient(pid,new Dictionary<string,bool>())){
   var s=game.Read();if(s.Name!=characterName)throw new InvalidOperationException("Identidade do personagem inconsistente.");
   string action="indisponível";try{action=game.ReadActivity().Kind.ToString();}catch{}
   uint[] confirmRows=new uint[0];try{confirmRows=game.ReadConfirmRows().Take(20).ToArray();}catch{}
   return new Snapshot{Name=s.Name,Level=s.Level,Hp=s.Hp,MaxHp=s.MaxHp,
    X=s.X,Y=s.Y,Z=s.Z,Target=target,NpcContext=context,NpcBusy=npcBusy,
    NpcWindow=dialog,TaskConfirmWindow=confirm,RawQuestIds=ids,
    ConfirmRows=confirmRows,Flight=s.FlightStateKnown?(s.IsFlying?"voando":"solo"):"estado de voo não validado",
    Action=action};
  }
 }
 static string Coords(Snapshot s){return s.X.ToString("0.0")+","+s.Y.ToString("0.0")+","+s.Z.ToString("0.0");}
 static string Hex(uint value){return "0x"+value.ToString("X8");}
 void Observe(Snapshot before,Snapshot now,int secondsSinceLastPos){
  if(before==null){
   Log("INICIAL; nivel="+now.Level+"; HP="+now.Hp+"/"+now.MaxHp+"; XYZ="+Coords(now)+"; voo="+now.Flight+
    "; ação="+now.Action+"; alvo="+Hex(now.Target)+"; contexto NPC="+Hex(now.NpcContext)+
    "; janela NPC="+now.NpcWindow+"; janela entrega="+now.TaskConfirmWindow+
    "; IDs internos ("+now.RawQuestIds.Length+")="+String.Join(",",now.RawQuestIds));
   return;
  }
  if(before.Target!=now.Target)Log("ALVO ALTERADO; "+Hex(before.Target)+" -> "+Hex(now.Target)+"; XYZ="+Coords(now));
  if(before.NpcContext!=now.NpcContext||before.NpcBusy!=now.NpcBusy||before.NpcWindow!=now.NpcWindow)
   Log("INTERAÇÃO NPC; contexto "+Hex(before.NpcContext)+" -> "+Hex(now.NpcContext)+
    "; busy "+before.NpcBusy+" -> "+now.NpcBusy+"; janela "+before.NpcWindow+" -> "+now.NpcWindow+
    "; XYZ="+Coords(now));
  if(before.TaskConfirmWindow!=now.TaskConfirmWindow)
   Log("JANELA ENTREGA; "+before.TaskConfirmWindow+" -> "+now.TaskConfirmWindow+"; XYZ="+Coords(now));
  if(before.Flight!=now.Flight)Log("VOO; "+before.Flight+" -> "+now.Flight+"; XYZ="+Coords(now));
  if(before.Action!=now.Action)Log("AÇÃO; "+before.Action+" -> "+now.Action+"; XYZ="+Coords(now));
  if(before.Hp!=now.Hp&&Math.Abs(before.Hp-now.Hp)>Math.Max(30,now.MaxHp/20))
   Log("HP; "+before.Hp+" -> "+now.Hp+"; XYZ="+Coords(now));
  if(!before.ConfirmRows.SequenceEqual(now.ConfirmRows))
   Log("LINHAS DA JANELA DE ENTREGA (IDs brutos); "+String.Join(",",now.ConfirmRows));
  var oldIds=new HashSet<ushort>(before.RawQuestIds),newIds=new HashSet<ushort>(now.RawQuestIds);
  var added=newIds.Except(oldIds).OrderBy(x=>x).ToArray(),removed=oldIds.Except(newIds).OrderBy(x=>x).ToArray();
  if(added.Length>0||removed.Length>0)
   Log("REGISTROS DE MISSÃO ALTERADOS; adicionados="+String.Join(",",added)+"; removidos="+String.Join(",",removed)+
    "; total interno="+now.RawQuestIds.Length+"; XYZ="+Coords(now));
 }
 void Run(CancellationToken token){
  Snapshot previous=null;DateTime lastPosition=DateTime.MinValue;int errors=0;
  var watch=Stopwatch.StartNew();
  try{
   while(watch.Elapsed.TotalSeconds<240&&!token.IsCancellationRequested){
    try{
     Snapshot current=Read();errors=0;
     Observe(previous,current,(int)(DateTime.UtcNow-lastPosition).TotalSeconds);
     double dx=previous==null?0:current.X-previous.X,dy=previous==null?0:current.Y-previous.Y,dz=previous==null?0:current.Z-previous.Z;
     if(previous!=null&&(dx*dx+dy*dy+dz*dz>=9)&&(DateTime.UtcNow-lastPosition).TotalSeconds>=3){
      Log("DESLOCAMENTO; XYZ="+Coords(current)+"; voo="+current.Flight+"; ação="+current.Action+
       "; alvo="+Hex(current.Target));lastPosition=DateTime.UtcNow;
     }
     if(previous!=null&&((int)watch.Elapsed.TotalSeconds)%15==0){
      Log("PULSO; XYZ="+Coords(current)+"; missão IDs internos="+current.RawQuestIds.Length+
       "; contexto NPC="+Hex(current.NpcContext)+"; ação="+current.Action);
     }
     previous=current;
    }catch(Exception ex){
     errors++;if(errors==1||errors%10==0)Log("LEITURA INDISPONÍVEL; "+ex.GetType().Name+": "+ex.Message);
     if(ex is InvalidOperationException||errors>=12)break;
    }
    if(token.WaitHandle.WaitOne(1000))break;
   }
  }catch(Exception ex){try{Log("FALHA DE GRAVAÇÃO; "+ex.Message);}catch{}}
  finally{try{Log("FIM; "+(token.IsCancellationRequested?"parado pelo usuário":watch.Elapsed.TotalSeconds>=240?"limite de 240 s":"leitura interrompida")+
   "; duração="+watch.Elapsed.TotalSeconds.ToString("0")+" s; linhas="+lines);}catch{}}
 }
 public async Task StopAsync(){stop.Cancel();if(worker!=null)await worker;}
 public void Dispose(){if(disposed)return;disposed=true;stop.Cancel();stop.Dispose();}
}
