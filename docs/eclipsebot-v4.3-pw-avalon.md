# EclipseBot V4.3 — Integração de materiais no PW Avalon

## Objetivo
Integrar ao EclipseBot (Windows, C#/.NET Framework 4.8 x86) o módulo de materiais baseado na lógica visual do MaterialBot, preservando as funções existentes da versão V4.1.

## Evidências observadas no cliente
- Personagem exibido: Cheirosa, no PW Avalon.
- Há um objeto próximo à árvore na captura fornecida pelo usuário; o nome não está completamente legível.
- O usuário confirmou que precisou clicar no aviso **Confirmar** e depois **escolher o material** na janela de seleção.
- A captura mostra uma barra de progresso em 37% durante a interação.
- Essas observações **não demonstram** que o EclipseBot já lê materiais, posições ou distâncias reais.

## Comportamento desejado
1. Selecionar um personagem já identificado pelo EclipseBot.
2. Clicar em **Atualizar materiais** para consultar objetos próximos, inicialmente sem coleta automática.
3. Mostrar nome, posição e distância somente quando obtidos de uma fonte de dados validada.
4. Permitir selecionar os tipos de materiais por personagem/rota.
5. Registrar e salvar pontos de rota e filtros.
6. Mostrar pontos e materiais em um mapa integrado, sem apresentar dados simulados como reais.
7. Em uma fase posterior, validar o fluxo de interação do PW Avalon: aviso de confirmação, escolha do material e progresso da coleta.
8. Prever pausa, parada, registro de falhas e limites de tentativas.

## Configurações iniciais
Conforme [materialbot-raios.md](materialbot-raios.md):
- Raio de identificação: 100 unidades de mapa (escala em metros reais ainda não validada).
- Raio de coleta: 30 unidades de mapa.
- Atualização automática: desativada.
- O raio de coleta nunca pode exceder o raio de identificação.

## Etapas técnicas
### Fase 1 — Interface e diagnóstico
- Preservar a V4.1 como base funcional.
- Corrigir os problemas de compilação observados na V4.2.
- Implementar interface, filtros, rotas, persistência e mapa.
- Instrumentar diagnóstico somente de leitura; não inventar offsets de memória.

### Fase 2 — Validação de dados reais
- Validar origem autorizada dos dados do jogo.
- Confirmar nome, coordenadas, mapa e distância de um material próximo.
- Testar ausência de materiais, mudança de mapa e múltiplos personagens.

### Fase 3 — Interação de coleta
- Confirmar fluxo real de confirmação e seleção do material.
- Implementar controle explícito de iniciar/pausar/parar, se a integração for validada.
- Impedir tentativas infinitas e registrar resultados.

## Estado
Especificação e evidências de interface documentadas. Código-fonte da V4.3, integração real e testes de compilação **ainda não publicados nem validados** neste repositório.
