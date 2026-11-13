# FarmZilla — The Classic 1.4.4

Base independente de diagnóstico, versão 0.1.0.0, Windows .NET Framework x86.
Não é uma versão jogável do bot nem deve ser publicada no canal Avalon.

## Teste no Windows

1. Baixe o artefato **FarmZilla-TheClassic-0.1.0-DIAGNOSTICO-x86** do workflow próprio e extraia o ZIP em uma pasta separada do FarmZilla Avalon.
2. Abra `FarmZilla-TheClassic-Diagnostico.exe`.
3. Clique em **Analisar executável** e selecione o `elementclient.exe` instalado no The Classic.
4. Com o The Classic aberto, clique em **Conferir clientes abertos**.
5. Clique em **Salvar relatório** e envie o JSON para a próxima etapa de adaptação.

A aplicação não exige licença FarmZilla, não modifica arquivos do jogo e não envia comandos.
O arquivo do jogo não é redistribuído. O relatório inclui nome do arquivo, SHA-256, arquitetura,
metadados PE, candidatos RTTI/assinaturas e identificação dos processos. Não inclui dump da memória,
caminho completo, senha ou conteúdo de chats.

Referência enviada para análise:

- SHA-256: `39d81c21ca72f373a787c936da1611281593d950668f2077016d77c1424338ce`
- Tamanho: 13.287.376 bytes; PE32 x86.

Arquivos diferentes podem ser analisados offline, mas não recebem a identidade de referência.
A consulta ao processo só lê seu cabeçalho após conferir o hash do arquivo e o tamanho do módulo.
Isso confirma identidade básica, não a integridade completa do processo nem offsets de personagem.
Assinaturas do Avalon são comparadas apenas para orientar o remapeamento; coincidências parciais
não liberam funções e não são usadas para interpretar HP, MP ou coordenadas.

## Próximas etapas

- Mapear e validar no jogo os campos de nome, classe, nível, HP, MP, posição, alvo e inventário.
- Criar perfil exclusivo The Classic e testar Espelho antes das demais automações.
- Integrar o mesmo visual do FarmZilla após a validação das leituras.
- No Control, cadastrar produtos/jogos com identificador estável (`avalon`, `tclassic`),
  release publicada e histórico separados. O servidor deve filtrar atualização e download por jogo;
  o cliente deve rejeitar artefatos de outro produto. A validação PE/versão/hash permanece obrigatória.
- Migrar o canal existente para `avalon` mantendo os clientes atuais funcionando.

O painel, o publicador, o licenciamento e os arquivos do Avalon não são alterados nesta entrega.
A configuração de cada jogo e a identidade de atualização devem ser separadas antes de publicar o bot Classic.

## Compilação

Execute `./FarmZilla/TheClassic/Build-Diagnostic.ps1` no Windows com Visual Studio 2022
(Build Tools/.NET Framework). O build inclui somente o diagnóstico e o analisador estático compartilhado;
não inclui pontes nativas, envio de pacotes, injeção, automações ou o atualizador do Avalon.

Opcional, diagnóstico do arquivo sem interface:

```powershell
Start-Process -Wait -FilePath .\FarmZilla-TheClassic-Diagnostico.exe -ArgumentList '--inspect "C:\Jogo The Classic\elementclient.exe" "relatorio.json"'
```
