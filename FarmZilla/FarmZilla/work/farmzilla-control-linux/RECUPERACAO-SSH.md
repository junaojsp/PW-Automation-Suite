# Recuperação SSH e deploy sem interrupção de acesso

Situação observada em 11/11/2026: após atualização do contêiner web, PuTTY perdeu acesso. O servidor Ubuntu continuou com sshd em 0.0.0.0:22, a interface ens18 recebeu pacotes SYN do IP 138.204.112.54 e o UFW foi temporariamente desabilitado para diagnóstico. Uma regra temporária `iptables -I INPUT 1 -s 138.204.112.54 -p tcp --dport 22 -j ACCEPT` permitiu recuperar o acesso. **Não foi comprovado que a recriação do contêiner tenha sido a causa direta.**

## Regras para próximas implantações

1. Não execute `ufw disable`, `iptables -F`, `docker network prune`, `docker compose down` ou reinicialize o servidor como parte do deploy web.
2. Antes de alterar contêineres: abra uma segunda sessão SSH e mantenha o console Proxmox disponível. Confirme `ss -lntp`, `ip route`, UFW e regras persistentes que autorizam o IP de administração.
3. As regras de firewall precisam continuar protegendo a máquina; desabilitar UFW é ação emergencial, não configuração de produção. Qualquer reativação deve ocorrer pelo console com plano de recuperação, após revisar as cadeias iptables/nftables, pois a falha anterior **não teve causa confirmada**.
4. Execute primeiro `stage-login-fix.sh`. Esse script apenas faz backup, atualiza arquivos e valida Compose. Não reconstrói nem reinicia os contêineres.
5. Somente durante janela controlada, no console Proxmox, execute `docker compose build web`, verifique os logs e, após confirmação do operador, `docker compose up -d --no-deps web`.
6. Após implantação: `curl -fsS http://127.0.0.1:8088/healthz`, `docker compose logs --tail=80 web`, teste SSH por um segundo terminal e confirme `hbbs`/`hbbr` via `docker ps`.

## Falha específica do Login V2

`server.js` exige `require('./auth')`. O Dockerfile original copiava apenas `server.js` e `index.html`, causando reinicialização do web por falta do `auth.js`. O Dockerfile corrigido copia os três arquivos e verifica a sintaxe JavaScript em build.

## Não fazer

- Não atualizar os contêineres do RustDesk neste processo.
- Não mexer nos volumes `data/postgres`, `data/caddy` ou no `.env`.
- Não inserir credenciais do servidor nos logs ou tickets.
