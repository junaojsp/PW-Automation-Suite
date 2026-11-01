# EclipseBot — Histórico de modificações e roteiro

> Registro de desenvolvimento baseado nas versões discutidas e nos testes relatados pelo usuário. **Não substitui testes de compilação ou execução.**

## Base do projeto
- Aplicativo Windows C# WinForms, projeto `Bagaceira.csproj`, `.NET Framework 4.8`, `x86`, assembly `EclipseBot`.
- Código derivado de fonte descompilada do Bagaceira; verificar direitos e licenças antes de redistribuir publicamente.
- Código-fonte publicado em [`EclipseBot/eclipse_integration`](../EclipseBot/eclipse_integration).
- O executável `EclipseBot.exe` **não foi identificado no repositório** na verificação de 01/11/2026.
- O documento `LEIA-ME-ECLIPSE.txt` da base informa que algumas telas são placeholders e que o licenciamento original foi mantido. Ele contém informações históricas que podem estar desatualizadas; conferir no código antes de assumir implementação.

## Histórico de versões

### V4.1 — Interface integrada e personagens (base de referência)
**Relatado como funcional no Windows do usuário:**
- Janela EclipseBot com painel Eclipse Materiais integrado.
- Correção para arrastar a janela pelo cabeçalho também na aba Materiais.
- Sincronização da lista de personagens detectados; capturas mostraram `Cheirosa` e `CheirosaEP`.
- Funções originais devem ser preservadas durante alterações futuras.

**Limites:** a exibição de personagens não comprova leitura de materiais próximos nem coleta automatizada. A versão publicada no GitHub precisa ser compilada/testada para confirmar equivalência à cópia local funcional.

### V4.2 — Seleção de materiais por rota (tentativa)
**Proposta e código de teste discutidos:**
- Botão **Selecionar materiais** na interface de rotas.
- Seletor com catálogo local, importação TXT, seleção por checkbox e persistência por rota.
- Módulo experimental `EclipseMaterialPicker.cs` na versão de teste.

**Falha relatada:** a V4.2 não gerou o EXE no computador do usuário. O erro de compilação específico não foi apresentado. **Não considerar a V4.2 validada nem incorporar suas funções como concluídas.** O diretório atualmente publicado contém `EclipseMateriaisForm.cs`, mas a lista de arquivos verificada não contém `EclipseMaterialPicker.cs`.

### V4.3 — Mapa e coleta por rota (planejada)
**Referência funcional/visual:** protótipo de Bot de Materiais compartilhado em captura de tela pelo usuário, com:
- Personagem e mapa selecionáveis.
- Painel **Por perto** com atualização manual.
- Mapa visual com posição, materiais, obstáculos e pontos.
- Rotas salvas, pontos gravados, materiais selecionados.
- Controles **Gravar**, **Parar gravação**, **GPS**, **Iniciar**, **Pausar**, **Retomar**, **Parar**.
- Raios de identificação/coleta configuráveis e diário de eventos.

**Cuidado:** o protótipo informa explicitamente que o mapa é uma simulação e não tem conexão com o cliente do jogo. Não apresentar simulação como leitura real.

## PW Avalon — observações de campo
Capturas enviadas pelo usuário mostram o personagem `Cheirosa` perto de um objeto no cenário, uma janela **Missões** com opção **Confirmar**, uma janela **Selecionar** com itens e uma barra de progresso. O usuário confirmou que precisou **clicar em Confirmar e escolher o material**. Portanto:
- O fluxo real de interação tem mais de uma etapa.
- As imagens não fornecem identificador, coordenadas ou distância do material.
- O `ElementClient.exe` foi enviado para investigação, mas a leitura real de materiais **não está validada**.

## Requisitos de implementação
1. Preservar o funcionamento da V4.1 e manter versões testáveis.
2. Corrigir a compilação antes de acrescentar automações.
3. Consultar materiais apenas por fonte de dados validada; não presumir offsets de memória.
4. Exibir dados reais com origem identificável; separar claramente dados simulados.
5. Configurar por personagem/rota os materiais selecionados, o mapa e os raios.
6. Usar inicialmente raio de identificação **100** e raio de coleta **30**, em **unidades de mapa**, até validar a conversão para metros reais.
7. Impedir raio de coleta maior que o raio de identificação.
8. Atualização automática desativada por padrão.
9. Registrar eventos, falhas, mudanças de mapa e interrupções.
10. Somente após validar a consulta, avaliar interação de coleta com confirmação/seleção e controles de parada.

## Situação de validação — 01/11/2026
| Item | Situação |
| --- | --- |
| Repositório `PW-Automation-Suite` | Confirmado |
| `EclipseBot/eclipse_integration` | 73 entradas no diretório, confirmadas via GitHub |
| `Bagaceira.csproj` | Confirmado: WinExe, net48, x86 |
| V4.1 no computador do usuário | Interface e personagens relatados como funcionais |
| Compilação da versão publicada | Não verificada |
| `EclipseBot.exe` publicado | Não localizado |
| V4.2 | Falha de compilação relatada |
| V4.3 | Planejada, não implementada |
| Materiais reais próximos | Não validados |
| Coleta automática | Não implementada/validada |

## Próximos passos
1. Adicionar `.gitignore` para `bin/`, `obj/`, logs e arquivos sensíveis.
2. Auditar credenciais, URLs internas, licenças e código de terceiros antes de ampliar a distribuição.
3. Criar workflow de build Windows para .NET Framework 4.8 x86, publicar artefatos de build e registrar falhas.
4. Comparar a cópia local V4.1 funcional com o código do GitHub.
5. Corrigir o seletor de materiais e compilar em ambiente Windows.
6. Implementar mapa/rotas com dados demonstrativos explicitamente identificados.
7. Validar leitura real de objetos próximos em modo somente consulta.
8. Testar a interação de coleta do PW Avalon somente depois de confirmar os dados.

## Documentos relacionados
- [Raios e comportamento do MaterialBot](materialbot-raios.md)
- [Plano V4.3 PW Avalon](eclipsebot-v4.3-pw-avalon.md)

## Convenção para novos registros
Para cada mudança: **data**, **versão**, **arquivos alterados**, **comportamento anterior**, **mudança**, **teste executado**, **resultado**, **pendências** e **commit**. Não marcar como concluído sem teste.
