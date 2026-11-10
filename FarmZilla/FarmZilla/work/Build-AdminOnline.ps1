$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'outputs\BagaceiraAdmin-2.0.1'
New-Item -ItemType Directory -Path $out -Force|Out-Null
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:x86 /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Security.dll "/win32icon:$root\outputs\FarmZilla-0.51.20\Bagaceira.ico" "/out:$out\BagaceiraAdmin.exe" "$PSScriptRoot\admin-source-v2\Admin.cs"
if($LASTEXITCODE -ne 0){throw 'Compilação do gerenciador online falhou.'}

