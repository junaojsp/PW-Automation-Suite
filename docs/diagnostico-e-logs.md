# EclipseBot — Diagnóstico e logs

## Objetivo
Instrumentar o aplicativo para testes incrementais com o cliente Perfect World, inspirado no mecanismo relatado do Bagaceira. A implementação exata do logger do Bagaceira ainda não foi verificada.

## Eventos
- `process.detected`, `process.exited`: PID, identificador de janela, nome do executável.
- `character.resolve.start/success/failure`: PID, estratégia utilizada, duração, erro estruturado; não registrar endereços de memória ou dumps por padrão.
- `bridge.connect.start/success/failure`: arquitetura, estado e códigos de erro.
- `action.start/success/failure`: ação (party, follow, buff, attack), duração, resultado e motivo da falha.
- `settings.changed`: nome do parâmetro, sem dados sensíveis.

## Formato
JSON Lines UTF-8, uma linha por evento, timestamp UTC ISO 8601, severity (`Debug`, `Info`, `Warning`, `Error`), sessionId, module, eventId, pid, elapsedMs, message, exceptionType e errorCode quando houver.

## Operação
- Pasta local `%LOCALAPPDATA%/EclipseBot/logs/`.
- Arquivos por dia, rotação por tamanho e retenção configurável (padrão 7 dias).
- Logs `Info` por padrão; `Debug` somente sob opção explícita.
- Não incluir tokens, credenciais, identificadores de conta ou dados privados de terceiros.
- Botão **Exportar diagnóstico** cria ZIP com logs recentes e resumo de versão/configuração não sensível.
- O botão não envia nada automaticamente; o usuário decide se compartilha.

## Critérios de aceite
1. Abrir o app sem jogo produz eventos de inicialização e ausência de cliente, sem travar.
2. Abrir dois clientes produz dois PIDs distintos.
3. Falha ao obter nome registra `character.resolve.failure` com motivo rastreável.
4. Cada comando registra início e resultado, inclusive timeout.
5. Fechar o jogo não derruba o EclipseBot.
6. Exportação omite dados sensíveis e gera arquivo legível.

**Status:** especificação adicionada; logger ainda não implementado nem testado.
