# FarmZilla - importacao inicial

Codigo-fonte de desenvolvimento do FarmZilla, versao 0.51.20, extraido do pacote enviado pelo proprietario.

## Estrutura

- `outputs/FarmZilla-0.51.20/`: fontes C# e recursos de texto da versao mais recente.
- `work/`: scripts PowerShell, testes, ferramentas e painel online.
- `docs/`: arquitetura e referencias tecnicas sem arquivo de credenciais.

## Importante

- O programa cliente depende de Windows, .NET Framework e cliente Perfect World. Nao rodar o cliente na VPS Linux.
- Scripts `work/Build-Release.ps1` e similares ainda usam caminhos legados. Atualizar os padroes para `outputs/FarmZilla-0.51.20` antes do primeiro build.
- Nao executar scripts de release ou provisionamento ate revisar e validar credenciais e destino.
- Arquivos executaveis, DLLs nativas, arquivos zip historicos e `docs/CREDENCIAIS-LOCAIS.md` foram excluidos deste pacote de importacao publica.
- `outputs/FarmZilla-0.51.20/recursos/RotaAvalon.NativeBridge.dll` deve ser reconstruida a partir de `rota-nativo/RotaNativeBridge.c`, ou restaurada por um canal privado autorizado.
- Testar a compilacao e operacao em um Windows isolado. Nenhum teste de compilacao foi executado durante esta preparacao.

## Proximos passos

1. Revisar o pacote antes de publicar, inclusive licencas e direitos de redistribuicao.
2. Importar na branch `chore/farmzilla-auditoria-inicial`, mantendo `main` intacta.
3. Corrigir caminhos de compilacao e preparar CI Windows para build/testes.
4. Testar reconhecimento de personagens e modulos existentes antes de modernizar a interface.
