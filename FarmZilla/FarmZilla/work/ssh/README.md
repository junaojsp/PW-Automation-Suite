# Conexão FarmZilla por GitHub Actions

VPS: `root@138.204.112.19:22`. Esta etapa configura uma chave exclusiva para diagnóstico por SSH. Não fornece um terminal interativo ao ChatGPT. O workflow só roda manualmente; não publica releases, altera licenças, reinicia serviços nem modifica RustDesk.

## Atualização para publicar executáveis

Para quem já configurou a conexão da PR #49, instale primeiro a correção do painel e a nova operação restrita. Em um checkout completo desta revisão, execute `bash FarmZilla/FarmZilla/work/ssh/install-publication.sh` como root. O instalador verifica os arquivos locais contra a revisão conhecida, faz backup e recria somente o serviço web. Se detectar edições locais desconhecidas, para sem sobrescrevê-las. A validação da aplicação usa o banco existente; nenhuma tabela de licenças é alterada. Em falha na instalação, tenta restaurar a imagem e arquivos anteriores.

O instalador exige que a autenticação SSH root já existente seja por chave. Os mesmos Secrets continuam válidos e o firewall permanece ativo.

Após a instalação, Actions → FarmZilla - Publicar versão validada aceita o ID de uma compilação Windows concluída com sucesso na revisão atual da main. Faz download do artefato oficial, valida PE x86, FileVersion, ProductVersion, SHA-256 e tamanho, informa o IP do executor e aguarda sua liberação temporária. Envia os bytes via `farmzilla-publish VERSION SHA256 BYTES`.

Na VPS, a entrada tem limite de 32 MB, hash e tamanho conferidos antes da publicação. O código fixo dentro do contêiner repete as verificações e grava release, publicação e auditoria em transação. Não aceita shell, comandos enviados pelo cliente, alteração de licença ou substituição de uma versão válida por outro build. Uma versão já publicada com o mesmo arquivo é uma repetição segura; outra versão precisa ser superior à atual. Uma falha com resultado de COMMIT incerto preserva o arquivo para conferência, evitando apagar uma publicação possivelmente confirmada.

O painel agora valida uploads e publicações, considera versões com revisão zero equivalentes, mostra cadastros inválidos e oferece Corrigir arquivo apenas para eles. A correção guarda o executável anterior em `.history`, mantém a referência de publicação e registra hashes na auditoria. Cadastros equivalentes históricos não são apagados ou renumerados automaticamente. Cadastros válidos exigem uma versão nova para qualquer alteração no executável.

A validação final de download autenticado, instalação, reinício e preservação das configurações precisa ocorrer no Windows pelo botão Update. O workflow de publicação não usa nem cria licença de teste.

## Configurar no servidor (instalação nova)

Primeiro instale a correção do painel acima; o setup recusa habilitar publicação sem o módulo validado no contêiner.

Copie `setup-farmzilla-ssh.sh`, `farmzilla-github-ssh` e `publish-stdin.cjs` para uma mesma pasta no servidor. Inspecione os arquivos e execute `bash setup-farmzilla-ssh.sh` como root.

O setup verifica a configuração SSH existente, cria uma chave Ed25519 em `/root/.farmzilla-github-ssh/id_ed25519` e adiciona a chave pública ao `authorized_keys`. As demais chaves são preservadas e o arquivo anterior é copiado para `/root/.farmzilla-github-ssh/authorized_keys.before.*.bak`. A chave criada fica limitada ao comando `/usr/local/sbin/farmzilla-github-ssh`, com `restrict`: sem terminal, encaminhamento ou execução de comandos arbitrários. Nenhum serviço é reiniciado e nenhuma regra de firewall ou opção do sshd é alterada.

Os únicos comandos aceitos são `farmzilla-check` e a operação de publicação descrita acima. A resposta informa o usuário, hostname, estado do contêiner `farmzilla-control-web-1` e existência do diretório de releases. Não imprime variáveis de ambiente nem credenciais.

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

O IP continua restrito à execução atual. A publicação recorrente sem liberação manual depende de um executor com origem de rede estável.

## Revogar

Remova exclusivamente a linha que termina com `farmzilla-github-actions` e contém esta chave em `/root/.ssh/authorized_keys`. Preserve as outras chaves. Exclua os dois Secrets no GitHub. A revogação não exige reiniciar o SSH.
