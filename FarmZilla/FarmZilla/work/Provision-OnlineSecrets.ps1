param([Parameter(Mandatory=$true)][string]$ServiceUrl)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
$root=Split-Path $PSScriptRoot -Parent
$tokenPath=Join-Path $PSScriptRoot 'release-private\online-admin-token.dpapi'
if(!(Test-Path -LiteralPath $tokenPath)){
 $random=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();$rng.GetBytes($random);$rng.Dispose()
 $newToken=[Convert]::ToBase64String($random).TrimEnd('=').Replace('+','-').Replace('/','_')
 [IO.File]::WriteAllBytes($tokenPath,[Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($newToken),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser))
 $newToken=$null;[Array]::Clear($random,0,$random.Length)
}
$rawToken=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($tokenPath),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
$rawKey=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'release-private\license-key.dpapi')),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
try{
 $adminToken=[Text.Encoding]::UTF8.GetString($rawToken)
 $sha=[Security.Cryptography.SHA256]::Create();try{$adminHash=[BitConverter]::ToString($sha.ComputeHash($rawToken)).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
 [xml]$keyXml=[Text.Encoding]::UTF8.GetString($rawKey)
 $mapping=@{n='Modulus';e='Exponent';d='D';p='P';q='Q';dp='DP';dq='DQ';qi='InverseQ'}
 $jwk=@{kty='RSA';alg='RS256';ext=$true;key_ops=@('sign')}
 foreach($field in $mapping.Keys){$jwk[$field]=([string]$keyXml.RSAKeyValue.($mapping[$field])).TrimEnd('=').Replace('+','-').Replace('/','_')}
 $secrets=@{ADMIN_TOKEN_SHA256=$adminHash;LICENSE_LEASE_PRIVATE_JWK=($jwk|ConvertTo-Json -Compress -Depth 4)}|ConvertTo-Json -Compress
 # Secrets go through stdin to Cloudflare's secret store, never command arguments or static assets.
 $secrets | & "$PSScriptRoot\online-control-tooling\node_modules\.bin\wrangler.cmd" secret bulk --config "$PSScriptRoot\online-control\wrangler.jsonc"
 if($LASTEXITCODE -ne 0){throw 'Não foi possível configurar os segredos.'}
 $adminConfigDirectory=Join-Path $env:LOCALAPPDATA 'BagaceiraAdmin';New-Item -ItemType Directory -Path $adminConfigDirectory -Force|Out-Null
 $settings=@{Service=$ServiceUrl.TrimEnd('/');Secret=$adminToken}|ConvertTo-Json -Compress
 [IO.File]::WriteAllBytes((Join-Path $adminConfigDirectory 'online-config.dat'),[Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($settings),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser))
 Write-Output 'Segredos configurados no serviço e acesso do gerenciador salvo protegido nesta conta Windows.'
}finally{
 [Array]::Clear($rawToken,0,$rawToken.Length);[Array]::Clear($rawKey,0,$rawKey.Length)
 $adminToken=$null;$secrets=$null;$settings=$null;$jwk=$null;$keyXml=$null
}
