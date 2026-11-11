using System;

// Catalog of Avalon packets already validated inside FarmZilla (TeleportBridge, QuestService, Attack, Mat Farm).
// Frost Farm lists them and turns coordinate captures into a Mat Farm trail. It does not send packets by itself.
sealed class FrostCommand {
 public readonly string Name, Code, Packet, Use;
 FrostCommand(string name,string code,string packet,string use){Name=name;Code=code;Packet=packet;Use=use;}
 public override string ToString(){return Name+" · "+Code+" · "+Packet;}
 public static readonly FrostCommand[] Catalog={
  new FrostCommand("Pedra de teleporte","0x6D","14 bytes · 6D 00 00 01 + item 36905/26651/14351/33916","Espelho: o secundário usa a mesma pedra que o principal."),
  new FrostCommand("Teleporte Especial (uso)","0x28","10 bytes · 28 00 00 01 + item 52256","Espelho: uso do item no secundário, depois o ponto."),
  new FrostCommand("Teleporte Especial (ponto)","0x31","9 bytes · 31 00 · serviço 3 · ação 1 + ponto","Só o formato de ponto de mapa; diálogo de missão 0x31 não entra aqui."),
  new FrostCommand("Janela de missão","0x31","9 bytes · serviço 3 · ação 3 ou 4 + id","Espelho depois que o principal confirma a missão na lista ativa."),
  new FrostCommand("Aceitar missão","0x25","22 bytes · serviço 7 · 12 bytes de dados","QuestService: aceitação validada no principal antes de repetir."),
  new FrostCommand("Entregar missão","0x25","18 bytes · serviço 6 · 8 bytes · fim zero","QuestService: entrega no formato Batedor da Mina."),
  new FrostCommand("Equipar da mochila","0x11","4 bytes · 11 00 · slot mochila · slot 0x16","Só o encaixe tático 22, capturado no principal."),
  new FrostCommand("Cancelar ação","0x2A","2 bytes · 2A 00","ESC do cliente: cancela a ação atual."),
  new FrostCommand("Tirar seleção","0x08","2 bytes · 08 00","ESC do cliente: remove o alvo."),
  new FrostCommand("Reviver na cidade","0x04","6 bytes · 04 00 00 00 00 00","Auto-ressurreição no cemitério mais próximo."),
  new FrostCommand("Aceitar Prece da Vida","0x57","6 bytes · 57 00 00 00 00 00","Aceita o convite de ressurreição."),
  new FrostCommand("Raposa WF (em si)","0x50","12 bytes · skill 312 · 0B 01","Lista fechada da WF; não envia outras skills 0x50."),
  new FrostCommand("Ferir WF (no monstro)","0x29","12 bytes · skill 320 · 0B 01","Lista fechada da WF; o alvo vem da seleção do principal."),
  new FrostCommand("Reviver pet WF","0x29","12 bytes · skill 329 · 0B 01 + id","F5 da WF no próprio personagem."),
  new FrostCommand("Chamar pet","0x64","6 bytes · 64 00 00 00 00 00","F6 da WF."),
  new FrostCommand("Navegação nativa","0x14A","mensagem interna do cliente, até 150 de trecho","Mat Farm: voo/caminhada até o ponto. Não controla altura sozinha."),
  new FrostCommand("Subir / descer voo","player+0xB78","0x10 sobe · 0x20 desce","RotaVertical: só com o personagem já voando.")
 };
 public static string Summary(){
  if(Catalog==null||Catalog.Length==0)throw new InvalidOperationException("Catálogo Frost vazio.");
  return Catalog.Length+" comandos Avalon já validados no FarmZilla.";
 }
}
