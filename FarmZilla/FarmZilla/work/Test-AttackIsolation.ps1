$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$sourcePath=Join-Path $root 'outputs\FarmZilla-0.51.20'
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testFile=Join-Path $PSScriptRoot 'AttackIsolationTests.cs'
if(-not (Test-Path -LiteralPath $compiler)){throw 'Compilador .NET Framework nao encontrado.'}
if(-not (Test-Path -LiteralPath $sourcePath -PathType Container)){throw 'Fonte 0.51.20 nao encontrada.'}
$files=@(Get-ChildItem -LiteralPath $sourcePath -Filter '*.cs' -File | ForEach-Object FullName)
if($files.Count -eq 0){throw 'Arquivos C# nao encontrados.'}
$out=Join-Path $PSScriptRoot 'AttackIsolationTests.exe'
$refs=@('/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll','/r:System.Management.dll')
$resources=@()
$resourcePath=Join-Path $sourcePath 'recursos'
if(Test-Path -LiteralPath $resourcePath -PathType Container){
 $resources=@(Get-ChildItem -LiteralPath $resourcePath -File | ForEach-Object {'/resource:'+$_.FullName+','+[IO.Path]::GetFileNameWithoutExtension($_.Name)})
}
& $compiler /nologo /target:exe /platform:x86 '/main:AttackIsolationTests' "/out:$out" $refs $resources $files $testFile
if($LASTEXITCODE -ne 0){throw 'Falha na compilacao dos testes de ataque.'}
& $out
if($LASTEXITCODE -ne 0){throw 'Falha nos testes de ataque independente.'}
Write-Host 'Testes de independencia do ataque aprovados.'
