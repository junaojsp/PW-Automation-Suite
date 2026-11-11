# Frost Farm — primeira etapa de teste Avalon (0.51.33)

Esta entrega abre uma janela de diagnóstico pelo botão Frost Farm. Não contém a rota de 454 passos, modos de boss ou DG automática: a lógica do executável de referência continua protegida e não foi recuperada. Não execute o binário de referência para usar esta tela.

## Teste no Windows

1. Abra o cliente PW Avalon, entre no personagem e abra FarmZilla.
2. Clique em Frost Farm e Atualizar. Confira nome e PID. Os erros de descoberta entram no log.
3. Selecione o personagem e clique em Iniciar captura de coordenadas.
4. Mova o personagem manualmente por um trecho conhecido. Compare as coordenadas de mapa e altura com as do jogo. A amostragem ocorre a cada segundo, portanto não captura todo movimento entre amostras.
5. Clique em Parar captura. Abra a pasta das capturas e confira o CSV com coordenadas do mundo e do mapa, horário UTC, PID e HP. O arquivo é observação, não uma rota executável. Não contém ID de instância/mapa validado.
6. Repita, fechando o cliente durante a captura: deve parar e registrar LEITURA_ERRO. Trocar de personagem deve ser feito com a captura parada.
7. Confira no log do FarmZilla os eventos Frost: DESCOBERTA, CAPTURA_INICIO, AMOSTRA, CAPTURA_FIM e eventuais erros. O canal existente de RemoteLog envia esses eventos quando o serviço e a autorização do computador estão disponíveis; sem conexão eles não aparecem imediatamente no painel.
8. Feche a janela durante uma captura. Reabra e inicie outra; cada sessão deve gerar um CSV diferente. Confirme que Espelho e Mat Farm continuam abrindo normalmente.

Dados locais: `%LOCALAPPDATA%\AvalonInterno\Frost` (CSV) e `%LOCALAPPDATA%\AvalonInterno\logs` (log diário). O CSV completo fica local; apenas eventos e amostras resumidas seguem pelo log existente. Os limites de retenção do log existente continuam valendo. Preserve o CSV se precisar investigar uma sessão longa.

## Limites para a próxima etapa

As coordenadas do bot antigo ainda não estão disponíveis para comparação. Os registros desta tela ajudam a validar o leitor Avalon e capturar uma rota manual, mas não descobrem sozinhos IDs de boss, interação com NPC ou regras de conclusão. Antes de habilitar movimento/combatе automático, validar rota, mapa/instância, alvos e exclusão mútua com as automações existentes.

A PR não publica no painel. O build e os testes automatizados não comprovam operação no jogo nem atualização completa no Windows. A versão anterior instalada permanece a base do teste de atualização após revisão e publicação do artefato correto.
