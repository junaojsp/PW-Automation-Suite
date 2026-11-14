$ErrorActionPreference = 'Stop'
# Release build of FarmZilla The Classic: native Rota bridge (zig) -> pinned hash -> FarmZilla-Classic.exe -> offline tests.
$compiler = Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe'
if (-not (Test-Path $compiler)) { throw 'Compilador .NET Framework não encontrado.' }
$source = Join-Path $PSScriptRoot 'FarmZilla-Classic'
$output = Join-Path $PSScriptRoot 'artifacts'
$work = Join-Path $env:RUNNER_TEMP 'farmzilla-classic'
if (-not $env:RUNNER_TEMP) { $work = Join-Path ([IO.Path]::GetTempPath()) 'farmzilla-classic' }
Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
Copy-Item -Recurse $source $work
New-Item -ItemType Directory -Force $output | Out-Null

$zigHome = Join-Path $work '.zig'
python -m pip install --quiet --target $zigHome --no-deps ziglang==0.16.0
if ($LASTEXITCODE -ne 0) { throw 'Instalação do zig falhou.' }
$zig = Join-Path $zigHome 'ziglang\zig.exe'
$dll = Join-Path $work 'recursos\RotaAvalon.NativeBridge.dll'
Push-Location (Join-Path $work 'recursos')
& $zig cc -target x86-windows-gnu -shared -O2 -fno-stack-protector -o $dll (Join-Path $work 'rota-nativo\RotaNativeBridge.c') -luser32 -lkernel32
$zigExit = $LASTEXITCODE
Pop-Location
if ($zigExit -ne 0) { throw 'Compilação da ponte nativa falhou.' }
Get-ChildItem (Join-Path $work 'recursos') -Include '*.pdb','*.lib' -Recurse | Remove-Item -Force
$hash = (Get-FileHash $dll -Algorithm SHA256).Hash.ToLowerInvariant()
$integration = Join-Path $work 'RotaIntegration.cs'
$text = [IO.File]::ReadAllText($integration)
$patched = [Regex]::Replace($text, 'BridgeSha256="[0-9a-f]{64}"', 'BridgeSha256="' + $hash + '"')
if ($patched -eq $text) { throw 'Hash da ponte nativa não aplicado.' }
[IO.File]::WriteAllText($integration, $patched, (New-Object Text.UTF8Encoding($true)))

$refs = @('/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll','/r:System.Management.dll')
$resources = @(Get-ChildItem (Join-Path $work 'recursos') -File | ForEach-Object { '/resource:' + $_.FullName + ',' + [IO.Path]::GetFileNameWithoutExtension($_.Name) })
$files = @(Get-ChildItem $work -Filter '*.cs' -File | ForEach-Object FullName)
$exe = Join-Path $output 'FarmZilla-Classic.exe'
& $compiler /nologo /target:winexe /platform:x86 /langversion:latest $refs ('/win32icon:' + (Join-Path $work 'FarmZilla.ico')) "/out:$exe" $resources $files
if ($LASTEXITCODE -ne 0) { throw 'Compilação do FarmZilla-Classic.exe falhou.' }

$tests = Join-Path $work 'ClassicReleaseTests.exe'
& $compiler /nologo /target:exe /platform:x86 /langversion:latest /main:ClassicReleaseTests $refs "/out:$tests" $resources $files (Join-Path $PSScriptRoot 'ClassicReleaseTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compilação dos testes falhou.' }
& $tests
if ($LASTEXITCODE -ne 0) { throw 'Testes do FarmZilla-Classic falharam.' }

if ((Get-Item $exe).VersionInfo.FileVersion -ne '0.6.1.0') { throw 'Versão interna incorreta.' }
$exeHash = (Get-FileHash $exe -Algorithm SHA256).Hash
@("FarmZilla-Classic.exe SHA-256: $exeHash", "Ponte nativa embutida SHA-256: $hash") | Set-Content (Join-Path $output 'SHA256-Classic.txt')
Write-Host "FarmZilla-Classic.exe: $exeHash"
