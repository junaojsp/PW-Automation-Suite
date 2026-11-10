# FarmZilla sem ativacao por PC (build standalone)

A compilacao normal por `work/Build-Release.ps1` agora define `FARMZILLA_STANDALONE`.

Neste modo:
- O aplicativo nao consulta a ativacao do dispositivo ao iniciar.
- Nao faz revalidacao online a cada 30 minutos.
- Espelho e Mat Farm ficam liberados para todos os PCs compativeis.
- O envio de logs ao painel de licencas nao inicia.
- A verificacao automatica de atualizacoes antigas nao inicia. O botao de atualizacao manual continua existindo; **nao instale uma atualizacao que exija licenca** em cima de uma build standalone.
- As rotinas de jogo (ataque, seguir, teleporte, coleta), os testes de licenca, o validador de atualizacoes assinadas e a verificacao de hash da ponte de coleta nao sao modificados.

Para testar no Windows:

```powershell
cd "$env:USERPROFILE\Downloads\PW-Automation-Suite\FarmZilla\FarmZilla"
& ".\work\Build-Release.ps1"
```

A build antiga com controle de ativacao pode ser produzida explicitamente com:

```powershell
& ".\work\Build-Release.ps1" -RequireLicense
```

Ambas as modalidades mantem o nome `Bagaceira.exe` e usam o mesmo destino; gere-as em pastas separadas se precisar guardar as duas. A build standalone e recomendada apenas para distribuicao autorizada pelo mantenedor do projeto.

**Dependencias de distribuicao:** `Bagaceira.exe` precisa ser compilado com todos os recursos embutidos. Se `recursos/RotaAvalon.NativeBridge.dll` nao estiver no checkout, a compilacao da ponte requer `work/Build-CoqueNative.ps1` na branch que contem esse script. Como a correcao do Coque ainda depende de incorporar a DLL gerada, nao presuma que um checkout limpo contenha essa DLL.

Validacao recomendada antes do merge: compilar ambas as variantes, executar os 38 testes, abrir a variante standalone num segundo PC sem arquivos de licenca, confirmar Espelho/Mat Farm, sem chamadas ao endpoint de ativacao, e verificar que o sistema de update nao substitui automaticamente a build por uma versao licenciada.
