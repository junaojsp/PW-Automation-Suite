# Perfil The Classic 1.4.4 — diagnóstico 0.2.0

Arquivo de referência: SHA-256 `39d81c21ca72f373a787c936da1611281593d950668f2077016d77c1424338ce`.
PE32 x86; ImageBase preferida `0x00400000`; SizeOfImage `0x00D66000`.

Os endereços abaixo foram obtidos por análise estática do executável enviado, não por copiar
os offsets do Avalon. As funções foram inspecionadas sem executar o cliente. O teste com o
personagem conectado continua necessário para confirmar o significado e comportamento dos campos.

| Campo | Localização | Evidência estática (VA na imagem preferida) |
| --- | --- | --- |
| Ponteiro CECGameRun | módulo + RVA `0xCA579C` | `0x4B3B88` chama construtor; `0x4B3B98` guarda resultado em `0x10A579C` |
| Cadeia alternativa | `[módulo + 0xCA3B3C] + 0x1C` | rotina CDlgHost em `0x649DB0–0x649DBF` |
| HostPlayer | CECGameRun + `0x28` | criação CECHostPlayer em `0x4BDC06`, guarda em `0x4BDC16` |
| Tipo CECGameRun | vtable RVA `0x9E5178` | RTTI e construtor em `0x4BC7B4` |
| Tipo CECHostPlayer | vtable RVA `0x9E7E50` | RTTI e construtor em `0x4FA245` |
| ID | HostPlayer + `0x4B8` | inicialização em `0x4FB3F9–0x4FB3FC`; comparações com alvo |
| Nível | HostPlayer + `0x4C4` | CDlgHost em `0x649DCE` e leitura em `0x4F62FB` |
| HP / máximo | HostPlayer + `0x4CC` / `0x518` | CDlgHost em `0x649DC8` / `0x649DDA`, valores usados na UI de HP |
| MP / máximo | HostPlayer + `0x4D0` / `0x51C` | CDlgHost em `0x649DEB` / `0x649DF7`, outra leitura em `0x64ADAD–0x64ADB3` |
| Nome (ponteiro UTF-16) | HostPlayer + `0x6D8` | CDlgHost em `0x649EE1`, copiado para texto e exibido |
| Classe ID | HostPlayer + `0x6DC` | CDlgHost em `0x649F28`, valida índice entre 0 e 11 |
| Posição X/Y/Z interna | HostPlayer + `0x3C` / `0x40` / `0x44` | método virtual da vtable Host, slot `0x1C`, em `0x4D281A–0x4D283A` |
| Alvo ID | HostPlayer + `0x59C` | `0x50B5BF–0x50B5CD` compara com ID próprio; outras leituras em `0x500C0A` e `0x57BA69` |

O leitor exige o hash de referência, verifica guardas do código carregado com ajuste de realocações,
compara as duas cadeias globais e exige as vtables exatas. Lê um bloco limitado do personagem,
valida atributos e nome, e confere novamente ponteiros/ID antes de devolver a amostra.

As coordenadas exportadas são internas X/Y/Z; ainda não há conversão validada para os números
mostrados no minimapa. O alvo é somente seu ID, sem nome/tipo resolvido. Inventário permanece pendente.
Nenhuma amostra ou coincidência de assinatura habilita automações.

## Integração de seguir/parar — teste 0.3.0

Reutiliza `InternalBridge` e `ProcessGate` do FarmZilla. Na ponte compartilhada, somente os
campos de ligação passam a vir do perfil; o perfil Avalon conserva `Host +0x34`, ID `+0x4F0`
e gerente de mensagens `Run +0x74`. O perfil Classic fornece `+0x28`, `+0x4B8`, `+0x68`.

- Enfileiramento da mensagem `0xEE`: método VA `0x825090` (RVA `0x425090`).
- UI do jogo em `0x712F54–0x712F96` e `0x7144E4–0x714526`: mensagem de 32 bytes,
  opcode em `+0`, alvo em `+0xC`; contexto é `[Run +0x68]+8`.
- Trabalho de seguir: vtable RVA `0x9E8238`; alvo em `work +0x20`.
- Gerente de trabalhos: `Host +0x13F8`, vtable RVA `0x9E8140`;
  construção em `0x4FB43D–0x4FB484` de três vetores de 16 bytes a partir de `+8`.
- O leitor consulta as duas filas usadas no FarmZilla (`+8`, `+0x18`).

O comando Parar conserva o argumento `1` usado pelo FarmZilla; sua semântica no Classic
precisa ser confirmada pelo teste em jogo. A confirmação exige a ausência de trabalho ativo de seguir.
O autoteste prova mensagem/thread/restauração em uma janela do próprio processo, não a semântica do jogo.
Não há retomada automática nem repetição em segundo plano nesta entrega.
