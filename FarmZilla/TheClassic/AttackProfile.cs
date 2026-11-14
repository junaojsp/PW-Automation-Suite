using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FarmZilla.TheClassic;

namespace FarmZilla.TheClassic {
 // Classic-only addresses for the attack/macro bridge. Evidence for every value is in PROFILE.md.
 public static class AttackProfile {
  public const uint AttackableQuery=0x10AF70,SelectTarget=0x110590,MacroStart=0x10DCB0,MacroStop=0x10DDA0,ComboSkill=0x9E6B0C,SendData=0x4C0C90;
  public const uint HostMacro=0x1418,HostTarget=0x59C,GameNet=0x20,GameConfig=0x18,MacroConfig=0xFE,FollowReady=0x68;
  public const int MacroCount=8;
  public static readonly ClassicProfile.Anchor[] Anchors={
   new ClassicProfile.Anchor(0x10AF70,0x51,0x53,0x55,0x56,0x57,0x8B,0xF9,0xF6,0x87,0x04,0x0B,0x00,0x00,0x0B),
   new ClassicProfile.Anchor(0x10AFBD,0x3B,0x9F,0xB8,0x04,0x00,0x00),
   new ClassicProfile.Anchor(0x10B09B,0xC2,0x08,0x00),
   new ClassicProfile.Anchor(0x110590,0x51,0x53,0x56,0x57,0x6A,0x12,0x8B,0xF9),
   new ClassicProfile.Anchor(0x110614,0x89,0xB0,0x9C,0x05,0x00,0x00),
   new ClassicProfile.Anchor(0x11067E,0xC2,0x04,0x00),
   new ClassicProfile.Anchor(0x10DCB0,0x51,0x56,0x8B,0xF1,0x8B,0x8E,0x18,0x14,0x00,0x00),
   new ClassicProfile.Anchor(0x10DCEF,0xC7,0x01,0x0C,0x6B,0xDE,0x00),
   new ClassicProfile.Anchor(0x10DD55,0xFF,0xB6,0x9C,0x05,0x00,0x00),
   new ClassicProfile.Anchor(0x10DD87,0xC2,0x0C,0x00),
   new ClassicProfile.Anchor(0x10DDA0,0x56,0x8B,0xF1,0x8B,0x8E,0x18,0x14,0x00,0x00,0x85,0xC9,0x74,0x10,0x8B,0x01,0x6A,0x01,0xFF,0x10,0xC7,0x86,0x18,0x14,0x00,0x00,0x00,0x00,0x00,0x00,0x5E,0xC3),
   new ClassicProfile.Anchor(0xD23C0,0x56,0x8B,0x74,0x24,0x0C,0x83,0xFE,0x07),
   new ClassicProfile.Anchor(0xD23D4,0x89,0x41,0x04),
   new ClassicProfile.Anchor(0xD23DB,0x89,0x41,0x2C),
   new ClassicProfile.Anchor(0xD23EF,0x89,0x71,0x1C),
   new ClassicProfile.Anchor(0xD2408,0x0F,0x10,0x84,0x10,0xFE,0x00,0x00,0x00),
   new ClassicProfile.Anchor(0xD24F0,0xFF,0x41,0x20),
   new ClassicProfile.Anchor(0xD2524,0xC6,0x41,0x24,0x01),
   new ClassicProfile.Anchor(0x4C0C90,0x55,0x8B,0xEC,0x6A,0xFF,0x68),
   new ClassicProfile.Anchor(0x4C0D6D,0xC2,0x08,0x00),
   new ClassicProfile.Anchor(0xE5343,0xB8,0x2A,0x00,0x00,0x00,0x6A,0x02,0x66,0x89,0x06,0x56,0x8B,0x49,0x20,0xE8)
  };
  public static void ValidateFile(PeImage pe){foreach(var a in Anchors)if((ulong)a.Rva+(uint)a.Bytes.Length>pe.SizeOfImage||!a.Bytes.SequenceEqual(pe.Img.Skip((int)a.Rva).Take(a.Bytes.Length)))throw new InvalidDataException("Guarda de ataque Classic difere do arquivo em RVA 0x"+a.Rva.ToString("X")+".");}
  // The reference client has no relocation table, so the absolute vtable inside an anchor is only valid at its preferred base.
  public static void Validate(IReadMemory memory,uint image,PeImage pe){
   if(image!=pe.ImageBase)throw new InvalidDataException("Cliente Classic carregado fora da base esperada.");
   ValidateFile(pe);
   foreach(var a in Anchors){var actual=memory.Read(image+a.Rva,a.Bytes.Length);if(actual==null||!actual.SequenceEqual(a.Bytes))throw new InvalidDataException("Guarda de ataque Classic alterada em memória (RVA 0x"+a.Rva.ToString("X")+").");}
  }
 }
 // Full validation (hash, header, reader, follow and attack guards) once per client process; the attack loop runs every 300 ms.
 public static class ClassicClient {
  static readonly object gate=new object();static readonly HashSet<string> validated=new HashSet<string>();
  public static uint Require(Process process){
   uint image=unchecked((uint)process.MainModule.BaseAddress.ToInt32());string key=process.Id+"|"+process.StartTime.ToUniversalTime().Ticks;
   lock(gate)if(validated.Contains(key))return image;
   var report=LiveReader.Inspect(process);
   if(!report.ReferenceMatch||!report.HeaderMatches||report.Character==null)throw new InvalidDataException("Perfil Classic recusado: "+report.Status+"; "+report.CharacterStatus);
   var pe=PeImage.Load(Analyzer.ReadFile(process.MainModule.FileName));
   using(var memory=new ReadOnlyProcess(process.Id)){FollowGuards.Validate(memory,image,pe);AttackProfile.Validate(memory,image,pe);}
   lock(gate)validated.Add(key);return image;
  }
  public static uint Host(Func<uint,uint> read,uint image){uint game=read(image+ClassicProfile.GameGlobal),run=read(game+ClassicProfile.GameRunOffset);return read(run+ClassicProfile.HostOffset);}
 }
}

public sealed class CharacterRef {
 public int Pid;public uint Id;public string Name;
 public string Key {get{return Pid+":"+Id;}}
}

// Same members the shared AttackController/NativeAttackRuntime use, read with the Classic offsets.
sealed partial class CharacterMemory : IDisposable {
 readonly ReadOnlyProcess memory;public uint Module,Host,Id;
 public CharacterMemory(int pid){using(var p=Process.GetProcessById(pid)){Module=ClassicClient.Require(p);memory=new ReadOnlyProcess(pid);}try{Host=ClassicClient.Host(U,Module);Id=U(Host+ClassicProfile.Id);if(Id<2)throw new Exception("Personagem ainda não entrou no mundo.");}catch{Dispose();throw;}}
 public uint U(uint at){return BitConverter.ToUInt32(memory.Read(at,4),0);}
 public byte[] Read(uint at,int count){return memory.Read(at,count);}
 public uint SelectedTarget {get{return U(Host+AttackProfile.HostTarget);}}
 uint Config(){return U(U(Module+ClassicProfile.GameGlobal)+AttackProfile.GameConfig);}
 public bool MacroConfigured(int n){if(n<0||n>=AttackProfile.MacroCount)return false;byte[] b=Read(Config()+AttackProfile.MacroConfig+(uint)(17*n),17);return Enumerable.Range(0,8).Any(i=>{short id=BitConverter.ToInt16(b,1+2*i);return id>0||id==-1;});}
 public bool AttackConfigured(){return MacroConfigured(0);}
 public uint RunningMacro(){uint combo=U(Host+AttackProfile.HostMacro);if(combo==0)return 0;if(U(combo)!=Module+AttackProfile.ComboSkill||U(combo+4)!=Host)throw new Exception("Estado da macro indisponível.");return combo;}
 public bool AnyMacroRunning(){uint combo=RunningMacro();return combo!=0&&Read(combo+0x24,1)[0]==0;}
 public bool OwnedMacroRunning(uint combo,int index){return combo!=0&&RunningMacro()==combo&&U(combo+0x1C)==(uint)index&&Read(combo+0x24,1)[0]==0;}
 public bool OwnedAttackRunning(uint combo,uint target){return combo!=0&&RunningMacro()==combo&&U(combo+0x1C)==0&&U(combo+0x2C)==target&&Read(combo+0x24,1)[0]==0;}
 public void Dispose(){if(memory!=null)memory.Dispose();}
}

sealed class State : IDisposable {
 public int Pid;public uint Id,Target;public bool Following;
 public State(int pid){Pid=pid;using(var p=Process.GetProcessById(pid)){uint image=ClassicClient.Require(p);using(var memory=new ReadOnlyProcess(pid)){Func<uint,uint> read=at=>BitConverter.ToUInt32(memory.Read(at,4),0);uint host=ClassicClient.Host(read,image);Id=read(host+ClassicProfile.Id);if(Id<2)throw new Exception("Entre no personagem antes de conectar.");Target=FollowState.Target(memory,image,host);Following=Target!=0;}}}
 public void Dispose(){}
}
