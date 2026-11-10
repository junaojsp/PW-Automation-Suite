param([string]$Source='outputs\FarmZilla-0.51.20',[switch]$RequireLicense)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$sourceCandidate=Join-Path $root $Source
if(-not (Test-Path -LiteralPath $sourceCandidate -PathType Container)){
    throw "Fonte nao encontrada: $sourceCandidate. Informe -Source outputs\FarmZilla-0.51.20 ou atualize o projeto."
}
$sourcePath=(Resolve-Path -LiteralPath $sourceCandidate).Path
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $compiler -PathType Leaf)){
    throw "Compilador .NET Framework x64 nao encontrado: $compiler"
}
$files=@(Get-ChildItem -LiteralPath $sourcePath -Filter '*.cs' -File | ForEach-Object FullName)
if($files.Count -eq 0){throw "Nenhum arquivo C# encontrado em $sourcePath"}
$icon=Join-Path $sourcePath 'Bagaceira.ico'
if(-not (Test-Path -LiteralPath $icon -PathType Leaf)){throw "Icone necessario nao encontrado: $icon"}
$refs=@('/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll','/r:System.Management.dll')
# Recursos opcionais embutidos usando nome sem extensao.
$resourcePath=Join-Path $sourcePath 'recursos'
$resources=@()
if(Test-Path -LiteralPath $resourcePath -PathType Container){
    $resources=@(Get-ChildItem -LiteralPath $resourcePath -File | ForEach-Object {'/resource:'+$_.FullName+','+[IO.Path]::GetFileNameWithoutExtension($_.Name)})
}
$defines=if($RequireLicense){@()}else{@('/define:FARMZILLA_STANDALONE')}
Write-Host "Compilando FarmZilla de: $sourcePath (modo: $(if($RequireLicense){'licenciado'}else{'independente'}))"
& $compiler /nologo /target:winexe /platform:x86 $defines $refs "/win32icon:$icon" "/out:$sourcePath\Bagaceira.exe" $resources $files
if($LASTEXITCODE -ne 0){throw 'Compilacao do cliente falhou.'}
foreach($test in @('UpdateTests','OnlineLicenseTests')){
    $testFile=Join-Path $PSScriptRoot ($test+'.cs')
    if(-not (Test-Path -LiteralPath $testFile -PathType Leaf)){throw "Teste nao encontrado: $testFile"}
    $testExe=Join-Path $PSScriptRoot ($test+'.exe')
    & $compiler /nologo /target:exe /platform:x86 $defines $refs "/main:$test" "/out:$testExe" $testFile $resources $files
    if($LASTEXITCODE -ne 0){throw "Falha compilando $test"}
    & $testExe
    if($LASTEXITCODE -ne 0){throw "Falha em $test"}
}
Get-FileHash -LiteralPath (Join-Path $sourcePath 'Bagaceira.exe') -Algorithm SHA256
