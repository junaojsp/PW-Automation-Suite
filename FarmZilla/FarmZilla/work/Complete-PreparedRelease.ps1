param([Parameter(Mandatory=$true)][string]$Tag)
$ErrorActionPreference='Stop'
if($Tag -notmatch '^v\d+\.\d+\.\d+$'){throw 'Tag inválida.'}
$data=Join-Path $PSScriptRoot 'online-control\release-data'
$release=Join-Path $data "releases\$Tag"
$envelope=Get-Content -LiteralPath (Join-Path $release 'update.json') -Raw|ConvertFrom-Json
$payload=[Convert]::FromBase64String($envelope.payload)
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
try{$rsa.FromXmlString([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'release-private\public-key.xml')));if(!$rsa.VerifyData($payload,'SHA256',[Convert]::FromBase64String($envelope.signature))){throw 'Assinatura inválida.'}}finally{$rsa.Dispose()}
$manifest=[Text.Encoding]::UTF8.GetString($payload)|ConvertFrom-Json
if(('v'+$manifest.version) -ne $Tag -or $manifest.product -ne 'AvalonInterno' -or $manifest.architecture -ne 'x86'){throw 'Metadados inválidos.'}
$exe=Join-Path $release 'Bagaceira.exe'
if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $manifest.sha256 -or (Get-Item -LiteralPath $exe).Length -ne $manifest.size){throw 'Executável diverge do manifesto.'}
$candidate=Join-Path $data ('current-'+[Guid]::NewGuid().ToString('N')+'.tmp')
$pointer=Join-Path $data 'current.json'
[IO.File]::WriteAllText($candidate,(@{tag=$Tag}|ConvertTo-Json -Compress),(New-Object Text.UTF8Encoding $false))
if(Test-Path -LiteralPath $pointer){$backup=$candidate+'.bak';[IO.File]::Replace($candidate,$pointer,$backup);Remove-Item -LiteralPath $backup}else{[IO.File]::Move($candidate,$pointer)}
Write-Output 'Pacote existente conferido; ponteiro local atualizado sem recompilar nem alterar a release imutável.'
