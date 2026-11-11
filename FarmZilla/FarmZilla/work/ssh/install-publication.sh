#!/bin/bash
# One-time deployment of validated release handling; no database reset or global restart.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
[[ $(id -u) == 0 ]] || { echo 'Execute como root' >&2; exit 1; }
root=/opt/farmzilla-control
here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
source_app="$here/../farmzilla-control-linux/app"
[[ -f $source_app/release-store.js && -f $here/publish-stdin.cjs ]] || { echo 'Execute a partir de um checkout completo desta revisão' >&2; exit 1; }
project=$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' farmzilla-control-web-1)
[[ $project == farmzilla-control ]] || { echo 'Projeto Docker diferente do esperado' >&2; exit 1; }
compose=''
for candidate in compose.yaml compose.yml docker-compose.yml docker-compose.yaml; do
 if [[ -f $root/$candidate ]]; then compose="$root/$candidate";break;fi
done
[[ -n $compose ]] || { echo 'Compose do FarmZilla não localizado' >&2; exit 1; }
# Refuse to overwrite unrelated local edits; accepted baseline hashes are recorded below.
python3 - "$source_app" "$root/app" <<'PY'
from pathlib import Path
import hashlib,sys
source,dest=map(Path,sys.argv[1:])
baselines={'server.js': 'ab6a9884494dd323459994553f360d4dd1265bac923c4edd9405f4dc8ceee69c', 'index.html': '684820f0ea53217407b4666bd0e927e985bdcdf01c4d8b152be8dccbf76fa351', 'Dockerfile': '48b38335625baaba09178e8cc3e1c3f360b5eca8be3dc94c61cb22c7101027a5'}
for name in ['server.js','index.html','Dockerfile','pe-version.js','release-store.js']:
 p=dest/name
 if p.is_symlink():raise SystemExit('Link simbólico inesperado: '+str(p))
 if not p.exists():
  if name in baselines:raise SystemExit('Arquivo obrigatório ausente: '+str(p))
  continue
 got=hashlib.sha256(p.read_bytes()).hexdigest()
 new=hashlib.sha256((source/name).read_bytes()).hexdigest()
 if got not in [new,baselines.get(name)]:raise SystemExit('Arquivo local diferente da revisão conhecida: '+str(p)+'. Compare antes de instalar.')
print('Arquivos locais conferidos; configurações e licenças não serão copiadas.')
PY
old_image=$(docker inspect --format '{{.Image}}' farmzilla-control-web-1)
old_ref=$(docker inspect --format '{{.Config.Image}}' farmzilla-control-web-1)
backup=$(mktemp -d "$root/publication-backup-XXXXXXXX")
chmod 700 "$backup"
files=(server.js index.html Dockerfile pe-version.js release-store.js)
for name in "${files[@]}"; do
 if [[ -f $root/app/$name ]]; then cp -p -- "$root/app/$name" "$backup/$name"; fi
done
rollback(){
 trap - ERR
 echo "Instalação falhou. Restaurando arquivos e imagem anterior. Backup: $backup" >&2
 for name in "${files[@]}"; do
  if [[ -f $backup/$name ]]; then cp -p -- "$backup/$name" "$root/app/$name";else rm -f -- "$root/app/$name";fi
 done
 docker tag "$old_image" "$old_ref"
 docker compose --project-directory "$root" -f "$compose" up -d --no-deps web
}
trap rollback ERR
for name in "${files[@]}"; do install -m 644 "$source_app/$name" "$root/app/$name"; done
docker compose --project-directory "$root" -f "$compose" build web
docker compose --project-directory "$root" -f "$compose" up -d --no-deps web
healthy=0
for attempt in {1..15}; do
 if docker exec farmzilla-control-web-1 node -e "require('/srv/app/release-store');fetch('http://127.0.0.1:3000/healthz',{signal:AbortSignal.timeout(3000)}).then(async r=>{if(!r.ok||(await r.json()).status!=='ok')process.exit(1)}).catch(()=>process.exit(1))"; then healthy=1;break;fi
 sleep 2
done
[[ $healthy == 1 ]]
bash "$here/setup-farmzilla-ssh.sh"
trap - ERR
printf 'Publicação instalada. Somente o serviço web do FarmZilla foi recriado. Backup: %s\n' "$backup"
printf 'Os Secrets existentes continuam válidos. Nenhum executável foi publicado por este instalador.\n'
