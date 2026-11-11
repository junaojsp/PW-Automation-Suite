#!/bin/bash
# Adds a dedicated restricted key. Does not change sshd, Docker or RustDesk.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
[[ $(id -u) == 0 ]] || { echo 'Execute como root' >&2; exit 1; }
source_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
runner="$source_dir/farmzilla-github-ssh"
[[ -f $runner ]] || { echo 'Arquivo farmzilla-github-ssh ausente ao lado do setup' >&2; exit 1; }
bash -n "$runner"
for program in ssh-keygen sshd install flock; do command -v "$program" >/dev/null; done
sshd -t
ssh_config=$(sshd -T)
grep -Eq '^permitrootlogin (yes|prohibit-password|without-password|forced-commands-only)$' <<< "$ssh_config" || { echo 'sshd não permite autenticação root por chave; configuração não foi alterada' >&2; exit 1; }
grep -qx 'pubkeyauthentication yes' <<< "$ssh_config" || { echo 'Autenticação por chave desativada; configuração não foi alterada' >&2; exit 1; }
grep -Eq '^authorizedkeysfile (.* )?(\.ssh/authorized_keys|/root/\.ssh/authorized_keys)( |$)' <<< "$ssh_config" || { echo 'AuthorizedKeysFile não usa /root/.ssh/authorized_keys; verifique a configuração' >&2; exit 1; }
grep -qx 'hostkey /etc/ssh/ssh_host_ed25519_key' <<< "$ssh_config" || { echo 'sshd usa outra chave de host; configuração não foi alterada' >&2; exit 1; }
[[ -f /etc/ssh/ssh_host_ed25519_key.pub ]] || { echo 'Chave pública Ed25519 do servidor ausente' >&2; exit 1; }
for target in /root/.ssh /root/.ssh/authorized_keys /root/.farmzilla-github-ssh /usr/local/sbin/farmzilla-github-ssh; do
 [[ ! -L $target ]] || { echo "Caminho simbólico inesperado: $target" >&2; exit 1; }
done
install -d -m 700 /root/.ssh /root/.farmzilla-github-ssh
exec 9>/root/.farmzilla-github-ssh/setup.lock
flock -n 9 || { echo 'Outro setup está em execução' >&2; exit 1; }
key=/root/.farmzilla-github-ssh/id_ed25519
if [[ ! -f $key ]]; then
 ssh-keygen -q -t ed25519 -N '' -C farmzilla-github-actions -f "$key"
fi
chmod 600 "$key"
public_key=$(ssh-keygen -y -f "$key")
if [[ -e /usr/local/sbin/farmzilla-github-ssh ]] && ! grep -q '^# FARMZILLA_GITHUB_SSH_V1' /usr/local/sbin/farmzilla-github-ssh; then
 echo 'Já existe outro comando no destino; nada será sobrescrito' >&2; exit 1
fi
install -m 700 "$runner" /usr/local/sbin/farmzilla-github-ssh
authorized=/root/.ssh/authorized_keys
if [[ -f $authorized ]]; then
 cp -p -- "$authorized" "/root/.farmzilla-github-ssh/authorized_keys.before.$(date +%Y%m%d%H%M%S).bak"
else
 install -m 600 /dev/null "$authorized"
fi
entry="restrict,command=\"/usr/local/sbin/farmzilla-github-ssh\" $public_key farmzilla-github-actions"
# Refuse an existing occurrence with different options; never leave a broad copy.
key_body=$(awk '{print $2}' <<< "$public_key")
if grep -Fq "$key_body" "$authorized"; then
 grep -Fxq "$entry" "$authorized" || { echo 'Chave já cadastrada com opções diferentes; revise antes de continuar' >&2; exit 1; }
else
 printf '\n%s\n' "$entry" >> "$authorized"
fi
chmod 600 "$authorized"
awk '{print "138.204.112.19 " $1 " " $2}' /etc/ssh/ssh_host_ed25519_key.pub > /root/.farmzilla-github-ssh/known_hosts
chmod 600 /root/.farmzilla-github-ssh/known_hosts
printf '\nConfiguração concluída. Nenhum serviço foi reiniciado.\n'
printf 'Secret FARMZILLA_SSH_PRIVATE_KEY: conteúdo de %s\n' "$key"
printf 'Secret FARMZILLA_SSH_KNOWN_HOSTS: conteúdo de /root/.farmzilla-github-ssh/known_hosts\n'
printf 'Não envie o conteúdo da chave privada pelo chat.\n'
