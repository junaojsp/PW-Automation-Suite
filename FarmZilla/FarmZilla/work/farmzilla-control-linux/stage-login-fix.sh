#!/usr/bin/env bash
# Stage-only recovery for the FarmZilla Control Login V2.
# This deliberately NEVER changes UFW/iptables/nftables, SSH, networking,
# Docker container state, or restarts any service.
set -euo pipefail
BASE=/opt/farmzilla-control
cd "$BASE"
test -f .env || { echo "ERRO: configuracao .env ausente"; exit 1; }
test -d app/repository/.git || { echo "ERRO: repositorio clonado ausente"; exit 1; }
test -f compose.yaml || { echo "ERRO: compose.yaml ausente"; exit 1; }

echo "=== PREFLIGHT SSH (somente leitura) ==="
ss -lntp
echo "=== CONTAINERS ATUAIS (somente leitura) ==="
docker ps --format 'table {{.Names}}\t{{.Status}}'
echo "=== BACKUP DOS ARQUIVOS WEB ==="
umask 077
stamp="$(date +%Y%m%d-%H%M%S)"
backup="$BASE/backups/login-v2-stage-$stamp"
mkdir -p "$backup"
for file in server.js auth.js index.html Dockerfile; do
  if [ -f "app/$file" ]; then cp -p "app/$file" "$backup/$file"; fi
done
echo "Backup: $backup"

echo "=== ATUALIZANDO FONTES GIT (NAO REINICIA SERVICOS) ==="
git -C app/repository pull --ff-only
src=app/repository/FarmZilla/FarmZilla/work/farmzilla-control-linux/app
for file in server.js auth.js index.html Dockerfile; do
  test -s "$src/$file" || { echo "ERRO: arquivo ausente: $src/$file"; exit 1; }
done
cp "$src/server.js" "$src/auth.js" "$src/index.html" "$src/Dockerfile" app/
docker compose config --quiet

echo "Arquivos preparados. Nenhum container foi alterado."
echo "NAO execute 'docker compose up' pelo PuTTY ate validar SSH e rede."
echo "A etapa de substituicao da imagem deve ser executada no console Proxmox."
