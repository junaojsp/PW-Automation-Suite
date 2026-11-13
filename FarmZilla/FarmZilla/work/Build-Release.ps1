param([string]$Source='outputs\FarmZilla-0.51.20')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$sourceCandidate=Join-Path $root $Source
if(-not (Test-Path -LiteralPath $sourceCandidate -PathType Container)){
    throw "Fonte nao encontrada: $sourceCandidate. Informe -Source outputs\FarmZilla-0.51.20 ou atualize o projeto."
}
$sourcePath=(Resolve-Path -LiteralPath $sourceCandidate).Path
$compiler=Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe'
if(-not (Test-Path -LiteralPath $compiler -PathType Leaf)){
    throw "Compilador .NET Framework x64 nao encontrado: $compiler"
}
$files=@(Get-ChildItem -LiteralPath $sourcePath -Filter '*.cs' -File | ForEach-Object FullName)
if($files.Count -eq 0){throw "Nenhum arquivo C# encontrado em $sourcePath"}
$icon=Join-Path $sourcePath 'FarmZilla.ico'
if(-not (Test-Path -LiteralPath $icon -PathType Leaf)){throw "Icone necessario nao encontrado: $icon"}
$refs=@('/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll','/r:System.Management.dll')
# Recursos opcionais embutidos usando nome sem extensao.
$resourcePath=Join-Path $sourcePath 'recursos'
$resources=@()
if(Test-Path -LiteralPath $resourcePath -PathType Container){
    $resources=@(Get-ChildItem -LiteralPath $resourcePath -File | ForEach-Object {'/resource:'+$_.FullName+','+[IO.Path]::GetFileNameWithoutExtension($_.Name)})
}
Write-Host "Compilando FarmZilla de: $sourcePath"
$stage=Join-Path $sourcePath 'FarmZilla.build.exe'
try {
    & $compiler /nologo /target:winexe /platform:x86 $refs "/win32icon:$icon" "/out:$stage" $resources $files
    if($LASTEXITCODE -ne 0){throw 'Compilacao do cliente falhou.'}
    foreach($test in @('UpdateTests','OnlineLicenseTests','FrostTests')){
        $testFile=Join-Path $PSScriptRoot ($test+'.cs')
        if(-not (Test-Path -LiteralPath $testFile -PathType Leaf)){throw "Teste nao encontrado: $testFile"}
        $testExe=Join-Path $PSScriptRoot ($test+'.exe')
        & $compiler /nologo /target:exe /platform:x86 $refs "/main:$test" "/out:$testExe" $testFile $resources $files
        if($LASTEXITCODE -ne 0){throw "Falha compilando $test"}
        & $testExe
        if($LASTEXITCODE -ne 0){throw "Falha em $test"}
    }

    $gate=Get-Content -LiteralPath (Join-Path $sourcePath 'LicenseGate.cs') -Raw
    $dialog=Get-Content -LiteralPath (Join-Path $sourcePath 'FarmZillaControlActivationDialog.cs') -Raw
    if($gate -notmatch 'FARMZILLA_LEGACY_LICENSE' -or $gate -notmatch 'FarmZillaControlActivationDialog.Ensure' -or $dialog -notmatch 'DeviceHash\(identity\)' -or $dialog -notmatch 'Copiar'){
        throw 'Novo licenciamento ausente da fonte. Publicacao cancelada.'
    }
    $target=Join-Path $sourcePath 'FarmZilla.exe'
    Copy-Item -LiteralPath $stage -Destination $target -Force
    Write-Host 'OK: FarmZilla.exe gerado. Bagaceira.exe nao sera distribuido.'
    Get-FileHash -LiteralPath $target -Algorithm SHA256
}
finally {
    Remove-Item -LiteralPath $stage -Force -ErrorAction SilentlyContinue
}
