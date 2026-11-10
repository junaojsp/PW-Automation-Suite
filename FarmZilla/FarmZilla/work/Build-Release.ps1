param([string]$Source='outputs\FarmZilla-0.19.2')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$sourcePath=(Resolve-Path -LiteralPath (Join-Path $root $Source)).Path
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$files=@(Get-ChildItem -LiteralPath $sourcePath -Filter '*.cs'|ForEach-Object FullName)
$refs=@('/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll','/r:System.Management.dll')
# Optional recursos folder (0.20.0+): each file is embedded under its name without extension (e.g. RotaAvalon.Catalogo.json -> RotaAvalon.Catalogo).
$resourcePath=Join-Path $sourcePath 'recursos'
$resources=@()
if(Test-Path -LiteralPath $resourcePath){$resources=@(Get-ChildItem -LiteralPath $resourcePath -File|ForEach-Object{'/resource:'+$_.FullName+','+[IO.Path]::GetFileNameWithoutExtension($_.Name)})}
& $compiler /nologo /target:winexe /platform:x86 $refs "/win32icon:$sourcePath\Bagaceira.ico" "/out:$sourcePath\Bagaceira.exe" $resources $files
if($LASTEXITCODE -ne 0){throw 'Compilação falhou.'}
foreach($test in @('UpdateTests','OnlineLicenseTests')){
 & $compiler /nologo /target:exe /platform:x86 $refs "/main:$test" "/out:$PSScriptRoot\$test.exe" "$PSScriptRoot\$test.cs" $resources $files
 if($LASTEXITCODE -ne 0){throw "Falha compilando $test"}
 & "$PSScriptRoot\$test.exe"
 if($LASTEXITCODE -ne 0){throw "Falha em $test"}
}
Get-FileHash -LiteralPath "$sourcePath\Bagaceira.exe" -Algorithm SHA256

