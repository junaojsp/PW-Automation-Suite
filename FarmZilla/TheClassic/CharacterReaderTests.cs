using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FarmZilla.TheClassic;
static class CharacterReaderTests {
 sealed class Memory:IReadMemory {
  public readonly Dictionary<uint,byte> Bytes=new Dictionary<uint,byte>();public Action<uint,int> Before;
  public byte[] Read(uint address,int count){if(Before!=null)Before(address,count);var b=new byte[count];for(uint i=0;i<count;i++){byte value;if(!Bytes.TryGetValue(address+i,out value))throw new IOException("Indisponível");b[i]=value;}return b;}
  public void Put(uint at,byte[] b){for(uint i=0;i<b.Length;i++)Bytes[at+i]=b[i];}
  public void U32(uint at,uint value){Put(at,BitConverter.GetBytes(value));}
 }
 const uint Run=0x20000000,Game=0x21000000,Host=0x22000000,Name=0x23000000;
 static byte[] Data(){var b=new byte[ClassicProfile.Length];Action<int,int> put=(at,v)=>Buffer.BlockCopy(BitConverter.GetBytes(v),0,b,at,4);put(ClassicProfile.Id,12345);put(ClassicProfile.Level,101);put(ClassicProfile.Class,7);put(ClassicProfile.Hp,3333);put(ClassicProfile.MaxHp,9999);put(ClassicProfile.Mp,1234);put(ClassicProfile.MaxMp,3456);put(ClassicProfile.Target,unchecked((int)0x80000001));put(ClassicProfile.Name,(int)Name);foreach(var pair in new[]{new{at=0x3C,v=120.25f},new{at=0x40,v=15.5f},new{at=0x44,v=-40.75f}})Buffer.BlockCopy(BitConverter.GetBytes(pair.v),0,b,pair.at,4);return b;}
 static Memory Fixture(uint imageBase){var m=new Memory();m.U32(imageBase+ClassicProfile.RunGlobal,Run);m.U32(imageBase+ClassicProfile.GameGlobal,Game);m.U32(Game+ClassicProfile.GameRunOffset,Run);m.U32(Run,imageBase+ClassicProfile.RunVtable);m.U32(Run+ClassicProfile.HostOffset,Host);m.Put(Host,Data());m.U32(Host,imageBase+ClassicProfile.HostVtable);m.Put(Name,Encoding.Unicode.GetBytes("TesteClássico\0"));return m;}
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS Classic "+name);}
 static void Reject(Action action,string name){bool failed=false;try{action();}catch{failed=true;}Check(failed,name);}
 public static void RunTests(){
  uint image=0x400000;var s=CharacterReader.Read(Fixture(image),image);
  Check(s.Name=="TesteClássico"&&s.CharacterId==12345&&s.ClassId==7&&s.Level==101,"nome, ID, classe e nível");
  Check(s.Hp==3333&&s.MaxHp==9999&&s.Mp==1234&&s.MaxMp==3456,"HP e MP não invertidos");
  Check(s.X==120.25f&&s.Y==15.5f&&s.Z==-40.75f&&s.TargetId==0x80000001,"posição interna e alvo uint32");
  Check(!s.AutomationEnabled&&s.Validation.Contains("confirmar"),"amostra não libera automação");
  Check(CharacterReader.Read(Fixture(0x600000),0x600000).Name==s.Name,"base realocada");
  var missing=Fixture(image);missing.U32(Run+ClassicProfile.HostOffset,0);Reject(()=>CharacterReader.Read(missing,image),"personagem não conectado");
  var wrong=Fixture(image);wrong.U32(Host,0x123456);Reject(()=>CharacterReader.Read(wrong,image),"tipo de personagem incorreto");
  var chain=Fixture(image);chain.U32(Game+ClassicProfile.GameRunOffset,Run+4);Reject(()=>CharacterReader.Read(chain,image),"cadeias globais discordam");
  var hp=Fixture(image);hp.U32(Host+ClassicProfile.Hp,10000);Reject(()=>CharacterReader.Read(hp,image),"HP maior que máximo");
  var mp=Fixture(image);mp.U32(Host+ClassicProfile.Mp,0xffffffff);Reject(()=>CharacterReader.Read(mp,image),"MP negativo");
  var nan=Fixture(image);nan.Put(Host+0x3C,BitConverter.GetBytes(Single.NaN));Reject(()=>CharacterReader.Read(nan,image),"posição NaN");
  var dead=Fixture(image);dead.U32(Host+ClassicProfile.Hp,0);Check(CharacterReader.Read(dead,image).Hp==0,"personagem morto pode ser diagnosticado");
  var name=Fixture(image);name.Put(Name,Encoding.Unicode.GetBytes(new string('A',32)));Reject(()=>CharacterReader.Read(name,image),"nome sem terminador");
  var partial=Fixture(image);partial.Bytes.Remove(Host+0x6DD);Reject(()=>CharacterReader.Read(partial,image),"leitura parcial");
  var changed=Fixture(image);changed.Before=(at,count)=>{if(at==Name)changed.U32(Host+ClassicProfile.Id,54321);};Reject(()=>CharacterReader.Read(changed,image),"personagem muda durante leitura");
  var pe=new PeImage{Img=new byte[0xD66000],SizeOfImage=0xD66000,ImageBase=image};
  var memory=new Memory();foreach(var a in ClassicProfile.Anchors){Buffer.BlockCopy(a.Bytes,0,pe.Img,(int)a.Rva,a.Bytes.Length);memory.Put(image+a.Rva,a.Bytes);}
  ClassicProfile.ValidateLoaded(memory,image,pe);Check(true,"guardas de arquivo e processo concordam");
  var anchor=ClassicProfile.Anchors[0];memory.U32(image+anchor.Rva+1,0x11111111);Reject(()=>ClassicProfile.ValidateLoaded(memory,image,pe),"código carregado modificado");
  pe.Relocs.Add(anchor.Rva+1);var relocated=new Memory();uint relocatedBase=0x600000;foreach(var a in ClassicProfile.Anchors)relocated.Put(relocatedBase+a.Rva,a.Bytes);relocated.U32(relocatedBase+anchor.Rva+1,unchecked(pe.U32(anchor.Rva+1)+relocatedBase-image));ClassicProfile.ValidateLoaded(relocated,relocatedBase,pe);Check(true,"realocação de operando exige valor exato");
  pe.Img[ClassicProfile.Anchors[1].Rva]^=1;Reject(()=>ClassicProfile.ValidateFile(pe),"offset divergente no arquivo");
 }
}
