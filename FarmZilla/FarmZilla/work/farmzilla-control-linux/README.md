# FarmZilla Control — instalação inicial no Linux

Painel administrativo MVP para cadastrar computadores, registrar estados administrativos, armazenar executáveis FarmZilla.exe e consultar SHA-256/histórico de auditoria.

**Servidor previsto:** Ubuntu 26.04 LTS, Docker Compose, RustDesk hbbs/hbbr em execução.
**Isolamento:** projeto Compose `farmzilla-control`, rede dedicada `farmzilla-backend`, PostgreSQL **sem portas publicadas** e web limitada a `127.0.0.1:8088`. Nao modificar hbbs/hbbr, UFW ou portas do SSH.

## Pré-requisitos

- Docker + Compose e acesso a imagens Docker Hub.
- `git`, `openssl` e `bash` no host.
- Disco com espaço para as imagens Docker, banco e futuras versões.
- Cliente SSH com túnel para consultar a interface, ou Nginx/HTTPS **apenas em uma segunda fase**.

## Instalar a partir desta branch (sem mexer no RustDesk)

No terminal root do servidor:

```bash
cd /opt/farmzilla-control
git clone --depth 1 --branch feat/farmzilla-control-linux-mvp https://github.com/junaojsp/PW-Automation-Suite.git app/repository
cp -a app/repository/FarmZilla/FarmZilla/work/farmzilla-control-linux/. ./
bash prepare.sh
docker compose up -d --build
docker compose ps
curl -fsS http://127.0.0.1:8088/healthz
```

O `prepare.sh` cria senhas aleatórias em `.env` (modo 600) e ajusta a pasta `releases` para o UID 1000, utilizado pelo contêiner. **Nao copie nem compartilhe o conteudo de .env.** Para recuperar as credenciais, consulte esse arquivo somente no servidor seguro. Nao repita `git clone` se o diretório `app/repository` já existir; para revisões futuras, execute `git -C app/repository pull` e copie apenas os arquivos revisados.

**Acesso via túnel SSH:** no PuTTY, Connection > SSH > Tunnels, adicione Source port `8088`, Destination `127.0.0.1:8088`, selecione Local e clique Add; reconecte ao servidor. No navegador do seu Windows abra `http://127.0.0.1:8088`. O navegador solicitará usuário e senha do `.env`.

Nao abra `8088` no UFW. Nao publique 80/443 ate haver dominio e configuracao HTTPS. HTTP Basic Auth so e aceitavel aqui por estar restrito ao loopback e trafegar pelo tunel SSH; **nao exponha a porta diretamente na internet**.

## Funcionalidades disponiveis nesta PR

- Dashboard com contagem de dispositivos cadastrados/bloqueados e versoes.
- Cadastrar PC por nome/observacao; marcar bloqueado/registrado. **Isso nao afeta o FarmZilla.exe standalone**.
- Upload manual de executaveis Windows `MZ` entre 1 KB e 32 MB, com versao unica, tamanho, hash SHA-256 e arquivo salvo no servidor. O teste `MZ` nao comprova assinatura digital nem integridade do fornecedor.
- Auditoria de cadastros, alteracoes de status e uploads.
- Acesso administrativo protegido por credenciais na configuracao local (Basic Auth).
- Health check `/healthz` para uso local.

## Limites e proxima fase

Este MVP **nao implementa** licenciamento real, ativacao por hardware, assinaturas de releases, instalacao remota nem distribui versoes aos clientes. O status bloqueado e **apenas registro administrativo**. Antes de disponibilizar o painel externamente: hostname, TLS valido, autenticacao por sessao/MFA, rate limit, backup e restore de banco/releases, politicas de retencao, escaneamento e assinatura de executaveis, gateway de distribuicao com manifestos assinados e testes de seguranca. Nenhum segredo de painel legado precisa ser migrado.

## Registro do bot no painel atual (farmzilla.jrconect.com.br)

O leitor de log sai do domínio antigo (`bagaceira-control.viacaomimo.workers.dev`) e passa a ficar em **Licenças → Ver registro**. Depois do merge na `main`:

```bash
cd /opt/farmzilla-control
git -C app/repository fetch origin main
git -C app/repository checkout main
git -C app/repository pull --ff-only origin main
cp app/repository/FarmZilla/FarmZilla/work/farmzilla-control-linux/app/server.js app/server.js
cp app/repository/FarmZilla/FarmZilla/work/farmzilla-control-linux/app/licenses.js app/licenses.js
cp app/repository/FarmZilla/FarmZilla/work/farmzilla-control-linux/app/index.html app/index.html
docker compose up -d --build web
docker compose ps
curl -fsS http://127.0.0.1:8088/healthz
```

Não rode `docker compose down`, não mexa no UFW/SSH e não reinicie hbbs/hbbr. A tabela de logs é criada na subida do serviço. Em **Licenças**, clique em **Enviar registro** e **Ver registro**. O FarmZilla 0.51.35 (ou mais novo) envia as linhas a cada minuto; o CSV da Frost continua só no PC.

## Operacao segura

```bash
cd /opt/farmzilla-control
docker compose ps
docker compose logs --tail=80 web
docker compose logs --tail=40 db
docker compose stop
```

`docker compose stop` para **somente** os servicos deste projeto Compose; nao use `docker stop $(docker ps -q)`, `docker system prune`, nem `docker compose down -v`. Isso preserva os containers RustDesk e o banco do FarmZilla.

Verificar RustDesk sem interromper: `docker ps --format 'table {{.Names}}\t{{.Status}}'`.

## Testes a validar no Ubuntu

1. `docker compose config --quiet` e `docker compose up -d --build`.
2. `curl -fsS http://127.0.0.1:8088/healthz` retorna `{"status":"ok"}`.
3. Sem credenciais, `curl -i http://127.0.0.1:8088/api/devices` retorna HTTP 401.
4. Login por tunel SSH; cadastrar PC, alternar status, fazer upload de executavel de teste e confirmar SHA-256.
5. `docker ps` mostra `hbbs` e `hbbr` intactos.
6. Verificar que as portas 8088 e 5432 nao estao publicadas ao mundo.

**Estado:** código ainda não testado no servidor. A PR deve ser revisada antes da implantação.


## HTTPS no dominio farmzilla.jrconect.com.br (PR de publicacao)

Esta versao adiciona Caddy como proxy HTTPS automatico para `farmzilla.jrconect.com.br`, publicando **somente** portas 80/443 TCP; PostgreSQL continua privado e API web continua em `127.0.0.1:8088` para diagnostico local.

### Pre-validacao obrigatoria

1. Apontar o registro DNS A de `farmzilla.jrconect.com.br` para `138.204.112.19`. Nao adicionar AAAA sem confirmar IPv6/roteamento. Se o DNS for administrado pelo Cloudflare, usar inicialmente **DNS only** ate a primeira emissao do certificado.
2. No host, conferir `getent ahostsv4 farmzilla.jrconect.com.br`. Para DNS sem proxy, o resultado deve conter `138.204.112.19`.
3. Confirmar que as portas 80/443 nao estao ocupadas com `ss -lntp '( sport = :80 or sport = :443 )'` e que `docker ps` mostra o RustDesk ativo.
4. Confirmar que os arquivos `.env` nao serao copiados para Git. O primeiro `bash prepare.sh` cria credenciais aleatorias.

### Instalacao no Ubuntu, depois de revisar PR e aprovar merge

```bash
cd /opt/farmzilla-control
if [ ! -d app/repository/.git ]; then
  git clone --depth 1 https://github.com/junaojsp/PW-Automation-Suite.git app/repository
else
  git -C app/repository pull --ff-only
fi
cp -a app/repository/FarmZilla/FarmZilla/work/farmzilla-control-linux/. ./
bash prepare.sh
docker compose config --quiet
# Abrir somente as portas necessarias ao HTTPS. Nao alterar as regras existentes do SSH/RustDesk.
ufw allow 80/tcp comment 'FarmZilla HTTPS ACME'
ufw allow 443/tcp comment 'FarmZilla HTTPS'
docker compose up -d --build
docker compose ps
curl -fsS http://127.0.0.1:8088/healthz
docker compose logs --tail=80 proxy
```

Quando o DNS estiver correto e o certificado emitido pelo Caddy, acessar **https://farmzilla.jrconect.com.br**. O navegador pedira as credenciais que estao no arquivo local `/opt/farmzilla-control/.env`; **nao cole esse arquivo em chats nem em issues**. Verificar o certificado e a identidade do site antes de inserir a senha.

Se o Caddy falhar para emitir certificado, **nao abrir HTTP administrativo sem TLS**; manter a administracao apenas via tunel SSH (`127.0.0.1:8088`) e verificar os logs. Nunca executar `docker compose down -v` ou parar os containers hbbs/hbbr.

### Limites de seguranca desta publicacao

O painel MVP usa autenticacao HTTP Basic com senha longa gerada aleatoriamente e criptografia TLS pelo Caddy. Antes de cadastrar clientes reais, implementaremos sessoes seguras, limitacao de tentativas, MFA, assinatura de arquivos, backups regulares e politicas de autorizacao. O upload de releases **armazena** o executavel, nao publica um endpoint de download nem instala atualizacoes em clientes.
