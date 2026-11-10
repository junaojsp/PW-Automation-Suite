# FarmZilla — código-fonte para continuidade

Pacote preparado em 31/10/2026. Base: **FarmZilla 0.19.2**, gerenciador **2.0.1**. Contém o código atual do cliente, gerenciador, serviço de autorização, testes e scripts. Leia `CLAUDE.md` antes de modificar.

## Começar no Claude Code

Extraia este pacote em uma pasta sua e abra essa pasta como projeto no Claude Code. Use a mensagem de `INICIAR-NO-CLAUDE.txt` para começar. Se for continuar no mesmo computador e workspace original, pode abrir diretamente:

`C:\Users\nyeds\Documents\Codex\2026-10-26\que`

O workspace original agora também contém `CLAUDE.md` e mantém o histórico completo. Este pacote é uma seleção portátil das fontes atuais, não uma cópia de credenciais ou do computador.

## Estrutura

| Caminho | Conteúdo |
|---|---|
| `CLAUDE.md` | Estado do projeto, decisões, restrições e próximos passos |
| `outputs/FarmZilla-0.19.2/` | Todos os fontes C# do cliente, ícone e documentação da versão |
| `work/admin-source-v2/Admin.cs` | Gerenciador de liberar/bloquear computadores |
| `work/online-control/` | Worker, painel web, banco SQL e testes do serviço |
| `work/online-control-tooling/` | Dependências fixadas do Wrangler, sem node_modules |
| `work/*.ps1` | Build, assinatura, exportação e publicação |
| `work/*Tests.cs`, `work/game-update-tests/` | Testes selecionados, sem binários |
| `work/detection-review/` | Testes/sonda de reconhecimento por janela e processo |
| `docs/ARQUITETURA.md` | Organização dos módulos e interfaces |
| `docs/CREDENCIAIS-LOCAIS.md` | Onde os segredos ficam e o que não foi empacotado |
| `docs/AGENTS-original.md` | Histórico e instruções do workspace de origem |
| `docs/game-profile-20261030.json` | Mapa de offsets da atualização Avalon |
| `MANIFEST-SHA256.csv` | Integridade dos arquivos fornecidos, exceto o próprio manifesto |

## Compilar sem acessar o jogo ou publicar

No PowerShell, a partir desta pasta:

```powershell
& .\work\Build-Release.ps1 -Source outputs\FarmZilla-0.19.2
& .\work\Build-AdminOnline.ps1
```

Saídas: `outputs/FarmZilla-0.19.2/Bagaceira.exe` e `outputs/BagaceiraAdmin-2.0.1/BagaceiraAdmin.exe`.

O nome técnico **Bagaceira.exe** é mantido para compatibilidade de atualização; o nome público é **FarmZilla**. Não renomeie arbitrariamente o exe, os identificadores do manifesto ou as pastas de identidade/configuração.

Build usado: Windows, .NET Framework csc.exe, x86, referências Drawing/Forms/Web.Extensions/Security/Management. O script executa 38 verificações de atualizador e licença. Construir não ativa o jogo, não registra PCs nem faz deploy.

Para os testes do servidor, use um Node com suporte a `node:sqlite` (o ambiente de origem usa 24.19.0):

```powershell
node --no-warnings .\work\online-control\control.test.mjs
```

Para instalar as ferramentas do backend quando necessário, execute `npm ci` dentro de `work/online-control-tooling`. Dependência fixada: Wrangler 4.156.0. Não foi feito login nem concedido acesso novo ao criar este pacote.

## Continuidade prioritária

O Esdras não reconhecia personagens. A 0.19.2 adicionou descoberta por classe de janela, além do nome do processo. **Ainda falta o resultado no PC dele.** Obter o registro após atualizar e clicar Atualizar antes de mudar hash, offsets ou permissões. Não assumir que esse caso já está resolvido.

## Publicação e segredos

Compilar e estudar este pacote não exige segredos. Publicar atualizações para as instalações existentes exige a chave privada de assinatura original e a autorização do proprietário. Elas não estão no ZIP. Consulte `docs/CREDENCIAIS-LOCAIS.md`.

`work/Release-Online.ps1 -Source outputs\FarmZilla-NOVA-VERSAO -SkipExport` é o fluxo atual de produção, em contexto autorizado. Não executar deploy durante a primeira leitura do projeto. Uma release nova exige número novo, testes e verificação remota; o usuário testa atualizando pelo aplicativo.

Os scripts deste pacote tiveram apenas ajustes de caminhos padrão para a fonte atual e de referência do ícone do gerenciador. As fontes do cliente/gerenciador/backend correspondem às fontes existentes no workspace; a compilação do pacote foi conferida; veja docs/VERIFICACAO-DA-ENTREGA.md.

Não foram incluídos: executáveis, node_modules, .git, chaves privadas, DPAPI, credenciais de administrador/Cloudflare, cadastros de clientes, logs de usuários, dumps, executáveis do jogo ou do Bagacinha original. Faça backup separado e privado das chaves se precisar migrar a infraestrutura.

