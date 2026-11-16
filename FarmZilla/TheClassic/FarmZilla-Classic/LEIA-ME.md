# Interface Rubi compacto

FarmZilla 0.20.0: janela 390×606 com nove secundários, controles de ataque, cinco atalhos programáveis, meditação e botões Montar PT, Remontar PT e Bufar. O botão Rota abre as rotas de coleta (mapa, materiais, inventário e log) do antigo RotaAvalon. Arraste pela barra superior, marca FZ, nome ou versão. A atualização fica em Ajustes > Atualizar aplicativo. Teclas, identificação do PC, autorização e lógica do jogo são preservadas da versão anterior.

# FarmZilla 0.20.0 — liberação e bloqueio online

Envie **somente FarmZilla.exe** aos usuários. O programa já inclui o endereço do serviço; não é necessário configurar um servidor em cada PC.

1. A pessoa abre o FarmZilla, copia o **Código do PC** e envia a você.
2. O PC aparece como **Aguardando** no gerenciador após consultar o servidor.
3. Você seleciona o código, informa um nome e clica em **Liberar PC**.
4. A pessoa clica em **Verificar liberação** e passa a usar o aplicativo.
5. Para suspender o acesso, selecione o PC no gerenciador e clique em **Bloquear PC**. **Liberar PC** permite usar novamente.

## Internet e prazo do bloqueio

O programa consulta a autorização ao abrir e a cada 30 minutos enquanto está em uso (desde a 0.19.12; antes, a cada cinco minutos). Um bloqueio pode levar até 30 minutos para chegar a um PC aberto. Uma autorização confirmada permite até 24 horas sem internet, conforme solicitado. Por isso, um computador desconectado pode continuar até a autorização já recebida expirar. A checagem local ocorre a cada 15 segundos; operações já iniciadas são encerradas pelo fluxo normal de limpeza.

Se o cliente recebe uma negativa explícita do servidor, apaga o cache de autorização: desligar a internet depois disso não restaura o acesso. Ao bloquear, o app desativa controles e atalhos, cancela operações pendentes e fecha suas capturas pelo fluxo normal. Não fecha o jogo.

## Identificação por hardware

O código BGC2 combina um resumo de identificadores de disco do Windows, placa-mãe, processador e UUID do equipamento, mais uma chave RSA exclusiva protegida por DPAPI nesta conta do Windows. São necessários pelo menos dois grupos de identificação de hardware válidos. Números de série brutos não são enviados ao serviço. A chave privada do PC não sai da máquina.

Trocar componentes relevantes ou mudar de instalação/conta Windows pode exigir reset da identificação e uma nova liberação. O identificador do processador sozinho não é exclusivo de cada CPU e números de série podem ser falsificados; o conjunto e a prova da chave dificultam cópias, mas não tornam o software inviolável contra adulteração, clonagem completa ou engenharia reversa.

## Atualizações

O app consulta novas versões ao abrir e a cada 30 minutos. Baixa e instala automaticamente quando não há secundários selecionados nem operação em andamento; caso esteja em uso, informa a disponibilidade e permite iniciar por **Configurações > Atualizar aplicativo**. As seleções dos personagens são refeitas após reiniciar. Teclas, preferências e autorização persistem.

O servidor exige autorização válida e estado **Liberado** para fornecer manifesto e executável. O cliente confere assinatura e SHA-256 antes da troca. Link direto anônimo não baixa o pacote. Um usuário autorizado ainda pode compartilhar um arquivo que já possui; a execução normal continua exigindo a autorização do PC.

Serviço: https://bagaceira-control.viacaomimo.workers.dev

## Migração

Esta versão substitui a licença permanente offline 0.14. A autorização antiga BGL1 não é aceita. A versão 0.14, se já entregue a alguém, não pode receber bloqueio remoto: substitua-a por esta versão e faça a nova liberação.

Esta versão acrescenta o espelhamento de entrega de missões no formato validado do Batedor da Mina. Com o app aberto e secundários selecionados, reabra o NPC no principal antes de aceitar ou entregar. Confirma individualmente e repete somente pendentes; confira o registro se algum personagem não concluir. Mantém autorização online, teclas e lógica de seguir.

## Compatibilidade com a atualização do Avalon

Perfil adaptado ao ElementClient.exe SHA-256 34fca82cafa1f38825228626aa3e8f98c56274c095cb46e317b2d665aaf69684. Endereços de estruturas, funções, tabelas e objetos remapeados; o reconhecimento voltou a identificar os seis personagens. As regras de seguir e os atalhos existentes foram preservados. Versões diferentes continuam sendo recusadas até validação própria.


Listas: o principal selecionado aparece sempre primeiro nas opções; a escolha atual é preservada. Secundários já escolhidos deixam de aparecer nas opções dos outros campos. Montar/remontar PT agora verifica respostas com intervalos menores, mantendo as confirmações de grupo e liderança.









Detecção: agora localiza clientes pela classe de janela e pelo nome do processo. Se não listar personagens, clique Atualizar na tela principal e veja Ajustes > Ver registro: clientes encontrados, versão/arquitetura e motivo da falha. Uma janela encontrada ainda precisa ser compatível e permitir a leitura do personagem.

Bufar: se o principal já estiver com uma macro em andamento (por exemplo, ataque iniciado no jogo), o FarmZilla espera até 15 segundos ela terminar e então aciona a macro 1 nele. Os secundários bufam na hora, sem esperar. Se a macro do principal não terminar nesse prazo, o registro informa e a macro 1 não é acionada nele.

Visual 0.19.4: cartões, botões e campos com relevo (sombra, gradiente e brilho no topo); botões afundam ao clicar. Posições, tamanho 390×606 e funções não mudaram. O texto do botão Desativar não pisca mais.

Ícone 0.19.5: o executável e o atalho da área de trabalho usam o selo FZ rubi em relevo, igual ao do topo do app.

Diagnóstico 0.19.6: ao usar no principal um item que não seja a Pedra de Teleporte (por exemplo, o Teleporte Especial), o registro mostra o item, o comando e a posição antes e 4 s depois. Nada é espelhado para os secundários por esse caminho; a Pedra de Teleporte continua espelhada como antes.

Fechar 0.19.7: com operação em andamento, o app não recusa mais fechar; interrompe os ciclos, espera a operação atual e remove as capturas normalmente. Se encontrar uma captura deixada por uma execução anterior do FarmZilla no principal, ela é removida antes de instalar a nova.

Diagnóstico ampliado 0.19.8 (opcional): se existir %LOCALAPPDATA%\AvalonInterno\diagnostico-comandos.txt, o registro mostra o tipo e o tamanho de cada comando enviado pelo principal (uma linha por tipo a cada 30 s). Sem o arquivo, nada muda. Nada é espelhado por esse caminho.

Teleporte Especial 0.19.9: ao usar o item Teleporte Especial no principal e escolher um ponto, os secundários selecionados usam o próprio Teleporte Especial e vão para o mesmo ponto. A chegada é conferida pela posição do principal; quem não chegar tem até 3 tentativas. Cada secundário precisa ter o item. A Pedra de Teleporte continua funcionando como antes.

Teleporte Especial 0.19.10: os secundários escolhem o ponto 1,5 s depois de usar o item (antes 0,8 s), esperam o item voltar ao inventário antes de uma nova tentativa e repetem mais rápido quando uma tentativa falha.

Registro no painel 0.19.11: o FarmZilla envia o registro (o mesmo de Ajustes > Ver registro: nomes de personagens, ações e falhas) ao serviço a cada minuto, para o administrador ver em /admin > Registro. Só PCs liberados enviam; o servidor guarda 7 dias e até 5000 linhas por PC. Não são enviadas senhas, chaves ou dados do jogo além do que aparece no registro.

Teleporte só com Seguir 0.19.11: em Ajustes, "Teleportar só quem está seguindo" (ligado por padrão). Com o Seguir desativado, o secundário não acompanha a Pedra nem o Teleporte Especial; o registro informa quem ficou. Desligue a opção para todos os selecionados acompanharem.

Abertura 0.19.12: num PC liberado, o FarmZilla confere a autorização sem mostrar janela e abre direto. A janela de autorização só aparece quando o PC precisa de atenção (aguardando liberação, bloqueado ou sem autorização válida), já com o motivo.

Novidades 0.19.13:
- Missão automática: quando a missão seguinte aparece sozinha depois de uma entrega e o principal aceita, os secundários aceitam também (confirmado na lista de missões de cada um, até 3 tentativas).
- Bufar sem trava: apertar Bufar de novo rebufa todos; personagem com outra macro rodando tem a macro interrompida e buffa. Durante o buff, qualquer outra tecla ou botão (Seguir, Atacar, Montar/Remontar PT, modo de ataque) encerra a espera e a nova função assume.
- Ataque sem trava: se o secundário estiver com alguma macro rodando (buff ou macro própria), ela é interrompida e o ataque começa.
- Tecla "Modo ataque": alterna entre Automático e Manual por tecla. Clique no campo e aperte a tecla; Delete apaga.

Principal pela janela ativa 0.19.14: ao abrir a lista Principal, a janela do jogo usada por último aparece no topo, marcada "● janela ativa". O principal só muda quando você clica nela.

Ícones de classe 0.19.14: o FarmZilla lê a classe de cada personagem automaticamente e mostra um emblema antes do nome (Principal, secundários e listas): Guerreiro, Mago, Feiticeira, Bárbaro, Arqueiro e Sacerdote. Os ícones são os da janela de grupo do próprio jogo, lidos do jogo instalado no PC (nada do jogo vai junto com o FarmZilla).

Janela automática 0.19.15: quando a janela automática conclui uma etapa de missão no principal (ex.: "Entendo!" que passa da 1863 para a 1864), os secundários que ainda estão na etapa recebem o mesmo comando e o FarmZilla confere se a etapa foi concluída em cada um. Se a missão só sumir no principal, sem etapa seguinte, nada é espelhado.

Janela automática 0.19.16: depois de confirmar que o secundário aceitou a missão nova ou avançou a etapa, o FarmZilla fecha a janela de NPC/missão que tinha ficado aberta na tela dele.

Pop-up 0.19.17: quando o principal fecha uma janela automática sem ação (notificação, "Entendo!" de etapa já concluída, X ou Voltar), as janelas automáticas abertas nos secundários também fecham. Conversas com NPC em andamento não são fechadas.

Ordem 0.19.18: a janela automática só é repetida nos secundários depois que a entrega/aceite de missão em andamento termina.

Ataque automático 0.19.19: enquanto o principal tiver um monstro selecionado, os secundários marcados repetem a macro 0 sem parar (combo contínuo). Selecione o seu próprio personagem (ou nada) para eles pararem e voltarem a seguir. O modo Manual continua: uma vez por tecla Atacar.

Parar ataque 0.19.20: quando o principal fica sem monstro selecionado (ESC ou selecionar a si mesmo), além de parar a macro o FarmZilla tira o alvo de cada secundário (como um ESC neles), para o ataque básico parar e o Seguir trazê-los de volta.

Principal também ataca 0.19.20: caixa "Atk" ao lado do Principal (marcada por padrão). Com ela marcada, o principal também usa a macro 0 no modo Automático (contínuo enquanto o monstro estiver selecionado) e no Manual (tecla Atacar).

ESC 0.19.21: o "tirar o alvo" dos secundários agora usa os mesmos comandos que o jogo envia no ESC (cancelar ação + tirar seleção), para o ataque básico parar de verdade.

Ataque 0.19.22: se a macro de algum personagem ficar parada no mesmo passo por 5 segundos (habilidade que não pode ser usada naquele momento), o FarmZilla reinicia a macro para ele continuar atacando enquanto o principal mantiver o alvo.

Suporte 0.19.23: na caixa Atk de um Sacerdote, um clique alterna Atk (vermelho) → Sup (verde) → desligado. Em Sup, quando o principal pega o TG de um monstro, o sacerdote seleciona o líder (o principal do FarmZilla) e usa a macro 2 (terceiro espaço da janela R) nele o tempo todo. Soltou o TG, ele para e volta a seguir.

Ataque 0.19.24: o sacerdote ganhou uma caixa própria "Sup" (verde) ao lado do Atk; marcar uma desmarca a outra. Quem estiver seguindo para de seguir antes de atacar ou dar suporte, para o ataque pegar depois de um ESC.

Troca de alvo 0.19.25: ataque e suporte começam em todos ao mesmo tempo, e a troca de monstro não faz mais uma rodada separada para parar a macro antiga — resposta mais rápida.


## Auto Quest 0.6.6 — primeiro teste assistido (com log)

**Esta versão NÃO completa missões automaticamente.** É o primeiro teste do motor de deslocamento no cliente The Classic com registro para corrigir comportamentos sem refazer o processo completo.

1. Abra **Auto Quest** no FarmZilla e selecione o personagem. Desligue Seguir, Mat Farm, Frost, Alto Cultivo e operações concorrentes.
2. Clique em **Teste beta com log**. Confira o destino X/Z do monstro; os pontos predefinidos para **CheirosoWB** (1750 / 1773,4) e **ProFile** (765,6 / 327,3) são referências dos logs de 16/11, **não** objetivos descobertos automaticamente. Você pode alterá-los.
3. Se houver uma missão atual, aceite-a **manualmente** no NPC. Se souber o ID **interno** ativo, preencha-o; do contrário deixe em branco para testar somente o caminho, sem F12.
4. Marque a confirmação e clique em **Iniciar teste**. O FarmZilla tenta voar **uma vez** (se marcado) e, se não decolar, usa o auto caminho nativo. Caminha em trechos de até 110 unidades ou voa em trechos de até 280.
5. Ao chegar, o personagem deve estar no chão e fora de uma transição de voo. **Ligar F12** é opcional e só fica disponível com ID interno monitorado ativo. O programa não considera mudança de byte como conclusão.
6. Depois de conferir o objetivo **no jogo**, clique em **Voltar à origem** para interromper o F12 que o teste ligou e navegar de volta ao ponto inicial. A entrega/recompensa ao NPC continua **manual**.
7. Clique em **Parar** para encerrar e use **Abrir TXT do teste** para enviar o arquivo em %LOCALAPPDATA%\FarmZillaClassic\diagnosticos\autoquest-beta-AAAAMMDD-HHMMSS-PID.txt.

O TXT inclui decisões de voo/caminhada, posição/HP/atividade, comandos enviados, mudanças dos registros de missão e falhas; a sessão dura no máximo 15 minutos. Se o jogo não confirmar progresso de um trecho em 24 segundos ou houver mudança de identidade/vida/ID monitorado, o teste interrompe comandos e registra o erro.

**Limitações intencionais:** não interpreta nome, mob, contador de objetivos ou NPC a partir dos 32 bytes internos; não emite pacotes de NPC/entrega; não executa coleta. O diagnóstico **Gravar diagnóstico** de 30 minutos continua separado e sem comandos de jogo.


## Auto Quest 0.6.7 — correção da parede e destravamento limitado

**Motivo:** no teste beta de 16/11, CheirosoWB ficou parado em X=1687,7 / Z=1301,5 após enviar GroundAutoPath na direção de X=1702,1 / Z=1410,5; o personagem não encontrou caminho e a versão 0.6.6 parou após 24 segundos.

- A missão de teste conhecida **CheirosoWB X=1750 / Z=1773,4**, iniciada perto do NPC original (aproximadamente X=1710 / Z=978), agora usa **40 pontos de passagem observados na caminhada bem-sucedida de 09:06–09:10**. A rota passa pela **saída oeste da cidade (X≈1539 quando Z≈1300)**, não pelo muro em X≈1750. Para outro personagem/área/destino, **não** se presume que esses pontos sejam válidos.
- O caminho de volta segue os mesmos pontos em ordem inversa até a origem real da sessão; nenhuma entrega de missão é automática.
- Em qualquer trajeto pelo chão, se a distância até o waypoint não diminuir por **8 segundos**, o módulo interrompe o comando e tenta **recuar**, **esquerda** e **direita** em manobras curtas, confirmando deslocamento real. Máximo de **3 tentativas por sessão**; caso contrário, para e grava o motivo em TXT. No voo, a falta de progresso interrompe sem manobra não verificada.
- O TXT agora identifica ROTA APRENDIDA, OBSTACULO DETECTADO, DESTRAVAMENTO e ponto de passagem utilizado.
- **Para testar esta correção:** primeiro reposicione o personagem perto do NPC inicial. Não inicie a beta a partir de onde ele ficou preso contra a parede; sem uma trilha de saída comprovada para aquele ponto, o aplicativo bloqueará essa tentativa.
- Não há leitura automática de objetivos/contadores de missões ou pacote NPC/entrega nesta versão. O F12 ainda é explícito e opcional; Mat Farm/Espelho/Frost mantidos.

Os pontos são amostras reais de posições, não um mapa de colisões. Se o personagem sair da região validada, o programa interrompe em vez de atravessar um atalho incerto.


## Auto Quest 0.6.8 — escaneamento por região (primeira fase)

O objetivo agora é reconhecer missões e NPCs de **qualquer cidade**, não decorar o trajeto do CheirosoWB. Este é o **leitor inicial**, com ações de jogo desativadas durante o escaneamento.

- Abra **Auto Quest** e escolha o personagem. Clique **Escanear missões / NPCs desta região**.
- A ferramenta lê os IDs internos atualmente ativos, consulta o arquivo **tasks.data da instalação do cliente** (se disponível), indexa títulos das missões de nível superior e varre os NPCs atualmente carregados no mundo, com IDs, tipos e posições. Registra também as linhas internas da janela de entrega que o cliente expõe.
- O escaneamento é somente leitura, não depende do NPC inicial nem do destino antigo. É possível repetir o escaneamento em cidades diferentes.
- O resultado é salvo em %LOCALAPPDATA%\FarmZillaClassic\diagnosticos\autoquest-cityscan-*.txt; **Abrir pasta do log** abre o último arquivo gerado.
- A chave instância/setor é uma grade aproximada por coordenadas. **Não** é identificação do nome da cidade nem do nome do NPC.
- A leitura de tasks.data confirma apenas os cabeçalhos/nomes de missões principais; quests filhas, disponibilidade por NPC, requisitos de nível/classe, objetivo, progresso, mob e coordenadas de entrega ainda não estão validados. Sem tasks.data, o TXT explica a ausência e ainda informa NPCs e missões ativas.
- NPCs detectados mostram classe nativa, ID e coordenadas, mas não são automaticamente classificados como oferecendo missões. O campo de nome é guardado em hexadecimal para investigar o formato correto do The Classic, sem nome inventado.
- Nenhum NPC é clicado, nenhuma missão é aceita ou entregue, nenhum caminho é iniciado durante o scanner. O teste beta de navegação permanece separado.

### Próximas etapas do Auto Quest geral

1. Validar a presença e versão de tasks.data no The Classic e decodificar missões filhas, requisitos, fases e objetivos.
2. Cruzar cada NPC carregado (e o catálogo de NPCs/mapas, quando decodificado) com missões que ele realmente oferece; confirmar a disponibilidade no cliente.
3. Usar os NPCs, requisitos e destinos confirmados para escolher missões por cidade; ativar navegação dinâmica com detecção de colisão/obstáculos.
4. Só liberar aceite/entrega/combate automáticos depois de validar cada comando e mudança de estado no jogo.

**Não confundir o scanner regional (dados observados) com um Auto Quest universal pronto.**
