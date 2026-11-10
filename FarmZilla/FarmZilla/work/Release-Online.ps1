param(
 [string]$Source='outputs\FarmZilla-0.19.2',
 [string]$ServiceUrl='https://bagaceira-control.viacaomimo.workers.dev',
 [switch]$VerifyOnly,
 [switch]$SkipExport,
 [string]$Notes='Atualização do Bagaceira.'
)
$ErrorActionPreference='Stop'
if(!$VerifyOnly){
 & "$PSScriptRoot\Publish-Update.ps1" -Source $Source -ServiceUrl $ServiceUrl -Notes $Notes
 & "$PSScriptRoot\Prepare-WorkerAssets.ps1"
 & "$PSScriptRoot\online-control-tooling\node_modules\.bin\wrangler.cmd" deploy --config "$PSScriptRoot\online-control\wrangler.jsonc"
 if($LASTEXITCODE -ne 0){throw 'Implantação não concluída.'}
}
$root=Split-Path $PSScriptRoot -Parent
$sourcePath=Join-Path $root $Source
$files=@('AppUpdate.cs','DiagnosticLog.cs','OnlineApi.cs','OnlineIdentity.cs','OnlineLicense.cs','LicenseCore.cs','ReleaseInfo.cs')|ForEach-Object{Join-Path $sourcePath $_}
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /platform:x86 /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Management.dll "/out:$PSScriptRoot\OnlineReleaseProbe.exe" "$PSScriptRoot\OnlineReleaseProbe.cs" $files
if($LASTEXITCODE -ne 0){throw 'Verificador não compilou.'}
& "$PSScriptRoot\OnlineReleaseProbe.exe" $ServiceUrl
if($LASTEXITCODE -ne 0){throw 'Release publicada, mas verificação remota falhou. Confira o serviço antes de anunciar a versão.'}
Write-Output 'Release conferida no serviço privado. Não foi criado download anônimo.'
if(!$VerifyOnly -and !$SkipExport){& "$PSScriptRoot\Export-Client.ps1" -Source $Source}

