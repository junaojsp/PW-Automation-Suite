# FarmZilla — plano de importação inicial

Origem: pacote local `FarmZilla-CodigoFonte-ClaudeCode-0.19.2.rar`, disponibilizado pelo proprietário do projeto.

## Diagnóstico preliminar
- O arquivo original reúne 9.402 entradas e 112 pastas de versões históricas do cliente.
- A versão numericamente mais recente identificada é `FarmZilla-0.51.20`.
- A árvore `EclipseBot/` já existe neste repositório e **não deve ser alterada**.
- O cliente FarmZilla é C#/.NET Framework WinForms e exige execução/testes no Windows.
- Os scripts PowerShell de build usam caminhos de versões antigas e precisam de revisão antes do build.

## Política de importação
1. Importar os arquivos do projeto sob `FarmZilla/`, sem substituir `EclipseBot/`.
2. Preservar fontes C#, recursos JSON, scripts de testes/build e fontes do painel online.
3. **Não versionar** `docs/CREDENCIAIS-LOCAIS.md`, senhas, tokens, chaves, executáveis produzidos, logs, arquivos .rar/.zip nem compilações históricas.
4. Revisar direitos de redistribuição antes de publicar DLLs e recursos de terceiros.
5. Não executar scripts de publicação/provisionamento nesta etapa.
6. Criar CI de build/testes em Windows, com PR e validação antes de qualquer merge.

## Próximas etapas
- Conferir a importação dos fontes 0.51.20.
- Corrigir scripts de build para apontar para a versão importada.
- Validar os testes existentes e registrar falhas.
- Planejar melhorias de UX, estabilidade, detecção de personagem e rotas.

**Estado:** documentação inicial. O envio integral do código deve ser confirmado separadamente. Nenhuma alteração de produção foi realizada.