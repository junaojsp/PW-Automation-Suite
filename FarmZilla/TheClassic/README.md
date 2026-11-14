# FarmZilla The Classic — teste 0.4.0

Pacote Windows .NET Framework x86 com dois executáveis:

- **FarmZilla-TheClassic-Teste.exe**: seleção de principal/secundários no layout FarmZilla e teste manual de Seguir/Parar, usando a mesma ponte interna do Avalon com perfil Classic.
- **FarmZilla-TheClassic-Diagnostico.exe**: leitura de personagem e relatório JSON, sem comandos.

O teste de seguir ainda precisa de confirmação no jogo. Ataque, buffs, macros, teleporte,
inventário e atualização automática do Classic não estão habilitados. Não publique este
pacote no canal Avalon do painel.

## Testar com duas contas

1. Extraia o ZIP em uma pasta separada do FarmZilla Avalon. Abra duas contas The Classic e entre no mapa; deixe os personagens próximos.
2. Abra **FarmZilla-TheClassic-Teste.exe**. O log já estará ativo; clique em **Sincronizar contas** caso necessário.
3. Escolha o principal e selecione um secundário diferente em uma das nove linhas do Espelho.
4. Clique em **Seguir principal**, caminhe com o principal e observe o secundário. O botão passa a **Parar seguimento**.
5. Clique em **Parar seguimento**, caminhe novamente e confira se o secundário parou de acompanhar.
6. Clique em **Registros** e envie o arquivo desta sessão junto com o resultado observado.

O log fica em `%LOCALAPPDATA%\FarmZilla\TheClassic\logs\teste-DATA-HORA-PID.log`.
Ele registra versão/hash do cliente, contas e snapshots descobertos, comandos, IDs/thread,
confirmação pelo trabalho de seguir, restauração da ponte e exceções completas. Não é enviado
automaticamente ao painel e não inclui dumps, senha ou chats.

`COMMAND_SENT` indica execução da mensagem; somente `STATE_CONFIRMED` indica que o leitor
observou o estado esperado. O movimento e o comportamento de Parar ainda precisam da sua observação.
Se houver falha na instalação/remoção da ponte, esse PID fica bloqueado para novos comandos;
reinicie esse cliente antes de repetir. Não existe repetição automática de comandos neste teste.
A saída tenta parar as contas acionadas por esta sessão, mesmo que você as desmarque depois.

## Diagnóstico de leitura

No diagnóstico separado, clique em **Analisar executável** e selecione o `elementclient.exe`
instalado. Com o personagem conectado, clique em **Ler personagens conectados**, compare os
valores com o jogo e use **Salvar relatório**. As últimas 12 amostras ficam no JSON.

Referência: SHA-256 `39d81c21ca72f373a787c936da1611281593d950668f2077016d77c1424338ce`,
13.287.376 bytes, PE32 x86. Outra compilação é recusada para comandos.
O executável do jogo não é redistribuído. Veja [PROFILE.md](PROFILE.md) para a evidência estática.

## Separação dos jogos

O adaptador `FollowProfile.cs` é compilado somente no EXE de teste Classic. O Avalon usa
seu próprio `GameProfile`; seus offsets, versão, licença e publicações continuam os mesmos.
A ponte `Bridge.cs` e o bloqueio `ProcessGate.cs` são compilados diretamente da fonte compartilhada.

O painel terá produtos/canais por jogo (`avalon`, `tclassic`) em etapa posterior. Cada EXE deverá
consultar somente seu produto e rejeitar downloads de outro jogo antes de habilitar atualizações Classic.

## Compilar

Execute `./FarmZilla/TheClassic/Build-Diagnostic.ps1` no Windows com Visual Studio 2022.
O workflow próprio entrega os dois EXEs, hashes, documentação e capturas da interface.
Os testes de leitura usam memória simulada; o teste da ponte usa exclusivamente uma janela
criada pelo próprio processo de teste, nunca um cliente de jogo.

## Layout compartilhado

O Classic usa os mesmos arquivos `RubiPalette.cs`, `FarmZillaFrame.cs`, `SideItem.cs`, `Theme.cs`,
`TickBox.cs`, `UiScale.cs`, `ClassIcons.cs` e o mesmo ícone do FarmZilla. Cores, moldura, cartões, botões,
seletores e menu são compilados da mesma fonte. A seleção mantém o backend de seguir/parar validado.
O log automático fica em segundo plano e o botão Registros abre sua pasta. Ferramentas, ataque,
suporte e demais comandos ainda não validados para o perfil Classic aparecem desabilitados.
Nenhum desses controles chama uma rotina de cliente com offsets Avalon.
