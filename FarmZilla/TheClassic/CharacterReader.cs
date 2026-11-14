using System;
using System.IO;
using System.Linq;
using System.Text;

namespace FarmZilla.TheClassic {
 public interface IReadMemory {byte[] Read(uint address,int count);}
 public sealed class CharacterSnapshot {
  public string ReadAtUtc,Name,RunAddress,PlayerAddress;
  public uint CharacterId,TargetId;
  public int ClassId,Level,Hp,MaxHp,Mp,MaxMp;
  public float X,Y,Z;
  public string Validation="Leitura coerente; confirmar os valores na tela do jogo";
  public bool AutomationEnabled=false;
 }
 public static class ClassicProfile {
  // Exact uploaded client only. Evidence for every offset is in PROFILE.md.
  public const uint RunGlobal=0xCA579C,RunVtable=0x9E5178,HostVtable=0x9E7E50;
  public const uint GameGlobal=0xCA3B3C,GameRunOffset=0x1C,HostOffset=0x28;
  public const int Id=0x4B8,Level=0x4C4,Hp=0x4CC,Mp=0x4D0,MaxHp=0x518,MaxMp=0x51C,Target=0x59C,Name=0x6D8,Class=0x6DC,Length=0x6E0;
  public sealed class Anchor {public uint Rva;public byte[] Bytes;public Anchor(uint rva,params byte[] bytes){Rva=rva;Bytes=bytes;}}
  public static readonly Anchor[] Anchors={
   new Anchor(0xB3B98,0xA3,0x9C,0x57,0x0A,0x01),
   new Anchor(0xBDC16,0x89,0x46,0x28),
   new Anchor(0x249DC8,0x8B,0x86,0xCC,0x04,0,0,0x8B,0xBE,0xC4,0x04,0,0),
   new Anchor(0x249DDA,0x8B,0x86,0x18,0x05,0,0),
   new Anchor(0x249DEB,0x8B,0x86,0xD0,0x04,0,0),
   new Anchor(0x249DF7,0x8B,0x86,0x1C,0x05,0,0),
   new Anchor(0x249EE1,0x8B,0x8E,0xD8,0x06,0,0),
   new Anchor(0x249F28,0x8B,0x80,0xDC,0x06,0,0),
   new Anchor(0xFB3FC,0x89,0x86,0xB8,0x04,0,0),
   new Anchor(0x10B5BF,0x8B,0x86,0x9C,0x05,0,0),
   new Anchor(0xD281A,0xF3,0x0F,0x10,0x41,0x3C)
  };
  public static void ValidateFile(PeImage pe){
   foreach(var anchor in Anchors){if((ulong)anchor.Rva+(uint)anchor.Bytes.Length>pe.SizeOfImage||!anchor.Bytes.SequenceEqual(pe.Img.Skip((int)anchor.Rva).Take(anchor.Bytes.Length)))throw new InvalidDataException("Código Classic diferente em RVA 0x"+anchor.Rva.ToString("X"));}
  }
  public static void ValidateLoaded(IReadMemory memory,uint imageBase,PeImage pe){
   ValidateFile(pe);
   foreach(var anchor in Anchors){
    var actual=memory.Read(checked(imageBase+anchor.Rva),anchor.Bytes.Length);
    if(actual.Length!=anchor.Bytes.Length)throw new IOException("Leitura incompleta da guarda Classic.");
    var expected=(byte[])anchor.Bytes.Clone();
    foreach(uint r in pe.Relocs.Where(r=>r>=anchor.Rva&&(ulong)r+4<=anchor.Rva+(uint)anchor.Bytes.Length)){
     uint value=unchecked(pe.U32(r)+imageBase-pe.ImageBase);Buffer.BlockCopy(BitConverter.GetBytes(value),0,expected,(int)(r-anchor.Rva),4);
    }
    if(!actual.SequenceEqual(expected))throw new InvalidDataException("Código em memória mudou em RVA 0x"+anchor.Rva.ToString("X"));
   }
  }
 }
 public static class CharacterReader {
  static uint Pointer(uint p){if(p<0x10000||p>0x7FFEFFFF||(p&3)!=0)throw new InvalidDataException("Ponteiro Classic indisponível; entre com o personagem no jogo.");return p;}
  static byte[] Read(IReadMemory memory,uint p,int size){var b=memory.Read(p,size);if(b==null||b.Length!=size)throw new IOException("Leitura incompleta.");return b;}
  static uint U32(IReadMemory memory,uint p){return BitConverter.ToUInt32(Read(memory,p,4),0);}
  static uint Add(uint p,uint offset){return checked(p+offset);}
  static void Vtable(IReadMemory memory,uint p,uint expected){if(U32(memory,p)!=expected)throw new InvalidDataException("Estrutura Classic não corresponde ao tipo esperado.");}
  static bool Coordinate(float value){return !Single.IsNaN(value)&&!Single.IsInfinity(value)&&Math.Abs(value)<100000;}
  static string ReadName(IReadMemory memory,uint pointer){
   Pointer(pointer);var value=new StringBuilder();
   for(uint i=0;i<32;i++){ushort unit=BitConverter.ToUInt16(Read(memory,Add(pointer,i*2),2),0);if(unit==0){if(String.IsNullOrWhiteSpace(value.ToString()))throw new InvalidDataException("Nome vazio.");return value.ToString();}char c=(char)unit;if(Char.IsControl(c)||Char.IsSurrogate(c)||c=='\uFFFD')throw new InvalidDataException("Nome Classic inválido.");value.Append(c);}
   throw new InvalidDataException("Nome sem terminador em 32 caracteres.");
  }
  public static CharacterSnapshot Read(IReadMemory memory,uint imageBase){
   uint run=Pointer(U32(memory,Add(imageBase,ClassicProfile.RunGlobal)));
   uint game=Pointer(U32(memory,Add(imageBase,ClassicProfile.GameGlobal)));
   if(U32(memory,Add(game,ClassicProfile.GameRunOffset))!=run)throw new IOException("Referências Classic não concordam.");
   Vtable(memory,run,Add(imageBase,ClassicProfile.RunVtable));
   uint player=Pointer(U32(memory,Add(run,ClassicProfile.HostOffset)));
   Vtable(memory,player,Add(imageBase,ClassicProfile.HostVtable));
   var data=Read(memory,player,ClassicProfile.Length);
   if(BitConverter.ToUInt32(data,0)!=Add(imageBase,ClassicProfile.HostVtable))throw new IOException("Personagem mudou durante a leitura.");
   var s=Decode(data);
   uint name=Pointer(BitConverter.ToUInt32(data,ClassicProfile.Name));s.Name=ReadName(memory,name);
   // Recheck chain and identity after all reads. Never return a mixed character snapshot.
   if(U32(memory,Add(imageBase,ClassicProfile.RunGlobal))!=run||U32(memory,Add(imageBase,ClassicProfile.GameGlobal))!=game||U32(memory,Add(game,ClassicProfile.GameRunOffset))!=run||U32(memory,Add(run,ClassicProfile.HostOffset))!=player||U32(memory,Add(player,ClassicProfile.Id))!=s.CharacterId||U32(memory,Add(player,ClassicProfile.Name))!=name)throw new IOException("Personagem mudou durante a leitura; tente novamente.");
   Vtable(memory,run,Add(imageBase,ClassicProfile.RunVtable));Vtable(memory,player,Add(imageBase,ClassicProfile.HostVtable));
   s.RunAddress="0x"+run.ToString("X8");s.PlayerAddress="0x"+player.ToString("X8");s.ReadAtUtc=DateTime.UtcNow.ToString("o");return s;
  }
  public static CharacterSnapshot Decode(byte[] data){
   if(data==null||data.Length!=ClassicProfile.Length)throw new InvalidDataException("Bloco de personagem incompleto.");
   var s=new CharacterSnapshot{CharacterId=BitConverter.ToUInt32(data,ClassicProfile.Id),TargetId=BitConverter.ToUInt32(data,ClassicProfile.Target),Level=BitConverter.ToInt32(data,ClassicProfile.Level),ClassId=BitConverter.ToInt32(data,ClassicProfile.Class),Hp=BitConverter.ToInt32(data,ClassicProfile.Hp),Mp=BitConverter.ToInt32(data,ClassicProfile.Mp),MaxHp=BitConverter.ToInt32(data,ClassicProfile.MaxHp),MaxMp=BitConverter.ToInt32(data,ClassicProfile.MaxMp),X=BitConverter.ToSingle(data,0x3C),Y=BitConverter.ToSingle(data,0x40),Z=BitConverter.ToSingle(data,0x44)};
   if(s.CharacterId==0||s.CharacterId>Int32.MaxValue||s.Level<1||s.Level>200||s.ClassId<0||s.ClassId>11||s.MaxHp<1||s.MaxHp>100000000||s.Hp<0||s.Hp>s.MaxHp||s.MaxMp<0||s.MaxMp>100000000||s.Mp<0||s.Mp>s.MaxMp||!Coordinate(s.X)||!Coordinate(s.Y)||!Coordinate(s.Z))throw new InvalidDataException("Atributos Classic inconsistentes; amostra recusada.");
   return s;
  }
 }
}
