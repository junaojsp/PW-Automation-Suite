using System;
using System.Collections.Generic;
using System.Linq;

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
  AutoQuestWorkflowTests.Run(Check);
  AutoQuestTravelTests.Run(Check);
  AutoQuestVerifiedDriverTests.Run(Check);
  AutoQuestEvidenceTests.Run(Check);
   AutoQuestBetaTests.Run(Check);
  AutoQuestRawTaskTests.Run(Check);
  AutoQuestRecoveryTests.Run(Check);
  AutoQuestCatalogTests.Run(Check);
  Check(AutoQuestDiagnostic.MaxDurationSeconds==1800&&AutoQuestDiagnostic.MaxLogLines>=25000,"Auto Quest: diagnóstico até 30 minutos com capacidade ampliada de linhas");
  Check(AutoQuestDiagnostic.CanRecord(TimeSpan.FromSeconds(900),false)&&AutoQuestDiagnostic.CanRecord(TimeSpan.FromSeconds(1799),false)&&!AutoQuestDiagnostic.CanRecord(TimeSpan.FromSeconds(1800),false)&&!AutoQuestDiagnostic.CanRecord(TimeSpan.FromSeconds(15),true),"Auto Quest: gravação 15-30 min e parada manual respeitadas");
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
 }
}


// The planned workflow is tested offline. These tests DO NOT claim live NPC/quest decoding is complete.
static class AutoQuestBetaTests {
 public static void Run(Action<bool,string> check){
  check(AutoQuestBetaSession.ValidDestination(1750,1773.4)&&
    AutoQuestBetaSession.ValidDestination(765.6,327.3)&&
    !AutoQuestBetaSession.ValidDestination(Double.NaN,100)&&
    !AutoQuestBetaSession.ValidDestination(0,100),
    "Auto Quest beta: aceita os dois destinos dos logs, bloqueia coordenadas invalidas");
  double x=AutoQuestBetaSession.Step(0,600,800,AutoQuestBetaSession.MaxStepGround);
  double z=AutoQuestBetaSession.Step(0,800,600,AutoQuestBetaSession.MaxStepGround);
  check(Math.Abs(Math.Sqrt(x*x+z*z)-110)<0.01&&
    Math.Abs(AutoQuestBetaSession.Step(0,600,800,AutoQuestBetaSession.MaxStepFlight)-168)<0.01,
    "Auto Quest beta: respeita limites menores que os da ponte nativa em solo e voo");
  check(!AutoQuestBetaSession.CanCombat(false,true,false,"Idle",1329,true)&&
    !AutoQuestBetaSession.CanCombat(true,false,false,"Idle",1329,true)&&
    !AutoQuestBetaSession.CanCombat(true,true,true,"Idle",1329,true)&&
    !AutoQuestBetaSession.CanCombat(true,true,false,"FlightTransition",1329,true)&&
    !AutoQuestBetaSession.CanCombat(true,true,false,"Moving",1329,true)&&
    !AutoQuestBetaSession.CanCombat(true,true,false,"Idle",0,true)&&
    !AutoQuestBetaSession.CanCombat(true,true,false,"Idle",1329,false)&&
    AutoQuestBetaSession.CanCombat(true,true,false,"Idle",1329,true),
    "Auto Quest beta: so libera F12 no local, pousado, sem transicao e com ID ativo");
  check(AutoQuestBetaSession.MaxMinutes<=15&&
    AutoQuestBetaSession.MaxStepGround<=120&&AutoQuestBetaSession.MaxStepFlight<=300,
    "Auto Quest beta: tempo/trechos limitados para a primeira execucao com log");
 }
}

// Replays the exact known successful walking corridor and the failure in front of the wall.
// Offline tests do not claim terrain geometry/collision or a verified quest objective.
// Synthetic tasks.data headers; no external binaries or client process needed.
// A title in the task catalog is informational only: it is not proof of a live
// NPC offer, mission stage, objective or reward.
static class AutoQuestCatalogTests {
 static byte[] Fixture(int version) {
  int count=2,one=20,two=96;
  byte[] data=new byte[192];
  Buffer.BlockCopy(BitConverter.GetBytes(AutoQuestTaskCatalog.ExpectedMagic),0,data,0,4);
  Buffer.BlockCopy(BitConverter.GetBytes(version),0,data,4,4);
  Buffer.BlockCopy(BitConverter.GetBytes(count),0,data,8,4);
  Buffer.BlockCopy(BitConverter.GetBytes(one),0,data,12,4);
  Buffer.BlockCopy(BitConverter.GetBytes(two),0,data,16,4);
  Task(data,one,1329,"Quest da Cidade",version);
  Task(data,two,1331,"Outro NPC",version);
  return data;
 }
 static void Task(byte[] data,int at,int id,string name,int version){
  int offset=version>=153?2:0;
  Buffer.BlockCopy(BitConverter.GetBytes(id),0,data,at+offset,4);
  for(int i=0;i<30;i++){
   char ch=i<name.Length?name[i]:'\0';
   var b=BitConverter.GetBytes((ushort)(ch^(ushort)id));
   Buffer.BlockCopy(b,0,data,at+offset+4+i*2,2);
  }
 }
 static AutoQuestTaskCatalog Parse(byte[] bytes){
  string file=System.IO.Path.GetTempFileName();
  try{System.IO.File.WriteAllBytes(file,bytes);return AutoQuestTaskCatalog.Load(file);}
  finally{System.IO.File.Delete(file);}
 }
 public static void Run(Action<bool,string> check){
  var old=Parse(Fixture(93));
  var newer=Parse(Fixture(153));
  check(old.Loaded&&old.Version==93&&old.HeaderCount==2&&old.Rejected==0&&
   old.Title(1329)=="Quest da Cidade"&&old.Title(1331)=="Outro NPC"&&
   old.Title(100)==null,"Scanner cidade: indexa títulos e IDs de missões de tasks.data v93 sem inferir estágios");
  check(newer.Loaded&&newer.Title(1329)=="Quest da Cidade"&&
   newer.Title(1331)=="Outro NPC","Scanner cidade: suporte ao deslocamento do ID de tarefas nas versões 153+");
  var invalid=Fixture(93);invalid[0]=0;
  bool blocked=false;
  try{Parse(invalid);}catch(System.IO.InvalidDataException){blocked=true;}
  check(blocked,"Scanner cidade: assinatura inválida do catálogo não é usada");
  var off=Fixture(93);
  Buffer.BlockCopy(BitConverter.GetBytes(18),0,off,16,4);
  var catalog=Parse(off);
  check(catalog.Titles.Count==1&&catalog.Rejected==1,
   "Scanner cidade: offsets de missões em cima da tabela são rejeitados");
  check(AutoQuestNpcProbe.IsNpcClass(".?AVCECNPC@@")&&
   !AutoQuestNpcProbe.IsNpcClass(".?AVCECMonster@@")&&
   !AutoQuestNpcProbe.IsNpcClass(".?AVCECNPCMan@@")&&
   !AutoQuestNpcProbe.IsNpcClass(null),
   "Scanner cidade: separa candidatos NPCs de monstros e do gerenciador");
  check(AutoQuestCityScan.Sector(1,1700,970)=="instância=1/setor=4,2",
   "Scanner cidade: setor e instância são coordenadas, não nome de cidade inventado");
 }
}

static class AutoQuestRecoveryTests {
 static double Distance(double x,double z,double tx,double tz){
  return Math.Sqrt((tx-x)*(tx-x)+(tz-z)*(tz-z));
 }
 public static void Run(Action<bool,string> check){
  check(AutoQuestGroundCorridor.ValidSpacing()&&AutoQuestGroundCorridor.Count==40,
   "Auto Quest: pontos do corredor gravado em 0.6.5 são contínuos e espaçados por menos de 45 unidades");
  check(AutoQuestGroundCorridor.IsKnownDestination("CheirosoWB",1750,1773.4)&&
   !AutoQuestGroundCorridor.IsKnownDestination("ProFile",1750,1773.4)&&
   !AutoQuestGroundCorridor.IsKnownDestination("CheirosoWB",1750,1500)&&
   !AutoQuestGroundCorridor.IsKnownDestination("CheirosoWB",1765,1788)&&
   AutoQuestGroundCorridor.For("ProFile",1710,978,1750,1773.4)==null,
   "Auto Quest: corredor aprendido só se aplica ao personagem/região do teste comprovado");
  bool rejectsWrongStart=false;
  try{AutoQuestGroundCorridor.For("CheirosoWB",1687.7,1301.5,1750,1773.4);}
  catch(System.IO.IOException){rejectsWrongStart=true;}
  check(rejectsWrongStart,"Auto Quest: início no ponto da parede não reutiliza trilha como atalho");
  var corridor=AutoQuestGroundCorridor.For("CheirosoWB",1725,979.9,1750,1773.4);
  check(corridor!=null&&corridor.Next(1725,979.9,false).Index<=4,
   "Auto Quest: do NPC começa pelos primeiros pontos, sem salto direto ao alvo");
  bool rejectsDrift=false;
  try{corridor.Next(1687.7,1301.5,false);}
  catch(System.IO.IOException){rejectsDrift=true;}
  check(rejectsDrift,"Auto Quest: personagem fora do corredor não atravessa parede por atalho");
  var middle=AutoQuestGroundCorridor.Sample(20);
  check(middle.X>=1537&&middle.X<=1541&&middle.Z>1300&&middle.Z<1320,
   "Auto Quest: na altura da parede usa saída oeste do caminho real (X cerca de 1539)");
  // Advance the same corridor using each observed waypoint; never skip straight to Z=1773.
  int prev=corridor.Next(1725,979.9,false).Index;
  bool ordered=true;
  for(int i=prev;i<AutoQuestGroundCorridor.Count;i++){
   var wp=AutoQuestGroundCorridor.Sample(i);
   int waypointIndex=corridor.Next(wp.X,wp.Z,false).Index;
   if(waypointIndex<i||waypointIndex>i+2)ordered=false;
  }
  check(ordered,"Auto Quest: a trilha avançará gradualmente, um waypoint por vez");
  var reversing=AutoQuestGroundCorridor.For("CheirosoWB",1725,979.9,1750,1773.4);
  var firstBack=reversing.Next(1750,1773.4,true);
  var secondBack=reversing.Next(firstBack.X,firstBack.Z,true);
  check(firstBack.Index==38&&secondBack.Index==37,
   "Auto Quest: retorno do mob vai pela trilha reversa, sem cortar por cima do muro");
  var reverseEnd=AutoQuestGroundCorridor.For("CheirosoWB",1725,979.9,1750,1773.4);
  var next=reverseEnd.Next(1750,1773.4,true);
  for(int i=0;i<50&&next.Index>=0;i++)next=reverseEnd.Next(next.X,next.Z,true);
  check(next.Index==-1&&Distance(next.X,next.Z,1725,979.9)<1,
   "Auto Quest: o retorno termina na origem real registrada, não só no primeiro sample");
  var backward=AutoQuestUnstuck.Candidate(1000,1000,1100,1000,1);
  var left=AutoQuestUnstuck.Candidate(1000,1000,1100,1000,2);
  var right=AutoQuestUnstuck.Candidate(1000,1000,1100,1000,3);
  check(Math.Abs(backward.X-978)<0.01&&Math.Abs(backward.Z-1000)<0.01&&
   Math.Abs(left.X-1000)<0.01&&Math.Abs(left.Z-1022)<0.01&&
   Math.Abs(right.X-1000)<0.01&&Math.Abs(right.Z-978)<0.01,
   "Auto Quest: as três tentativas limitadas são recuar, esquerda e direita");
  check(!AutoQuestUnstuck.Moved(1001,1001,1000,1000)&&
   AutoQuestUnstuck.Moved(1006,1000,1000,1000)&&
   AutoQuestUnstuck.MaxAttempts==3&&AutoQuestUnstuck.NoGainSeconds==8,
   "Auto Quest: avanço de seis unidades libera recuperação e prazo de oito segundos");
  bool refusedFourth=false;
  try{AutoQuestUnstuck.Candidate(1000,1000,1100,1000,4);}
  catch(ArgumentOutOfRangeException){refusedFourth=true;}
  check(refusedFourth,"Auto Quest: quarta manobra é bloqueada pela política");
 }
}

static class AutoQuestWorkflowTests {
 static AutoQuestActor Actor(double x=10,double z=10,bool owned=false,bool flying=false){
  return new AutoQuestActor{X=x,Y=100,Z=z,Hp=500,CharacterVerified=true,ExclusiveLock=true,
    FlightKnown=true,IsFlying=flying,F12Owned=owned};
 }
 static AutoQuestObservedTask Task(ushort id,AutoQuestStage stage,uint npc=0,double x=10,double z=10,
    string region=null,int progress=0,int required=0){
  return new AutoQuestObservedTask{QuestId=id,Title="Missão "+id,Stage=stage,StageRevision=1,
   StageVerified=true,LocationVerified=true,AcceptedVerified=stage!=AutoQuestStage.AcceptAtNpc,
   AvailableVerified=stage==AutoQuestStage.AcceptAtNpc,ReadyForDeliveryVerified=stage==AutoQuestStage.DeliverToNpc,
   NpcInstanceId=npc,X=x,Y=100,Z=z,RegionKey=region,Progress=progress,Required=required};
 }
 public static void Run(Action<bool,string> check){
  // NPC A offers quest; the CURRENT stage then sends the player to a different NPC B.
  var offer=Task(100,AutoQuestStage.AcceptAtNpc,110,10,10);
  var accepted=AutoQuestWorkflow.Decide(Actor(),new[]{offer});
  check(accepted.Move==AutoQuestMove.AcceptQuest&&accepted.NpcInstanceId==110,"Quest: aceitar no NPC inicial A");
  var talk=Task(100,AutoQuestStage.TalkToNpc,220,70,10);
  var toB=AutoQuestWorkflow.Decide(Actor(),new[]{talk});
  check(toB.Move==AutoQuestMove.MoveToNpc&&toB.NpcInstanceId==220,"Quest: seguir para NPC B diferente do inicial");
  var atB=AutoQuestWorkflow.Decide(Actor(70,10),new[]{talk});
  check(atB.Move==AutoQuestMove.InteractNpc&&atB.NpcInstanceId==220,"Quest: conversar com o NPC B");
  var antiRepeat=new AutoQuestActionGuard();
  check(antiRepeat.TryIssue(atB,talk)&&!antiRepeat.TryIssue(atB,talk),
   "Quest: não repetir interação sem confirmação de nova etapa");
  talk.StageRevision=2;
  check(antiRepeat.TryIssue(atB,talk),"Quest: nova revisão confirmada libera interação");
  var wolfA=Task(100,AutoQuestStage.KillMonsters,0,100,100,"Bosque",0,10);
  var wolfB=Task(101,AutoQuestStage.KillMonsters,0,101,98,"Bosque",0,8);
  var distant=Task(102,AutoQuestStage.KillMonsters,0,400,400,"Montanha",0,12);
  var trip=AutoQuestWorkflow.Decide(Actor(70,10),new[]{wolfA,wolfB,distant});
  check(trip.Move==AutoQuestMove.MoveToRegion&&trip.QuestIds.SequenceEqual(new ushort[]{100,101}),
   "Quest: mesmo bosque agrupa duas missões em uma viagem");
  var farInSameLabel=Task(104,AutoQuestStage.KillMonsters,0,200,220,"Bosque",0,3);
  var separated=AutoQuestWorkflow.Decide(Actor(70,10),new[]{wolfA,wolfB,farInSameLabel});
  check(separated.Move==AutoQuestMove.MoveToRegion&&separated.QuestIds.SequenceEqual(new ushort[]{100,101}),
   "Quest: regiões de mesmo nome distantes não viram uma área única");
  var landing=AutoQuestWorkflow.Decide(Actor(100,100,false,true),new[]{wolfA,wolfB,distant});
  check(landing.Move==AutoQuestMove.WaitForLanding,"Quest: não iniciar F12 no ar");
  var farm=AutoQuestWorkflow.Decide(Actor(100,100),new[]{wolfA,wolfB,distant});
  check(farm.Move==AutoQuestMove.StartF12&&farm.QuestIds.SequenceEqual(new ushort[]{100,101}),
   "Quest: inicia F12 somente na área com várias missões");
  wolfA.Progress=10;
  var keep=AutoQuestWorkflow.Decide(Actor(100,100,true),new[]{wolfA,wolfB,distant});
  check(keep.Move==AutoQuestMove.KeepF12&&keep.QuestIds.SequenceEqual(new ushort[]{101}),
   "Quest: manter F12 até a segunda missão terminar");
  wolfB.Progress=8;
  var stop=AutoQuestWorkflow.Decide(Actor(100,100,true),new[]{wolfA,wolfB,distant});
  check(stop.Move==AutoQuestMove.StopF12,"Quest: desligar F12 antes de viajar à próxima região");
  var wait=AutoQuestWorkflow.Decide(Actor(100,100),new[]{wolfA,wolfB});
  check(wait.Move==AutoQuestMove.WaitForStageUpdate,"Quest: aguardar confirmação do estágio após progresso completo");
  var delivery=Task(100,AutoQuestStage.DeliverToNpc,330,500,10);
  var goDeliver=AutoQuestWorkflow.Decide(Actor(100,100),new[]{delivery,distant});
  check(goDeliver.Move==AutoQuestMove.MoveToNpc&&goDeliver.NpcInstanceId==330,
   "Quest: entrega em NPC C mesmo quando outra área tem combate pendente");
  var turnIn=AutoQuestWorkflow.Decide(Actor(500,10),new[]{delivery});
  check(turnIn.Move==AutoQuestMove.DeliverQuest&&turnIn.NpcInstanceId==330,
   "Quest: missão previamente aceita pronta para entrega no NPC C");
  var sameNpc=Task(105,AutoQuestStage.DeliverToNpc,330,500,10);
  var multi=AutoQuestWorkflow.Decide(Actor(500,10),new[]{delivery,sameNpc});
  var turnInGate=new AutoQuestActionGuard();
  check(multi.Move==AutoQuestMove.DeliverQuest&&multi.QuestIds.SequenceEqual(new ushort[]{100,105})&&
   turnInGate.TryIssue(multi,delivery)&&turnInGate.TryIssue(multi,sameNpc)&&!turnInGate.TryIssue(multi,delivery),
   "Quest: agrupar entrega no mesmo NPC mas enviar um comando por missão");
  delivery.HasRewardChoice=true;
  var otherDelivery=AutoQuestWorkflow.Decide(Actor(500,10),new[]{delivery,sameNpc});
  check(otherDelivery.Move==AutoQuestMove.DeliverQuest&&otherDelivery.QuestIds.SequenceEqual(new ushort[]{105}),
   "Quest: entrega simples não fica bloqueada por recompensa de outra missão");
  check(AutoQuestWorkflow.Decide(Actor(500,10),new[]{delivery}).Move==AutoQuestMove.ManualReward,
   "Quest: recompensa com escolha não dispara entrega automática");
  delivery.HasRewardChoice=false;delivery.ReadyForDeliveryVerified=false;
  check(AutoQuestWorkflow.Decide(Actor(500,10),new[]{delivery}).Move==AutoQuestMove.AwaitVerifiedData,
   "Quest: não entregar sem status de conclusão confirmado");
  var unknown=Task(33,AutoQuestStage.Unknown);
  check(AutoQuestWorkflow.Decide(Actor(),new[]{unknown}).Move==AutoQuestMove.AwaitVerifiedData,
   "Quest: ID bruto não significa missão pronta para entrega");
  var dead=Actor(100,100,true);dead.Hp=0;
  check(AutoQuestWorkflow.Decide(dead,new[]{wolfA}).Move==AutoQuestMove.StopF12,
   "Quest: desligar autocultivo no personagem morto");
  var conflict=Actor(100,100);conflict.ExclusiveLock=false;
  check(AutoQuestWorkflow.Decide(conflict,new[]{wolfA}).Move==AutoQuestMove.Pause,
   "Quest: bloquear sem exclusividade Mat Farm/Frost");
  var collect=Task(103,AutoQuestStage.CollectItems,0,100,100,"Mina",0,4);
  check(AutoQuestWorkflow.Decide(Actor(100,100),new[]{collect}).Move==AutoQuestMove.AwaitVerifiedData,
   "Quest: F12 não substitui coleta manual de recursos");
  var done=Task(100,AutoQuestStage.Finished);
  check(AutoQuestWorkflow.Decide(Actor(),new[]{done}).Move==AutoQuestMove.Finished,
   "Quest: concluir somente após confirmação do jogo");
 }
}


// Pure fixture tests: validate byte-delta collection without reading a game client.
// Travel is planned offline until verified quest targets and the live Auto Quest executor exist.
static class AutoQuestTravelTests {
 static AutoQuestActor Actor(bool flying=false,bool stateKnown=true){
  return new AutoQuestActor{X=10,Y=100,Z=10,Hp=1000,
   CharacterVerified=true,ExclusiveLock=true,FlightKnown=stateKnown,IsFlying=flying};
 }
 static AutoQuestDecision Target(){
  return new AutoQuestDecision{Move=AutoQuestMove.MoveToRegion,QuestIds=new ushort[]{1329,1331},
   RegionKey="regiao-confirmada",X=1800,Y=200,Z=1700};
 }
 public static void Run(Action<bool,string> check){
  var destination=Target();
  var foot=new AutoQuestTravelPolicy();
  var ground=foot.Select(Actor(),destination,TimeSpan.Zero,false);
  check(ground.Command==AutoQuestTravelCommand.GroundAutoPath&&ground.X==1800&&ground.Z==1700,
   "Auto Quest: sem voo equipado usa auto caminho sem tentar decolar");
  var air=new AutoQuestTravelPolicy();
  check(air.Select(Actor(),destination,TimeSpan.Zero,true).Command==AutoQuestTravelCommand.TryTakeoff,
   "Auto Quest: com voo equipado tenta decolar uma vez");
  check(air.Select(Actor(),destination,TimeSpan.FromSeconds(3),true).Command==AutoQuestTravelCommand.WaitForTakeoff,
   "Auto Quest: aguarda decolagem sem repetir comandos");
  check(air.Select(Actor(true),destination,TimeSpan.FromSeconds(4),true).Command==AutoQuestTravelCommand.FlyToTarget,
   "Auto Quest: estado voando confirmado usa percurso aéreo");
  var landedAction=AutoQuestDecision.Of(AutoQuestMove.WaitForLanding,"Aguardar pouso.");
  check(air.Select(Actor(true),landedAction,TimeSpan.FromSeconds(5)).Command==AutoQuestTravelCommand.Land,
   "Auto Quest: manda pousar antes de NPC ou F12");
  check(air.Select(Actor(),landedAction,TimeSpan.FromSeconds(6)).Command==AutoQuestTravelCommand.None,
   "Auto Quest: pouso confirmado libera reavaliação do planejador");
  var fallback=new AutoQuestTravelPolicy();
  check(fallback.Select(Actor(),destination,TimeSpan.Zero).Command==AutoQuestTravelCommand.TryTakeoff,
   "Auto Quest: equipamento desconhecido não é confundido com ausência de voo");
  check(fallback.Select(Actor(),destination,TimeSpan.FromSeconds(6)).Command==AutoQuestTravelCommand.GroundAutoPath,
   "Auto Quest: sem decolagem em seis segundos troca para caminho terrestre");
  var nextNpc=new AutoQuestDecision{Move=AutoQuestMove.MoveToNpc,QuestIds=new ushort[]{1329},
   NpcInstanceId=123,X=1712,Y=220,Z=982};
  check(fallback.Select(Actor(),nextNpc,TimeSpan.FromSeconds(30),true).Command==AutoQuestTravelCommand.GroundAutoPath,
   "Auto Quest: não repete decolagem falhada ao voltar para outro NPC");
  fallback.Reset();
  check(fallback.Select(Actor(),nextNpc,TimeSpan.FromSeconds(31),true).Command==AutoQuestTravelCommand.TryTakeoff,
   "Auto Quest: nova sessão ou equipamento alterado permite nova tentativa");
  check(new AutoQuestTravelPolicy().Select(Actor(false,false),destination,TimeSpan.Zero).Command==AutoQuestTravelCommand.WaitForFlightState,
   "Auto Quest: estado de voo incerto bloqueia comandos");
  var denied=Actor();denied.ExclusiveLock=false;
  check(new AutoQuestTravelPolicy().Select(denied,destination,TimeSpan.Zero).Command==AutoQuestTravelCommand.Pause,
   "Auto Quest: preservar trava de exclusividade Espelho/Mat/Frost");
  var invalid=Target();invalid.X=Double.NaN;
  check(new AutoQuestTravelPolicy().Select(Actor(),invalid,TimeSpan.Zero).Command==AutoQuestTravelCommand.Pause,
   "Auto Quest: destino sem coordenadas válidas não gera navegação");
  check(new AutoQuestTravelPolicy().Select(Actor(),AutoQuestDecision.Of(AutoQuestMove.AwaitVerifiedData,
    "Sem estágio validado."),TimeSpan.Zero).Command==AutoQuestTravelCommand.None,
   "Auto Quest: não navega quando faltam dados reais da missão");
  var task=new AutoQuestObservedTask{QuestId=1329,Stage=AutoQuestStage.KillMonsters,
   StageRevision=1,StageVerified=true,LocationVerified=true,AcceptedVerified=true,
   RegionKey="bosque",Required=3,Progress=0,X=10,Y=100,Z=10};
  var nearFlying=AutoQuestWorkflow.Decide(Actor(true),new[]{task});
  var afterLanding=AutoQuestWorkflow.Decide(Actor(),new[]{task});
  check(nearFlying.Move==AutoQuestMove.WaitForLanding&&
    new AutoQuestTravelPolicy().Select(Actor(true),nearFlying,TimeSpan.Zero).Command==AutoQuestTravelCommand.Land&&
    afterLanding.Move==AutoQuestMove.StartF12,
   "Auto Quest: ao chegar voando pousa e só então inicia F12");
 }
}

// No client is controlled in these tests: all action sinks are recording fakes.
static class AutoQuestVerifiedDriverTests {
 sealed class Fake : IAutoQuestGameCommands {
  public readonly List<string> Sent=new List<string>();
  public readonly List<double> GroundX=new List<double>(),AirX=new List<double>();
  public void Takeoff(double x,double z){Sent.Add("takeoff");}
  public void GroundPath(double x,double y,double z){Sent.Add("walk");GroundX.Add(x);}
  public void FlightPath(double x,double z){Sent.Add("fly");AirX.Add(x);}
  public void Descend(){Sent.Add("descend");}
  public void Cultivate(bool enabled){Sent.Add(enabled?"f12_on":"f12_off");}
  public void Halt(){Sent.Add("halt");}
 }
 static AutoQuestActor Actor(double x=0,double z=0,bool flying=false,bool known=true){
  return new AutoQuestActor{X=x,Y=100,Z=z,Hp=1000,
   CharacterVerified=true,ExclusiveLock=true,FlightKnown=known,IsFlying=flying};
 }
 static AutoQuestObservedTask Fight(double x=1000,double z=0,int progress=0,int required=3){
  return new AutoQuestObservedTask{QuestId=1329,Title="Teste",Stage=AutoQuestStage.KillMonsters,
   StageRevision=1,StageVerified=true,LocationVerified=true,AcceptedVerified=true,
   RegionKey="Area verificada",Required=required,Progress=progress,X=x,Y=100,Z=z};
 }
 public static void Run(Action<bool,string> check){
  var unknown=new AutoQuestObservedTask{QuestId=1329,Stage=AutoQuestStage.Unknown,
   StageRevision=0,StageVerified=false,LocationVerified=false};
  var f0=new Fake();
  using(var d=new AutoQuestVerifiedDriver(f0)){
   var r=d.Tick(Actor(),new[]{unknown},TimeSpan.Zero);
   check(r.Stage==AutoQuestMove.AwaitVerifiedData&&f0.Sent.Count==0,
    "Quest driver: só IDs brutos e bytes sem decodificação não autorizam comando");
  }
  var f1=new Fake();
  using(var d=new AutoQuestVerifiedDriver(f1)){
   var a=d.Tick(Actor(),new[]{Fight()},TimeSpan.Zero,false);
   var b=d.Tick(Actor(),new[]{Fight()},TimeSpan.FromSeconds(5),false);
   check(a.Travel==AutoQuestTravelCommand.GroundAutoPath&&a.Issued&&
     f1.GroundX.Count==1&&Math.Abs(f1.GroundX[0]-120)<0.01&&!b.Issued,
    "Quest driver: sem voo, auto caminho em perna de 120 sem repetir a cada tick");
   var stalled=d.Tick(Actor(),new[]{Fight()},TimeSpan.FromSeconds(21),false);
   check(stalled.Travel==AutoQuestTravelCommand.Pause&&!d.Running&&f1.Sent.Contains("halt"),
    "Quest driver: sem progresso em 20 s para em vez de comandar caminho indefinidamente");
  }
  var f2=new Fake();
  using(var d=new AutoQuestVerifiedDriver(f2)){
   d.Tick(Actor(),new[]{Fight()},TimeSpan.Zero);
   d.Tick(Actor(),new[]{Fight()},TimeSpan.FromSeconds(3));
   d.Tick(Actor(),new[]{Fight()},TimeSpan.FromSeconds(6));
   check(f2.Sent.SequenceEqual(new[]{"takeoff","walk"})&&Math.Abs(f2.GroundX[0]-120)<0.01,
    "Quest driver: tenta voar uma vez, espera seis segundos e segue pelo chão");
  }
  var f3=new Fake();
  using(var d=new AutoQuestVerifiedDriver(f3)){
   d.Tick(Actor(0,0,true),new[]{Fight()},TimeSpan.Zero);
   d.Tick(Actor(300,0,true),new[]{Fight()},TimeSpan.FromSeconds(12));
   check(f3.Sent.SequenceEqual(new[]{"fly","fly"})&&
     f3.AirX.Count==2&&Math.Abs(f3.AirX[0]-300)<0.01&&Math.Abs(f3.AirX[1]-600)<0.01,
    "Quest driver: rota aérea em trechos de 300, reavaliando posição");
  }
  var f4=new Fake();
  using(var d=new AutoQuestVerifiedDriver(f4)){
   var t=Fight(1000,0,0,1);
   d.Tick(Actor(1000,0,true),new[]{t},TimeSpan.Zero);
   d.Tick(Actor(1000,0,true),new[]{t},TimeSpan.FromSeconds(1));
   d.Tick(Actor(1000,0,true),new[]{t},TimeSpan.FromSeconds(2));
   var farm=d.Tick(Actor(1000),new[]{t},TimeSpan.FromSeconds(3));
   t.Progress=1;
   var stop=d.Tick(Actor(1000),new[]{t},TimeSpan.FromSeconds(4));
   check(f4.Sent.SequenceEqual(new[]{"descend","descend","halt","f12_on","f12_off"})&&
     farm.Stage==AutoQuestMove.StartF12&&stop.Stage==AutoQuestMove.StopF12,
    "Quest driver: descida, F12 só após pouso e desligamento ao concluir objetivos");
  }
  var f5=new Fake();
  using(var d=new AutoQuestVerifiedDriver(f5)){
   var t=Fight(1000,0);t.Stage=AutoQuestStage.DeliverToNpc;t.NpcInstanceId=330;
   t.ReadyForDeliveryVerified=true;
   var r=d.Tick(Actor(1000),new[]{t},TimeSpan.Zero);
   check(r.Stage==AutoQuestMove.DeliverQuest&&f5.Sent.Count==0,
    "Quest driver: não inventa pacote NPC/entrega mesmo com etapa validada");
  }
  var f6=new Fake();
  using(var d=new AutoQuestVerifiedDriver(f6)){
   var t=Fight(1000,0);
   d.Tick(Actor(1000),new[]{t},TimeSpan.Zero);
   var unsafeActor=Actor(1000);unsafeActor.ExclusiveLock=false;
   var r=d.Tick(unsafeActor,new[]{t},TimeSpan.FromSeconds(1));
   check(r.Travel==AutoQuestTravelCommand.Pause&&!d.Running&&
     f6.Sent.SequenceEqual(new[]{"f12_on","f12_off"}),
    "Quest driver: perda de exclusividade desliga só F12 que ele próprio iniciou");
  }
  var unknownAfterMove=new Fake();
  using(var d=new AutoQuestVerifiedDriver(unknownAfterMove)){
   d.Tick(Actor(),new[]{Fight()},TimeSpan.Zero,false);
   var r=d.Tick(Actor(),new[]{unknown},TimeSpan.FromSeconds(2),false);
   check(r.Stage==AutoQuestMove.AwaitVerifiedData&&
     unknownAfterMove.Sent.SequenceEqual(new[]{"walk","halt"}),
    "Quest driver: etapa deixou de ser validada, cancela auto caminho existente");
  }
  var landingNeverFinishes=new Fake();
  using(var d=new AutoQuestVerifiedDriver(landingNeverFinishes)){
   d.Tick(Actor(1000,0,true),new[]{Fight(1000)},TimeSpan.Zero);
   var r=d.Tick(Actor(1000,0,true),new[]{Fight(1000)},TimeSpan.FromSeconds(46));
   check(!d.Running&&r.Travel==AutoQuestTravelCommand.Pause,
    "Quest driver: pouso sem confirmação em 45 segundos pausa a sessão");
  }
  var f8=new Fake();
  using(var d=new AutoQuestVerifiedDriver(f8)){
   var t=Fight(1000);
   d.Tick(Actor(),new[]{t},TimeSpan.Zero,false);
   var ready=d.Tick(Actor(1000),new[]{t},TimeSpan.FromSeconds(12),false);
   check(ready.Stage==AutoQuestMove.StartF12&&
     f8.Sent.SequenceEqual(new[]{"walk","halt","f12_on"}),
    "Quest driver: cancela trecho de auto caminho antes de ligar F12");
  }
  var f9=new Fake();
  using(var d=new AutoQuestVerifiedDriver(f9)){
   var t=Fight(1000);t.Stage=AutoQuestStage.DeliverToNpc;
   t.NpcInstanceId=330;t.ReadyForDeliveryVerified=true;
   d.Tick(Actor(),new[]{t},TimeSpan.Zero,false);
   var ready=d.Tick(Actor(1000),new[]{t},TimeSpan.FromSeconds(12),false);
   check(ready.Stage==AutoQuestMove.DeliverQuest&&
     f9.Sent.SequenceEqual(new[]{"walk","halt"}),
    "Quest driver: pára auto caminho diante do NPC e não inventa pacote de entrega");
  }
  var f7=new Fake();
  using(var d=new AutoQuestVerifiedDriver(f7)){
   var noFlight=Actor(0,0,false,false);
   var r=d.Tick(noFlight,new[]{Fight()},TimeSpan.Zero);
   check(r.Travel==AutoQuestTravelCommand.WaitForFlightState&&f7.Sent.Count==0,
    "Quest driver: estado de voo desconhecido não inicia viagem");
  }
  // Real 0.6.5 diagnostic for ID 1329: byte +06 changed 02->03 during combat,
  // then ID disappeared after the NPC. Neither is a verified objective counter or reward.
  byte[] record=new byte[32];record[0]=0x31;record[1]=0x05;record[6]=0x02;
  byte[] changed=(byte[])record.Clone();changed[6]=0x03;
  var oldRow=new AutoQuestRawTaskRecord{Id=1329,Occurrence=0,Data=record};
  var newRow=new AutoQuestRawTaskRecord{Id=1329,Occurrence=0,Data=changed};
  var changes=AutoQuestRawTaskDiff.Changes(new[]{oldRow},new[]{newRow});
  var removed=AutoQuestRawTaskDiff.Changes(new[]{newRow},new AutoQuestRawTaskRecord[0]);
  check(changes.Count==1&&changes[0].Kind=="ALTERADO"&&changes[0].Detail.Contains("06:02>03")&&
    removed.Count==1&&removed[0].Kind=="REMOVIDO",
   "Quest log 0.6.5: correlacionar progresso candidato e saída no NPC, sem supor conclusão");
 }
}

// Exact 0.6.5 task 1329 bytes from the user's ground-navigation recording.
// No quest completion, counter, NPC identity or reward is inferred from this replay.
static class AutoQuestEvidenceTests {
 static AutoQuestRawTaskRecord Rec(ushort id,int occurrence,string hex){
  var data=new byte[hex.Length/2];
  for(int i=0;i<data.Length;i++)data[i]=Convert.ToByte(hex.Substring(i*2,2),16);
  return new AutoQuestRawTaskRecord{Id=id,Occurrence=occurrence,Data=data};
 }
 static List<AutoQuestEvidence> Replay(AutoQuestEvidenceTracker tracker,
   AutoQuestRawTaskRecord[] before,AutoQuestRawTaskRecord[] after,
   bool nearNpc,bool action){
  return tracker.Observe(before,after,AutoQuestRawTaskDiff.Changes(before,after),nearNpc,action);
 }
 public static void Run(Action<bool,string> check){
  const string initial="3105FFFFFFFF02D3F1FA6A0000D8D8A541000000000000000000000000000000";
  const string updated="3105FFFFFFFF03D3F1FA6A0000D8D8A541000000000000000000000000000000";
  var initialRecord=Rec(1329,0,initial);
  var updatedRecord=Rec(1329,0,updated);
  var tracker=new AutoQuestEvidenceTracker();
  var added=Replay(tracker,new AutoQuestRawTaskRecord[0],
    new[]{initialRecord},true,false);
  var changed=Replay(tracker,new[]{initialRecord},
    new[]{updatedRecord},false,true);
  var removed=Replay(tracker,new[]{updatedRecord},
    new AutoQuestRawTaskRecord[0],true,false);
  check(added.Count==1&&added[0].Kind==AutoQuestEvidenceKind.AddedNearNpc&&
    changed.Count==1&&changed[0].Kind==AutoQuestEvidenceKind.Offset06ChangedDuringActivity&&
    changed[0].Detail.Contains("02 -> 03")&&removed.Count==2&&
    removed.Any(e=>e.Kind==AutoQuestEvidenceKind.RemovedNearNpc)&&
    removed.Any(e=>e.Kind==AutoQuestEvidenceKind.CorrelatedRawCycle),
    "Quest evidência 0.6.5: 1329 adicionado NPC, offset +06 02>03 após atividade, removido NPC");
  var actor=new AutoQuestActor{X=1708,Y=221,Z=980,Hp=1627,
   CharacterVerified=true,ExclusiveLock=true,FlightKnown=true};
  var unverified=new AutoQuestObservedTask{QuestId=1329,Stage=AutoQuestStage.Unknown,
   StageRevision=0,StageVerified=false};
  check(AutoQuestWorkflow.Decide(actor,new[]{unverified}).Move==AutoQuestMove.AwaitVerifiedData,
    "Quest evidência: ciclo correlacionado NÃO vira objetivo ou entrega validada");
  var noisy=new AutoQuestEvidenceTracker();
  var unrelated=Rec(544,0,"2002FFFFFF170243CCFA6A000058F29641000000000000000000000000000000");
  var shifted=Rec(544,0,"2002FFFFFF160243CCFA6A000058F29641000000000000000000000000000000");
  var noise=Replay(noisy,new[]{unrelated},new[]{shifted},true,false);
  check(noise.Count==0,"Quest evidência: reindex da lista no NPC não é progresso ou ciclo");
  var withoutNpc=new AutoQuestEvidenceTracker();
  Replay(withoutNpc,new AutoQuestRawTaskRecord[0],new[]{initialRecord},false,true);
  Replay(withoutNpc,new[]{initialRecord},new[]{updatedRecord},false,true);
  var outside=Replay(withoutNpc,new[]{updatedRecord},new AutoQuestRawTaskRecord[0],true,false);
  check(!outside.Any(x=>x.Kind==AutoQuestEvidenceKind.CorrelatedRawCycle),
    "Quest evidência: mudança fora do NPC sem aceitação associada não produz ciclo");
  var withoutActivity=new AutoQuestEvidenceTracker();
  Replay(withoutActivity,new AutoQuestRawTaskRecord[0],new[]{initialRecord},true,false);
  var random=Replay(withoutActivity,new[]{initialRecord},new[]{updatedRecord},false,false);
  var removedWithoutActivity=Replay(withoutActivity,new[]{updatedRecord},
   new AutoQuestRawTaskRecord[0],true,false);
  check(random.Count==0&&!removedWithoutActivity.Any(x=>x.Kind==AutoQuestEvidenceKind.CorrelatedRawCycle),
    "Quest evidência: alteração sem atividade associada não se confunde com progresso");
  var occurrences=new AutoQuestEvidenceTracker();
  var secondInitial=Rec(1329,1,initial);
  var secondUpdated=Rec(1329,1,updated);
  Replay(occurrences,new AutoQuestRawTaskRecord[0],new[]{initialRecord},true,false);
  Replay(occurrences,new[]{initialRecord},new[]{initialRecord,secondInitial},true,false);
  Replay(occurrences,new[]{initialRecord,secondInitial},new[]{initialRecord,secondUpdated},false,true);
  var differentOccurrence=Replay(occurrences,new[]{initialRecord,secondUpdated},
    new[]{secondUpdated},true,false);
  check(!differentOccurrence.Any(x=>x.Kind==AutoQuestEvidenceKind.CorrelatedRawCycle),
    "Quest evidência: alteração da ocorrência 1 não completa ciclo da ocorrência 0");
  tracker.Reset();
  var afterReset=Replay(tracker,new[]{updatedRecord},new AutoQuestRawTaskRecord[0],true,false);
  check(!afterReset.Any(x=>x.Kind==AutoQuestEvidenceKind.CorrelatedRawCycle),
    "Quest evidência: reiniciar gravação não reutiliza contexto de outra sessão");
 }
}

static class AutoQuestRawTaskTests {
 static AutoQuestRawTaskRecord Record(ushort id,int occurrence=0,byte flag=0){
  var bytes=new byte[32];Buffer.BlockCopy(BitConverter.GetBytes(id),0,bytes,0,2);
  bytes[4]=flag;
  return new AutoQuestRawTaskRecord{Id=id,Occurrence=occurrence,Data=bytes};
 }
 public static void Run(Action<bool,string> check){
  var first=new[]{Record(1333),Record(1334)};
  var reordered=new[]{Record(1334),Record(1333)};
  check(AutoQuestRawTaskDiff.Changes(first,reordered).Count==0,
   "Auto Quest: reordenação da lista não gera falso progresso de missão");
  var changed=new[]{Record(1333,0,1),Record(1334)};
  var delta=AutoQuestRawTaskDiff.Changes(first,changed);
  check(delta.Count==1&&delta[0].Id==1333&&delta[0].Kind=="ALTERADO"&&delta[0].Detail.Contains("04:00>01"),
   "Auto Quest: alteração de bytes em missão com mesmo ID é detectada");
  check(AutoQuestRawTaskDiff.Changes(first,new[]{Record(1333),Record(1334),Record(1335)}).Count==1&&
    AutoQuestRawTaskDiff.Changes(first,new[]{Record(1333),Record(1334),Record(1335)})[0].Kind=="ADICIONADO",
   "Auto Quest: nova entrada interna registrada sem afirmar conclusão do diário");
  var removed=AutoQuestRawTaskDiff.Changes(first,new[]{Record(1334)});
  check(removed.Count==1&&removed[0].Id==1333&&removed[0].Kind=="REMOVIDO",
   "Auto Quest: entrada interna removida identificada");
  var duplicates=new[]{Record(1333),Record(1333,1)};
  var duplicatesUpdated=new[]{Record(1333),Record(1333,1,8)};
  var duplicateDiff=AutoQuestRawTaskDiff.Changes(duplicates,duplicatesUpdated);
  check(duplicateDiff.Count==1&&duplicateDiff[0].Occurrence==1,
   "Auto Quest: entradas de ID repetido são diferenciadas pela ocorrência");
  bool rejected=false;try{AutoQuestRawTaskDiff.Changes(first,new[]{
    new AutoQuestRawTaskRecord{Id=3,Occurrence=0,Data=new byte[6]}
   });}catch(ArgumentException){rejected=true;}
  check(rejected,"Auto Quest: registros truncados são rejeitados");
 }
}
