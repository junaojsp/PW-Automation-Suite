# FarmZilla Control — Login V2 (PR #16)

## O que muda

- Substitui HTTP Basic (janela do navegador) por formulário de login próprio.
- Senha administrativa armazenada como **scrypt + salt individual** no PostgreSQL.
- O `ADMIN_USER` e a `ADMIN_PASSWORD` do arquivo `.env` servem somente para criar a primeira conta na inicialização, se a tabela de contas estiver vazia. Após mudar a senha pelo painel, **o .env não sobrescreve a senha nova**.
- Sessões persistentes no PostgreSQL por 12 horas, token aleatório, cookie `__Host-fz_session` com `Secure`, `HttpOnly` e `SameSite=Strict`, verificação CSRF para ações de escrita.
- Até 10 tentativas inválidas de login por 15 minutos por instância (limitação temporária; fase seguinte: rate limit compartilhado no proxy).
- Troca de senha com confirmação da senha atual e encerramento de todas as sessões.
- Navegação lateral, painel renovado e menu Segurança.
- Tabelas `devices`, `releases` e `audit_log` existentes preservadas. Tabelas novas de autenticação são criadas pelo próprio aplicativo, sem apagar dados.

## Implantação segura — servidor Ubuntu/RustDesk

**Não execute antes do merge e do backup.** Verifique que a stack atual está saudável.

```bash
cd /opt/farmzilla-control
mkdir -p backups
docker compose exec -T db pg_dump -U farmzilla -d farmzilla > "backups/farmzilla-antes-login-$(date +%Y%m%d-%H%M%S).sql"
test -s "$(ls -t backups/farmzilla-antes-login-*.sql | head -1)" || { echo "Backup falhou"; exit 1; }
git -C app/repository pull --ff-only
cp app/repository/FarmZilla/FarmZilla/work/farmzilla-control-linux/app/server.js app/server.js
cp app/repository/FarmZilla/FarmZilla/work/farmzilla-control-linux/app/auth.js app/auth.js
cp app/repository/FarmZilla/FarmZilla/work/farmzilla-control-linux/app/licenses.js app/licenses.js
cp app/repository/FarmZilla/FarmZilla/work/farmzilla-control-linux/app/index.html app/index.html
docker compose up -d --build web
docker compose ps
curl -fsS http://127.0.0.1:8088/healthz
docker ps --format 'table {{.Names}}\t{{.Status}}'
```

Este procedimento **não recria ou reinicia o PostgreSQL, o proxy e os contêineres RustDesk de propósito**. Como garantia adicional, antes do deploy, compare `docker compose config` com a configuração em uso.

### Verificações

1. Abra **https://farmzilla.jrconect.com.br/** em janela anônima. Deve aparecer tela visual própria de login (sem popup HTTP Basic).
2. Utilize o usuário e a senha administrativa **já definidos** em `.env`. Nao envie credenciais pelo chat.
3. Dashboard deve mostrar os mesmos computadores/releases cadastrados.
4. Em **Segurança**, altere a senha. Deve retornar ao login; teste senha nova e confirme que a antiga não funciona.
5. Teste botão **Sair** e acesso a `/api/devices` sem sessão (deve retornar 401).
6. Confira que os contêineres `hbbs` e `hbbr` continuam ativos.

**Atenção:** o cookie usa `Secure` e requer HTTPS para o login funcionar no navegador; o endereço local HTTP é apenas para `/healthz`. Não configure TLS inseguro e não compartilhe a senha.

### Recuperação

Se o serviço web não inicializar, veja `docker compose logs --tail=100 web`. Não remova o banco nem edite manualmente hashes. Se necessário, faça rollback somente dos arquivos `app/server.js`, `app/auth.js` e `app/index.html` para o commit anterior e execute `docker compose up -d --build web`. As tabelas novas podem permanecer sem afetar os dados anteriores.

## Limitações conhecidas

A autenticação não substitui MFA e limitação de tentativas persistente no proxy. Antes de dar acesso a terceiros, adicionar 2FA, controle de papéis e rate limit distribuído. O cadastro/bloqueio de computadores segue administrativo; o FarmZilla standalone não passa a exigir licença nesta PR.
