# Ponte nativa da Rota

Fonte: `RotaNativeBridge.c` (RotaAvalon 0.6.1). O binário vai embutido no Bagaceira.exe a partir de `../recursos/RotaAvalon.NativeBridge.dll`.

Recompilar (Windows, Python):

```powershell
python -m pip install --target <pasta>\zig --no-deps ziglang==0.16.0
<pasta>\zig\ziglang\zig.exe cc -target x86-windows-gnu -shared -O2 -fno-stack-protector -o RotaAvalon.NativeBridge.dll RotaNativeBridge.c -luser32 -lkernel32
```

O zig também gera `.pdb` e `.lib` ao lado da DLL: apague-os de `recursos` (tudo nessa pasta é embutido). A saída não é idêntica byte a byte entre pastas diferentes; o hash que vale é o da DLL em `recursos`.

Desde a 0.50.0 a requisição é versão 2: os endereços do jogo chegam em `Request.rva[]` (ordem `GameProfile.NativeOrder` / enum `R_*`) com `image_size`; não volte a fixar RVAs no C.

Ao trocar a DLL, atualizar `RotaPaths.BridgeSha256` em `RotaIntegration.cs` (o cliente recusa qualquer outro hash) e rodar `work/game-update-tests/RotaIntegrationTest.cs`.
