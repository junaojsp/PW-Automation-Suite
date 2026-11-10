# FarmZilla 0.46.6 — teclas Seguir e Bufar respondem na hora

Registro de 05/11 08:04–08:05: as teclas chegavam na hora (cada aperto registrado no mesmo segundo; a função de atalhos só com o jogo na frente não é a causa). 08:05:07 "Seguir ativado" sem ninguém se mexer até mais dois apertos (08:05:16/20): a espera crescente da 0.46.4 (até 30 s após falhas) continuava valendo depois do aperto. 08:05:10 "Aguarde a operação atual para bufar": a retomada do seguir ocupava o app.

- Tecla Seguir: zera a espera e as falhas das contas e executa na hora, passando à frente de uma retomada em andamento (`RunFollowNow`).
- Bufar também passa à frente da retomada do seguir (`YieldFollowRepair`).

Validação: compilação x86, 38 verificações, autotestes. Falta conferir no jogo.

Publicada e verificada em 05/11/2026 (-SkipExport), Worker dd0abcef-56e6-494d-a1f6-c78d0aa2a062.


# FarmZilla 0.46.5 — mapa e lista aparecem também onde não há materiais carregados

Relato do dono (05/11 07:22): no Mat Farm sumiram o mapa e a lista de materiais. Leitura da memória (só leitura) nos 6 clientes: mundo e gerenciador de objetos normais, mas a tabela de objetos vazia (manager+0x1C = 0, contagem 769) — nenhum material carregado por perto (cidade). O FarmZilla tratava isso como "Mundo ainda indisponível" e não desenhava nada.

- `NearbyResources.Read`: tabela vazia devolve o quadro com o terreno e nenhum material (mapa e lista do catálogo aparecem); só ponteiros realmente inválidos continuam sendo erro.

Validação: compilação x86, 38 verificações, autotestes, leitura conferida nos clientes abertos. Publicada e verificada em 05/11/2026 (-SkipExport), Worker 66ed89b5-f0df-4fe5-926e-e4e294c8f654.


# FarmZilla 0.46.4 — Montar PT não fica mais bloqueado e lista só com materiais de chão

Registro de 05/11 07:13–07:14 (0.46.3): Limeira, Lima e uLima longe do principal não confirmavam o seguir; a retomada tentava a cada 3 s (cerca de 1,7 s por tentativa), deixava o FarmZilla ocupado o tempo todo (peso) e recusou "Montar PT" às 07:13:53.

- Seguir: espera crescente entre tentativas de quem não consegue seguir (3, 6, 12… até 30 s), registro só na 1ª e a cada 3 falhas. Montar PT, Remontar PT, Alto cultivo e Ativar/Desativar interrompem a retomada do seguir (até 4 s) e rodam na hora (`YieldFollowRepair`).
- Mat Farm: a lista mostra só materiais coletados no chão — os 5 tipos de recurso de produção do catálogo (2861 madeira, 2862 minério de ferro, 2863 pedra, 2864 carvão, 2865 ervas/plantas; 49 recursos). Baús, portais, objetos de evento e de missão saíram. Escolhidos continuam aparecendo.

- Pedido do dono: botão Ajustes do menu lateral removido; botão "Atualizar cliente" agora se chama "Update" ("Update disponível" quando há versão nova).

Validação: compilação x86, 38 verificações, autotestes. Falta conferir no jogo.

Publicada e verificada em 05/11/2026 (-SkipExport), Worker bbda9368-9c2d-46d3-a544-94df0b1492d9.


# FarmZilla 0.46.3 — Ativar/Desativar liga e desliga o bot inteiro

Pedido do dono: o botão Ativar/Desativar do Espelho tem que ligar e desligar o bot. Antes só soltava as teclas e parava o ataque.

- `BotSwitch.cs` `SetBotActive`: Desativar para o seguir em todas as contas, desliga o Alto cultivo (sem voltar a seguir), para ataque/repetição, PT e missões, pausa o espelhamento (timer de teleporte/missões sai cedo com o bot desativado) e solta as teclas. Ativar liga as teclas e todas as contas voltam a seguir.

Validação: compilação x86, 38 verificações, autotestes, prévia. Falta testar no jogo.

Publicada e verificada em 04/11/2026 (-SkipExport), Worker 61bf2cdc-aca6-4b32-a8a4-55c92e0dbc55.


# FarmZilla 0.46.2 — todas as contas seguem

Registro da 0.46.1 (04/11 22:04:49): o seguir automático ao abrir ligou só a Limaozinho — era a única secundária no slot naquele momento; as outras entraram depois e ficaram paradas.

- `AutoFollow.cs`: enquanto o seguir estiver desejado (ligado ao abrir ou pela tecla), a cada 1,5 s toda secundária marcada que ainda não segue passa a seguir, inclusive as colocadas depois. Desligar o seguir pela tecla (`Toggle` grava `followWanted`) ou ligar o Alto cultivo param isso; desligar o Alto cultivo volta a seguir.

Validação: compilação x86, 38 verificações, autotestes. Falta conferir no registro.

Publicada e verificada em 04/11/2026 (-SkipExport), Worker afbc3393-e92a-4528-ab46-8c7c22f29504.


# FarmZilla 0.46.1 — tecla do Alto cultivo em Comandos

Pedido do dono: o campo da tecla do Alto cultivo junto das outras teclas, no cartão Comandos (saiu de Ajustes). Linhas do cartão 27 px apart para caber sem mudar o tamanho. Validação: compilação x86, 38 verificações, prévia. Publicada e verificada em 04/11/2026 (-SkipExport), Worker c2363524-22b6-40a0-b8cf-ffcdfe80afb8 (1º probe falhou por propagação; -VerifyOnly aprovado).


# FarmZilla 0.46.0 — alto cultivo por tecla e seguir ligado ao abrir

Pedidos do dono: (1) tecla no Espelho que liga o alto cultivo (F12 do jogo) nas contas — elas param de seguir e cultivam sozinhas — e, apertando de novo, desliga e voltam a seguir; (2) ao abrir o FarmZilla, já ligar o seguir.

Desmontagem (só leitura): F12 enviado à janela por mensagem não teve efeito (teste ao vivo: Limeira parada 8 s). O botão do alto cultivo (VA 0x5071F0) usa a instância única CECAutoPolicy (RVA 0x313620, objeto 0xD9C654): rodando = nome da política (+0x28) diferente da string vazia (RVA 0x11BA970); parar = RVA 0x3136E0; ligar = RVA 0x313790 com "AutoKillMonster" (RVA 0x866FD8), zera +0x38, apaga a macro em execução do personagem (+0x1D3C) e grava a posição atual como ponto de partida.

- Ponte nativa: operação 8 `auto_policy` (confere assinaturas, nome da política e os tipos CECAutoPolicy/CECHostPlayer). DLL SHA-256 58840e22b578213ecb830d85484c30329921a19a3a515f22c75877db579941ad.
- Espelho: tecla Alto cultivo (atalho 7, padrão F11 — o Windows reserva o F12 — trocar em Ajustes; arquivo alto-cultivo.txt). Liga: seguir pausa, ataque/repetição do FarmZilla param e ficam desligados, alto cultivo liga no principal e nos secundários marcados. Desliga: para em todos e o seguir volta.
- `AutoFollow.cs`: ao abrir, quando principal e secundários marcados estão no mundo e nenhum segue, liga o seguir (mesmo caminho da tecla Seguir).
- Teste ao vivo da operação 8 (Limeira): ligado, andou atrás de monstro (~21 em 4 s) e lutou; desligar confirmou que estava rodando e parou.

Validação: compilação x86, 38 verificações, autotestes, teste ao vivo. Falta testar a tecla com todas as contas e o seguir automático ao abrir.

Publicada e verificada em 04/11/2026 (-SkipExport), Worker 1e5b7ef9-5d47-43fb-b05d-026a1b8e8bc9.


# FarmZilla 0.45.9 — decolagem com avanço curto

Registro da 0.45.8 (04/11 21:03–21:07): subidas/descidas retas pelo Espaço/Z ok; 21:07:05 Lima no chão (28,5): "abrindo o voo" esperou 7 s sem abrir (o comando de altura sozinho não abre o voo), depois o caminho abriu e às 21:07:21 subiu reto.

- `Straight` no chão: abre o voo com um avanço curto (12, mais 12 a cada 1,5 s, até 6 s) junto com o comando de altura, como uma decolagem normal; assim que voa, para no lugar e sobe reto com o Espaço.

Validação: compilação x86, 38 verificações, autotestes. Falta conferir no registro ("Voo aberto; subindo reto").

Publicada e verificada em 04/11/2026 (-SkipExport), Worker 880359ff-2336-474e-bfa9-8fd099c7a3cf.


# FarmZilla 0.45.8 — no chão, abre o voo e sobe reto

Pedido do dono: no chão, abrir o voo (já cadastrado no personagem) e então subir. O comando de altura do jogo abre o voo equipado; no registro de 20:55 a Lima levou mais que os 2,5 s esperados.

- `Straight`: no chão pede a abertura do voo (altura +5), espera até 7 s (pede de novo aos 3,5 s); voando, sobe reto com o Espaço. Registro: "No chão; abrindo o voo" → "Voo aberto; subindo reto" (ou "Voo não abriu; decolando pelo caminho").

Validação: compilação x86, 38 verificações, autotestes. Falta conferir no registro.

Publicada e verificada em 04/11/2026 (-SkipExport), Worker 8a0d320e-c3df-420b-bb32-9ca654410ecd.


# FarmZilla 0.45.7 — começar no chão não desliga mais a subida reta

Registro da 0.45.6 (04/11): 19:52–20:46 (Limaozinho) 44 subidas/descidas retas pelo Espaço/Z, 22 recompensas, 1 coleta falhou, 1 luta encerrada. 20:55 farm iniciado na Lima no chão (21,8): não decolou a tempo, os passos curtos não moveram e a subida reta foi desligada para o farm inteiro ("indisponível" → linha inclinada) — o "parou" do dono.

- `Straight`: no chão, se não decolar em 2,5 s, devolve "não feito" sem desligar nada (registro "No chão; decolando pelo caminho"); "indisponível" só é marcado com o personagem voando.
- `ClimbHere`: tenta a subida reta; se ainda estiver no chão, a decolagem fica com o caminho até o próximo ponto (sem passadas laterais) e a próxima subida/descida já volta ao Espaço/Z.

Validação: compilação x86, 38 verificações, autotestes. Falta conferir no registro.

Publicada e verificada em 04/11/2026 (-SkipExport), Worker d3c1f1dc-9612-4e33-9dd9-9c331c9e03cb.


# FarmZilla 0.45.6 — Espaço/Z de verdade (função de teclado do jogo)

Registro da 0.45.5 (04/11 18:47–18:50): só a tecla segurada de fora não move parado no ar (descida parada 1 min em 74,6, dois pontos falharam). Testes ao vivo: altura alvo do jogo sozinha 0,0; altura alvo + direção 0,0.

Desmontagem do ElementClient.exe (só leitura, iced-x86 do workspace original): a função de teclado do personagem RVA 0x3D73C0 (VA 0x7D73C0) recebe um comando com a tecla em +0x0C; tabela 0x7D75A0: 8 = 0x10 subir, 9 = 0x20 descer (0–7 = direções horizontais). Voando (host+0x758 == 2) ela cria o movimento de teclado (trabalho tipo 1, modo 3, sem destino) e o registra (0x806A50). O jogo zera host+0xB78 a cada quadro (0x7F1478).

- Ponte nativa: operação 7 `key_move` (confere a assinatura 55 8B EC 83 EC 14 56 8B F1 e o tipo CECHostPlayer) chama essa função como o Espaço/Z. DLL recompilada (Zig 0.16.0), SHA-256 0f49a02ce31598dc51aaf8fc07cffabcf37c95306039277fc16f6d88b3513c70 fixado em `RotaPaths.BridgeSha256`.
- `Straight`: "aperta" Espaço/Z pela ponte a cada 250 ms e segura a direção entre os quadros; sem caminho e sem passos laterais. Os passos curtos ficam só como reserva.
- Teste ao vivo com a DLL nova (personagens parados no ar): subir +16,4 em 3 s; descer −23,0 em 3 s com deslocamento lateral 0,00.

Validação: compilação x86, 38 verificações, autotestes (inclui a ponte recompilada), testes ao vivo. Falta conferir no registro do farm.

Publicada e verificada em 04/11/2026 (-SkipExport), Worker e0d729e7-bbd7-4d55-9128-f921870bc7c0.


# FarmZilla 0.45.5 — subida reta também saindo do chão

Registro da 0.45.3 (04/11 18:38–18:45): 8 subidas/descidas retas e 6 com as passadas laterais antigas (o "pra frente, pra trás" que o dono viu), quase todas na subida logo após coletar (personagem no chão: a subida reta só era tentada já voando). Uma coleta falhou às 18:44:04 por ficar 4,7 alto depois das passadas.

- `Straight`: no chão pede a decolagem do jogo (altura um pouco acima) e então segura Espaço do mesmo jeito (inclui a ordem da 0.45.4: só tecla primeiro, caminho só depois da altura). Descida com o personagem já no chão conta como concluída. As passadas laterais ficam só para cliente em que nada vertical funciona.

Validação: compilação x86, 38 verificações, autotestes. Falta conferir no registro.

Publicada e verificada em 04/11/2026 (-SkipExport), Worker 99834b91-1df9-46a9-b4c2-3e375950dd5c.


# FarmZilla 0.45.4 — sobe só com Espaço, depois liga o caminho

Pedido do dono: o vai e vem curto (passos de 8 da 0.45.2) fica estranho. Ordem certa: subir só segurando Espaço (sem caminho automático), chegar na altura programada, só então ligar o caminho até o material; em cima dele descer só com Z; coletar; subir com Espaço.

- `Straight`: primeiro só a direção segurada (Espaço/Z), sem nenhum comando de movimento; o caminho só é ligado depois (`FlyHigh` após `ClimbHere`). Se a altura não mudar em 1,2 s só com a tecla, continua com os passos curtos da 0.45.2 naquela subida/descida (registro "Espaço/Z sozinho não moveu; passos curtos"); depois de 3 vezes seguidas, usa os passos direto na sessão. Observação: no teste da 0.45.2 o personagem que "não subiu" parado provavelmente estava no chão coletando; o teste só-tecla parado no ar não pôde ser repetido (nenhum personagem parado no ar em 40 s com o farm rodando).

Validação: compilação x86, 38 verificações, autotestes. Falta conferir no registro qual modo foi usado ("modo":"espaço/Z" ou "passos").

Publicada e verificada em 04/11/2026 (-SkipExport), Worker dfa8092f-5eac-447f-b978-a3317f77c334.


# FarmZilla 0.45.3 — descida reta também no material achado fora do centro

Registro da 0.45.2 (04/11 18:22–18:32, 3 contas): "Subindo/descendo reto" confirmado nas 3 (18:24:05/07/21), 15 subidas/descidas retas, nenhuma rampa, nenhum travamento, 7 recompensas; defesa da Limaozinho encerrada em 10,9 s ("sem dano há 5 s", antes 90 s). Uma falha: 18:26:58 material achado fora do centro da área foi descido com as passadas laterais e parou 3,6 alto demais ("Material indisponível ou fora do alcance").

- `Settle`: quando o ponto não está embaixo do personagem, primeiro voa nivelado até em cima dele e só então desce reto.

Validação: compilação x86, 38 verificações, autotestes. Falta conferir no registro.

Publicada e verificada em 04/11/2026 (-SkipExport), Worker 76bd4ae6-12d6-4711-9f88-e8ac60a6f95e.


# FarmZilla 0.45.2 — subida reta parado no ar e fim de luta rápido

Registro com a 0.45.1 (04/11 18:09): "Subida reta indisponível" nas 3 contas de novo. Testes ao vivo pedidos pelo dono: segurando a direção, os dois personagens que estavam voando em movimento subiram reto (+7,3 em 1,5 s); o que estava parado no ar não se mexeu (0,0). Passos de voo reto de 8 em volta do lugar a cada 150 ms + direção segurada: parado no ar subiu +4,3 em 1,5 s com 3,4 de deslocamento lateral; passo 5 não teve efeito (−0,3).

- `Straight`: em vez de parar o personagem, mantém o "movimento" com passos de 8 em volta do ponto enquanto segura a direção; confirma em 2 s (variação mínima 2). Ao terminar, solta a direção e para no lugar.
- Defesa: o monstro morto continua na lista do jogo (Limaozinho esperou 90 s duas vezes: 18:09:31→18:11:01 e 18:11:41→18:13:11). Agora 5 s sem levar dano encerram a luta ("Luta encerrada; voltando à rota"); se o monstro continuar vivo e bater, a defesa recomeça.

Validação: compilação x86, 38 verificações, autotestes, testes ao vivo da direção vertical. Falta conferir no registro do farm.

Publicada e verificada em 04/11/2026 (-SkipExport), Worker 1b5fc25c-38a4-4e45-9fea-a20f650017f6.


# FarmZilla 0.45.1 — subida/descida reta funcionando

Registro da 0.45.0 (04/11 17:59, 3 contas): "Subida reta indisponível" nas três. Leitura da memória (só leitura): o jogo liga e apaga player+0xB78 a cada quadro, então o valor escrito a cada 40 ms era apagado. Teste ao vivo pedido pelo dono (Limao voando, 1,5 s, reescrevendo ao ser apagado): descendo −5,3/s passou a subir +7,3 em 1,5 s (49 reescritas) — a direção funciona quando é mantida.

- `VerticalControl`: uma linha de execução por cliente repõe a direção cerca de 1 vez por milissegundo enquanto ela é pedida (resolução de 1 ms do Windows só durante a subida/descida) e solta na hora quando não é mais pedida, ao parar ou fechar.
- Cada Iniciar farm volta a tentar a subida reta (a falha não fica marcada para a sessão inteira).

Validação: compilação x86, 38 verificações, autotestes, teste ao vivo da direção vertical. Falta conferir no registro do farm ("Subindo/descendo reto").

Publicada e verificada em 04/11/2026 (-SkipExport), Worker 4b739f84-4ad3-4ba1-ba6a-1f0d3ef5f0c5.


# FarmZilla 0.45.0 — subida e descida em linha reta (como Espaço / Z)

Pedido do dono: sem o balanço de um lado para o outro ao subir/descer; subir e descer em linha reta como segurando Espaço e Z ("é fundamental"). Espiral rejeitada pelo dono.

- `RotaVertical.cs` + `GameClient.Vertical`: segura a direção vertical de voo do personagem (player+0xB78: 0x10 sobe, 0x20 desce, observada com as teclas) e solta ao chegar na altura (sempre solta, também em erro, parada ou fechamento). Só com o personagem já voando; no chão primeiro decola pelo comando de altura do jogo.
- `Straight` em RotaExecution.cs: confere ao vivo — se a altura não mudar em 1,5 s, o cliente não aceita e não é tentado de novo na sessão (registro "Subida reta indisponível"). Dano durante a subida/descida solta o controle e defende.
- Sem a subida reta: linha reta inclinada (alto até 400 antes do ponto, desce numa reta até ele; a subida acontece na reta até o próximo ponto). As passadas de um lado para o outro ficam só como último recurso para acertar a altura final.

Validação: compilação x86, 38 verificações, autotestes. NÃO testado no jogo — a escrita de 0xB78 é experimental (pode o jogo sobrescrever a cada quadro); o registro mostra "Subindo/descendo reto" quando funciona.

Publicada e verificada em 04/11/2026 (-SkipExport), Worker 02fe5859-6520-4dc1-9203-5191d72f6331.


# FarmZilla 0.44.0 — Mat Farm destrava, defende rápido e voa sempre alto

Pedidos do dono: (1) travado numa parede ele forçava para frente — tentar trás, depois os lados, mudar o trajeto; (2) atacado, demorava a revidar; (3) voava baixo entre materiais perto — sempre subir à altura programada, ir alto, descer reto, coletar, subir reto.

- Travamento (`Progress` + `Escape` em RotaExecution.cs): sem chegar 2 mais perto do alvo em 2,5 s = travado (antes: 5–6 s parado por completo). Desvia para trás, depois esquerda, depois direita (28 de passo; no voo sobe +8 enquanto desvia), cada um até realmente se mover, e reenvia o trecho. No voo, após 4 desvios cai no caminho automático do jogo (como antes); no chão, após 4 desvios o ponto falha e o farm segue (nunca para).
- Defesa: para o movimento em andamento antes de atacar (ele continuava voando para longe), procura o monstro até 60 (antes 40), reinicia a macro 0 1 s após terminar (antes 2,5 s), confere a cada 0,15 s e volta à rota 0,25 s após matar (antes 0,8 s).
- Sempre alto: removido o atalho de "material perto, vai baixo" (170) e a descida em rampa de 400. Agora: sobe reto no lugar até a altura de voo (`ClimbHere`), voa alto até em cima do ponto, desce reto (passadas curtas de 30 sobre o ponto, porque o jogo não aceita movimento só vertical) conferindo se o material está lá, coleta, e o próximo ponto começa subindo reto. Pontos de área também saem da altura de voo. O tempo da subida/descida passa a ser calculado pela altura (vertical é lento).

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo (o farm fica mais lento por subir/descer sempre — pedido do dono).

Publicada e verificada em 04/11/2026 (-SkipExport), Worker 4afc1827-f2eb-4879-b2bd-91802f52402c (1º probe falhou por propagação; -VerifyOnly aprovado).


# FarmZilla 0.43.0 — todos os materiais na lista e pesquisa

Pedido do dono: lista de materiais com todos e um filtro para pesquisar, sem pesar o app.

- Lista do Mat Farm com todos os coletáveis do catálogo (272 nomes, 428 variantes), uma linha por nome: marcar a linha marca todas as variantes daquele nome; Pontos/Perto somados; sem ponto aparece vazio. Escolhidos no topo, depois por pontos.
- Campo "Pesquisar material…" acima da lista (ignora acento e maiúsculas) e contador "N materiais / N de M".
- Leveza: lista do catálogo montada uma vez (`catalogMaterials`); aba oculta não redesenha; nomes sem acento em cache; ícones lidos só para linhas com pontos, marcadas ou achadas na pesquisa; reordenar a lista por pontos novos no máximo a cada 3 s (pesquisa e marcação reordenam na hora). Medido: 1,0 ms por atualização.

Validação: compilação x86, 38 verificações, MaterialListTest (5) e autotestes, prévia. Não testado no jogo.

Publicada e verificada em 03/11/2026 (-SkipExport), Worker 30d7387d-066b-4a28-b764-429f49670f32.


# FarmZilla 0.42.0 — rota do Mat Farm em .txt

Pedido do dono: salvar a rota do Mat Farm como .txt e carregar quando precisar.

- `RotaRouteText.cs` (`FarmRouteText`): arquivo de texto com nome, personagem, altura, raio, materiais e os pontos na ordem do farm (`ponto=material;item;x;y;z;area`, ponto decimal; vírgula também aceita na leitura). Até 5000 pontos, 2 MB.
- Mat Farm: botões "Salvar rota (.txt)" (salva o plano em uso: o carregado, o do farm rodando ou o automático atual) e "Carregar rota (.txt)" (marca os materiais da rota, ajusta altura e raio e passa a usar exatamente esses pontos, volta após volta). Com rota carregada o botão vira "Tirar rota carregada" e o farm volta ao plano automático. Clique no mapa continua tirando pontos. Pasta padrão: Rota\rotas. Cada conta (aba) carrega a sua.

Validação: compilação x86, 38 verificações, RouteTextTest (7) e autotestes, prévia. Não testado no jogo.

Publicada e verificada em 03/11/2026 (-SkipExport), Worker c551158e-1220-4cd2-9f0a-4baf9d329e08.


# FarmZilla 0.41.1 — cliente fechado não trava mais o Remontar PT

Relato do dono: "remontar pt quebrou". Registro de 03/11 11:56–11:59: o cliente do Limago (PID 6868) tinha fechado e continuava marcado; o Remontar PT parava na verificação inicial ("Processo com identificação 6868 não está em execução") e o seguir repetia o erro a cada 3 s. Montar PT só pulava o Limago.

- `ClientWatch.cs`: a cada 2 s confere se os clientes da lista ainda estão abertos; quando um fecha, avisa no registro e sincroniza os personagens (o mesmo que "Sincronizar personagens"), sem interromper operação em andamento.
- Sincronizar: personagem que volta com outro PID retorna ao mesmo slot (e como principal) pelo nome.
- Remontar PT e Montar PT ignoram secundário com cliente fechado (com aviso) em vez de parar.

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo.

Publicada e verificada em 03/11/2026 (-SkipExport), Worker a631f376-6acb-4545-8700-57818ac5c487.


# FarmZilla 0.41.0 — Atacar manual em repetição

Pedido do dono: no modo "Manual por tecla", apertar Atacar repete o ataque a cada 4 s até apertar de novo.

- `AttackRepeat.cs`: a tecla Atacar (atalho 5) liga a repetição quando o ataque é aceito (mesmas conferências de antes: monstro no principal, alguém marcado em Ataque/Suporte) e ataca na hora; a cada 4 s pede o ataque de novo no monstro atual do principal. Sem monstro selecionado o ciclo é pulado e a repetição continua esperando. Apertar de novo desliga e para as macros.
- Encerra sozinha ao trocar o modo de ataque, Desativar, fechar, perder a autorização ou o Espelho; pula o ciclo durante Bufar, meditação, teleporte, missões ou PT.

Validação: compilação x86, 38 verificações, autotestes de ataque, prévia. Não testado no jogo.

Publicada e verificada em 03/11/2026 (-SkipExport), Worker 9c97bf90-7de4-4d32-b7a6-66937a95e99a.


# FarmZilla 0.40.0 — Mat Farm com até 10 contas

Pedido do dono: farmar com até 10 contas ao mesmo tempo, cada uma com o seu mapa, na mesma tela de hoje separada por conta.

- Abas no topo do Mat Farm (`RotaLink.cs`): uma por conta (nome do personagem conectado, "●" com farm rodando), "+ Conta" até 10 e "Fechar conta" (pede confirmação se o farm estiver rodando). Cada aba é uma tela completa do Mat Farm (`RotaForm` com `slot`); só a aba escolhida aparece, as outras continuam farmando.
- Cada conta tem os seus materiais, altura de voo, raio e pontos tirados (conta 1 usa os arquivos de antes, contas 2..10 `*-contaN.txt`); o histórico de materiais do mapa é um só para todas (lido e gravado só pela tela).
- O mesmo personagem não pode ser conectado em duas contas (`RotaForm.connected`).
- Defesa (macro 0) já era por personagem e sem estado compartilhado; fechar o FarmZilla ou tirar o Mat Farm para todas as contas.

Validação: compilação x86, 38 verificações, autotestes, prévia fora da tela com 3 contas (uma visível por vez, volta ao Espelho 548x606). Não testado no jogo com várias contas.

Publicada e verificada em 03/11/2026 (-SkipExport), Worker 05b489c6-837d-4e27-9c10-4b0c80765968 (1º probe falhou por propagação; -VerifyOnly aprovado).


# FarmZilla 0.39.0 — Espelho mais claro e Montar PT mais rápido

Pedido do dono:
- Ataque ligado por padrão ao colocar um personagem num slot; sacerdote (EP) entra em Suporte; pode trocar a qualquer momento (`rolePid` em `UpdateAttackRows`: o padrão só vale quando o personagem do slot muda).
- Caixas maiores e claras (`TickBox.cs`: quadrado com borda visível desligado, preenchido com ✓ ligado) e cabeçalhos inteiros "Ataque" e "Suporte" (combos dos secundários de 158 para 128 px).
- Ajustes só no menu lateral, embaixo; ferramentas em cima (Espelho, Mat Farm). Botão "Ajustes" do painel virou "Atualizar cliente" (procura/instala atualização); "Atualizar" virou "Sincronizar personagens" (texto em duas linhas no `NeonButton`).
- Montar PT: `PartyService.JoinAll` convida todos os secundários numa passada e aceita cada convite assim que chega (antes: um por vez, esperando a confirmação); confirmação nos dois clientes igual; 10 s por tentativa, 2 tentativas só para quem faltou.

Validação: compilação x86, 38 verificações, autotestes, prévia renderizada fora da tela. Não testado no jogo (Montar PT em paralelo precisa de teste real).

Publicada e verificada em 03/11/2026 (-SkipExport), Worker ce346cff-3218-4f89-bbba-bd247c18d7c3.


# FarmZilla 0.38.0 — licença vitalícia, ativação por código e ferramentas por PC

Pedido do dono: sem pedidos "Aguardando" (o cliente copia o código e envia; o dono cola no painel e ativa); depois de ativado, não depender de internet (venda vitalícia), mas ainda poder bloquear; deletar liberação; liberar ou tirar a Rota (qual "bot" cada um usa); painel mais bonito.

Servidor (work/online-control):
- `/v1/device/register` não cria mais pedido aguardando: código desconhecido só recebe "ainda não ativado". `/v1/admin/activate` (código colado + nome + ferramentas) cria o PC já liberado; a chave do PC é gravada na primeira consulta (o código é o hash do hardware e da chave, então outro PC não toma o código).
- Licença vitalícia `BGL3` (`format:3` no pedido): sem prazo, assinada com a mesma chave das autorizações, leva `features` (controle = Personagens; rota = Rota/Farm) e `log`. Clientes antigos continuam recebendo só a BGL2 de 24 h. Downloads de atualização aceitam BGL2 e BGL3 (sempre exigindo o PC liberado no banco).
- Deletar: `/v1/admin/remove` agora apaga qualquer PC (o PC perde o acesso na próxima consulta); pedidos aguardando antigos ficam escondidos.
- Registro só quando o dono liga "Enviar registro" para aquele PC (padrão desligado; antes todos enviavam a cada minuto).
- Banco: colunas `features` (padrão 'controle,rota') e `log_enabled` (padrão 0) — `migrate-0.38.0.sql`, rodar uma vez no D1 remoto antes de implantar.
- Painel: cartão "Ativar computador", ferramentas e registro por PC em botões, Bloquear/Liberar, Deletar com confirmação, sem aba Aguardando.

Cliente:
- Pede a licença vitalícia; sem internet usa a salva sem limite de tempo. Consulta online a cada 30 min só para receber bloqueio, deleção ou mudança de ferramentas; recusa explícita do servidor encerra pelo fluxo normal e apaga o cache.
- Nomes (pedido do dono): o painel de personagens se chama **Espelho** e a Rota/Farm **Mat Farm** (menu do app e painel).
- Ferramentas: sem "rota", o item fica "Mat Farm · bloqueado" e um Mat Farm aberto é fechado; sem "controle", o Espelho fica desativado com aviso, atalhos e espelhamento (timer de teleporte/seguir) desligados, ataque/missões/PT param.
- Registro enviado só com a licença indicando `log`.
- Rota: nome do personagem sem "PID" na lista e em "Conectado".

Validação: compilação x86, 38 verificações, 46 verificações do servidor (control.test.mjs), LifetimeLicenseTest (13) e autotestes anteriores. Painel conferido com dados de exemplo no navegador (desktop e celular). Não testado no jogo.

Publicada e verificada em 03/11/2026 (-SkipExport), Worker bfb4068b-4d68-4834-9dfe-66a497027824; migração D1 aplicada antes (3 liberados e 1 bloqueado ficaram com Espelho e Mat Farm).


# FarmZilla 0.37.0 — atalhos só com o jogo na frente

Relato do usuário: as teclas dos atalhos (1–6) ficam bloqueadas em outros programas (não digitam no navegador, WhatsApp etc.).

- Causa: RegisterHotKey entrega a tecla só ao FarmZilla, em qualquer janela.
- Agora os atalhos (seguir, montar/remontar PT, bufar, atacar, modo de ataque) são registrados só enquanto uma janela do jogo (processo ElementClient) está em primeiro plano, e liberados quando outro programa fica na frente (conferência a cada 0,3 s, junto do acompanhamento da janela ativa). Ao voltar ao jogo registram de novo; as linhas "atalho registrado" não se repetem no log a cada troca (falhas continuam). Dentro do jogo nada muda: digitar a tecla pura num PM do jogo continua sendo o atalho (usar combinação Ctrl/Alt/Shift nessas teclas, ou o mapeamento do chat depois).

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo.

Publicada e verificada em 03/11/2026 (-SkipExport), Worker e41966ea-062c-4f6c-baca-0aa81ac2e796.


# FarmZilla 0.36.0 — mapa de materiais embutido (pontos do PW Helper)

Autorização: o desenvolvedor do PW Helper autorizou o uso do que estiver no executável (WhatsApp, 03/11/2026 09:29, "o que tiver no executável e conseguir tirar tá autorizado"). Confirmado depois pelo usuário: o desenvolvedor também autorizou a venda junto com o FarmZilla; só não fornece o código-fonte.

- Extração só de leitura do resources/app.asar (Electron; programa nunca executado): 4.490 pontos de 40 materiais coletáveis do mapa do mundo, em coordenadas de mapa (x 94–793, y 102–1009) → mundo X=(x−400)×10, Z=(y−550)×10. Sem deslocamento sistemático contra 417 pontos reais dos nossos escaneamentos (mediana 22 de distância: o PW Helper marca o centro da área de nascimento). Sem altura.
- Materiais casados pelo item que a mina dá e conferidos pelos escaneamentos (minérios/madeiras exatos; ervas nascem misturadas): fulling=3096 Fuling, ferro_negro=3080, carvao_betumioso=3090, grama_cultivo_espiritual=3540 etc. Arquivo embutido recursos/RotaAvalon.PontosMapa.json (95 KB).
- `MaterialMap.Merge`: no mapa do mundo, os pontos entram na lista de materiais, no mapa e no plano do farm como conhecidos; um escaneamento real do mesmo material a até 40 tem prioridade.
- Pontos de área (`Waypoint.Area`, sem altura): voa por cima, procura o material até 60 do centro e desce em degraus de 20 até achar (para abaixo de 8); a altura achada fica no ponto para as próximas voltas. Busca/conferência a 60 nesses pontos (25 nos escaneados).

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo.

Publicação concluída: Worker d97ca84f-f050-403d-b0a7-b8b7ae2d6d4e, 03/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.36.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.35.0 — mapinha do farm sempre na frente

- Botão "Mapinha por cima" na aba Farm: janela pequena (300×330), sempre na frente e sem tomar o foco do jogo, só com o mapa do farm (personagem, plano numerado, materiais escolhidos, cercas) e uma linha de status (farmando/parado, volta, coletados, o que está fazendo). Arrasta pela barra, redimensiona pelo canto, fecha no ×; espelha o mapa da aba Farm a cada atualização.

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo.


# FarmZilla 0.34.0 — sequência fixa e menos tempo no ar

Pedido do usuário: ainda perde muito tempo no ar; seguir sempre a sequência da seleção; material indisponível → vai ao próximo. Vai deixar o farm rodando a noite.

- Sequência fixa: o plano numerado feito ao iniciar o farm é repetido na mesma ordem a cada volta (antes era refeito a partir da posição atual); pontos tirados ou materiais desmarcados saem; plano vazio é refeito (ex.: após escanear). Materiais marcados depois de iniciar só entram ao reiniciar o farm.
- Sem subir parado: a altura muda enquanto anda para o próximo ponto (antes subia no lugar antes de sair). Descida em rampa começa 400 antes do ponto (antes 250).
- Material ausente detectado do alto: na descida, a até 90 do ponto e até 36 acima do chão mapeado (medido: o jogo carrega materiais até ~41 abaixo e ~120 ao lado), confere o material; duas leituras sem ele (0,7 s) → pula ainda no alto ("pulado_ausente", visto_de_cima) e segue ao próximo, sem descer e subir de novo.

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo.

Publicação concluída: Worker 0d6c0b51-7740-4ae6-8567-8cce2d7c5db4, 03/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.34.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.33.0 — subida e descida mais precisas (sem rodopiar)

Relato do usuário: na subida/descida o personagem fica rodopiando e perde muito tempo de um ponto a outro.

- Causa: a altura só muda em movimento, e a rotina fazia saltos de 8 para cada lado em cima do ponto, girando quase no lugar.
- `Settle` (0.33.0): passadas retas de 30 sobre o ponto, descendo/subindo ao longo da passada; para assim que chega na altura (tolerância 1,0, antes 1,5), sem terminar a passada; passada travada troca de eixo e alarga (até 70); termina com pouso exato em (x,z). Menos curvas, mais altura por passada.

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo.

Publicação concluída: Worker 92d3f206-4018-4f45-aab2-c3d486262e9a, 03/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.33.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.32.0 — recompensa conferida por item novo; coleta mais rápida

Teste real da 0.31 (Lima, 02:07–02:28): 3081 (missão 61537) não tem recompensa cadastrada; nos pontos 1 e 3 a conferência aceitou pelo temporário consumido (recompensa_recebida item 0), e no ponto 7 repetiu a segunda opção e falhou (missao_nao_apareceu). Materiais cadastrados (797, 817, Carvão) concluíram. Cada ponto levava ~2 min, quase tudo subir/descer.

- Conferência por item novo: a recompensa é detectada como QUALQUER item novo no inventário além do temporário (`GainedExcept`), o que vale também para 3081. Confirma a missão só quando o item chega; enquanto a missão ainda está pendente (`InspectMaterialReward`), repete a segunda opção; missão sumiu com item novo = recebida. Vale para material com e sem recompensa cadastrada.
- Mais rápido: esperas da missão reduzidas (1,5 s→0,7; 1,2→0,6; retomadas 2,5 s). Material a até 170 de distância é alcançado baixo, sem subir à altura de voo e descer de novo (`perto`), poupando a subida entre materiais próximos.

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo.

Publicação concluída: Worker 01b82ef1-c34d-4643-9b67-59afefebe28c, 02/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.32.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.31.0 — recompensa da missão conferida pelo item final

Teste real da 0.30 (Lima, 23:02): Salgueiro coletado, missão 61532 aberta, segunda opção enviada, mas Tábua Polida 797 ficou 3→3; a conferência aceitou porque o temporário sumiu (abrir a missão já consome o temporário) e seguiu no mesmo instante — recompensa perdida.

- Material com recompensa conhecida: só conta com aumento do item final (até 12 s). Enquanto não chega, repete a escolha da segunda opção a cada 2 s (3 vezes) se a janela de recompensa ainda estiver aberta ("segunda_opcao_repetida"); depois reabre a missão se ela voltar a aparecer (até 3 tentativas). Temporário consumido só vale para materiais sem recompensa conhecida.

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo.

Publicação concluída: Worker b86ef0d3-8396-42dc-80ff-1dcb5b7c6045, 02/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.31.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.30.0 — o farm só para com "Parar"

Teste real da 0.29 (Lima, 22:39–22:48): ciclo completo (subir, ponto, descer, coletar, missão 797 0→3), depois 5 defesas seguidas com monstros derrotados; um monstro a 38,6 (provavelmente inalcançável/não atacante) não morreu em 90 s — a 1ª vez pulou o ponto, a 2ª ocorreu fora do ponto e encerrou a rotina ("erro: Monstro não derrotado em 90 s").

- Defesa: escolhe o mais próximo até 25 (depois até 40), ignora por 2 min monstros já desistidos; após 90 s desiste ("defesa_desistiu") e volta à rotina, sem erro.
- Farm: qualquer falha dentro ou fora de um ponto é registrada e contornada; vida < 30% → espera recuperar até 70% (defende se continuar apanhando, máx. 5 min); mochila cheia → espera 60 s e tenta; sem vida/troca de personagem/cliente fechado → espera 15 s; 6 falhas seguidas → pausa 30 s e continua. Só "Parar" encerra. Rota manual mantém o comportamento anterior (para em erro).

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo.

Publicação concluída: Worker 2f582b2c-32d3-4348-9b48-0829bc614c1f, 02/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.30.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.29.0 — cerca do raio no mapa; escaneamento na altura de voo

- Cerca: o mapa da aba Farm desenha o raio do farm (rubi, centro onde o farm começa) e o raio de escanear (azul, em volta do personagem), tracejados e com rótulo; mudar um raio ajusta o zoom para caber.
- Medição só de leitura (02/11/2026, ProFile, 63 s, 43→63 de altura): na altura 63 o jogo carregou materiais até 41 de altura abaixo (Y 411) e 123 na horizontal; durante a subida não havia material perto (posição não registrada); acima de 63 não houve amostra. Escaneamento passa a voar na Altura de voo (antes na altura atual), faixas de 120.
- Farm em execução (Lima, 22:39–22:43) fez o ciclo pedido: subiu 35→63, foi ao ponto, desceu a 25 (alvo 24), coletou, defendeu, missão 61532 com recompensa 797 0→3 na 1ª tentativa, subiu de novo. Idas e voltas para subir/descer reduzidas de 25 para 8, para ficar em cima da coordenada do material.

Validação: compilação x86, 38 verificações, autotestes; prévia com a cerca. Não testado no jogo.

Publicação concluída: Worker 856813d7-89db-48b2-a287-9b32867ae60c, 02/11/2026, pelo workspace original. Primeira verificação logo após a implantação falhou (propagação); -VerifyOnly aprovado (versão 0.29.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.28.0 — raio do farm e pontos tirados da rota

Pedido do usuário (imagem do plano cruzando o mapa): poder editar/remover pontos para a rota não ir longe.

- "Raio do farm" (padrão 1500, salvo em dados_usuario/raio_farm.txt): só entram pontos até esse raio do lugar onde o farm começou (antes de iniciar, do personagem). As voltas seguintes usam o mesmo centro.
- Clicar num ponto do mapa da aba Farm tira o ponto da rota (salvo em dados_usuario/pontos_excluidos.txt); "Restaurar pontos (N)" devolve todos.
- Prévia do plano ao vivo com o personagem conectado (atualiza ao marcar materiais, mudar o raio ou tirar pontos).
- `FarmPlanner.Build` com raio, centro e excluídos; `FarmPlannerTest` com 3 casos novos.
- Teste real da 0.27.0 (farm 20:49–21:12): coletas e missões confirmadas (817 21→24, 797 14→17); controle de altura respondeu (alvo 53/48/41 → 53,3/49,2/42). Entre materiais a menos de 250 não subia e batia no cenário. Agora sempre sobe primeiro até a altura de voo (idas e voltas curtas onde está), viaja nela e desce só no ponto; trechos acima de 500 começam a descer 250 antes.
- Missão do material (pedido: "diminuir a velocidade, check do clique"): nos logs, toda coleta de 3081 terminou sem missão (temporário 62493 expirava). Regra confirmada em jogo: missão = material + 58456 (3076→61532, 3090→61546, 3091→61547); vale para a família 3074–3092 (3081→61537). Ponte nativa: com missão esperada, clica só a linha com esse número (antes só aceitava as 3 conhecidas). Execução: espera 1,5 s após coletar, até 15 s para a missão, 1,2 s antes de escolher a recompensa (janela até 5 s), confere item final ou temporário consumido em 10 s; até 3 tentativas, cada uma no log. DLL SHA-256 574e907a179d3ac0f8d46e430a81993d94dea5f9b41883e74d65e8550132f4ef.
- Farm é a única tela da Rota (abas escondidas; Mapa, Rota manual e Log continuam montados para o código e a autoverificação).

Validação: compilação x86, 38 verificações, autotestes; prévia com cópia do histórico real (267 materiais): Salgueiro + Carvão no raio 1500 → 4 pontos (antes 14 pelo mapa todo). Não testado no jogo.

Publicação concluída: Worker 490412e9-1274-4f88-81ef-3080203bf985, 02/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.28.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.27.0 — altura de voo programável (comando nativo do jogo)

Sessão com o usuário (02/11/2026 19:57): leitura só de memória, 20 Hz, enquanto ele segurou Espaço e Z em voo (ProFile). Subida 18–27 s (Y 224→291), descida 27–37 s (291→220). Campos: host+0xB78 = direção vertical (0x10 sobe, 0x20 desce), host+0xF1C = velocidade vertical (±7,5), host+0xB04 = parado (1).

Desmontagem só de leitura (capstone): o trabalho de movimento automático tem controle de altura — +0x64 liga, +0x68 altura-alvo (escala Y×0,1), +0x6D decolagem automática, +0x6E manter altura atual; a cada passo (RVA 0x40DAF0) liga 0x10/0x20 em host+0xB78 até a altura. Mensagem 0x14A (tratamento RVA 0x3D7000): tipo 0 = voo reto até X/Z (inteiros) com controle de altura ligado pelo 3º campo (0x40F060); tipo 1 = altura-alvo inteira (+0x68), decolagem automática; tipo 2 = desliga; tipo 3 = caminho calculado (a navegação antiga) que desliga a altura (+0x64=0) — por isso a altura nunca valia.

- Ponte nativa: operação 6 `post_automove` põe na fila do jogo (RVA 0x44E8F0, conferido pelo prólogo) as mensagens tipo 0/1/2, na thread da janela, como o próprio cliente faz (0x4DFF80). DLL recompilada (Zig 0.16.0), 248 832 bytes, SHA-256 fixado 157e0c0aa13274a0c750f8a9bc2aeae5c65ab99d8e1a1a8ea9a9998194f6096f; código de máquina da chamada conferido.
- Execução: viagem em trechos de até 300 com voo reto + altura-alvo (sobe até a "Altura de voo" enquanto vai); pontos de coleta: últimos 250 com alvo na altura gravada do ponto, depois idas e voltas de 25 até ficar a 1,5 dela (máx. 80 s); trecho travado 6 s → contorna com a navegação antiga; recusa da operação 6 → segue na altura atual (aviso "altura_indisponivel"). Mapeamentos continuam na altura atual.
- Interface: "Altura de voo" na aba Farm (salva em dados_usuario/altura_voo.txt, padrão 60) e de volta na aba Rota manual.

Validação: compilação x86, 38 verificações, autotestes (ponte com DLL nova: ida e volta pelo gancho), planejador. Não testado no jogo: operação 6 (subir/descer pela mensagem do jogo), descida por idas e voltas, voo reto com obstáculos.

Publicação concluída: Worker d8b53ede-72dc-4b37-b19c-0eb3e89a8940, 02/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.27.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.26.0 — Farm automático de materiais (inclui a 0.25.0, não publicada separadamente)

Pedido do usuário (02/11/2026): remodelar o bot de materiais — saber os pontos de cada material, ir até eles, coletar os selecionados e seguir ao próximo até ser interrompido; interface simples. Autorizou seguir sem confirmação enquanto ausente (~30 min).

- Aba "Farm" (abre primeiro): lista de materiais coletáveis com "Pontos" (lugares escaneados neste mapa) e "Perto" (carregados agora); marcar = escolher (mesmas escolhas da rota, salvas). "Iniciar farm"/"Parar", status (volta, coletados, pontos no plano, pulados, defesas, agora), mapa com o plano. "Escanear arredores" com raio (até 30 000).
- `FarmPlanner`: pontos do histórico do mapa atual, só materiais escolhidos e com nível, sem repetir o mesmo lugar (6), ordem do mais próximo ao seguinte a partir do personagem, até 200 por volta. Replanejado a cada volta (novos achados entram); pontos coletados há menos de 150 s são pulados.
- Execução (`RotaExecution.cs`): um "visitar ponto" para rota manual e farm. No farm, ponto vazio, não alcançado ou coleta não confirmada é pulado e registrado; para com 6 falhas seguidas, mochila cheia, vida < 30%, sem vida ou troca de personagem. Missão de material pendente é concluída antes de coletar (antes parava a execução). Coleta de qualquer material coletável: validados pelo item temporário; os demais por aumento no inventário; missão do material quando aparece, sempre com a segunda recompensa. Voo desligado (ex.: derrubado em luta, caso real das 19:10) não para mais: aviso "sem_voo" e segue pelo caminho automático do jogo.
- Altura: a navegação nativa não controla altura (mensagem 0x14A = caminho automático X/Z; o 4º campo dos chamadores do jogo é o mapa, formato "%f%f%f%d"). Viagem na altura atual. Subir até a altura programada depende de localizar o comando nativo de subir do voo (sessão ao vivo com o usuário segurando Espaço, só leitura).
- Abas: Farm · Mapa e materiais · Rota manual · Log da execução (Inventário saiu da vista; continua lido).
- Da 0.25.0: mapa da aba Rotas mostra todos os coletáveis lembrados; leitura/gravação de materiais continua com a Rota oculta enquanto algo executa.

Validação: compilação x86, 38 verificações; `FarmPlannerTest` (novo) e demais autotestes; prévia da aba Farm com cópia do histórico real (130 materiais; Salgueiro + Carvão → plano de 8 pontos). Não testado no jogo: farm contínuo, coleta de materiais não validados, seguir sem voo.

Publicação concluída: Worker 717ea83b-569f-4187-a0b8-c65d5497aaa8, 02/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.26.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.25.0 — materiais lembrados também no mapa da aba Rotas; escaneamento grava com a tela oculta

Relato do usuário: "não está salvando as posições do escaneamento", "eles somem, não consigo programar a rota". Conferido: o histórico estava gravando (130 materiais, 7 no trajeto do escaneamento das 19:14, 12 perto de Campo de Su Muer); a aba Mapa e materiais mostrava todos (render com cópia do histórico real). O mapa da aba Rotas só mostrava os materiais já escolhidos.

- Mapa da aba Rotas mostra os mesmos coletáveis lembrados que a aba Mapa (mais os escolhidos); clicar adiciona o ponto e escolhe o material.
- Com rota/escaneamento em execução, a leitura de materiais (e a gravação no histórico) continua mesmo com a Rota oculta ou minimizada; parada, segue sem ler.

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo.


# FarmZilla 0.24.0 — lista só de materiais próximos, escolhas salvas; altura fora da navegação

Teste real da 0.23.0 (02/11 19:04): coleta de 3091 e 3076, missões 61547/61532 e recompensas (817 6→9, 797 9→12); defesa com macro 0 duas vezes (monstro derrotado em 21 s). Altura não mudou ("altura_nao_atingida" 22,6 pedido, 25,1 real; pontos a menos de 420 não tentavam subir).

- Desmontagem só de leitura do ElementClient.exe (capstone 5.0.7 instalado na pasta temporária com autorização do usuário): a navegação nativa (módulo+0x4DFF80) só põe na fila a mensagem 0x14A com X/Y/Z e um modo; é o caminho automático do jogo até X/Z — chamadas do próprio jogo passam Y=0. Altura não é controlável por ela. Viagem passa a manter a altura atual (comportamento que coletou com sucesso); campo "Altura de voo" escondido até existir subida nativa (próximo passo: localizar o controle de subir em voo com leitura de memória enquanto o usuário sobe no jogo).
- Lista "Materiais para a rota": "Só próximos" marcado por padrão e baseado nos materiais carregados ou lembrados dentro do raio da lista (antes: só os carregados); escolhidos sempre aparecem. Escolhas salvas em `dados_usuario/escolhas_materiais.txt` e recarregadas ao abrir/criar rota.
- Painel de materiais 0,6 ms por ciclo no teste de carga.

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo: lista de próximos com o cliente conectado.

Publicação concluída: Worker 82c5ada9-7a4a-46de-a0e6-46e1b1da07f9, 02/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.24.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.23.0 — rota sobe/desce andando; "Escanear raio"

Teste real da 0.22.0 (02/11 18:46, log da Rota): "subindo" e o personagem parado até o usuário parar (25 s). A navegação nativa ignora um destino só vertical. Em seguida duas tentativas pararam com o voo desligado.

- Altura muda só junto com o avanço: viagem na altura de voo subindo o mais cedo possível (até 110 por trecho de 120); para pontos de coleta, descida em rampa nos últimos 420 até a altura gravada do ponto, chegando junto com a posição; material procurado lá embaixo. Chegada é horizontal; altura não aplicada vira linha "altura_nao_atingida" no log (não para a rota). Linha "chegou" com a altura em cada ponto.
- Personagem parado 5 s após um comando de movimento → para com "O personagem não se moveu com a navegação (altura …)" em vez de esperar 35 s. Luta durante um trecho reenvia o trecho.
- Início exige voo ligado com mensagem clara.
- "Escanear raio" (Mapa e materiais): varre em faixas de 120 o quadrado do raio escolhido (até 30 000, limitado ao mundo e a 8000 destinos) em volta do personagem, gravando todo material encontrado; mostra a estimativa de tempo no log. Medição só de leitura no cliente: o jogo mantém materiais carregados só a ~110 de distância, por isso não há leitura instantânea de raio grande. Mapeamentos voam na altura atual do personagem (não na de 60), onde os materiais aparecem.
- Raio da lista até 30 000; lista de próximos limitada aos 300 mais perto.

Validação: compilação x86, 38 verificações, autotestes. Não testado no jogo: se a altura muda junto com o avanço (o log "chegou"/"altura_nao_atingida" mostrará), escaneamento longo.

Publicação concluída: Worker ba2c84d4-5550-4122-8b07-ba5d0dc0d985, 02/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.23.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.22.0 — mapa do jogo na Rota, rotina com altura 60 e defesa com macro 0

Pedidos do usuário (02/11/2026): mapa como o mapa grande do jogo, com os ícones dos materiais escaneados para montar a rota; posições reais dos materiais; ao executar, subir à altura 60, ir ao ponto, descer, coletar, atacar com a macro 0 o monstro que atacar, fazer a missão, subir de novo e seguir. Complemento: na altura 60 o material não é lido; descer ao ponto mapeado e só então identificar.

- Mapa: blocos `surfaces/maps/RRC.dds` do jogo instalado (11×8, 1024 unidades cada; lidos em memória, nada distribuído), conferido pela Cidade do Dragão em (554, 653). Materiais como selos redondos com o ícone do item (aro dourado carregado, vermelho nível insuficiente, cinza lembrado, Rubi escolhido); nomes só com zoom; nome do personagem junto ao marcador. O mapa mostra todos os coletáveis lembrados; clicar num selo escolhe o material e adiciona o ponto.
- Posições: o cliente não traz posições de materiais (só servidor). `task_npc.data` e `world_targets.txt` conferidos: são NPCs de missão e cidades. As posições vêm do escaneamento (histórico/Mapear área), exatas.
- Rotina (`RotaExecution.cs`): sobe até a "Altura de voo" da rota (campo novo na aba Rotas, padrão 60 = Y 600; conferido no cliente: 60,8 de altura = Y 608), viaja nela em trechos de até 120 na horizontal e 110 na vertical; acima do ponto desce até a altura gravada do ponto e só lá procura o material (até 25 de distância); sem material, sobe e segue; coleta (até 3 pedidos se uma luta interromper), missão de material e segunda recompensa como antes; sobe de novo.
- Defesa: vida caindo durante a rota → monstro mais próximo até 40 (gerenciador de NPCs em mundo+0x24, tabela +0x1C/+0x20, `CECMonster` com id em +0x118, mapeado só por leitura) recebe a macro 0 pela ponte de ataque do FarmZilla (`NativeAttackRuntime`), repetida até o monstro sumir (máx. 90 s); vida abaixo de 30% para a rota. Navegação/coleta não exigem mais vida cheia (só vivo).
- Teste só de leitura no cliente aberto: 33 monstros lidos em 1 ms, mais próximo a 14,3.

Validação: compilação x86, 38 verificações, autotestes, prévias do mapa. Não testado no jogo: subir/descer pela navegação nativa (movimento vertical), coleta após descer, defesa com macro 0 em voo — primeiro teste do usuário.

Correção de processo: a rotina foi escrita por engano na pasta já publicada da 0.21.0 e o arquivo foi restaurado da cópia publicada (workspace original) antes de seguir na 0.22.0.

Publicação concluída: Worker b3cc12a8-b682-4731-98dc-cd72d42ebc71, 02/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.22.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.21.0 — menu lateral, Rota na mesma janela, Rota mais leve

Pedidos do usuário: menu lateral no estilo da imagem enviada mantendo o visual Rubi; a Rota não deve abrir outra janela com o espelho aberto; otimizar ("está muito pesado"); histórico só com itens coletáveis no chão, no mundo, Céu e Inferno.

- Menu lateral (`SideMenu.cs`, 158 px): Personagens, Ajustes e, em Ferramentas, Rota. Personagens = painel aprovado 390×606 inalterado, deslocado; janela 548×606. Rota aparece na mesma janela (cresce até 1278×800, limitada à área de trabalho) e volta ao tamanho/posição do painel. Botão Rota da barra inferior removido (barra volta a Desativar · Atualizar · Ajustes).
- Rota embutida (`RotaForm.Embed`): sem cabeçalho próprio; criada no primeiro uso. Escondida ou minimizada não lê o jogo para as telas (rota em execução segue na própria thread; gravação de trajeto continua).
- Otimização (medida fora do jogo, histórico ~9 mil locais, 40 materiais): histórico 26 → 2 ms por ciclo (índices por instância e material), painel de materiais 109 → 1,6 ms (lista refeita só quando o filtro muda, contagens por dicionário, lista de próximos só quando muda, nada com a aba oculta); inventário só redesenhado quando muda; mapas só redesenham quando o personagem se move; blocos de terreno pré-multiplicados e interpolação bilinear simples.
- Histórico: só materiais coletáveis (nome e item no catálogo: 428 de 1523); objetos de missão e registros sem nome aparecem enquanto carregados mas não são lembrados, e saem do arquivo antigo. Separado por mapa: "mundo" ou limites do terreno (Céu, Inferno e outros mapas entram sozinhos; aviso "Histórico: mapa novo" no log). Lista de materiais mostra só coletáveis (mais os já escolhidos).
- Bot_Rota_1.5.3.exe não foi usado: Defender = Trojan:Win32/XWorm.BAC!MTB; leitura estática mostrou carregador com 1,2 MB cifrado e chave de 32 bytes, sem código do jogo.

Validação: compilação x86, 38 verificações, autotestes (Rota integrada, ataque, teleporte, lista, registro, capturas), prévias fora da tela do painel 548×606 e da Rota na mesma janela com volta ao painel. Não testado no jogo: rota real, Céu/Inferno (limites reais ainda não vistos), desempenho conectado.

Publicação concluída: Worker f9029f15-6de4-4c2b-951d-a1969a234e9d, 02/11/2026, pelo workspace original. Verificador remoto aprovado (versão 0.21.0, assinatura/tamanho/hash; download anônimo negado). -SkipExport respeitado.



# FarmZilla 0.20.0 — Rota integrada (RotaAvalon 0.6.1)

Pedido do usuário: juntar o RotaAvalon ao FarmZilla numa ferramenta só, com a interface do FarmZilla, num submenu "Rota", recebendo as atualizações e regras do FarmZilla.

O que mudou:
- Botão **Rota** na barra inferior (Ativar · Rota · Atualizar · Ajustes); janela 390×606 mantida. Abre a janela "FarmZilla · Rota" (uma por FarmZilla) com as abas Mapa e materiais, Rotas, Inventário e Log da execução — mesmas funções e limites do RotaAvalon 0.6.1 (navegação nativa até 150 unidades por comando, coleta com missões 61532/61546/61547, segunda recompensa, voo obrigatório, para em dano/mochila cheia/falha).
- Visual Rubi: tema escuro aplicado sobre o layout original (`RotaTheme`), abas próprias (`RubiTabs` + faixa desenhada), barras de vida/mana, combo de personagem com ícone de classe vindo da lista do FarmZilla, barra de título escura no Windows 11.
- Fontes: `Rota*.cs` (namespace `RotaAvalon`, sem `Main` próprio; `MemoryProbe` reduzido ao leitor `ProcessMemory`). Integração em `RotaIntegration.cs` e `RotaLink.cs`.
- Um exe só: catálogo (`recursos/RotaAvalon.Catalogo.json`) e ponte nativa (`recursos/RotaAvalon.NativeBridge.dll`) embutidos; `work/Build-Release.ps1` passou a embutir a pasta opcional `recursos` (versões antigas compilam igual).
- Ponte nativa recompilada a partir de `rota-nativo/RotaNativeBridge.c` (Zig 0.16.0 do PyPI, `zig cc -target x86-windows-gnu -shared -O2 -fno-stack-protector ... -luser32 -lkernel32`). Mesmo tamanho do binário original (247 296 bytes); difere em 20 bytes (data/identificador de símbolos). SHA-256 fixado em `RotaPaths.BridgeSha256` = a904bc6bc6b3f0b24a99df80b575492c612f7b3c6e2e5f98fec6998284e367d4. É gravada só quando a Rota executa, em `%LOCALAPPDATA%\AvalonInterno\Rota\RotaBridge-a904bc6bc6b3.dll`, e o hash é conferido antes de cada carga.
- Arquivos da Rota em `%LOCALAPPDATA%\AvalonInterno\Rota` (logs, dados_usuario/materiais_observados.json, rotas). Mensagens da Rota também vão ao registro do FarmZilla (/admin > Registro) com prefixo "Rota:".
- Trava (`RotaLock`): personagem em rota fica fora de seguir, teleporte, missões, pop-ups, buff, meditação, montar/remontar PT e ataque/suporte do FarmZilla até a rota parar (o principal em rota não ataca nem bufa pelo FarmZilla). Liberado quando a execução termina.
- Licença: rota só inicia com o FarmZilla autorizado; negativa para a execução. Fechar o FarmZilla para a rota (aguarda a chamada nativa em andamento) e guarda alterações não salvas em `rotas/rota-nao-salva-*.json`, sem diálogo.

Validação: compilação x86 e 38 verificações (atualizador/licença); `RotaIntegrationTest` (novo): autoverificação original da Rota sem ler clientes (rotas, catálogo 1523, histórico, mapeamento, janela com 4 abas e mapa selecionado), extração da ponte com hash fixado e reposição de arquivo sujo, ida e volta real da DLL recompilada pelo gancho na janela do próprio processo (operação 0, nenhuma função do jogo), recusa de DLL alterada, trava por PID; demais autotestes (ataque, teleporte especial, lista principal, registro, capturas) aprovados. Prévias fora da tela da janela principal e das quatro abas da Rota.

Não testado: navegação, coleta e recompensa no jogo real dentro do FarmZilla (dependem do usuário, com voo ligado); leitura de clientes reais pela Rota; convivência real com seguir/ataque durante uma rota.


# FarmZilla 0.19.25 — resposta mais rápida na troca de alvo

Teste real da 0.19.24 aprovado pelo usuário, com pedido: "aumenta mais a velocidade de resposta para atk e suporte quando muda de TG". Registro anterior: comandos de início saíam um personagem por vez (~0,12–0,16 s cada); na troca de monstro havia ainda uma rodada sequencial de parada das macros antigas antes dos novos inícios.

Alteração (`AttackController.Tick`):
- Fase 1 decide quem precisa de comando (leitura de memória); fase 2 confere o alvo do principal uma vez e envia todos os inícios (ataque e suporte) em paralelo — cada personagem é um processo de jogo próprio, com sua própria ponte; resultados aplicados e registrados em ordem depois.
- Troca de monstro de um atacante: a sessão antiga é descartada sem `Stop` separado; o novo `Start` já interrompe a macro em execução (`StopMacro(-1)`) antes de iniciar a macro 0 no novo alvo. Suporte segue sem reinício na troca (0.19.23).
- Demais regras (contínuo, destravar, ESC, seguir) inalteradas.

Validação: compilação x86, 38 verificações; `AttackContinuousTest` (novo: troca de monstro sem rodada de parada; contadores do simulador atômicos para os inícios paralelos) e demais autotestes. Não testado com personagens.


Publicação concluída: Worker f6fd0c50-366f-4180-b5da-15f6d713fd53, 01/11/2026. Verificador remoto aprovado (versão 0.19.25, assinatura/tamanho/hash; download anônimo negado). Exe publicado SHA-256 5CC41097A2A6AAFBD3856911D8D5A6E442F04B389AAFBA1515C130D716697C9B. -SkipExport respeitado.

## 0.46.7 — ataque começa na hora

- Pedido do dono: no Automático, o principal pegou o TG e todos já começam a combater; na tecla Atacar (Manual), a mesma rapidez.
- `AttackFast.cs`: verificação a cada 100 ms (só leitura do alvo do principal) chama o ataque na hora para TG novo e acompanha as macros em execução (reinício e parada sem esperar o timer de 300 ms que também cuida de teleporte, missões, meditação e seguir).
- TG novo e tecla Atacar fazem a retomada do seguir ceder a vez; Bufar/PT/Alto esperam até 2 s uma checagem curta do ataque em vez de recusar.
- Alvo novo: a conferência "atacável" no principal roda junto com os inícios dos secundários (antes vinha antes de todos); o início do próprio principal espera essa conferência (mesma ponte).
- Cancelar o seguir antes do ataque confere a cada 20 ms (antes 100 ms).
- Testado: compilação (38 verificações), AttackContinuousTest, PrincipalListTest, RotaIntegrationTest. Não testado no jogo.
- Publicada e verificada 05/11/2026, Worker `4d81fad1-b1b9-4c07-930b-64c3685804eb`, -SkipExport.

## 0.46.8 — escolher quem recebe o suporte

- Pedido do dono: escolher em quem o sacerdote (EP) marcado em Suporte usa a macro 2 (suporte e cura).
- `SupportTarget.cs`: campo "Suporte em" abaixo dos secundários (padrão "Líder (principal)", ou qualquer personagem aberto); salvo por nome em `%LOCALAPPDATA%\AvalonInterno\suporte-alvo.txt`; nome que não está aberto ou o próprio sacerdote = líder.
- `AttackController.Tick(..., supportOn)`: trocar a escolha para e recomeça o suporte no novo alvo; status do teleporte ficou em uma linha.
- Testado: compilação (38 verificações), SupportTargetTest (novo), AttackContinuousTest, PrincipalListTest, prévia da tela. Não testado no jogo (o sacerdote precisa alcançar/ver o personagem escolhido).
- Publicada e verificada 05/11/2026, Worker `3a912fb5-adfa-4869-b9ae-e5160135897c`, -SkipExport.

## 0.47.0 — Mat Farm Manual (trajeto gravado), defesa que troca de alvo

- Pedido do dono: aba Manual no Mat Farm — gravar o caminho voando, salvar/carregar em .txt, rodar repetindo; onde o material marcado tiver nascido perto do caminho, para, desce reto, coleta (com missão) e sobe de volta.
- `RotaTrail.cs` (`TrailRoute`, `TrailRecorder`: ponto a cada 20, curva > 25° após 8, mudança de altura ≥ 3, estado de voo), arquivo `tipo=trajeto`, `trajeto=x;y;z;voo`, `coleta=material;item;x;y;z`, `alcance=`, `materiais=`.
- `RotaTrailRun.cs` (`RouteExecution.StartTrail`): começa no ponto mais perto; voa cada ponto na altura gravada; olha os materiais marcados a cada 0,6 s dentro de "Descer até" (padrão 50); `Visit` do farm; volta à altura e continua o mesmo trecho; circular se o fim está a ≤ 60 do início, senão ida e volta; material tentado fica 150 s de lado.
- `RotaTrailPanel.cs`/`RotaTrailForm.cs`: botões Automático/Manual, Gravar/Parar gravação, Rodar trajeto, Parar, Salvar/Carregar trajeto, "Descer até"; último trajeto e modo guardados por conta (`trajeto.txt`, `modo_farm.txt`); mapa mostra a linha do trajeto.
- Defesa (todas as rotas): 60 s por monstro (antes 90), depois troca; após matar/desistir, dano nos últimos 2 s (ou no próximo 1 s) = pega o próximo monstro mais perto; 5 s sem dano = volta à rota; limite 6 monstros/4 min.
- Removidos "Escanear arredores" e o raio dele (materiais já vêm do mapa embutido).
- Testado: compilação (38 verificações), TrailTest (novo, inclui tela Manual fora da tela), MaterialListTest, RouteTextTest, RotaIntegrationTest, AttackContinuousTest, SupportTargetTest. Não testado no jogo (gravação, execução e a nova defesa).
- Publicada e verificada 05/11/2026, Worker `e78b0306-0516-4a0e-a01d-b1e65b84e8e8`, -SkipExport (1º probe falhou por propagação; -VerifyOnly aprovado).

## 0.47.1 — carregar rotas de outro programa (Mat Name / cordx-cordy)

- Pedido do dono: usar no Manual as rotas gravadas por outro programa só para carregar e rodar; nada dessas rotas é embutido ou copiado no FarmZilla (lembra só o caminho do arquivo em `trajeto-externo.txt`).
- `RotaTrailImport.cs`: "Mat Name:" (nomes, inclusive cortados em 25 letras — "Pedreira de Pedra de Amol" = Amolar 3086, "Ameixeira de Cálice Verme" = Vermelho 3543) + "Coordenadas:" com `cordx = x, cordy = z` (X/Z internos, vírgula decimal, sem altura). Conferido: pontos a 8–78 dos materiais listados no mapa embutido.
- Sem altura no arquivo: cada trecho voa na altura em que o personagem estiver. Rota importada fecha a volta se o retorno ao início não for maior que o maior trecho dela.
- Salvar trajeto oferece também o formato Mat Name/cordx (sem altura).
- Testado: TrailImportTest (exemplo próprio + leitura, sem cópia, das 6 rotas do dono: todas com todos os materiais reconhecidos), TrailTest, compilação (38 verificações). Não testado no jogo.
- Publicada e verificada 05/11/2026, Worker `66a2a392-5436-4ea2-bd9e-cbc229f844c8`, -SkipExport.

## 0.47.2 — raio do farm automático começa em 900

- Pedido do dono: o raio do farm automático abre sempre em 900 (antes 1500 ou o último salvo); quem quiser mais aumenta na tela. Rota .txt carregada continua usando o raio dela.
- Testado: compilação (38 verificações). Não testado no jogo.
- Publicada e verificada 05/11/2026, Worker `e9511d23-1d5a-44d9-920b-e2ab79345039`, -SkipExport.

## 0.47.3 — Manual voa na altura de voo; defesa não larga monstro vivo

- Observado ao vivo (05/11 10:11–10:16, Limago, VILA DA PEDRA importada): rota sem altura + começo no chão = voava a 23–28 (chão), batia em casas/montanha (6 destravar em 1 min parado em x≈1270 z≈-1230) e era atacado o tempo todo; defesa encerrava "sem dano 5 s" com o monstro vivo e recomeçava 2 s depois (801065C3 4 vezes).
- Manual: campo "Altura de voo" (padrão 60, salvo por conta em `altura_trajeto.txt`) para pontos sem altura; decola e sobe reto antes do primeiro trecho; após coletar volta a essa altura.
- Defesa: "sem dano" só encerra se o início da macro falhar (alvo indisponível/morto) ou se o monstro estiver a mais de 40; senão continua até morrer ou 60 s.
- Testado: compilação (38 verificações), TrailTest, TrailImportTest, RotaIntegrationTest. Não testado no jogo.
- Publicada e verificada 05/11/2026, Worker `80a370e7-9741-4a61-8b01-16ffa6f10652`, -SkipExport.

## 0.47.4 — atualização automática não fecha o Mat Farm rodando

- Dono: "do nada o aplicativo volta para o Espelho e para". Registro 05/11: 09:43:44 (0,3 s após achar 0.46.8) e 10:32:13 a consulta automática (30 min / 3 s após abrir) instalou e reiniciou com o Limago em rota — só esperava não haver secundários marcados no Espelho.
- `UpdateUI.cs`: `MatFarmRunning()` — com qualquer conta do Mat Farm rodando, a consulta automática só avisa "Nova versão disponível"; instala sozinha apenas sem nada rodando. Botão Update continua perguntando antes.
- Testado: compilação (38 verificações). Não testado no app (a 0.47.3 ainda tem a regra antiga até atualizar).
- Publicada e verificada 05/11/2026, Worker `826ed710-f604-42ea-b699-6176fe397e12`, -SkipExport.

## 0.47.5 — atualização só pelo botão Update

- Pedido do dono: a atualização só instala quando a pessoa clica em Update; a consulta automática (30 min / 3 s após abrir) apenas avisa "Nova versão disponível" e muda o botão para "Update disponível".
- Testado: compilação (38 verificações). Versões ≤ 0.47.3 ainda instalam sozinhas ao achar esta.
- Publicada e verificada 05/11/2026, Worker `39742275-e8c2-47f1-b45b-cc71637e4941`, -SkipExport.

## 0.47.6 — altura padrão do Manual 40

- Pedido do dono após a observação ao vivo (subir/descer ~0,75/s; 60→25 ≈ 47 s): "Altura de voo" do Manual para rotas sem altura começa em 40 (antes 60). Valor salvo em arquivo novo `altura_trajeto_v2.txt` para todas as contas começarem em 40.
- Testado: compilação (38 verificações). Não testado no jogo.
- Publicada e verificada 05/11/2026, Worker `9f681679-3279-4d05-bf82-6f93564c90e0`, -SkipExport.

## 0.47.7 — TG em você = todos seguem na hora; controle não tira do combate

- Pedido do dono: pegar o próprio TG tem que fazer os secundários seguirem "a milhão" (fugir do boss), sem ESC; conta longe tem que voltar a seguir em 1–3 s ao chegar perto; retomar TG de monstro combate na hora; atordoado/sono não pode mandar de volta ao líder enquanto há TG de monstro.
- `AttackController.StopAll(release)`: para as macros de todos ao mesmo tempo, sem ESC; `TakeReleased` faz a tela mandar seguir na hora (`FollowNowAll`).
- `MaintainFollowing`: comandos de seguir em paralelo (antes um por um, até 1,5 s cada); falha (conta longe) tenta de novo a cada 2 s (antes 3→30 s); registro só na 1ª e a cada 30 falhas.
- `Engaged`: secundário fica em combate enquanto existir sessão (principal com TG de monstro), mesmo com macro interrompida/falha — seguir não puxa no meio do controle. Leitura por cópia (`Snapshot`) para não cruzar com o ciclo de ataque.
- `Command` (seguir): `blocked` com trava, porque agora roda em paralelo.
- Testado: compilação (38 verificações), AttackContinuousTest (expectativa do ESC trocada pela nova regra), SupportTargetTest, PrincipalListTest. Não testado no jogo.
- Publicada e verificada 05/11/2026, Worker `6f9695a9-7d7a-4518-948c-6b9dca6d2226`, -SkipExport.

## 0.47.8 — "Minério de Ferro Negro" separado por item (Liga de Aço)

- Dono: "não tem liga de ferro?". O jogo dá o mesmo nome a 3080 (Aço de Alto Carbono) e 3082 (Liga de Aço); a lista juntava os dois numa linha.
- `ResourceCatalog.FarmName`: nome repetido com itens diferentes vira "Nome → Item" na lista do Mat Farm (único caso no catálogo: Minério de Ferro Negro → Aço de Alto Carbono / → Liga de Aço). Pesquisa acha também pelo item ("liga").
- Testado: compilação (38 verificações), MaterialListTest (nova verificação), RotaIntegrationTest. Não conferido no app.
- Publicada e verificada 06/11/2026, Worker `2416f02b-a1f8-4a38-bbde-7cd91cd2407d`, -SkipExport.

## 0.47.9 — lista do Mat Farm pelo nome do item

- Pedido do dono: todos os materiais com o nome do item que dão (ex.: Pilha de Salgueiro → "Tábua Polida", Minério de Ferro Negro → "Liga de Aço"/"Aço de Alto Carbono"). Os 49 itens têm nomes diferentes.
- `ResourceCatalog.FarmName` = item; `MaterialName` = nome no chão, usado na pesquisa e na dica ao parar o mouse ("No chão: …"). Substitui a separação só do Ferro Negro da 0.47.8.
- Testado: compilação (38 verificações), MaterialListTest (item no nome, nomes únicos, pesquisa "ferro negro"), RotaIntegrationTest, TrailImportTest. Não conferido no app.
- Publicada e verificada 06/11/2026, Worker `2c170a34-a5e6-49ae-9c70-c8057cea669d`, -SkipExport (1º probe falhou por propagação; -VerifyOnly aprovado).

## 0.48.0 — missão do material antes da defesa; vida baixa não para a luta

- Log 06/11 22:39 (Limago, farm, Liga de Aço 3082): atacado cavando → defesa → vida < 30% = "parado por segurança" (exceção) → ponto e missão perdidos → esperando vida voltar sem lutar → morreu (hp 170 → 0).
- Pedido do dono: priorizar sempre a missão, mesmo com monstro batendo; temporário → missão → espera maior → item definitivo.
- `RouteExecution`: `missionFirst` durante a missão do material — dano não inicia defesa e vida baixa não interrompe; o dano fica marcado (`hitDuringMission`) e a defesa começa logo depois da missão. Esperas: 1,5 s após o temporário (antes 0,7), 1,2 s após a missão aparecer (0,6), 1,5 s após a 2ª opção antes de conferir, repetição da 2ª opção a cada 4 s (2), conferência até 14 s (10).
- Defesa: vida < 30% não encerra mais a luta (antes lançava exceção e o personagem ficava parado apanhando).
- Observado: ervas (ex. 3538, 3543) sem missão — "missao_nao_apareceu" após 12 s de espera; não alterado ainda.
- Testado: compilação (38 verificações), RotaIntegrationTest, TrailTest, MaterialListTest. Não testado no jogo.
- Publicada e verificada 06/11/2026, Worker `cec73977-f1ee-4922-acf3-603dce482405`, -SkipExport.

## 0.48.1 — ervas sem missão não esperam 12 s

- Log 05/11: ervas/flores (3538, 3543) nunca mostraram missão e cada coleta esperava 12 s ("missao_nao_apareceu"). Materiais sem missão conhecida (fora da família 3074–3092) esperam agora só 4 s (+1,5 s após o item); os da família continuam até 12 s.
- Testado: compilação (38 verificações). Não testado no jogo.
- Publicada e verificada 06/11/2026, Worker `9dac44b3-8473-4770-993a-bf6fb420991f`, -SkipExport.

## 0.48.2 — missão das ervas/flores pelo mesmo número dos minérios

- Log 06/11 23:01:53 (Limago, VILA DA PEDRA): Verme Bastão 3538 coletado (item 62609), "missao_nao_apareceu expected 0" — o dono pegou a missão na mão. A janela só aceita a linha com o id exato da missão e as ervas não tinham id (só 3074–3092 = material+58456).
- `MissionFor`: mesma numeração (material + 58456) para todos os materiais de chão 3074–3103 e 3535–3553 (ex.: 3538 → 61994). Hipótese: confirmada só na família 3074–3092 (3076, 3077, 3081, 3082, 3086, 3090, 3091); se errada, nenhuma linha é escolhida, como antes.
- Testado: compilação (38 verificações). Não testado no jogo.
- Publicada e verificada 06/11/2026, Worker `a2459529-5a56-4d90-b121-2c8afd05ca14`, -SkipExport.

## 0.49.0 — bolsa tática equipada sozinha

- Pedido do dono: a bolsa tática que se ganha ao entrar na cidade de gelo deve ser equipada sozinha em todas as contas ao aparecer na mochila.
- Descoberta (06/11/2026 20:16, com o registro de comandos ligado): item 52287 (estava nas 6 mochilas; saiu da do Limao, posição 20). Comando do jogo ao equipar no principal: `11 00 14 16` (posição 20 da mochila → equipamento 22), seguido de `15 00`.
- `BagAutoEquip.cs`: a cada 2 s lê a mochila (só leitura) das contas da lista; achando 52287, envia `11 00 <posição> 16` por `TeleportBridge.SendEquip` (lista fechada: só tipo 0x11 para o equipamento 22) e confere em até 1,5 s que saiu da mochila; até 3 tentativas, depois 10 min de pausa naquela conta. Só equipa de novo se aparecer bolsa nova.
- `RotaSelfTest`: regra da 0.48.2 (erva 3546 → missão 62002; baú 4308 sem missão). A 0.48.2 foi publicada sem rodar este teste; ele não roda dentro do app.
- Testado: compilação (38 verificações), RotaIntegrationTest, TrailTest, MaterialListTest. Equipar nos secundários NÃO testado no jogo (o comando só foi visto no principal).
- Publicada e verificada 06/11/2026, Worker `775e4d01-01e5-4206-aec6-01f90c817d55`, -SkipExport.

## 0.50.0 — endereços do jogo por assinatura de bytes (preparada, NÃO publicada)

Pergunta do dono: cada atualização do jogo troca o ElementClient.exe (SHA-256) e o FarmZilla para, porque tudo estava preso a um hash e a endereços fixos. A validação **não foi retirada**: ela passou a aceitar também uma compilação nova cujo perfil completo seja encontrado por assinaturas.

Como funciona (`GameProfile.cs`, `GameSignatures.cs`, tabela gerada `GameSignatures.Data.cs`):
- Hash conhecido (`34fca82c…`): usa os endereços validados de sempre, sem procurar nada (comportamento igual à 0.49.0).
- Hash desconhecido: lê o ElementClient.exe do disco (o arquivo do próprio processo), procura 38 alvos — 20 funções, 6 dados globais (root 0x9B870C, root da Rota 0x116E55C, tabela/contagem de teleporte, texto "AutoKillMonster", string vazia) e 12 tipos por RTTI (vtables de seguir, combo, janelas NPC/missão, time, caixa de mensagem, missões, itens) — e confere 6 guardas de deslocamento do personagem (host+0x1D3C macro, +0x5FC alvo, +0xB78 vertical, +0x4F0 id, +0x1D1C trabalhos, +0x1D24 missões).
- Regras: cada assinatura achada precisa ser única no .text; um alvo com duas assinaturas (início da função + chamada; ou duas referências ao global) precisa que todas as achadas concordem; endereço na seção certa (código/dados); referência a global precisa cair numa relocação; prólogo esperado (6 bytes) conferido em toda função; guarda precisa estar dentro da função esperada e ter o mesmo deslocamento. **Qualquer falha recusa o perfil inteiro** ("Cliente diferente do perfil analisado."), com o motivo no registro (`Perfil do jogo recusado …`).
- Depois de aceito pelo arquivo, confere no módulo carregado (leitura de memória) 16 bytes de código de cada uma das 20 funções (a partir de +5, para não confundir com a captura do próprio FarmZilla em SendData; relocações ignoradas) e o tamanho da imagem. Resultado guardado por arquivo (tamanho+data) e por processo; a varredura leva ~1,5 s uma vez por compilação nova.
- Por que o arquivo e não a memória para procurar: o código no disco é o mesmo, não tem os desvios que o próprio FarmZilla instala (captura em SendData) e permite o autoteste sem jogo; a memória entra na conferência final.
- Pontes C#: `Bridge`, `AttackBridge`, `BuffBridge`, `MeditationBridge`, `NpcBridge`, `PartyBridge`, `TeleportBridge`, `TeleportCapture`, `CharacterMemory` (Attack/Buff/Party/Quest/Teleport), `State`, `ClientDiscovery` e `RotaAvalon.GameClient` usam `GameProfile` em vez do hash e dos RVAs fixos. `RouteDocument.ClientHash` continua sendo só a etiqueta do formato das rotas salvas (coordenadas do mundo), não trava o executável.
- Ponte nativa: `RotaNativeBridge.c` versão 2 recebe na requisição os 17 endereços (ordem `GameProfile.NativeOrder`) e o tamanho da imagem; recusa tabela fora da imagem; manteve as conferências de prólogo, RTTI e "AutoKillMonster". DLL recompilada com Zig 0.16.0, SHA-256 `5d0183acf9c531e1aea90680ffffe3ce228ba76c0b062b8e675c283432b778b6` fixado em `RotaPaths.BridgeSha256`. Operações 1–8 só funcionam com FarmZilla 0.50.0+ (versão 2 da requisição).
- Gerador: `work/game-signatures/make-signatures.cjs` (só leitura do exe; iced-x86 do workspace original) gera `GameSignatures.Data.cs` e `signatures.json`. Curingas em relocações e destinos de call/jmp; nenhum código do jogo é distribuído, só padrões curtos (12–53 bytes).

Testado:
- Compilação `Build-Release.ps1` (19 verificações do atualizador + 19 de licença).
- `work/game-update-tests/GameSignatureTest.cs` (sem jogo, só lê o exe e cópias temporárias): as 38 assinaturas e 6 guardas resolvem no exe atual exatamente para os RVAs validados, cada assinatura isolada tem uma ocorrência; hash conhecido usa o perfil validado; cópia com 1 byte a mais no fim (hash novo, mesmo código) é aceita por assinaturas com os mesmos endereços em C# e na tabela da ponte; código conferido com o exe mapeado só como imagem de recurso (nada executado) e recusado quando 1 byte de KeyMove muda; recusas: assinatura ambígua (padrão de KeyMove copiado para preenchimento), tipo RTTI ausente (CECTeam), guarda de estrutura mudada (0x1D3C → 0x1D40), executável não x86.
- Demais testes de `work/game-update-tests` passam (inclui RotaIntegrationTest: DLL nova responde pelo gancho), exceto `UnifiedCaptureTest`, que **já falhava igual na 0.49.0** ("Filtro deixou passar outro comando" — teste antigo, não relacionado).

Limites (ler antes de confiar):
- **Validação real só acontece quando o jogo atualizar.** Não há cópia do exe anterior (94629590…) para testar uma atualização de verdade; todos os testes usam o exe atual ou cópias dele.
- Assinaturas curtas podem não ser encontradas numa atualização que mexa nessas funções → o perfil é recusado (fica como hoje: bloqueado, precisa remapear). Esse é o resultado esperado na dúvida.
- Deslocamentos de estruturas: só 6 do personagem são conferidos. Os demais (ex.: root+0x1C, run+0x34, host+0x748 nome, +0x74C classe, +0x10F8 inventário, campos das janelas e da ponte nativa) continuam fixos. Na atualização de 30/10/2026 os campos host+0x1960… mudaram para +0x1D1C…; uma mudança assim nas guardas recusa o perfil, mas uma mudança só num campo não guardado não seria detectada pelas assinaturas — as leituras existentes (RTTI, faixas de valores, nome válido) continuam como segunda barreira. Recomendado: após a primeira atualização aceita por assinaturas, conferir no registro e testar no jogo com cuidado antes de confiar nas operações.
- Varredura de ~1,5 s roda na thread que pede o perfil pela primeira vez (pode congelar a janela uma vez por compilação nova).
- Nada foi executado no jogo; nenhum processo do jogo ou o FarmZilla em uso foram tocados.
- Publicada e verificada 06/11/2026, Worker `60593be7-92a6-4032-bb92-bea5af52f483`, -SkipExport (a pedido do dono, para o Esdras testar com o jogo atualizado).

## 0.50.1 — TG em você: cancelar ação + seguir, todos juntos

- Registro 06/11 21:53/21:54: ao pegar o próprio TG as macros pararam na hora, o seguir saiu 0,2 s depois e as 5 contas recusaram ("estado de seguimento não confirmado" em 1,5 s) — ainda terminando a ação de ataque; nova tentativa só 2 s depois; só Limeira seguiu, 6 s após o clique.
- `AttackController.StopAll(release)`: cada conta, em paralelo, para a macro e recebe cancelar ação + tirar seleção (`Release`, 2A 00 + 08 00), depois o seguir (`FollowNowAll`). Volta o comando que a 0.47.7 tirou: a lentidão de antes era a ordem uma-por-uma, não o ESC.
- `FollowRetrySeconds` 2 → 1 s.
- AttackContinuousTest com a regra nova. Testado: compilação (38 verificações), AttackContinuousTest, SupportTargetTest, GameSignatureTest. Não testado no jogo.
- Publicada e verificada 06/11/2026, Worker `1326533c-2d03-4f96-a3e7-a7e4721e3057`, -SkipExport.

## 0.50.2 — aceitar ressurreição sozinho (caixa "Reviver")

- Captura 06/11/2026 22:24:47 com Lima principal (dono deu Prece da Vida e só clicou aceitar): `57 00 00 00 00 00`. Antes vieram 8A 00 e 87 00, que não são o aceite.
- `TeleportBridge.SendReviveAccept` (só esse comando fixo). `BagAutoEquip.cs`: no mesmo ciclo de 2 s, conta da lista com vida 0 há ≥ 1 s envia o aceite a cada 2 s até reviver (sem oferta o jogo ignora); registro "morto; aceitando…" e "voltou à vida". Nunca usa voltar à cidade.
- Caixa "Reviver" ao lado de "Meditação" (padrão marcada, `reviver.txt`); desmarcada não aceita. Prévia conferida.
- Testado: compilação (38 verificações), AttackContinuousTest, GameSignatureTest, RotaIntegrationTest, prévia. Aceite automático não testado no jogo.
- Publicada e verificada 06/11/2026, Worker `60ad5864-3f10-4642-8fdc-0bf1398a27e9`, -SkipExport.

## 0.50.3 — EP dá Prece da Vida sozinho em quem morreu

- Pedido do dono: o sacerdote (EP) ressuscitar automaticamente quem morrer no grupo.
- `AutoResurrect.cs`: no ciclo de 2 s (`BagAutoEquip.cs`), conta da lista com vida 0 → um sacerdote vivo da lista (classe 7, fora do Mat Farm) seleciona o morto e roda a macro 4 (índice 3, quarto espaço da janela R) pelo mesmo caminho do suporte (`AttackBridge.StartMacroOn`). Uma tentativa por morto a cada 8 s até reviver; o EP fica 6 s fora do ataque e do seguir (`ResBusy`) para não cortar a reza. Sem a macro 4 cadastrada: aviso único. O morto aceita sozinho com "Reviver" (0.50.2).
- Caixa "EP ressuscita (macro 4)" embaixo do Bufar (padrão marcada, `ressuscitar.txt`); linha de missões encurtada. Prévia conferida.
- Só vale para contas abertas no FarmZilla (lê a vida delas); jogadores de fora não.
- Testado: compilação (38 verificações), AttackContinuousTest, GameSignatureTest, RotaIntegrationTest, prévia. Não testado no jogo.
- Publicada e verificada 06/11/2026, Worker `5ab5a363-3f60-4a35-a2e2-0503db47352c`, -SkipExport.

## 0.50.4 — Mat Farm: morreu → reviver na cidade e continuar

- Antes: morto no Mat Farm = "Personagem sem vida; rota parada" a cada 15 s, sem fim (ex.: 06/11 22:45, Limago).
- Captura 07/11/2026 12:04:27 (Limago principal, botão reviver na cidade): `04 00 00 00 00 00` (junto veio `8A 00`, de tela; o `57 00…` às 12:04:25 era o aceite automático do FarmZilla). Observação: entre 10:05 e 12:00 rodava aí uma versão alterada fora daqui (linhas "Velocidade … ajuste"), sem captura.
- `RouteExecution.DeathRecovery` (automático e Manual): espera até 12 s por ressurreição (aceite pelo "Reviver"); senão `ReviveInTown` (`RotaLink.RotaReviveInTown` → `TeleportBridge.SendTownRevive`), confere vida > 0 em até 10 s, 3 tentativas; depois 3 s e a rotina segue (viaja de volta pelos pontos/trajeto). `RecoverLife` também chama ao morrer esperando vida.
- Testado: compilação (38 verificações), RotaIntegrationTest, TrailTest, GameSignatureTest, AttackContinuousTest. Não testado no jogo.
- Publicada e verificada 07/11/2026, Worker `bbb3e1af-ffda-42cd-a25a-38f955e10dca`, -SkipExport. Dono confirmou: reviver na cidade só no Mat Farm; no Espelho (PT) a Prece da Vida do sacerdote + aceite automático já funcionam e ficam como estão.

## 0.50.5 — uma ponte por cliente; EP não é interrompido ao ressuscitar

- Registro 07/11 12:26:30–12:27:02: Limago (mago) morreu; Limeira (EP) recebeu "Prece da Vida (macro 4)", mas entre as tentativas (a cada 8 s, EP fora do ataque só 6 s) o ataque religou o suporte dela (12:26:53) e cortou a reza; a próxima tentativa abriu outra ponte no mesmo cliente → "ponte de ataque sem restauração" (12:26:56) e o jogo da Limeira fechou (12:27:02). O mago continuou morto; o "atacando" dele às 12:26:50 mostra que o ataque também mexia em conta morta.
- `ProcessGate.cs`: todas as pontes (ataque, teleporte/controle, seguir, buff, PT, NPC, meditação) esperam a vez por cliente (até 10 s) no construtor e liberam no Dispose. Teste `ProcessGateTest.cs`.
- `AutoResurrect`: enquanto houver alguém morto, os sacerdotes ficam fora do ataque/suporte/seguir; tentativa a cada 10 s (EP reservado 10 s). Contas mortas ficam fora do ataque e do seguir (`IsDead`).
- Testado: compilação (38 verificações), ProcessGateTest, AttackContinuousTest, GameSignatureTest, RotaIntegrationTest, SupportTargetTest, TrailTest. Não testado no jogo.
- Publicada e verificada 07/11/2026, Worker `a8010a59-6591-4ece-8af8-3a43b35d2c36`, -SkipExport.

## 0.50.6 — EP aperta ESC antes da Prece da Vida

- Pedido do dono: com "aura infinita" (ou qualquer ação) ativa, o EP não reza; precisa de ESC antes.
- `AutoResurrect`: antes da macro 4, envia cancelar ação `2A 00` (o mesmo do ESC, já usado desde a 0.19.21) ao EP, espera 0,3 s e então seleciona o morto e roda a macro 4. A trava por cliente (0.50.5) mantém ESC e macro em sequência.
- Testado: compilação (38 verificações), AttackContinuousTest, ProcessGateTest, RotaIntegrationTest. Não testado no jogo (se a aura infinita não parar com o cancelar ação, é preciso capturar o comando que a desliga).
- Publicada e verificada 07/11/2026, Worker `1b86825c-b425-4080-ad16-7c8e85ef7cda`, -SkipExport.

## 0.50.7 — corrige: EP não dava a Prece desde a 0.50.5

- Registro 07/11 13:28:13: Lima e Limago mortos; Limeira saiu do ataque (proteção ok), mas nenhuma "Prece da Vida". Erro da 0.50.5: a proteção marca todo sacerdote como reservado (`resBusy`) e em seguida a escolha do EP pulava os reservados — nunca sobrava nenhum.
- `AutoResurrect`: a escolha não olha mais a reserva; um sacerdote reza um morto por vez (`castUntil` 6 s), para o ESC do próximo não cortar a reza do anterior.
- Testado: compilação (38 verificações), AttackContinuousTest, ProcessGateTest. Não testado no jogo.
- Publicada e verificada 07/11/2026, Worker `08504b43-82cd-47f1-90a8-73ffba367f86`, -SkipExport.

## 0.51.0 — WF raposa + Ferir no combate

- Pedido do dono: no combate a WF (feiticeira) vira raposa (F7) e dá o Ferir (F8, só na raposa) sempre que estiverem prontos, depois volta ao combo normal.
- Captura 07/11/2026 (Limaozinho principal): 13:37:12 `50 00 38 01 00 00 0B 01 <próprio id>` = habilidade 312 nela (raposa); 13:37:21 `29 00 40 01 00 00 0B 01 <monstro>` = 320 (Ferir); ela volta em pé sozinha (dono: estava em pé ao repetir F7 às 13:37:56). Recargas informadas pelo dono: raposa 7 s, Ferir 30 s (usado 31).
- `FoxCombo.cs`: a cada 0,5 s, WF (classe 3) com Ataque marcado e em combate com TG de monstro: ESC (2A 00) → raposa → 2 s → Ferir no alvo do principal; fica 4 s fora do ataque/seguir (`FoxBusy`); repete a cada 31 s. `TeleportBridge.SendFoxSkill` lista fechada (só 0x50/312 e 0x29/320). Caixa "WF raposa" ao lado de "EP revive" (`wf-raposa.txt`, padrão ligado). Prévia conferida.
- Pet (F5): capturado 13:40:13 `29 00 49 01 … <próprio id>` (329 reviver mascote), 13:40:48 `64 00 00 00 00 00` (chamar), `67 …` (ordens do pet). Não implementado: falta ler na memória se o pet está morto.
- Testado: compilação (38 verificações), AttackContinuousTest, ProcessGateTest, RotaIntegrationTest, prévia. Não testado no jogo.
- Publicada e verificada 07/11/2026, Worker `8bdcad8a-a87f-4e20-a1da-5ff82eee98be`, -SkipExport.

## 0.51.1 — WF volta em pé depois do Ferir

- Teste do dono na 0.51.0: Limaozinho virou raposa, deu o Ferir e ficou em raposa; a macro 0 não voltou.
- `FoxCombo`: ESC → F7 (raposa) → 2 s → Ferir → espera a raposa recarregar (7 s desde o 1º F7, +0,5 s) → F7 de novo (volta em pé) → a macro 0 volta. Fica fora do ataque/seguir só até o 2º F7 (~8 s).
- Pet: objeto achado na memória (tipo `CECPet`, id do pet em +0x118, mesmo id de host+0x91C); falta ver o que muda quando morre.
- Testado: compilação (38 verificações), AttackContinuousTest, ProcessGateTest. Não testado no jogo.
- Publicada e verificada 07/11/2026, Worker `caa11b6d-de7e-4cb0-a5ca-15e2b1777e67`, -SkipExport.

## 0.51.2 — Ferir garantido e pet da WF sozinho (caixa "WF auto")

- Dono: "às vezes ela deita (raposa) e não manda o Ferir". `FoxCombo`: Ferir enviado aos 2 s, 3,5 s e 5 s após a raposa (repetidos são recusados pelo jogo se o primeiro pegou); F7 de volta aos 7,5 s.
- Pet (leitura de memória 07/11, Limaozinho): host+0x91C = id do pet; objeto `CECPet` com o id em +0x118, fora da tabela de monstros; ao morrer o objeto é destruído (13:57:00.8 virou `A3DObject`) e host+0x91C mantém o id antigo; ao invocar, id novo e (até agora) o mesmo endereço. Nenhum ponteiro fixo achado a partir de manager/host/world (9 referências soltas).
- `PetCare.cs` (ciclo de 2 s do `BagAutoEquip`): WF viva da lista (fora do Mat Farm) com id de pet: objeto guardado ainda é CECPet com o id = vivo; senão uma varredura de memória (só leitura); não achou = F5 `29 00 49 01 00 00 0B 01 <id>` → 2 s → F6 `64 00 00 00 00 00` → confere; 15 s até tentar de novo. `TeleportBridge.SendPetCare` lista fechada. Liga com a caixa "WF auto" (antes "WF raposa").
- Testado: compilação (38 verificações), AttackContinuousTest, ProcessGateTest, RotaIntegrationTest. Não testado no jogo. Atenção: se o dono guardar o pet de propósito, o FarmZilla invoca de novo (desligar "WF auto").
- Publicada e verificada 07/11/2026, Worker `cef96b0d-b829-45e9-a7be-e94e24597b42`, -SkipExport.

## 0.51.3 — Ferir pela macro 2 da WF

- Dono: "ela deita e não dá ferir, depois fica deitada". O Ferir por comando direto não anda até o monstro (a WF luta de longe) e o 2º `50 00 38 01…` não levanta ela (o levantar com F7 não passa pelo envio que a captura vê: 14:10:57 deita, ~14:11:05 levanta, nada capturado; os `21 00 01 00 …` são consultas de personagens).
- `FoxCombo`: ESC → raposa (`50 00 38 01…`) → 2 s → macro 2 da WF (3º espaço da janela R, só o Ferir; dono confirmou que o Ferir só sai deitada) no monstro do principal via `StartMacroOn` (o jogo anda e usa), até 3 tentativas → aos 7,5 s ESC como tentativa de levantar → macro 0 volta. Sem a macro 2 cadastrada: aviso.
- Testado: compilação (38 verificações), AttackContinuousTest, ProcessGateTest. Não testado no jogo.
- Publicada e verificada 07/11/2026, Worker `6ff520e2-ed63-4414-ab75-40c7348c9ba6`, -SkipExport.

## 0.51.4 — Ferir da WF: macro 2 no monstro liberada

- Registro 07/11 14:22:19 e 14:22:54: "raposa/Ferir: Comando inválido." — o modo 5 da ponte de ataque (selecionar + rodar macro, feito para o suporte do sacerdote) só aceitava jogador no lado C#; o código nativo já seleciona qualquer alvo (mesma seleção usada pelo ataque no monstro).
- `AttackBridge.StartMacroOnMonster` (só ids de monstro 0x80…) e a checagem do modo 5 aceita jogador ou monstro; `FoxCombo` usa isso para a macro 2 (Ferir).
- Testado: compilação (38 verificações), AttackContinuousTest, SupportTargetTest, ProcessGateTest. Não testado no jogo.
- Publicada e verificada 07/11/2026, Worker `6e50aaa2-5142-4e64-a860-91e1ac781f1b`, -SkipExport.

## 0.51.5 — WF levanta pela macro 3

- Teste do dono na 0.51.4 (08/11 16:51:46 e 16:52:17): deitou e deu o Ferir (macro 2 no monstro funcionou), mas ficou deitada.
- O 2º F7 (levantar) não manda nada que a captura veja; `FoxCombo` agora, aos 7,5 s do 1º F7, roda a macro 3 da WF (4º espaço da janela R, só a habilidade da raposa) nela mesma (`StartMacroOn`), como o jogo faz no F7. Sem a macro 3: ESC + aviso único no registro.
- Testado: compilação (38 verificações), AttackContinuousTest, ProcessGateTest. Não testado no jogo.
- Publicada e verificada 08/11/2026, Worker `491d76a0-8df7-4bc5-b1e9-12ddb60beb48`, -SkipExport.

## 0.51.6 — tempos da WF

- Dono: Ferir a cada 30 s e raposa 8 s. `FoxCombo`: `FoxCooldown=8`, `FerirCooldown=30` — ciclo a cada 30 s; levantar (macro 3) aos 8,5 s após deitar; fora do ataque até ~11 s.
- Testado: compilação (38 verificações). Não testado no jogo.
- Publicada e verificada 08/11/2026, Worker `05ea7127-ed14-47d0-a1d1-15e746696343`, -SkipExport.

## 0.51.7 (08/11/2026)
- Registro do Esdras 08/11 18:48–19:02 (CheirosoWB, Mat Farm): Sulfeto de Arsênico 3535 coletado 3× (item 62523), mas `missao_nao_apareceu expected 61991` todas as vezes — material+58456 (0.48.2) não vale para ervas; nenhuma erva/flor confirmou missão até hoje.
- Agora: antes da coleta lê as linhas da janela "missões para concluir" (`GameClient.ReadConfirmRows`, só leitura: gameui+0x6B0 CDlgTaskConfirm, linhas +0x258/+0x274, id em rótulo+0xF4); se a esperada não aparece, registra `missao_janela` (linhas antes/depois) e, se surgiu exatamente uma linha nova (fora dos minérios 3074–3092), grava `missao_aprendida` em `%LOCALAPPDATA%\AvalonInterno\Rota\missoes-aprendidas.txt` e tenta de novo com ela.
- Testado: build 19+19; RotaIntegrationTest, RouteTextTest, TrailTest, FarmPlannerTest; leitura da janela nos 6 clientes do dono (sem erro, vazia). Não testado com erva no jogo.
- Publicada e verificada, Worker `6c0e5110-9e77-4cc4-b98d-da41b601cbc9`, -SkipExport.

## 0.51.8 (08/11/2026)
- Registro do Esdras (08/11 18:18–18:19 e outros, 56 vezes): CheirosaEP "macro travada no passo 2; reiniciada" a cada ~5,5 s — o passo 2 da macro dela demora mais que 5 s (habilidade longa ou recarga), então a macro nunca passava dele e ela parecia não atacar. Outras contas travaram no passo 2 só 2–12 vezes.
- Agora: travar de novo no mesmo passo espera mais (5 → 10 → 15 → 20 s, `StallLimit`); chegar a um passo seguinte ou a macro terminar sozinha zera. Mensagem mostra o tempo ("por N s").
- Testado: build 19+19, AttackContinuousTest, SupportTargetTest. Não testado no jogo.
- Publicada e verificada, Worker `2f3a6921-ce34-46eb-a434-1aee9e03e7fd`, -SkipExport.

## 0.51.9 (08/11/2026)
- Pedido do dono: o EP ressuscita também quem está na PT e não está no FarmZilla, com o EP em ataque ou em suporte.
- Membro da PT lido da PT do próprio sacerdote (só leitura): id em +0xC, vida em +0x1C, vida máxima em +0x24 (conferido 08/11 nos clientes do dono: Lima 87184 vida 5659, Limeira 90304 vida 6873, iguais à vida real). Vida 0 com máxima > 0 = morto → mesma rotina (ESC, macro 4 nele, a cada 10 s). Fora de vista: 3 falhas = deixa 60 s sem prender o EP.
- Testado: build 19+19, AttackContinuousTest, ProcessGateTest, leitura da PT nos clientes do dono. Não testado com alguém de fora morto.
- Publicada e verificada, Worker `ed571b18-6786-4dc0-92ff-19553929cb6f`, -SkipExport.

## 0.51.10 (08/11/2026)
- Registros 08/11 (dono 21:13, Esdras 21:17–21:18): "Remontagem interrompida na etapa verificação inicial: Há membros da PT fora da seleção" — conta no Mat Farm (fica fora da seleção), desmarcada ou membro de fora do FarmZilla.
- Agora: membros fora da seleção também saem (0x1F, conferido na PT do principal) e no fim são convidados de novo — contas do FarmZilla aceitam pelo app (`Join`), membro de fora recebe o convite (0x1B) e aceita no jogo dele.
- Testado: build 19+19. Não testado no jogo.
- Publicada e verificada, Worker `3d59ae0d-72db-4ff3-a56c-fb4e63de5558`, -SkipExport.

## 0.51.11 (08/11/2026)
- Dono: o EP em suporte fica curando/dando suporte sem TG de monstro; deve agir só com TG de monstro.
- Causa provável (sem prova no registro): o suporte só começa com TG de monstro, mas ao perder o TG `StopAll` só parava sessões "não terminadas"; se a macro 2 parecia terminada (combo trocado) e o jogo continuava repetindo, ela não recebia o parar.
- Agora: sem TG de monstro (e em toda troca), sacerdote em suporte sempre recebe parar macro 2 (`StopSupport`), mesmo com a sessão terminada.
- Testado: build 19+19, AttackContinuousTest, SupportTargetTest. Não testado no jogo.
- Publicada e verificada, Worker `1a2325b9-4b46-48bf-87c7-b30ec889624e`, -SkipExport.

## 0.51.12 (09/11/2026)
- Dono: duas pedras de teleporte devem funcionar. Mochila da Lima lida só por leitura (09/11): 36905 (631) e 26651 (320), mesma classe de item (ItemTransmitScroll, tipo +8E852C).
- Agora: `TeleportService.StoneIds = {36905, 26651}`; o principal usar qualquer uma é espelhado e cada secundário procura a MESMA pedra (`FindStone(id do pacote)`); `TeleportBridge.SendTeleport` aceita as duas (lista fechada).
- Não testado no jogo: a 26651 não foi capturada em uso; o destino é lido pela mesma tabela da 36905 (se a 26651 usar outra tabela, aparece "Destino não encontrado").
- Testado: build 19+19, SpecialTeleportTest, ItemUseCaptureTest.
- Publicada e verificada, Worker `a4bcaf29-2258-4c08-9491-7b5ad630be78`, -SkipExport.

## 0.51.13 (09/11/2026)
- Medido nos registros locais 02–09/11 (108 missões espelhadas): a maioria confirma em ~1 s, mas várias só começaram 6–18 s depois de capturadas (2146 6,3 s; 61841 17,4/18,7 s; 61980 6,9 s; 9523 hoje 11,1 s). Causa: `missions.Pump` só roda com o app livre (`busy`), e `busy` fica ligado pela retomada do seguir (contas longe) e pelos ciclos de ataque/meditação.
- Agora: missão na fila faz a retomada do seguir ceder a vez (`YieldFollowRepair`, como Montar PT/Bufar) e a retomada do seguir não começa enquanto houver missão (`missions.Working`). Mensagens mostram o tempo de cada conta e o CHECK mostra o tempo desde o principal.
- Não mudou: as 3 tentativas e as conferências (NPC aberto até 6 s, missão na lista até 8 s). Falhas 0/5 (missão indisponível nos secundários, ex. 19980) continuam levando ~20 s.
- Testado: build 19+19. Não testado no jogo.
- Publicada e verificada, Worker `acf0159d-d31a-45cf-8ebe-659f7db55124`, -SkipExport.

## 0.51.14 (09/11/2026)
- Medido nos registros 08–09/11 (33 TGs): do comando de seleção do principal (0x02 com id de monstro) até as linhas "atacando" ~1,9 s (mediana), quase constante; as linhas saem juntas depois que todos os inícios terminam, então é o tempo do mais lento.
- Cada início de secundário que está seguindo: ponte ESC (2A 00) + espera do seguir soltar (até 1 s) + ponte de ataque. Agora a espera do seguir é no máximo 0,3 s.
- Registro novo: "Ataque: TG novo → início Nome X s, …" quando algum início passa de 0,5 s — para achar o resto do atraso.
- Visto também: 38 reinícios "macro anterior interrompida" menos de 1,5 s depois de começar (06–09/11) — são troca de alvo do principal (o alvo lido mudou), não alterado.
- Testado: build 19+19, AttackContinuousTest, SupportTargetTest. Não testado no jogo.
- Publicada e verificada, Worker `0adf5685-1fe3-4124-b5d9-5145e9bf5e20`, -SkipExport.

## 0.51.15 (09/11/2026)
- Dono mandou a lista de "Pedra de Teleporte": 14351, 26651, 33916, 36905. `TeleportService.StoneIds` com as quatro; secundário usa a mesma pedra do principal.
- Testado: build 19+19, SpecialTeleportTest, ItemUseCaptureTest. 14351/33916 sem captura real.
- Publicada e verificada, Worker `de4d3c98-2819-49ee-814e-e485b706d18c`, -SkipExport.

## 0.51.16 (09/11/2026)
- Forma da WF achada por gravação só leitura da Limaozinho (09/11 09:31–09:32, 6 trocas, terminou em pé conferido): host+0xED = 1 raposa (deitada), 0 em pé; +0x6B4 (16727/0) e +0x420 bit 0 mudam junto.
- Combo: só manda Ferir quando já está raposa (até 2 tentativas de virar, senão tenta em 3 s); levantar confere a forma e repete a macro 3 até 3 vezes.
- Publicada e verificada, Worker `a23e1264-b7fb-4f19-ad4e-2ad8acdb1f1b`, -SkipExport.

## 0.51.17 (09/11/2026)
- Dono: tudo rápido — deitou, Ferir, em pé, bate. Agora ao terminar a macro do Ferir (máx. 2,5 s) já tenta levantar com a macro 3 a cada 0,8 s até a forma dizer em pé (limite: recarga da raposa + 3 s). Não espera mais 8,5 s fixos.
- Testado: build 19+19, AttackContinuousTest (0.51.16). Não testado no jogo.
- Publicada e verificada, Worker `5c54f3fb-e062-4cb4-b4c5-9bdddd60428e`, -SkipExport.

## 0.51.18 (09/11/2026)
- Registro do Esdras 09/11 10:03–10:04: principal ProFile usa a pedra 36905; "gorlami: Sem Pedra de Teleporte 36905 no inventário" em todos os teleportes (os outros 4 chegaram).
- Agora: secundário procura a mesma pedra do principal e, sem ela, qualquer uma das quatro (`FindAnyStone`); o pacote enviado leva a posição e o id da pedra dele. Mensagem sem pedra lista as quatro.
- Hipótese: as quatro "Pedra de Teleporte" usam os mesmos destinos. Não testado no jogo.
- Testado: build 19+19, SpecialTeleportTest, ItemUseCaptureTest.
- Publicada e verificada, Worker `52cac318-e2c8-466a-983d-835e36569f74`, -SkipExport.

## 0.51.19 (09/11/2026)
- Esdras: CheirosoWR às vezes pega a missão do material, não completa e sai voando. Registro (07:00–10:10): toda coleta terminou em "recompensa_recebida", mas houve 5 "missao_pendente_concluida" em pontos seguintes = missão que ficou aberta. A conferência aceitava qualquer item novo (`GainedExcept`, ex. drop de monstro durante a coleta) como recompensa.
- Agora: item novo só vale como recompensa com a missão já fora da janela (`MissionPending` falso); item final conhecido continua valendo na hora. Mesma regra antes da missão aparecer e no fim das tentativas; se ainda aberta no fim, tenta concluir (`FinishPendingMission`) antes de sair.
- Limite: o registro remoto não diz qual conta (várias abas) — atribuição ao CheirosoWR pelo relato.
- Testado: build 19+19, RotaIntegrationTest, RouteTextTest, TrailTest. Não testado no jogo.
- Publicada e verificada, Worker `d632823b-a616-4708-a2e8-e3d54be594a3`, -SkipExport.

## 0.51.20 (09/11/2026)
- Dono (print do notebook): Mat Farm com textos cortados ("Personage", "Desconect", "Automátic", "Carregar traieto"). Causa: o app é DPI-aware (`SetProcessDPIAware`) com caixas em pixels fixos (`AutoScaleMode.None`), mas as fontes em pontos crescem com o zoom do Windows.
- Agora: `UiScale.F = 96 / DPI da tela`; as 60 fontes do app multiplicam o tamanho por F (a de 18 px já era em pixels). Em 100% nada muda; em 125%/150% fica igual ao de 100%, nos mesmos pixels (menor fisicamente).
- Testado: build 19+19, RotaIntegrationTest, TrailTest, MaterialListTest. Não testado num Windows com zoom.
- Publicada e verificada, Worker `b768fbc8-1f57-44d0-b638-231fe940acdd`, -SkipExport.
