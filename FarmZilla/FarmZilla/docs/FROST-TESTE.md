# Frost Farm — captura, catálogo e trajeto (0.51.34)

O botão Frost Farm abre a ferramenta nativa do FarmZilla. Ela lê o cliente Avalon, lista os comandos que o próprio FarmZilla já valida e transforma a captura de coordenadas num trajeto `.txt` do Mat Farm. Não copia lógica de outro executável e não envia pacotes sozinha.

## O que a tela faz

1. **Captura e trajeto:** escolhe o personagem, grava CSV a cada segundo e, se o personagem se moveu, gera o trajeto do Mat Farm (mesmo formato `tipo=trajeto`).
2. **Comandos Avalon:** catálogo dos opcodes já usados no Espelho e no Mat Farm (pedra, Teleporte Especial, missões, ESC, revive, WF, navegação nativa, voo vertical).
3. O envio continua no Espelho (espelhamento) e no Mat Farm (rodar trajeto). O Frost Farm só observa e exporta.

## Teste no Windows

1. Abra o cliente PW Avalon, entre no personagem e abra FarmZilla.
2. Clique em Frost Farm e Atualizar. Confira nome e PID. Os erros de descoberta entram no log.
3. Na aba Comandos Avalon, confira a lista (códigos 0x6D, 0x28, 0x31, 0x25, 0x11, 0x2A, 0x08, 0x04, 0x57, 0x50, 0x29, 0x64, 0x14A).
4. Volte à captura, selecione o personagem e clique em Iniciar captura de coordenadas.
5. Mova o personagem por um trecho conhecido. Compare mapa e altura com o jogo. A amostragem é a cada segundo.
6. Clique em Parar captura. Se houver movimento suficiente, o FarmZilla grava um `.txt` ao lado do CSV. Senão, o log registra TRAJETO_ADIADO.
7. Clique em Gerar trajeto Mat Farm se quiser forçar a exportação. Abra a pasta das capturas e confira CSV + trajeto.
8. No Mat Farm, modo Manual, Carregar trajeto e selecione o `.txt` gerado. Confira pontos no mapa. Só rode o trajeto depois de marcar materiais e validar no jogo.
9. Feche o cliente durante a captura: deve parar com LEITURA_ERRO. Trocar de personagem só com a captura parada.
10. Confirme que Espelho e Mat Farm continuam abrindo normalmente.

Dados locais: `%LOCALAPPDATA%\AvalonInterno\Frost` (CSV e trajeto) e `%LOCALAPPDATA%\AvalonInterno\logs`. O CSV completo fica local; eventos resumidos seguem pelo log existente quando o serviço está disponível.

## Limites

Boss, DG automática e skill sender continuam fora desta tela: faltam IDs de instância/alvo e regras de conclusão validadas no Avalon. O catálogo não amplia a lista fechada de skills (312, 320, 329). Um trajeto gerado aqui não liga ataque nem teleporte sozinho.

A PR não publica no painel. O build e os testes automatizados não comprovam operação no jogo.
