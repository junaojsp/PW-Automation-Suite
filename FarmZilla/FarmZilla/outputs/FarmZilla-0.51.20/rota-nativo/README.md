# Ponte nativa x86

O aplicativo envia uma notificação privada (WM_APP+0x5A7) para acordar a thread da janela. WH_GETMESSAGE executa a chamada dentro dessa thread e consome a notificação. Nenhuma mensagem de teclado ou mouse é emitida. O pedido, o resultado e as coordenadas são lidos/escritos em uma área de memória limitada; não há patch no executável original.

Validações: PID/thread, nonce, protocolo, hash do cliente pelo GameClient, nome do processo, tamanho da imagem e assinaturas/RTTI dos pontos usados. O diagnóstico sem ação passou em janela de teste e no cliente. Em timeout, a área de pedido não é liberada enquanto houver incerteza sobre callback concorrente; isso evita acesso à memória já liberada, ao custo de uma página até o processo encerrar.

## Estado validado em 02/11/2026

- Navegação: rotina RVA 0x4DFF80, ECX aponta para vetor XYZ, EDX=0 e argumento de pilha zero, removido pelo chamador. Esse caminho enfileira mensagem 0x14A no gerenciador interno. Testes até 139,656 e 141,657 confirmaram deslocamento e chegada horizontal. No segundo, o foco permaneceu no chat. A altura solicitada por esse vetor não controlou a altura de cruzeiro; falta mapear o controlador correspondente.
- Coleta: host TraceMatter em RVA 0x3FA070, ECX=personagem, argumentos instância e gathering=true, ret 8. Dois testes confirmaram aproximação/coleta em voo e recebimento de item temporário.
- Missão/recompensa: ciclo completo de Salgueiro comprovado em analise_nativa/CICLO_08_SUCESSO.md (item 797: 3→6). IDs observados: 61532 Salgueiro, 61546 Carvão, 61547 Núcleo. PrepareMaterialReward pode exigir ID esperado para não confirmar uma tarefa diferente.

A versão 0.6 integra o worker RouteExecution ao botão Executar rota, com cancelamento, log JSONL, pulo de ausentes e confirmação de inventário. Requer voo já ativo; mantém altitude atual. Dano interrompe até integrar defesa. O ciclo individual foi comprovado; a volta integrada de vários pontos ainda precisa ser validada. O primeiro teste do worker parou corretamente por voo desligado.

## Compilar

O pacote de fontes inclui RotaAvalon.NativeBridge.dll, gerada deste fonte, para permitir compilar o aplicativo C# sem instalar ferramentas extras. compilar.ps1 copia essa DLL para a entrega.

Para alterar o código C, instalar o compilador local: `python -m pip install --target .tools/zig --no-deps ziglang==0.16.0`. Depois executar `native/compilar_ponte.ps1`. Dependência: [distribuição oficial Zig para PyPI](https://pypi.org/project/ziglang/0.16.0/). O compilador e seus caches ficam em .tools, fora do pacote de fontes e do Git.

Diagnóstico: `bin/NativeNavigation.exe --bridge-self-test`, seguido de `--ping PID`. Teste de destino curto: `--navigate PID MAP_X MAP_Z ALTURA`; o resultado separa confirmação do callback, movimento observado e chegada. NativeCollection é ferramenta experimental; não usar como executor pronto.
