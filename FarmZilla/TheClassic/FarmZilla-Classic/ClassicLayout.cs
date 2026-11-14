// Deslocamentos de estrutura do PW The Classic (elementclient.exe 39d81c21…), mapeados por desmontagem.
// Entre parênteses, o valor correspondente do Avalon que cada um substitui. Detalhes em TheClassic/PROFILE.md.
static class ClassicLayout {
 // CECGameRun ([Root+0x1C] ou [RotaRoot]).
 public const uint RunHost=0x28,RunUi=0x10,RunWorld=0x14,RunQueue=0x68;                  // (0x34, 0x1C, 0x20, 0x74)
 // CECHostPlayer.
 public const uint HostId=0x4B8,HostLevel=0x4C4,HostHp=0x4CC,HostMp=0x4D0;                 // (0x4F0, 0x4FC, 0x504, 0x508)
 public const uint HostMaxHp=0x518,HostMaxMp=0x51C,HostTarget=0x59C;                       // (0x578, 0x57C, 0x5FC)
 public const uint HostName=0x6D8,HostClass=0x6DC,HostMoveMode=0x6E8,HostFlags=0x764;      // (0x748, 0x74C, 0x758, 0x7D4)
 public const uint HostTeam=0x7A8,HostVertical=0xAF8,HostNpcTalk=0xD9C,HostNpcBusy=0xDAA;  // (0x818, 0xB78, 0xEFC, 0xF0A)
 public const uint HostPack=0xF98,HostWork=0x13F8,HostTasks=0x1400,HostMacro=0x1418;       // (0x10F8, 0x1D1C, 0x1D24, 0x1D3C)
 public const int HostReadSize=0x768;                                                      // (0x7D8)
 // Configuração das macros (janela R): macro N em [[Root]+0x18]+MacroConfig+17*N.        // (0x102)
 public const uint MacroConfig=0xFE;
 // CECGameUIMan ([[run+RunUi]+8]) e CDlgTaskConfirm.
 public const uint UiTaskConfirm=0xD54,UiTaskHint=0xD58,UiNpcTalk=0x10A0;                  // (0x6B0, 0x6B4, 0xA54)
 public const uint ConfirmLabels=0x250,ConfirmCount=0x26C;                                 // (0x258, 0x274)
 // CECWorld.
 public const uint WorldNpcs=0x20,WorldMatters=0x24;                                       // (0x24, 0x28)
 // CECMatterMan: a tabela por id começa em +0x10. Os baldes ficam em +0xC e a quantidade em +0x18 dessa tabela,
 // portanto no gerenciador são +0x1C e +0x28. O Avalon lia a quantidade em +0x18 e, no Classic, isso para o farm.
 public const uint MatterHashBuckets=0x1C,MatterHashCount=0x28;
 public const uint MatterId=0x110,MatterTemplate=0x114;
 // CECComboSkill: passo atual, incrementado pelo cliente ao escolher a habilidade seguinte.
 public const uint ComboStep=0x20;
 // CECHPWorkMan: 3 filas (+0x08, +0x18, +0x28); +0x38 é a prioridade atual, não uma quarta fila (Avalon: 4).
 public const int WorkQueues=3;
 // CECPet: id do dono (o Classic não guarda o id do pet no personagem como o Avalon em host+0x91C).
 public const uint PetMaster=0x254;
 // CECNPC: nome (ACString, lido por CECNPC::GetName 0x151704), estados estendidos e alvo da habilidade em conjuração;
 // no Avalon ficam 0x2C adiante.
 public const uint NpcName=0x258,NpcExtStates=0x210,NpcSkillTarget=0x2E8;                   // (0x284, 0x23C, 0x314)
}
