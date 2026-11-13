# Frost 0.51.35 — teste do código Bagaceira

Fonte: Codigo Fonte V2.zip fornecido pelo proprietário. FrostBot, DgBot, DgRouteData e WolfRoom substituem a tentativa de recuperar o antigo binário protegido. São 454 passos preservados, com cinco etapas de boss. Não foram confirmados os IDs e as mecânicas no Avalon por este ambiente.

## Teste Windows

1. Abra os clientes Avalon, ative o FarmZilla e sincronize personagens. Deixe o botão principal mostrando Desativar: o polling de interações depende do aplicativo ativo. Não execute outro bot nas mesmas contas.
2. Em Frost Farm, sincronize e escolha líder e secundários. O líder participa automaticamente. Modos de boss usam as contas escolhidas; não tomam todas as contas abertas silenciosamente.
3. Teste Dragão e Lobo separadamente, dentro das salas correspondentes, e pare pelo botão Parar Frost. Confira o log antes de mudar de modo.
4. Para a DG, selecione as contas e inicie. O programa pode solicitar interações manuais com NPCs para aprender os pacotes de entrada/missão/saída; siga o status e o log. A captura usa o mesmo serviço de interceptação do FarmZilla em modo Frost, sem espelhamento de missões/teleporte durante esse modo.
5. Nesta PR a DG termina após um ciclo. A remontagem automática da PT e o reinício contínuo não estão habilitados na interface.
6. Teste parar no deslocamento e combate; não feche os clientes enquanto a parada está em andamento. Fechar a janela ou FarmZilla solicita parada cooperativa e aguarda as rotinas. Nenhuma thread é abortada à força.
7. Teste indisponibilidade do cliente, rota ocupada no Mat Farm e perda da autorização. Registre versão, modo, personagens e horário. Os eventos Frost runtime seguem por DiagnosticLog/RemoteLog quando o serviço estiver disponível; os logs locais permanecem em %LOCALAPPDATA%\AvalonInterno\logs.
8. Após parar, confira Espelho/Mat Farm e o funcionamento das automações existentes. O aprendizado da DG usa %LOCALAPPDATA%\AvalonInterno\frost-dg-aprendido.txt, separado de licença/configuração.

O build usa Roslyn do Visual Studio 2022 Enterprise no runner, com alvo .NET Framework x86. Verificações automatizadas não substituem teste de movimento, combate, interação e atualização/reinício no Windows. A PR não publica nada no painel.
