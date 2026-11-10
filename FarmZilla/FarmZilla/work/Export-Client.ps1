param([string]$Source='outputs\FarmZilla-0.19.2')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$sourceExe=Join-Path (Join-Path $root $Source) 'Bagaceira.exe'
$destination=Join-Path $root 'outputs\Enviar-aos-usuarios'
$destinationExe=Join-Path $destination 'Bagaceira.exe'
if(!(Test-Path -LiteralPath $sourceExe)){throw 'Executável do cliente não encontrado.'}
if(([Diagnostics.FileVersionInfo]::GetVersionInfo($sourceExe)).ProductName -notin @('Bagaceira','FarmZilla')){throw 'O arquivo não é um cliente FarmZilla/Bagaceira.'}
New-Item -ItemType Directory -Path $destination -Force|Out-Null
$unexpected=@(Get-ChildItem -LiteralPath $destination -Force|Where-Object{$_.Name -ne 'Bagaceira.exe'})
if($unexpected.Count){throw 'A pasta de envio contém outros arquivos. Revise-os antes de exportar; nenhum arquivo foi apagado.'}
$sourceHash=(Get-FileHash -LiteralPath $sourceExe -Algorithm SHA256).Hash
if(!(Test-Path -LiteralPath $destinationExe) -or (Get-FileHash -LiteralPath $destinationExe -Algorithm SHA256).Hash -ne $sourceHash){Copy-Item -LiteralPath $sourceExe -Destination $destinationExe -Force}
if((Get-FileHash -LiteralPath $destinationExe -Algorithm SHA256).Hash -ne $sourceHash){throw 'A cópia não corresponde ao cliente original.'}
Write-Output "Pronto para enviar: $destinationExe"
Write-Output ('Versão: '+[Diagnostics.FileVersionInfo]::GetVersionInfo($destinationExe).FileVersion+' | arquivo único | SHA-256 conferido.')


