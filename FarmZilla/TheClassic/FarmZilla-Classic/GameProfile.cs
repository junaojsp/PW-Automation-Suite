using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

// FarmZilla 0.50.0 — perfil do cliente do jogo.
// Hash conhecido (34fca82c…): usa os endereços validados de sempre, sem procurar nada.
// Hash desconhecido: lê o ElementClient.exe do disco (o mesmo arquivo do processo), procura todas as assinaturas
// (GameSignatures) e só aceita se TODAS resolverem de forma única e as guardas de deslocamento conferirem; depois
// confirma no módulo carregado (leitura de memória) que o código nos endereços achados é o mesmo do arquivo.
// Qualquer falha = "Cliente diferente do perfil analisado." como antes.
public sealed class GameProfile {
 public const string KnownSha256="34fca82cafa1f38825228626aa3e8f98c56274c095cb46e317b2d665aaf69684";
 public const uint KnownSizeOfImage=19456000;
 public readonly uint HostOffset=0x34,CharacterIdOffset=0x4F0,FollowManagerOffset=0x74;
 public readonly string Sha256;public readonly bool Known;public readonly uint SizeOfImage;readonly Dictionary<string,uint> map;readonly byte[][] code;
 GameProfile(string sha,bool known,uint size,Dictionary<string,uint> values,byte[][] codeBytes){Sha256=sha;Known=known;SizeOfImage=size;map=values;code=codeBytes;
  FollowPost=Rva("FollowPost");AttackSelect=Rva("AttackSelect");AttackNormal=Rva("AttackNormal");MacroStart=Rva("MacroStart");MacroStop=Rva("MacroStop");SendData=Rva("SendData");NpcClose=Rva("NpcClose");NpcCancel=Rva("NpcCancel");Root=Rva("Root");RotaRoot=Rva("RotaRoot");TeleportTable=Rva("TeleportTable");TeleportCount=Rva("TeleportCount");FollowWork=Rva("FollowWork");ComboSkill=Rva("ComboSkill");DlgNpc=Rva("DlgNpc");DlgTask=Rva("DlgTask");DlgTaskConfirm=Rva("DlgTaskConfirm");DlgTaskList=Rva("DlgTaskList");Team=Rva("Team");TeamMember=Rva("TeamMember");DlgMessageBox=Rva("DlgMessageBox");TaskInterface=Rva("TaskInterface");ItemTransmitScroll=Rva("ItemTransmitScroll");ItemTaskDice=Rva("ItemTaskDice");}
 string[] codeNames;
 public uint Rva(string name){uint v;if(!map.TryGetValue(name,out v))throw new Exception("Perfil sem o endereço "+name+".");return v;}
 public string Describe(){return Known?"perfil validado":"perfil por assinaturas ("+map.Count+" endereços)";}
 // Endereços (RVA) usados pelas pontes e leituras; campos para não pesar nos laços de varredura.
 public readonly uint FollowPost,AttackSelect,AttackNormal,MacroStart,MacroStop,SendData,NpcClose,NpcCancel,Root,RotaRoot,TeleportTable,TeleportCount,FollowWork,ComboSkill,DlgNpc,DlgTask,DlgTaskConfirm,DlgTaskList,Team,TeamMember,DlgMessageBox,TaskInterface,ItemTransmitScroll,ItemTaskDice;
 // Ordem fixa dos endereços entregues à ponte nativa (RotaNativeBridge.c, Request.rva[]). Não reordenar sem mudar a DLL.
 public static readonly string[] NativeOrder={"RotaRoot","Root","TraceMatter","TaskLookup","HintCommand","TaskConfirmRefresh","TaskConfirmCommand","AwardSelect","AwardConfirm","FollowPost","KeyMove","AutoPolicyGet","AutoPolicyChange","AutoPolicyStop","AutoKillName","EmptyString","Navigate"};
 public uint[] NativeTable(){var t=new uint[NativeOrder.Length];for(int i=0;i<t.Length;i++)t[i]=Rva(NativeOrder[i]);return t;}

 sealed class Entry {public GameProfile Profile;public string Failure;}
 static readonly object gate=new object();
 static readonly Dictionary<string,Entry> byFile=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
 static readonly Dictionary<string,bool> confirmed=new Dictionary<string,bool>();
 internal static Action<string> Log=delegate(string s){DiagnosticLog.Write(s);};

 static GameProfile current;
 public static GameProfile Current(){var c=current;if(c!=null)return c;var values=new Dictionary<string,uint>();foreach(var t in GameSignatures.Targets)values[t.Name]=t.Rva;return current=new GameProfile(KnownSha256,true,KnownSizeOfImage,values,null);}
 // Perfil para um arquivo; null com o motivo em failure. Guarda o resultado enquanto tamanho e data do arquivo não mudarem.
 public static GameProfile ForFile(string path,out string failure){
  var info=new FileInfo(path);string key=info.FullName+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks;Entry e;
  lock(gate){if(byFile.TryGetValue(key,out e)){failure=e.Failure;return e.Profile;}}
  e=Build(File.ReadAllBytes(info.FullName));
  lock(gate){byFile[key]=e;}
  if(e.Profile!=null&&!e.Profile.Known)Log("Perfil do jogo por assinaturas aceito: SHA-256 "+e.Profile.Sha256+".");
  else if(e.Profile==null)Log("Perfil do jogo recusado ("+Path.GetFileName(path)+"): "+e.Failure);
  failure=e.Failure;return e.Profile;}
 static Entry Build(byte[] file){
  string sha;using(var h=SHA256.Create())sha=BitConverter.ToString(h.ComputeHash(file)).Replace("-","").ToLowerInvariant();
  if(sha==KnownSha256)return new Entry{Profile=Current()};
  PeImage pe;try{pe=PeImage.Load(file);}catch(Exception ex){return new Entry{Failure="SHA-256 "+sha+": "+ex.Message};}
  var r=SignatureScanner.Resolve(pe);
  if(!r.Ok)return new Entry{Failure="SHA-256 "+sha+": "+r.Errors.Count+" assinatura(s) sem resultado único — "+string.Join("; ",r.Errors.ToArray())};
  // Bytes de código esperados (do arquivo) para conferir no módulo carregado: 16 bytes a partir de +5 em cada função
  // (os 5 primeiros podem estar com o desvio da própria captura do FarmZilla), relocações ignoradas.
  var names=new List<string>();foreach(var t in GameSignatures.Targets)if(t.Sigs[0].Kind==GameSignatures.Kind.Fn||t.Sigs[0].Kind==GameSignatures.Kind.Rel)names.Add(t.Name);
  var codeBytes=new byte[names.Count*2][];
  for(int i=0;i<names.Count;i++){uint at=r.Rva[names[i]]+5;var b=new byte[16];var m=new byte[16];Buffer.BlockCopy(pe.Img,(int)at,b,0,16);
   for(uint k=0;k<16;k++){bool rel=false;for(uint z=0;z<4;z++)if(at+k>=z&&pe.Relocs.Contains(at+k-z))rel=true;m[k]=(byte)(rel?0:1);}
   codeBytes[2*i]=b;codeBytes[2*i+1]=m;}
  var p=new GameProfile(sha,false,pe.SizeOfImage,r.Rva,codeBytes);p.codeNames=names.ToArray();return new Entry{Profile=p};}

 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr p,IntPtr a,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 // Perfil do processo; lança "Cliente diferente do perfil analisado." se não houver.
 public static GameProfile Require(Process process){string failure;var p=TryFor(process,out failure);if(p==null)throw new Exception("Cliente diferente do perfil analisado.");return p;}
 public static GameProfile Require(int pid){using(var p=Process.GetProcessById(pid))return Require(p);}
 public static GameProfile TryFor(Process process,out string failure){
  var module=process.MainModule;var p=ForFile(module.FileName,out failure);if(p==null)return null;
  if(p.Known)return p;
  if((uint)module.ModuleMemorySize!=p.SizeOfImage){failure="tamanho do módulo carregado diferente do arquivo";return null;}
  string key=process.Id+"|"+process.StartTime.Ticks+"|"+p.Sha256;bool ok;
  lock(gate){if(confirmed.TryGetValue(key,out ok)){if(!ok)failure="código carregado diferente do arquivo";return ok?p:null;}}
  ok=p.ConfirmLoaded(process.Id,unchecked((uint)module.BaseAddress.ToInt32()),out failure);
  lock(gate){confirmed[key]=ok;}
  if(!ok)Log("Perfil do jogo recusado no PID "+process.Id+": "+failure);
  return ok?p:null;}
 internal bool ConfirmLoaded(int pid,uint moduleBase,out string failure){
  failure=null;IntPtr h=OpenProcess(0x410,false,pid);if(h==IntPtr.Zero){failure="leitura do cliente negada";return false;}
  try{for(int i=0;i<codeNames.Length;i++){uint at=moduleBase+Rva(codeNames[i])+5;var got=new byte[16];UIntPtr n;
    if(!ReadProcessMemory(h,new IntPtr(unchecked((int)at)),got,new UIntPtr(16),out n)||n.ToUInt32()!=16){failure="leitura de "+codeNames[i]+" falhou";return false;}
    for(int k=0;k<16;k++)if(code[2*i+1][k]!=0&&got[k]!=code[2*i][k]){failure="código de "+codeNames[i]+" diferente do arquivo";return false;}}
   return true;}finally{CloseHandle(h);}}
 // Só para autotestes: limpa o cache.
 internal static void ResetCache(){lock(gate){byFile.Clear();confirmed.Clear();}}
}
