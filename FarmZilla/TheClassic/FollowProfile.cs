using System;
using System.Diagnostics;
using System.IO;
using FarmZilla.TheClassic;

// Adapter compiled ONLY into the Classic test executable. The Avalon executable
// retains its own GameProfile. The actual InternalBridge source is shared unchanged
// apart from profile-provided field offsets and optional character identity check.
public sealed class GameProfile {
 public readonly uint Root=ClassicProfile.GameGlobal,FollowPost=0x425090,HostOffset=0x28,CharacterIdOffset=0x4B8,FollowManagerOffset=0x68;
 public static GameProfile Current(){return new GameProfile();}
 public static GameProfile Require(Process target){
  var report=LiveReader.Inspect(target);
  if(!report.ReferenceMatch||!report.HeaderMatches||report.Character==null)throw new InvalidDataException("Perfil Classic recusado: "+report.Status+"; "+report.CharacterStatus);
  var pe=PeImage.Load(Analyzer.ReadFile(target.MainModule.FileName));
  using(var memory=new ReadOnlyProcess(target.Id))FollowGuards.Validate(memory,unchecked((uint)target.MainModule.BaseAddress.ToInt32()),pe);
  return Current();
 }
}

namespace FarmZilla.TheClassic {
 public static class FollowGuards {
  public static readonly ClassicProfile.Anchor[] Anchors={
   new ClassicProfile.Anchor(0x425090,0x55,0x8B,0xEC,0x6A,0xFF),
   new ClassicProfile.Anchor(0x312F54,0x8B,0x49,0x68,0xC7,0x45,0xD0,0xEE,0,0,0),
   new ClassicProfile.Anchor(0x312F6C,0x89,0x45,0xDC),
   new ClassicProfile.Anchor(0xFB484,0x89,0x86,0xF8,0x13,0,0),
   new ClassicProfile.Anchor(0x114A7E,0xC7,0x41,0x20,0,0,0,0)
  };
  public static void Validate(IReadMemory memory,uint image,PeImage pe){foreach(var anchor in Anchors){for(int i=0;i<anchor.Bytes.Length;i++)if(pe.Img[anchor.Rva+i]!=anchor.Bytes[i])throw new InvalidDataException("Guarda de seguir Classic difere do arquivo.");var actual=memory.Read(image+anchor.Rva,anchor.Bytes.Length);if(actual.Length!=anchor.Bytes.Length)throw new IOException("Leitura incompleta da guarda de seguir.");for(int i=0;i<actual.Length;i++)if(actual[i]!=anchor.Bytes[i])throw new InvalidDataException("Guarda de seguir Classic alterada em memória.");}}
 }
}
