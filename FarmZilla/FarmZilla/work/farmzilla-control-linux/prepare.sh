#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
if [ "$(id -u)" -ne 0 ]; then echo "Execute como root no servidor." >&2; exit 1; fi
if ! command -v docker >/dev/null 2>&1; then echo "Docker ausente." >&2; exit 1; fi
if ! docker compose version >/dev/null 2>&1; then echo "Docker Compose ausente." >&2; exit 1; fi
umask 077
mkdir -p data/postgres releases
# The API container runs as user node (UID 1000); grant ownership only to its release directory.
chown 1000:1000 releases
chmod 700 releases
if [ ! -f .env ]; then
  dbpass="$(openssl rand -hex 32)"
  adminpass="$(openssl rand -hex 24)"
  printf 'DB_PASSWORD=%s\nADMIN_USER=admin\nADMIN_PASSWORD=%s\n' "$dbpass" "$adminpass" > .env
  chmod 600 .env
  echo "Novo .env criado (senha nao exibida; consulte localmente com permissao root)."
else
  echo ".env existente preservado."
fi
docker compose config --quiet
echo "Configuração validada."
echo "Para iniciar: docker compose up -d --build"
echo "Acesso local: http://127.0.0.1:8088 (nao exposto publicamente)"
