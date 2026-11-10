# Painel FarmZilla — arquitetura e fases

## Decisoes de produto

- O aplicativo distribuido passara a se chamar **FarmZilla.exe**.
- Remover o atualizador legado do **modo standalone**: nenhuma consulta de versao em segundo plano, nenhum download automatico e nenhum instalador legado na inicializacao. O botao **Update** da interface antiga passa a **Config.**.
- **Nao trocar nem remover a seguranca do binario/rotas**: assinaturas de atualizacoes futuras e SHA da ponte nativa devem permanecer.
- **A versao standalone nao depende de licenca**. O painel pode catalogar computadores e releases, mas nao pode fingir bloquear remotamente uma versao standalone ja distribuida. Para uma oferta comercial licenciada, fazer build gerenciada separada e explicita, apos aprovar a politica de ativacao.
- Nao migrar silenciosamente os usuarios nem alterar o servico legado.

## O que ja existe

A pasta `work/online-control` possui painel web, API de dispositivos, autenticacao por chave administrativa/PIN e banco Cloudflare D1, alem de um fluxo de publicacao de update assinado. Isso e **referencia**, nao o painel novo. A versao antiga esta acoplada aos formatos de licenca/manifesto e ao nome `Bagaceira.exe`.

## Painel novo: escopo proposto

### 1. Dashboard

Versao mais recente, releases publicadas, computadores registrados, computadores ativos (ultima conexao real), erros de publicacao e atividade. Nunca mostrar "online" sem um ping/heartbeat verificado.

### 2. Computadores e licencas

Cadastro/identificacao e nome amigavel por PC; estado pendente/ativo/bloqueado; funcionalidades Espelho e Mat Farm; trilha de auditoria de quem modificou permissoes. **Estes estados so restringem uso em builds gerenciadas**, nao na build standalone.

### 3. Atualizacoes

Upload de `FarmZilla.exe` x86 e versao, notas, canal `teste`/`producao`, SHA-256, assinatura digital e historico. Publicacao precisa de confirmacao administrativa e rollback para manifesto anterior. Cliente consulta **somente por acao explicita** do usuario ou pela futura politica opt-in, sem instalar enquanto houver cliente de jogo/rota em execucao.

### 4. Seguranca

Administracao autenticada com sessao curta, 2FA se disponivel, autorizacao por funcao, rate limit e logs de auditoria. Segredo da assinatura fora do banco, repositorio, assets e cliente. Manifesto HTTPS assinado; hash SHA-256 antes de substituir arquivo; instalacao atomica com rollback; impedir caminho de arquivo arbitrario.

## Proxima implementacao (PR independente)

1. Criar novo Worker/API e banco de dados **sem reutilizar os segredos do servico anterior**. Rotas /v1/admin/releases, /v1/admin/devices, /v1/client/releases/latest, com testes de autenticacao e validacao de manifesto.
2. Criar o frontend do painel com Dashboard, Computadores/Licencas, Versoes e Auditoria.
3. Criar uploader de release com assinatura e caminho oficial `FarmZilla.exe` e separacao teste/producao.
4. Introduzir integracao do cliente apenas apos definir politica standalone versus gerenciada e testar segundo PC.

## Criterios de aceite desta fase 1

- `Build-Release.ps1` gera `FarmZilla.exe` em standalone; `-RequireLicense` preserva `Bagaceira.exe` legado.
- Modo standalone nao chama `LicenseGate.Ensure`, nem `InitUpdates` ou `AppUpdate.RunInstaller`.
- Botao antes chamado Update passa a Config. e nao dispara chamadas ao servidor legado.
- Funcoes de Espelho, Mat Farm e DLL nativa permanecem sem mudancas.
- Testar compilacao em Windows, testes existentes, e abertura em segundo computador.

**Estado:** apenas fase 1 implementada nesta PR. Nenhuma API ou painel novo foi publicado.
