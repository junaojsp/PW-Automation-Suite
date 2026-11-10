param(
 [Parameter(Mandatory=$true)][string]$ServiceUrl,
 [string]$Source='outputs\FarmZilla-0.19.2',
 [string]$Notes='Atualização do Bagaceira.'
)
$ErrorActionPreference='Stop'
$service=$null
if(![Uri]::TryCreate($ServiceUrl,[UriKind]::Absolute,[ref]$service) -or $service.Scheme -ne 'https' -or !$service.IsDefaultPort -or $service.Host -eq 'github.com' -or $service.UserInfo -or $service.Query -or $service.Fragment -or $service.AbsolutePath -ne '/'){throw 'Informe a origem HTTPS do servidor privado, sem caminho, usuário, senha ou parâmetros.'}
$ServiceUrl=$service.GetLeftPart([UriPartial]::Authority)
& "$PSScriptRoot\Build-Release.ps1" -Source $Source
$root=Split-Path $PSScriptRoot -Parent
$sourcePath=(Resolve-Path -LiteralPath (Join-Path $root $Source)).Path
$exe=Join-Path $sourcePath 'Bagaceira.exe'
$version=([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion -replace '\.0$','')
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Versão inválida.'}
$tag='v'+$version
$data=Join-Path $PSScriptRoot 'online-control\release-data'
$dest=Join-Path $data "releases\$tag"
if(Test-Path -LiteralPath $dest){throw 'Versão já preparada. Gere um número de versão novo; releases são imutáveis.'}
$metadata=[ordered]@{product='AvalonInterno';architecture='x86';version=$version;url="$ServiceUrl/releases/$tag/Bagaceira.exe";sha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant();size=(Get-Item -LiteralPath $exe).Length;notes=$Notes}
$payload=[Text.Encoding]::UTF8.GetBytes(($metadata|ConvertTo-Json -Compress))
Add-Type -AssemblyName System.Security
$raw=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'release-private\signing-key.dpapi')),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
try {
 $rsa.FromXmlString([Text.Encoding]::UTF8.GetString($raw))
 if(!([IO.File]::ReadAllText((Join-Path $sourcePath 'ReleaseInfo.cs'))).Contains($rsa.ToXmlString($false))){throw 'Chave de atualização diferente da chave fixada no cliente.'}
 $signature=$rsa.SignData($payload,'SHA256')
}finally{[Array]::Clear($raw,0,$raw.Length);$rsa.Dispose()}
New-Item -ItemType Directory -Path $dest -Force|Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $dest 'Bagaceira.exe')
$envelope=@{payload=[Convert]::ToBase64String($payload);signature=[Convert]::ToBase64String($signature)}|ConvertTo-Json -Compress
[IO.File]::WriteAllText((Join-Path $dest 'update.json'),$envelope,(New-Object Text.UTF8Encoding $false))
$pointer=Join-Path $data 'current.json'
$candidate=Join-Path $data ('current-'+[Guid]::NewGuid().ToString('N')+'.tmp')
[IO.File]::WriteAllText($candidate,(@{tag=$tag}|ConvertTo-Json -Compress),(New-Object Text.UTF8Encoding $false))
if(Test-Path -LiteralPath $pointer){$pointerBackup=$candidate+'.bak';[IO.File]::Replace($candidate,$pointer,$pointerBackup);Remove-Item -LiteralPath $pointerBackup}else{[IO.File]::Move($candidate,$pointer)}
Write-Output "Pacote privado assinado preparado em $dest. Nenhum envio à internet foi feito."
Write-Output 'Para publicar e conferir no serviço atual, use work/Release-Online.ps1.'

