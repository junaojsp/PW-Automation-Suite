# MaterialBot — Raios configuráveis

## Configuração inicial
- Raio de identificação: **100 m**
- Raio de coleta: **30 m**
- Atualização automática: **desativada**

## Comportamento
1. O botão **Atualizar** realiza uma consulta manual aos materiais próximos, respeitando o raio de identificação.
2. A lista esquerda apresenta os materiais identificados e a contagem por tipo.
3. A lista direita contém os materiais e pontos registrados na rota.
4. O executor somente inicia a interação de coleta quando a distância até o alvo for menor ou igual ao raio de coleta.
5. A distância de coleta não deve ultrapassar a distância de identificação; validar essa regra na interface e na configuração persistida.
6. Os valores devem ser editáveis na interface e persistidos por perfil/rota, sem recompilar o aplicativo.
7. Pausar/parar, registrar falhas e evitar tentativas infinitas de coleta.

## Implementação proposta
- `MaterialScanner`: recebe `IdentificationRadius` e filtra materiais pela distância calculada.
- `RouteRecorder`: registra nome, mapa, posição tridimensional, altitude e ordem dos pontos.
- `RouteExecutor`: recebe `CollectionRadius` e controla aproximação, interação e próximo ponto.
- `Settings`: valores padrão `identificationRadius=100`, `collectionRadius=30`, `autoRefresh=false`.

## Pendências técnicas
- Confirmar a escala real das coordenadas do Perfect World antes de exibir a unidade como metros reais.
- Identificar fonte autorizada de dados de posição e materiais; não presumir offsets de memória.
- Criar testes de filtragem nas bordas (30 e 100), persistência, mudança de mapa e interrupção.

**Estado:** especificação aprovada; implementação e testes pendentes.
