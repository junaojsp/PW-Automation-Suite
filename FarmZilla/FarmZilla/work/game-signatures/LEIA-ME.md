# Assinaturas do perfil do jogo (FarmZilla 0.50.0+)

Gera a tabela `GameSignatures.Data.cs` do cliente a partir do ElementClient.exe validado. Só lê o exe.

```powershell
node .\work\game-signatures\make-signatures.cjs "D:\Avalon-Classico-Chave-12\Avalon Clássico Ofc\element\ElementClient.exe" .\work\game-signatures\signatures.json .\outputs\FarmZilla-NOVA\GameSignatures.Data.cs
```

- Precisa do iced-x86 (`C:\Users\nyeds\Documents\Codex\2026-10-26\que\work\package`, ou variável `ICED`).
- Os alvos (nome → RVA validado) estão no topo de `make-signatures.cjs`; os nomes são os de `GameProfile` e a ordem da ponte nativa é `GameProfile.NativeOrder`.
- Depois de gerar, rodar `work/game-update-tests/GameSignatureTest.cs`.
- Quando o jogo atualizar e o perfil por assinaturas for aceito e conferido no jogo, dá para gerar a tabela nova a partir do exe novo (com os RVAs novos nos alvos) e torná-lo o hash conhecido.
