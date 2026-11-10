param([switch]$SkipIntegrationTest)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Join-Path $root 'outputs\FarmZilla-0.51.20'
$native=Join-Path $source 'rota-nativo\RotaNativeBridge.c'
$resource=Join-Path $source 'recursos\RotaAvalon.NativeBridge.dll'
$integration=Join-Path $source 'RotaIntegration.cs'
$build=Join-Path $PSScriptRoot 'Build-Release.ps1'
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
foreach($path in @($native,$resource,$integration,$build)){
 if(-not (Test-Path -LiteralPath $path -PathType Leaf)){throw "Arquivo ausente: $path"}
}
$zig=(Get-Command zig -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
if(-not $zig){
 $installed=Join-Path $root '.tools\zig\ziglang\zig.exe'
 if(Test-Path -LiteralPath $installed -PathType Leaf){$zig=$installed}
}
if(-not $zig){
 $target=Join-Path $root '.tools\zig'
 Write-Host 'Instalando compilador Zig local (sem modificar PATH global)...'
 python -m pip install --target "$target" --no-deps 'ziglang==0.16.0'
 if($LASTEXITCODE -ne 0){throw 'Falha instalando Zig. Verifique conexao ou instale Zig 0.16.0.'}
 $zig=Join-Path $target 'ziglang\zig.exe'
}
if(-not (Test-Path -LiteralPath $zig -PathType Leaf)){throw "Zig nao encontrado: $zig"}
$work=Join-Path $env:TEMP ('farmzilla-coque-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$newDll=Join-Path $work 'RotaAvalon.NativeBridge.dll'
$oldDll=[IO.File]::ReadAllBytes($resource)
$oldText=[IO.File]::ReadAllText($integration)
$changed=$false
try{
 Write-Host 'Compilando ponte nativa x86...'
 & $zig cc -target x86-windows-gnu -shared -O2 -fno-stack-protector -o $newDll $native -luser32 -lkernel32
 if($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $newDll -PathType Leaf)){throw 'Compilacao nativa falhou.'}
 $newHash=(Get-FileHash -LiteralPath $newDll -Algorithm SHA256).Hash.ToLowerInvariant()
 if([IO.FileInfo]$dllInfo=(Get-Item -LiteralPath $newDll)){if($dllInfo.Length -lt 1000){throw 'DLL inesperadamente pequena.'}}
 $pattern='BridgeSha256="[0-9a-fA-F]{64}"'
 if([regex]::Matches($oldText,$pattern).Count -ne 1){throw 'Constante SHA-256 da ponte ausente ou duplicada; nada foi alterado.'}
 $newText=[regex]::Replace($oldText,$pattern,('BridgeSha256="'+$newHash+'"'))
 [IO.File]::Copy($newDll,$resource,$true)
 [IO.File]::WriteAllText($integration,$newText,[Text.UTF8Encoding]::new($false))
 $changed=$true
 Write-Host "Ponte compilada, embutida e SHA-256 atualizado: $newHash"
 Write-Host 'Compilando FarmZilla e executando testes de release...'
 & $build
 if($LASTEXITCODE -ne 0){throw 'Build-Release falhou.'}
 if(-not $SkipIntegrationTest){
  Write-Host 'Compilando teste integrado da ponte...'
  if(-not (Test-Path -LiteralPath $compiler)){throw 'Compilador C# .NET Framework nao encontrado.'}
  $files=@(Get-ChildItem -LiteralPath $source -Filter '*.cs' -File | ForEach-Object FullName)
  $resources=@(Get-ChildItem -LiteralPath (Join-Path $source 'recursos') -File | ForEach-Object {'/resource:'+$_.FullName+','+[IO.Path]::GetFileNameWithoutExtension($_.Name)})
  $test=Join-Path $root 'work\game-update-tests\RotaIntegrationTest.cs'
  if(-not (Test-Path -LiteralPath $test)){throw 'RotaIntegrationTest.cs nao encontrado.'}
  $testExe=Join-Path $work 'RotaIntegrationTest.exe'
  & $compiler /nologo /target:winexe /platform:x86 '/main:RotaIntegrationTest' '/r:System.Drawing.dll' '/r:System.Windows.Forms.dll' '/r:System.Web.Extensions.dll' '/r:System.Security.dll' '/r:System.Management.dll' "/out:$testExe" $resources $files $test
  if($LASTEXITCODE -ne 0){throw 'Compilacao do teste de integracao falhou.'}
  & $testExe
  if($LASTEXITCODE -ne 0){throw 'Teste de integracao falhou.'}
 }
 Write-Host 'PASS: ponte nativa do Coque, compilacao e verificacoes concluidas.'
 Write-Host "DLL: $resource"
 Write-Host "SHA256: $newHash"
 Write-Host 'Valide em jogo: Coque = primeira opcao; outros materiais = segunda opcao.'
} catch {
 if($changed){
  [IO.File]::WriteAllBytes($resource,$oldDll)
  [IO.File]::WriteAllText($integration,$oldText,[Text.UTF8Encoding]::new($false))
  Write-Warning 'DLL e hash restaurados aos valores anteriores. O exe compilado deve ser reconstruido antes de usar.'
 }
 throw
} finally {
 Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
