# Auditoria do Espelho — ElementClient The Classic, 17/11/2026

## Base de verificação
- Element antigo: SHA-256 `39d81c21ca72f373a787c936da1611281593d950668f2077016d77c1424338ce`.
- Element atualizado: SHA-256 `2987ee326956e694391c35e11f94e674f38cf6395cd7188359ec57b32daa26f8`.
- Relato do usuário: reconhecimento de 4 processos; **Seguir e Bufar funcionam**, **Montar PT, Remontar PT e Teleporte falham**. O registro anterior a essa confirmação continha tentativas de Seguir não confirmadas e falhas de validação do perfil; não deve ser usado como evidência de falha atual do Seguir.

## Módulos revisados e estado

| Função do Espelho | Código principal | Resultado |
|---|---|---|
| Detecção e seleção principal/secundários | `ClientDiscovery.cs`, `CharacterLists.cs`, `GameProfile.cs` | Confirmado pelo usuário: quatro personagens reconhecidos; validação ao executar comandos é separada |
| Seguir / Parar e retomada automática | `Bridge.cs`, `App.cs`, `FollowPersistence.cs`, `AutoFollow.cs` | Reportado funcionando; **preservar**, não trocar os RVAs por tentativa |
| Bufar / Suporte | `BuffBridge.cs`, `BuffMemory.cs`, `BuffService.cs`, `BuffUI.cs` | Bufar reportado funcionando; suporte específico ainda sem confirmação isolada |
| Ataque automático/manual | `AttackBridge.cs`, `AttackMemory.cs`, `AttackService.cs`, `AttackUI.cs` | Não confirmado na versão nova; usa funções de macro do perfil e estado do alvo |
| Montar PT | `PartyBridge.cs`, `PartyMemory.cs`, `PartyService.cs`, `PartyUI.cs` | Reportado falhando; convite enviado por `0x1B`, busca por fila/janela e aceitação `0x1C`; leitura ainda usa `InviteQueueRva`, campos de janela e `HostTeam` legados |
| Remontar PT | `PartyRemount.cs`, `RemountUI.cs`, arquivos da PT | Reportado falhando; depende de ler a composição/liderança, expulsar, transferir liderança, sair e convidar de novo |
| Pedra de Teleporte | `TeleportCapture.cs`, `TeleportService.cs`, `TeleportBridge.cs` | Reportado falhando; pode falhar em captura do principal, seleção dos secundários, inventário/tabela de destinos ou envio |
| Teleporte Especial | Mesmos módulos | Sem teste isolado; protocolo de uso e escolha de ponto é diferente da Pedra |
| Missões e NPC | `QuestMemory.cs`, `QuestService.cs`, `QuestDelivery.cs`, `NpcBridge.cs` | Não confirmado; depende da captura e offsets de diálogos/quest |
| Auto Pot | `InlineAutoPot.cs`, `App.cs`, leitura em `RotaClient.cs` | Registro anterior mostrou incompatibilidade; não confundir configuração UI com leitura de HP/MP |
| Reviver / ressurreição | `AutoResurrect.cs`, `ReviveUI.cs`, `TeleportBridge.cs` | Não confirmado; depende de ponte e campos de personagem |
| Alto cultivo | `AltoCultivo.cs`, `RotaNativeBridge.cs` | Não confirmado; depende da tabela nativa de RVAs por perfil |
| Teclas e comandos | `HotkeyFocus.cs`, `App.cs`, `AttackUI.cs`, `PartyUI.cs` | Interface e eventos presentes; sucesso depende da ação subjacente |

## Causas prováveis que exigem confirmação
1. **PT:** `PartyMemory.QueuedInvite` usa `ClassicLayout.InviteQueueRva=0xCADE10`, que não foi adicionado ao perfil atualizado. `DirectPop` e `CheckInvite` usam posições fixas de UI/convite. O código pode enviar o convite sem detectar/aceitar a janela no secundário.
2. **Teleporte:** `TeleportCapture` aplica gancho em `G.SendData` com prólogo esperado fixo de cinco bytes. Mesmo quando a captura está ativa, `TeleportService.Recipients` pode descartar secundários quando a opção “teleportar só quem está seguindo” está ligada. No envio, `CharacterMemory.FindStone` e `FindDestination` dependem de vtables e tabelas de inventário/destino. A captura não garante a chegada.
3. **Validação em execução:** `ClientDiscovery.Compatible` verifica perfil por arquivo, enquanto `GameProfile.Require` valida memória carregada. Mensagens genéricas impediam distinguir esses passos; a PR #88 expõe o motivo de validação.
4. **Ponte nativa:** `RotaNativeBridge` recebe 17 RVAs do `GameProfile.NativeTable`. Alterações globais ou deslocamentos na nova versão podem afetar vários recursos simultaneamente.
5. **Critério de sucesso:** status “concluído” deve representar confirmação em ambos os clientes, não apenas término do método.

## Ações nesta PR
- Preservar integralmente Seguir e Bufar.
- Registrar motivo real da recusa do perfil em comandos e leituras.
- Reportar estágio de busca e confirmação de convites; evitar “Montar PT concluído” sem confirmação real.
- Instrumentar captura da Pedra e seleção de destinatários no Teleporte, sem alterar a ponte ou enviar novos comandos.

## Para próxima correção funcional
Testar cada etapa com apenas um secundário, coletando logs para identificar exatamente o ponto onde a operação falha. Depois, comparar no Element antigo e novo as cadeias/estruturas específicas de PT, inventário e destinos. Só habilitar novos offsets após leitura coerente e teste em jogo. **Esta auditoria estática não certifica as automações que ainda não foram testadas.**
