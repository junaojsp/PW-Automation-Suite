using System;
using System.Collections.Generic;

// Offline checks of the FarmZilla The Classic release build (no game client needed).
static class ClassicReleaseTests {
 static int failures;
 static void Check(bool ok,string what){Console.WriteLine((ok?"OK    ":"FALHA ")+what);if(!ok)failures++;}
 static int Main(){
  var p=GameProfile.Current();
  Check(p.Known&&p.Sha256==GameProfile.KnownSha256&&GameProfile.KnownSha256.StartsWith("39d81c21"),"perfil = elementclient.exe do The Classic");
  Check(p.SizeOfImage==0xD66000,"SizeOfImage 0xD66000");
  Check(p.HostOffset==0x28&&p.CharacterIdOffset==0x4B8&&p.FollowManagerOffset==0x68,"run+0x28 personagem, id +0x4B8, fila run+0x68");
  var expected=new Dictionary<string,uint>{{"Root",0xCA3B3C},{"RotaRoot",0xCA579C},{"FollowPost",0x425090},{"AttackSelect",0x10AF70},{"NpcClose",0x75B0B0},{"KeyMove",0xE5690},{"TraceMatter",0x10AC20},{"EmptyString",0xB32034},{"Navigate",0x41A6D0},{"Pet",0x9ECB34}};
  foreach(var kv in expected)Check(p.Rva(kv.Key)==kv.Value,kv.Key+" = 0x"+kv.Value.ToString("X"));
  var table=p.NativeTable();
  Check(table.Length==17,"tabela da ponte nativa com 17 endereços");
  bool inside=true;foreach(var rva in table)if(rva<0x1000||rva>=p.SizeOfImage)inside=false;
  Check(inside,"todos os endereços da ponte nativa dentro da imagem");
  try{var dll=RotaAvalon.RotaPaths.Embedded();Check(dll.Length>4096&&dll[0]=='M'&&dll[1]=='Z',"ponte nativa embutida e com o hash fixado");}
  catch(Exception e){Check(false,"ponte nativa embutida: "+e.Message);}
  Check(ClassIcons.Name(3)=="Feiticeira"&&ClassIcons.Name(7)=="Sacerdote"&&ClassIcons.Name(11)=="Tormentador"&&ClassIcons.Name(13)=="Classe 13","nomes das classes do Classic");
  Check(ClassicLayout.HostTarget==0x59C&&ClassicLayout.HostMacro==0x1418&&ClassicLayout.HostTeam==0x7A8&&ClassicLayout.NpcName==0x258,"deslocamentos do personagem e do NPC");
  try{
   // 8x(24*8) RLE strip, bottom-up like the game's sprites; frame N filled with blue level N*10.
   int w=8,h=24*8;var tga=new List<byte>{0,0,10,0,0,0,0,0,0,0,0,0,(byte)w,0,(byte)h,0,32,8};
   for(int y=h-1;y>=0;y--){tga.Add((byte)(0x80|(w-1)));tga.AddRange(new byte[]{(byte)(y/8*10),0,0,255});}
   using(var strip=ClassIcons.Tga(tga.ToArray()))Check(strip.Width==8&&strip.Height==h&&strip.GetPixel(0,5*8+3).B==50&&strip.GetPixel(0,11*8).B==110,"tira de ícones TGA: quadro N = classe N");}
  catch(Exception e){Check(false,"tira de ícones TGA: "+e.Message);}
  Console.WriteLine(failures==0?"Todos os testes passaram.":failures+" falha(s).");
  return failures==0?0:1;
 }
}
