using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

// FarmZilla The Classic — perfil do cliente do jogo.
// Só o elementclient.exe analisado do PW The Classic (39d81c21…) é aceito; os endereços estão em GameSignatures.Data.cs e
// ClassicLayout.cs. As assinaturas do Avalon não servem para o Classic, então qualquer outro hash é recusado.
public sealed class GameProfile {
 public const string KnownSha256="39d81c21ca72f373a787c936da1611281593d950668f2077016d77c1424338ce";
 // Atualização The Classic de 17/11/2026: perfil separado obtido comparando os dois executáveis.
 public const string UpdatedSha256="2987ee326956e694391c35e11f94e674f38cf6395cd7188359ec57b32daa26f8";
 public const uint KnownSizeOfImage=0xD66000;
 public readonly uint HostOffset=ClassicLayout.RunHost,CharacterIdOffset=ClassicLayout.HostId,FollowManagerOffset=ClassicLayout.RunQueue;
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

 static GameProfile updated;
 // RVAs da atualização de 17/11/2026, comparados por blocos de código e referências globais.
 // O perfil original permanece intocado. O processo atualizado ainda deve passar pela guarda em memória.
 public static GameProfile Updated(){
  if(updated!=null)return updated;
  var values=new Dictionary<string,uint>();foreach(var t in GameSignatures.Targets)values[t.Name]=t.Rva;
  var moved=new Dictionary<string,uint>{
   {"FollowPost",0x425150u},{"SendData",0x4C0DC0u},{"NpcClose",0x75B2B0u},
   {"NpcCancel",0x282020u},{"TaskLookup",0x52D120u},{"HintCommand",0x309A60u},
   {"TaskConfirmRefresh",0x3095A0u},{"TaskConfirmCommand",0x309440u},
   {"AwardSelect",0x1AAE80u},{"AwardConfirm",0x1AAC10u},
   {"AutoPolicyGet",0x44A990u},{"AutoPolicyStop",0x44AC20u},
   {"AutoPolicyChange",0x44ACD0u},{"Navigate",0x41A790u},
   {"Root",0xCA3B4Cu},{"RotaRoot",0xCA57ACu},
   {"TeleportTable",0xCB69B0u},{"TeleportCount",0xCB69BCu},
   {"AutoKillName",0xA21A50u},{"FollowWork",0x9E8208u},{"ComboSkill",0x9E6ADCu},
   {"DlgNpc",0x9FFBE0u},{"DlgTask",0xA06CB8u},{"DlgTaskConfirm",0xA06DA4u},
   {"DlgTaskList",0xA06ED4u},{"Team",0x9EC2DCu},{"TeamMember",0x9EC2D4u},
   {"DlgMessageBox",0x9FF0E4u},{"TaskInterface",0xA2AA9Cu},
   {"ItemTransmitScroll",0x9EDCF0u},{"ItemTaskDice",0x9EF398u},{"Pet",0x9ECB04u}
  };
  foreach(var pair in moved)values[pair.Key]=pair.Value;
  return updated=new GameProfile(UpdatedSha256,true,KnownSizeOfImage,values,null);
 }
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
  // A atualização conserva SizeOfImage, mas RVAs antigos já não apontam todos para inícios de funções.
  // Não converter hash novo em perfil Known: isso permitiria operações de memória com endereços inválidos.
  if(sha==UpdatedSha256){
   PeImage pe;
   try{pe=PeImage.Load(file);}catch(Exception ex){return new Entry{Failure="ElementClient atualizado: PE inválido ("+ex.Message+")."};}
   if(pe.SizeOfImage!=KnownSizeOfImage||pe.ImageBase!=0x400000u)return new Entry{Failure="Imagem do ElementClient atualizado diferente da imagem mapeada."};
   return new Entry{Profile=Updated()};
  }
  return new Entry{Failure="SHA-256 "+sha+": não é o elementclient.exe do PW The Classic analisado (39d81c21…)."};}

 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr p,IntPtr a,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 // Perfil do processo; lança "Cliente diferente do perfil analisado." se não houver.
 public static GameProfile Require(Process process){string failure;var p=TryFor(process,out failure);if(p==null){string reason=String.IsNullOrWhiteSpace(failure)?"verificação de compatibilidade sem diagnóstico":failure;Log("PID "+process.Id+": perfil recusado em memória: "+reason);throw new InvalidDataException("Cliente não validado para comandos (PID "+process.Id+"): "+reason);}return p;}
 public static GameProfile Require(int pid){using(var p=Process.GetProcessById(pid))return Require(p);}
 public static GameProfile TryFor(Process process,out string failure){
  var module=process.MainModule;var p=ForFile(module.FileName,out failure);if(p==null)return null;
  if(p.Known){
   if((uint)module.ModuleMemorySize!=p.SizeOfImage){failure="tamanho do módulo carregado diferente do cliente analisado";return null;}
   if(p.Sha256==UpdatedSha256&&!ValidateUpdatedLoaded(process,out failure))return null;
   return p;
  }
  if((uint)module.ModuleMemorySize!=p.SizeOfImage){failure="tamanho do módulo carregado diferente do arquivo";return null;}
  string key=process.Id+"|"+process.StartTime.Ticks+"|"+p.Sha256;bool ok;
  lock(gate){if(confirmed.TryGetValue(key,out ok)){if(!ok)failure="código carregado diferente do arquivo";return ok?p:null;}}
  ok=p.ConfirmLoaded(process.Id,unchecked((uint)module.BaseAddress.ToInt32()),out failure);
  lock(gate){confirmed[key]=ok;}
  if(!ok)Log("Perfil do jogo recusado no PID "+process.Id+": "+failure);
  return ok?p:null;}
 // Leitura somente do estado: impede que o novo hash autorize pontes com objeto/versão incompatível.
 // São exigidos ambos os caminhos do jogo, as vtables e os campos centrais do personagem.
 static uint ReadDword(IntPtr h,uint address){
  var buffer=new byte[4];UIntPtr got;
  if(!ReadProcessMemory(h,new IntPtr(unchecked((int)address)),buffer,new UIntPtr(4),out got)||got.ToUInt32()!=4)
   throw new IOException("Leitura de memória falhou em 0x"+address.ToString("X8"));
  return BitConverter.ToUInt32(buffer,0);
 }
 static bool ValidateUpdatedLoaded(Process process,out string failure){
  failure=null;
  IntPtr h=OpenProcess(0x410,false,process.Id);
  if(h==IntPtr.Zero){failure="Sem permissão de leitura para validar o ElementClient atualizado.";return false;}
  try{
   uint baseAddress=unchecked((uint)process.MainModule.BaseAddress.ToInt32());
   uint run=ReadDword(h,baseAddress+0xCA57ACu);
   uint game=ReadDword(h,baseAddress+0xCA3B4Cu);
   if(run<0x10000u||game<0x10000u||ReadDword(h,game+0x1Cu)!=run)throw new IOException("Ponteiros de jogo inconsistentes.");
   if(ReadDword(h,run)!=baseAddress+0x9E5148u)throw new IOException("Vtable de CECGameRun não corresponde ao Element atualizado.");
   uint host=ReadDword(h,run+ClassicLayout.RunHost);
   if(host<0x10000u||ReadDword(h,host)!=baseAddress+0x9E7E20u)
    throw new IOException("Vtable de CECHostPlayer não corresponde ao Element atualizado.");
   uint id=ReadDword(h,host+ClassicLayout.HostId);
   uint level=ReadDword(h,host+ClassicLayout.HostLevel);
   uint hp=ReadDword(h,host+ClassicLayout.HostHp);
   uint maxHp=ReadDword(h,host+ClassicLayout.HostMaxHp);
   if(id==0||id>Int32.MaxValue||level<1||level>200||maxHp==0||maxHp>100000000||hp>maxHp)
    throw new IOException("Dados de personagem incompatíveis com o perfil atualizado.");
   // Inícios de função: um cliente modificado em memória não pode executar as pontes.
   var checks=new Dictionary<uint,byte[]>{
    {0x425150u,new byte[]{0x55,0x8B,0xEC,0x6A,0xFF}},
    {0x4C0DC0u,new byte[]{0x55,0x8B,0xEC,0x6A,0xFF}},
    {0x75B2B0u,new byte[]{0x55,0x8B,0xEC,0x6A,0xFF}},
    {0x282020u,new byte[]{0x51,0x56,0x57,0x8B,0xF9}}
   };
   foreach(var entry in checks){
    var data=new byte[entry.Value.Length];UIntPtr got;
    if(!ReadProcessMemory(h,new IntPtr(unchecked((int)(baseAddress+entry.Key))),data,new UIntPtr((uint)data.Length),out got)||got.ToUInt32()!=data.Length)
     throw new IOException("Falha na leitura da função 0x"+entry.Key.ToString("X"));
    for(int i=0;i<data.Length;i++)if(data[i]!=entry.Value[i])throw new IOException("Código diferente em 0x"+entry.Key.ToString("X"));
   }
   return true;
  }catch(Exception e){failure="Element atualizado identificado, mas validação de memória bloqueou comandos: "+e.Message;return false;}
  finally{CloseHandle(h);}
 }
 internal bool ConfirmLoaded(int pid,uint moduleBase,out string failure){
  failure=null;IntPtr h=OpenProcess(0x410,false,pid);if(h==IntPtr.Zero){failure="leitura do cliente negada";return false;}
  try{for(int i=0;i<codeNames.Length;i++){uint at=moduleBase+Rva(codeNames[i])+5;var got=new byte[16];UIntPtr n;
    if(!ReadProcessMemory(h,new IntPtr(unchecked((int)at)),got,new UIntPtr(16),out n)||n.ToUInt32()!=16){failure="leitura de "+codeNames[i]+" falhou";return false;}
    for(int k=0;k<16;k++)if(code[2*i+1][k]!=0&&got[k]!=code[2*i][k]){failure="código de "+codeNames[i]+" diferente do arquivo";return false;}}
   return true;}finally{CloseHandle(h);}}
 // Só para autotestes: limpa o cache.
 internal static void ResetCache(){lock(gate){byFile.Clear();confirmed.Clear();}}
}
