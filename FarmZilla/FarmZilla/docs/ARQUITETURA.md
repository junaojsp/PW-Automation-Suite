# Organização do projeto

## Cliente C# / WinForms

Todos os `.cs` de `outputs/FarmZilla-0.19.2` são compilados juntos. `AvalonInterno` é uma classe parcial; o nome interno é histórico.

| Módulo | Responsabilidade |
|---|---|
| `App.cs` | Inicialização, descoberta/seleção, timer de coordenação, atalhos e fechamento |
| `RubiUI.cs`, `Theme.cs` | Layout 390×606, marca FarmZilla e controles pintados |
| `CharacterLists.cs` | Principal primeiro; opções dos secundários sem duplicação |
| `ClientDiscovery.cs` | União de processos/janelas; arquitetura/hash e diagnóstico |
| `FollowPersistence.cs`, `Bridge.cs` | Intenção de seguir e ponte nativa correspondente |
| `TeleportService.cs`, `TeleportBridge.cs`, `TeleportCapture.cs` | Estado do personagem, pedra/destino, execução e captura compartilhada |
| `PartyService.cs`, `PartyMemory.cs`, `PartyBridge.cs`, `PartyRemount.cs` | Convites, confirmação e etapas de remontagem |
| `BuffService.cs`, `BuffMemory.cs`, `BuffBridge.cs` | Macro 1 uma vez |
| `AttackService.cs`, `AttackMemory.cs`, `AttackBridge.cs` | Alvo, macro 0 e política automática/manual |
| `MeditationService.cs`, `MeditationBridge.cs` | Acompanhar sentar/levantar |
| `QuestService.cs`, `QuestMemory.cs`, `QuestDelivery.cs`, `NpcBridge.cs` | Aceitação/entrega, lista ativa, repetição individual e cancelamento de interação |
| `*UI.cs` | Integração dos módulos ao formulário; algumas rotinas assíncronas também coordenam operações |
| `LicenseCore.cs`, `OnlineIdentity.cs`, `OnlineLicense.cs`, `OnlineApi.cs`, `LicenseGate.cs`, `AuthorizationUI.cs` | Identidade, assinatura, cache e autorização |
| `AppUpdate.cs`, `UpdateUI.cs`, `ReleaseInfo.cs` | Manifesto assinado, download, troca/reinício e versão/chave pública |
| `DiagnosticLog.cs` | Registro em `%LOCALAPPDATA%\AvalonInterno\logs` |

## Coordenação de operações

O timer principal lê as seleções e coordena captura, missões, meditação, ataque e manutenção do seguir. `busy`, cancelamentos e o estado de cada serviço evitam sobreposição incompatível. A seleção ou teleporte cancela operações pendentes onde necessário. Fechar o app espera a restauração/remoção das pontes; se não confirmar, informa o problema. Não trocar isso por encerramento forçado.

As pontes são específicas do cliente x86 validado. Executam comandos na thread apropriada e verificam restauração. Fontes de `work/game-update-tests` exercitam fixtures próprias; verifique o arquivo antes de executar qualquer teste que mencione clientes reais.

## Missões e estados

Aceitação validada: opcode 0x25, serviço 7, dados de 12 bytes, pacote total 22. Entrega validada: opcode 0x25, serviço 6, dados de 8 bytes, pacote total 18, parâmetro final zero. Abertura NPC é capturada separadamente. Formatos e recompensas diferentes não são universalmente suportados.

O serviço espera confirmar a mudança no principal, abre o NPC nos destinatários pertinentes, envia o comando e confere a lista ativa. Só repete pendentes e fecha a interação por cleanup em `finally`. Validação real histórica da missão 1951 confirmou as sucessoras 835/843/849 em quatro secundários; não reaplicar automaticamente esse evento histórico.

## Estado remoto e APIs

Backend: `worker.mjs` roteia; `control.mjs` aplica autorização/desafio/lease e acesso aos assets; `admin-page.mjs` gera o painel; `schema.sql` define tabelas; `keys.mjs` contém somente chaves públicas.

- `POST /v1/device/register`: cadastro do código/resumo/chave pública do PC.
- `POST /v1/device/lease`: desafio/prova assinada para obter autorização.
- `GET /v1/admin/devices`: lista de dispositivos; exige credencial administrativa.
- `POST /v1/admin/device`: permitir/bloquear e nomear; exige credencial administrativa.
- `/admin`: página do painel. Manifesto e binários são entregues somente após autorização.

Veja os contratos exatos em `control.mjs`, `worker.mjs` e `OnlineApi.cs`; não usar este resumo como especificação substituta do código. O gerenciador chama as APIs online; não usa mais licença permanente offline.

## Perfil e diagnóstico

O mapa em `game-profile-20261030.json` relaciona offsets antigos e novos. O cliente possui guards explícitos de hash/arquitetura e validações de objetos. A identificação do login/conta não participa da descoberta: são encontrados processos e janelas, depois o personagem é lido no mundo.

Uma janela com título do jogo não prova compatibilidade. `ClientDiscovery` mantém o nome da janela como contexto de erro e não cria uma entrada habilitada sem a leitura válida do personagem.
