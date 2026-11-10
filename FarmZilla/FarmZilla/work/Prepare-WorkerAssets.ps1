param([string]$DataRoot = "$PSScriptRoot\online-control\release-data")
$ErrorActionPreference='Stop'
$pointer=Get-Content -LiteralPath (Join-Path $DataRoot 'current.json') -Raw|ConvertFrom-Json
if($pointer.tag -notmatch '^v\d+\.\d+\.\d+$'){throw 'Versão inválida.'}
$release=Join-Path $DataRoot "releases\$($pointer.tag)"
$target=Join-Path $PSScriptRoot "online-control\assets\releases\$($pointer.tag)"
if((Get-Item -LiteralPath (Join-Path $release 'Bagaceira.exe')).Length -gt 25MB){throw 'Executável excede o limite do asset Cloudflare.'}
New-Item -ItemType Directory -Path $target -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $release 'Bagaceira.exe') -Destination (Join-Path $target 'Bagaceira.exe')
Copy-Item -LiteralPath (Join-Path $release 'update.json') -Destination (Join-Path $PSScriptRoot 'online-control\assets\update.json')
Write-Output 'Assets preparados localmente. Não houve implantação nem upload.'
