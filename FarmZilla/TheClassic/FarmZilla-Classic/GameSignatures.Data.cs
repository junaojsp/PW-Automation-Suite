// FarmZilla The Classic: endereços (RVA) do elementclient.exe do PW The Classic
// 39d81c21ca72f373a787c936da1611281593d950668f2077016d77c1424338ce, mapeados por desmontagem (TheClassic/PROFILE.md).
// O cliente não tem tabela de relocação e sempre carrega em 0x400000. Sem assinaturas: só esse hash é aceito.
public static partial class GameSignatures {
 static readonly GameSignatures.Sig[] None=new GameSignatures.Sig[0];
 public static readonly GameSignatures.Target[] Targets=new GameSignatures.Target[]{
  new GameSignatures.Target("FollowPost",0x425090u,None),
  // AttackSelect no Classic recebe (alvo, forçar) e devolve -1/0/1 (ret 8); o Avalon passa 3 argumentos.
  new GameSignatures.Target("AttackSelect",0x10AF70u,None),
  new GameSignatures.Target("AttackNormal",0x110590u,None),
  new GameSignatures.Target("MacroStart",0x10DCB0u,None),
  new GameSignatures.Target("MacroStop",0x10DDA0u,None),
  new GameSignatures.Target("SendData",0x4C0C90u,None),
  // AUIDialog::Show virtual (vtable +0x20) do Classic: 3 argumentos (mostrar, modal, ativar), ret 0xC.
  new GameSignatures.Target("NpcClose",0x75B0B0u,None),
  new GameSignatures.Target("NpcCancel",0x281F80u,None),
  new GameSignatures.Target("TraceMatter",0x10AC20u,None),
  new GameSignatures.Target("TaskLookup",0x52CFF0u,None),
  new GameSignatures.Target("HintCommand",0x3099C0u,None),
  new GameSignatures.Target("TaskConfirmRefresh",0x309500u,None),
  new GameSignatures.Target("TaskConfirmCommand",0x3093A0u,None),
  new GameSignatures.Target("AwardSelect",0x1AADE0u,None),
  new GameSignatures.Target("AwardConfirm",0x1AAB70u,None),
  // Sem método próprio de tecla no Classic: CECHostPlayer::ProcessMessage (mensagem 0xDD, tecla em +0x0C), ret 4.
  new GameSignatures.Target("KeyMove",0xE5690u,None),
  new GameSignatures.Target("AutoPolicyGet",0x44A860u,None),
  new GameSignatures.Target("AutoPolicyStop",0x44AAF0u,None),
  new GameSignatures.Target("AutoPolicyChange",0x44ABA0u,None),
  new GameSignatures.Target("Navigate",0x41A6D0u,None),
  new GameSignatures.Target("Root",0xCA3B3Cu,None),
  new GameSignatures.Target("RotaRoot",0xCA579Cu,None),
  new GameSignatures.Target("TeleportTable",0xCB69A0u,None),
  new GameSignatures.Target("TeleportCount",0xCB69ACu,None),
  new GameSignatures.Target("AutoKillName",0xA21AD0u,None),
  // No Classic é a variável que guarda o ponteiro da string vazia (o jogo compara com o valor dela, não com o endereço).
  new GameSignatures.Target("EmptyString",0xB32034u,None),
  new GameSignatures.Target("FollowWork",0x9E8238u,None),
  new GameSignatures.Target("ComboSkill",0x9E6B0Cu,None),
  new GameSignatures.Target("DlgNpc",0x9FFC10u,None),
  new GameSignatures.Target("DlgTask",0xA06CE8u,None),
  new GameSignatures.Target("DlgTaskConfirm",0xA06DD4u,None),
  new GameSignatures.Target("DlgTaskList",0xA06F04u,None),
  new GameSignatures.Target("Team",0x9EC30Cu,None),
  new GameSignatures.Target("TeamMember",0x9EC304u,None),
  new GameSignatures.Target("DlgMessageBox",0x9FF114u,None),
  new GameSignatures.Target("TaskInterface",0xA2AA90u,None),
  new GameSignatures.Target("ItemTransmitScroll",0x9EDD20u,None),
  new GameSignatures.Target("ItemTaskDice",0x9EF3C8u,None),
  new GameSignatures.Target("Pet",0x9ECB34u,None),
 };
 public static readonly GameSignatures.Guard[] Guards=new GameSignatures.Guard[0];
}
