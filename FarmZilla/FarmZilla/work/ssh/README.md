# Conexão FarmZilla por GitHub Actions

VPS: `root@138.204.112.19:22`. Esta etapa configura uma chave exclusiva para diagnóstico por SSH. Não fornece um terminal interativo ao ChatGPT. O workflow só roda manualmente; não publica releases, altera licenças, reinicia serviços nem modifica RustDesk.

## Configurar no servidor

Copie `setup-farmzilla-ssh.sh` e `farmzilla-github-ssh` para uma mesma pasta no servidor. Inspecione os arquivos e execute `bash setup-farmzilla-ssh.sh` como root.

O setup verifica a configuração SSH existente, cria uma chave Ed25519 em `/root/.farmzilla-github-ssh/id_ed25519` e adiciona a chave pública ao `authorized_keys`. As demais chaves são preservadas e o arquivo anterior é copiado para `/root/.farmzilla-github-ssh/authorized_keys.before.*.bak`. A chave criada fica limitada ao comando `/usr/local/sbin/farmzilla-github-ssh`, com `restrict`: sem terminal, encaminhamento ou execução de comandos arbitrários. Nenhum serviço é reiniciado e nenhuma regra de firewall ou opção do sshd é alterada.

O único comando aceito é `farmzilla-check`. A resposta informa o usuário, hostname, estado do contêiner `farmzilla-control-web-1` e existência do diretório de releases. Não imprime variáveis de ambiente nem credenciais.

Se autenticação root por chave não estiver habilitada ou a configuração usar outro arquivo de chaves, o setup para e informa o motivo. Regras `Match` e firewall ainda podem impedir o acesso: nesse caso, analisar a falha antes de qualquer ajuste de acesso.

## Configurar os Secrets do repositório

Em Settings → Secrets and variables → Actions, crie:

| Secret | Conteúdo |
| --- | --- |
| `FARMZILLA_SSH_PRIVATE_KEY` | Arquivo `/root/.farmzilla-github-ssh/id_ed25519` completo, com BEGIN e END |
| `FARMZILLA_SSH_KNOWN_HOSTS` | Arquivo `/root/.farmzilla-github-ssh/known_hosts` completo |

Transfira os conteúdos diretamente do seu terminal/WinSCP para os Secrets; não cole a chave no chat, em issues, arquivos do repositório ou logs. O setup não imprime a chave privada. O known_hosts é gerado da chave pública local do próprio servidor; o cliente exige essa identidade com StrictHostKeyChecking.

## Testar

Após mesclar a PR, execute Actions → FarmZilla - Testar conexão SSH → Run workflow, usando a branch main. Somente considerar conectada depois que esse teste retornar sucesso. O executor temporário remove seus arquivos de chave ao concluir.

Esta entrega ainda não automatiza publicação. Após validar a conexão, o comando restrito pode receber uma operação específica de publicação com validação da versão PE, hash, backup e auditoria, sem liberar shell genérico.

## Revogar

Remova exclusivamente a linha que termina com `farmzilla-github-actions` e contém esta chave em `/root/.ssh/authorized_keys`. Preserve as outras chaves. Exclua os dois Secrets no GitHub. A revogação não exige reiniciar o SSH.
