$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe'
if (-not (Test-Path $compiler)) { throw 'Compilador .NET Framework não encontrado.' }
$shared = Join-Path $PSScriptRoot '../FarmZilla/outputs/FarmZilla-0.51.20'
$sources = @(
  (Join-Path $PSScriptRoot 'Diagnostic.cs'),
  (Join-Path $PSScriptRoot 'CharacterReader.cs'),
  (Join-Path $shared 'GameSignatures.cs'),
  (Join-Path $shared 'GameSignatures.Data.cs')
)
$output = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Force $output | Out-Null
$refs = @('/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll')
$exe = Join-Path $output 'FarmZilla-TheClassic-Diagnostico.exe'
& $compiler /nologo /target:winexe /platform:x86 /main:FarmZilla.TheClassic.Program $refs "/out:$exe" $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilação do diagnóstico falhou.' }
$tests = Join-Path $output 'DiagnosticTests.exe'
& $compiler /nologo /target:exe /platform:x86 /main:DiagnosticTests $refs "/out:$tests" (Join-Path $PSScriptRoot 'DiagnosticTests.cs') (Join-Path $PSScriptRoot 'CharacterReaderTests.cs') $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilação dos testes falhou.' }
& $tests
if ($LASTEXITCODE -ne 0) { throw 'Testes do diagnóstico falharam.' }
if ((Get-Item $exe).VersionInfo.FileVersion -ne '0.4.0.0') { throw 'Versão interna incorreta.' }
Get-FileHash $exe -Algorithm SHA256 | Format-List | Out-String | Set-Content (Join-Path $output 'SHA256.txt')

# Compile the command-capable test app from the SAME InternalBridge used by Avalon.
$followSources = $sources + @(
  (Join-Path $PSScriptRoot 'FollowProfile.cs'),
  (Join-Path $PSScriptRoot 'ClassicFollow.cs'),
  (Join-Path $PSScriptRoot 'ClassicMirrorLayout.cs'),
  (Join-Path $shared 'RubiPalette.cs'),
  (Join-Path $shared 'FarmZillaFrame.cs'),
  (Join-Path $shared 'SideItem.cs'),
  (Join-Path $shared 'Theme.cs'),
  (Join-Path $shared 'UiScale.cs'),
  (Join-Path $shared 'ClassIcons.cs'),
  (Join-Path $shared 'Bridge.cs'),
  (Join-Path $shared 'ProcessGate.cs')
)
$iconArg = '/win32icon:' + (Join-Path $shared 'FarmZilla.ico')
$mirror = Join-Path $output 'FarmZilla-TheClassic-Teste.exe'
& $compiler /nologo /target:winexe /platform:x86 /main:FarmZilla.TheClassic.ClassicMirrorProgram $iconArg $refs "/out:$mirror" $followSources
if ($LASTEXITCODE -ne 0) { throw 'Compilação do Espelho de teste falhou.' }
$followTests = Join-Path $output 'ClassicFollowTests.exe'
& $compiler /nologo /target:exe /platform:x86 /main:ClassicFollowTests $iconArg $refs "/out:$followTests" (Join-Path $PSScriptRoot 'ClassicFollowTests.cs') $followSources
if ($LASTEXITCODE -ne 0) { throw 'Compilação dos testes de seguir falhou.' }
& $followTests
if ($LASTEXITCODE -ne 0) { throw 'Testes de seguir falharam.' }
if ((Get-Item $mirror).VersionInfo.FileVersion -ne '0.4.0.0') { throw 'Versão do Espelho incorreta.' }
Get-FileHash $mirror -Algorithm SHA256 | Format-List | Out-String | Add-Content (Join-Path $output 'SHA256.txt')
