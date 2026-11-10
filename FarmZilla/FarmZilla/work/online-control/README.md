# Serviço Bagaceira — implantação atual

Publicado em 30/10/2026 na conta Cloudflare previamente autenticada:

- Worker: `bagaceira-control`
- URL: https://bagaceira-control.viacaomimo.workers.dev
- Painel: https://bagaceira-control.viacaomimo.workers.dev/admin
- D1 exclusivo: `bagaceira-licenses`, ID `25b61b44-cdfa-4b55-8043-4091c1fa5547`.
- Somente esse Worker e banco foram criados/alterados para esta tarefa. Outros projetos da conta não foram modificados.

## Modelo de autorização

O cliente calcula BGH2 a partir do hardware e BGC2 a partir desse resumo e de sua chave pública. A chave RSA do PC é DPAPI CurrentUser e fica local. O banco recebe apenas resumos, chave pública, estado e nomes que o administrador definir.

Um novo cadastro fica pendente. O administrador autoriza pelo painel ou gerenciador, autenticado com uma chave aleatória de 256 bits. O servidor guarda apenas o SHA-256 dessa credencial como segredo `ADMIN_TOKEN_SHA256`. `LICENSE_LEASE_PRIVATE_JWK` é a chave de assinatura de autorizações no cofre de segredos da Cloudflare; não está em assets, repositórios ou clientes. A chave de assinatura de atualizações permanece exclusivamente local.

O desafio de dois minutos é assinado pela chave privada do PC e consumido uma vez. Autorizações BGL2 duram 24 horas e só são renovadas para PCs liberados. O cliente consulta a cada cinco minutos e uma negativa explícita invalida o cache. Estado é conferido também antes de entregar updates, mesmo que a autorização assinada ainda esteja no prazo.

O painel web é público apenas como formulário de login; lista de PCs e alterações exigem a chave administrativa. O frontend usa textContent para os dados, não injeta nomes em HTML e não armazena credenciais em localStorage. Não há autenticação por segredo no endereço.

## Publicação de mudanças futuras

1. Criar fontes da próxima versão em uma pasta nova, mantendo a versão instalada intacta.
2. Atualizar versões em `App.cs` e `ReleaseInfo.cs` e manter o serviço HTTPS correto.
3. Executar `work/Release-Online.ps1 -Source outputs\Bagaceira-VERSAO` no contexto Windows do proprietário.
4. Esse comando compila/testa, prepara o pacote imutável assinado, copia somente os assets autorizados e implanta o Worker. Depois exige 401 anônimo e valida assinatura/hash da release usando o PC ativado do administrador.
5. Conferir o resultado remoto antes de anunciar a versão. O instalador fecha capturas pelo fluxo normal antes de substituir o executável.

`work/Provision-OnlineSecrets.ps1` só é necessário na instalação/rotação. Ele lê as chaves DPAPI locais e envia os segredos por stdin para o Wrangler. Não imprime os segredos, não grava JWK privado em arquivos de assets e não deve ser executado em logs de depuração. O acesso do Wrangler já existia nesta conta; não foi criada uma permissão OAuth nova.

## Testes

- `node --no-warnings work/online-control/control.test.mjs`: SQLite real em memória via node:sqlite, casos de permitir, bloquear, liberar novamente, replay e troca de chave.
- `work/Build-Release.ps1`: cliente e testes criptográficos/cache/atualizador.
- `work/OnlineRuntimeSmoke.exe`: teste real do PC do administrador. **Muda o estado desse PC e termina liberado**; não executar durante uso de cliente de outra pessoa.
- `work/Release-Online.ps1 -VerifyOnly`: teste remoto somente de leitura da release.

O plano gratuito tem quotas. Monitorar uso do Worker e D1; não há compromisso de uso ilimitado. A proteção não é inviolável e a tolerância offline impede bloqueio instantâneo de um PC desconectado.

Referências oficiais: https://developers.cloudflare.com/d1/worker-api/ e https://developers.cloudflare.com/workers/static-assets/binding/
