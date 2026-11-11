# FarmZilla Control - validação antes da migração do gate

## Estado
- A API `POST /api/bot/activate` vincula a chave a um único deviceHash.
- O cliente `FarmZillaControlLicense.cs` usa HTTPS e DPAPI.
- A janela `FarmZillaControlActivationDialog.cs` existe, mas **não** está ligada ao `LicenseGate.Ensure`.
- O gate antigo usa `OnlineLicense.Evaluate`, `AuthorizationSession.Begin` e permissões `controle`/`rota`; substituí-lo sem um adaptador pode impedir a abertura ou bloquear recursos.

## Testes obrigatórios antes de habilitar
1. Compilar no Windows com `Build-Release.ps1`; executar os testes existentes.
2. Emitir uma chave de teste no painel; ativar no PC A e verificar a gravação do vínculo.
3. Reabrir no PC A: não solicitar chave novamente.
4. Tentar usar a mesma chave no PC B: recusar, sem alterar o vínculo do PC A.
5. Suspender, revogar e expirar a licença: impedir novas autorizações.
6. Liberar PC pelo painel: permitir novo vínculo somente após confirmação administrativa.
7. Simular falha de rede: distinguir indisponibilidade de recusa explícita; definir tolerância offline limitada e testável.
8. Validar `AuthorizationSession.Has("controle")` e `Has("rota")` em todos os caminhos de inicialização e monitoramento.
9. Confirmar que o timer de verificação antigo não consulta o serviço legado quando o novo modo estiver habilitado.
10. Verificar que nenhum log contém a chave de licença em texto claro.

## Critérios de implantação
- O modo novo deve ser ativado inicialmente apenas no executável de teste.
- Não publicar atualização automática nem remover a autorização legada até passar nos testes.
- O painel deve mostrar versão e último acesso do PC autorizado.
- Manter SSH, firewall, RustDesk e banco existentes inalterados.
