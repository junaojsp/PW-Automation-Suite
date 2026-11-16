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
  ClassicSimulator.Run(Check);
  Console.WriteLine(failures==0?"Todos os testes passaram.":failures+" falha(s).");
  return failures==0?0:1;
 }
}
// Replays the three failures seen in game against the Classic layout: the invite dialog the app never
// accepted, the empty attack macro and the Atacar key, and a material the old table walk could not see.
static class ClassicSimulator {
 static byte[] Dialog(uint data,uint ticket,bool visible){var b=new byte[0x104];b[0x90]=(byte)(visible?1:0);Buffer.BlockCopy(BitConverter.GetBytes(data),0,b,0xFC,4);Buffer.BlockCopy(BitConverter.GetBytes(ticket),0,b,0x100,4);return b;}
 static int[] Matters(Dictionary<uint,uint> mem,uint manager,uint countOffset){
  uint count,buckets;if(!mem.TryGetValue(manager+countOffset,out count)||!mem.TryGetValue(manager+ClassicLayout.MatterHashBuckets,out buckets)||count<1||count>8)return new int[0];
  var ids=new List<int>();
  for(uint i=0;i<count;i++){uint node;if(!mem.TryGetValue(buckets+i*4,out node))continue;int guard=0;while(node!=0&&guard++<8){uint next,obj,key;
    if(!mem.TryGetValue(node,out next)||!mem.TryGetValue(node+4,out obj)||!mem.TryGetValue(node+8,out key))break;
    uint id,template;if(mem.TryGetValue(obj+ClassicLayout.MatterId,out id)&&MatterNode.Same(key,id)&&mem.TryGetValue(obj+ClassicLayout.MatterTemplate,out template))ids.Add(unchecked((int)template));
    node=next;}}
  return ids.ToArray();
 }
 public static void Run(Action<bool,string> check){
  uint inviter=105361,ticket=0x00A1B2C3;var box=Dialog(inviter,ticket,true);
  var found=PartyInvite.Match(box,PartyInvite.BoxName,inviter);var packet=found==null?new byte[0]:PartyInvite.Accept(found);
  check(found!=null&&packet.Length==10&&packet[0]==0x1C&&BitConverter.ToUInt32(packet,2)==inviter&&BitConverter.ToUInt32(packet,6)==ticket,"convite no Win_Pop é aceito com o pacote do botão Sim");
  check(PartyInvite.Match(box,"Game_InviteGroup",inviter)!=null,"o nome Game_InviteGroup continua válido");
  check(PartyInvite.Match(box,PartyInvite.BoxName,1)==null&&PartyInvite.Match(Dialog(inviter,ticket,false),PartyInvite.BoxName,inviter)==null,"outro popup ou janela escondida não é este convite");
  var rec=new byte[(int)ClassicLayout.InviteRecord];Buffer.BlockCopy(BitConverter.GetBytes(inviter),0,rec,(int)ClassicLayout.InviteInviter,4);Buffer.BlockCopy(BitConverter.GetBytes(ticket),0,rec,(int)ClassicLayout.InviteTicket,4);
  var queued=PartyInvite.FromRecord(rec,"Game_InviteGroup",inviter);var queuedPacket=queued==null?new byte[0]:PartyInvite.Accept(queued);
  check(queued!=null&&queuedPacket.Length==10&&queuedPacket[0]==0x1C&&BitConverter.ToUInt32(queuedPacket,2)==inviter&&BitConverter.ToUInt32(queuedPacket,6)==ticket,"a fila do cliente aceita o convite sem achar a janela na memória");
  check(PartyInvite.FromRecord(rec,"Win_Poplist",inviter)==null&&PartyInvite.Match(box,"Win_Poplist",inviter,true)!=null,"outro aviso da mesma fila é ignorado; o popup fixo aceita pelo id do líder");
  var empty=new byte[17];var filled=new byte[17];Buffer.BlockCopy(BitConverter.GetBytes((short)123),0,filled,1,2);var shifted=new byte[17];Buffer.BlockCopy(BitConverter.GetBytes((short)123),0,shifted,0,2);
  check(!MacroSlot.Filled(empty)&&MacroSlot.Filled(filled)&&!MacroSlot.Filled(shifted)&&MacroSlot.Dump(empty)=="0,0,0,0,0,0,0,0","macro 0 vazia explica o aviso; habilidade no byte 1 é reconhecida");
  check(AttackHotkey.Refusal(true,0)==null&&AttackHotkey.Refusal(true,1)==null&&AttackHotkey.Refusal(false,1)!=null,"a tecla Atacar funciona no Automático; sem atalhos ela avisa");
  check(ClassicLayout.ComboStep==0x20&&AttackController.StallSeconds==15,"passo preso 15 s no combo+0x20 reinicia a macro");
  uint manager=0x200000,buckets=0x300000,node=0x400000,obj=0x500000;var mem=new Dictionary<uint,uint>();
  mem[manager+0x18]=0;mem[manager+ClassicLayout.MatterHashCount]=1;mem[manager+ClassicLayout.MatterHashBuckets]=buckets;mem[buckets]=node;mem[node]=0;mem[node+4]=obj;mem[node+8]=1;mem[obj+ClassicLayout.MatterId]=0xC0000123;mem[obj+ClassicLayout.MatterTemplate]=2862;
  var seen=Matters(mem,manager,ClassicLayout.MatterHashCount);var missed=Matters(mem,manager,0x18);
  check(seen.Length==1&&seen[0]==2862&&missed.Length==0,"mat farm lê o minério em +0x28 mesmo quando a chave do nó não é o id");
  var world=RotaAvalon.TerrainBounds.ForInstance(1);
  check(world!=null&&world.IsMainWorld&&RotaAvalon.TerrainBounds.ForInstance(121)==null,"a instância 1 abre o mapa do mundo e os pontos de erva");
  var catalog=new RotaAvalon.ResourceCatalog();
  var tier1=new RotaAvalon.ResourceDefinition{id=3098,idTipo=2865,nivelMinimo=80,nivelRecurso=1,nome="Eufórbio",item1Id=1};
  var ore=new RotaAvalon.ResourceDefinition{id=3079,idTipo=2862,nivelMinimo=80,nivelRecurso=1,nome="Minério de Ferro",item1Id=1};
  var gate=new RotaAvalon.ResourceDefinition{id=1,idTipo=2865,nivelMinimo=150,nivelRecurso=1,nome="Alto",item1Id=1};
  check(catalog.GatherLevel(tier1)==1&&catalog.GatherLevel(ore)==1&&catalog.GatherLevel(gate)==150,"erva e minério de nível 80 no catálogo viram tier 1; um mínimo real continua");
  var player=new RotaAvalon.ClientSnapshot{Name="CheirosaEP",Level=6,X=342,Y=230,Z=444};
  var marker=new RotaAvalon.ResourceMarker{TemplateId=3098,RequiredLevel=1,LevelAllowed=true,X=400,Z=500,Y=0,Seed=true,ItemId=1};
  var blocked=new RotaAvalon.ResourceMarker{TemplateId=1,RequiredLevel=150,LevelAllowed=false,X=400,Z=500,Y=0,Seed=true,ItemId=1};
  var plan=RotaAvalon.FarmPlanner.Build(new[]{marker,blocked},new[]{3098,1},player,400);
  check(plan.Points.Count==1&&plan.Points[0].ResourceId==3098,"nível 6 planeja o mat de tier 1 e deixa de fora o mínimo real");
  check(FollowFlight.ShouldTakeoff(true,true,false)&&!FollowFlight.ShouldTakeoff(true,false,false)&&!FollowFlight.ShouldTakeoff(true,true,true)&&!FollowFlight.ShouldTakeoff(false,true,false),"seguidor no chão decola só quando o líder está no ar");
  double nx,nz,nx2,nz2;RotaAvalon.FlightFace.Hold(10,20,out nx,out nz);RotaAvalon.FlightFace.Hold(10,20,out nx2,out nz2);
  check(nx==nx2&&nz==nz2&&nx==18&&nz==20,"a subida e a descida não invertem a direção");
  check(RotaAvalon.MapMark.SealRadius(0.15)>=8&&RotaAvalon.MapMark.KindLetter(2865)=="E"&&RotaAvalon.MapMark.KindLetter(2862)=="M","no zoom do farm o mat e a erva continuam visíveis");
  using(var swatch=RotaAvalon.MapMark.Swatch(2865))check(swatch!=null&&swatch.Width==24&&swatch.GetPixel(4,4).R>150,"a lista mostra uma marca de erva quando o ícone do jogo não abre");
 }
}
