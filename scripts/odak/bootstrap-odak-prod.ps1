# Production sunucuda ilk .env dosyalarini olusturur (SSH) — test .env KOPYALANMAZ
# Kullanım: .\scripts\odak\bootstrap-odak-prod.ps1
# Yalnizca .env.odak.prod.example -> .env (192.168.20.8 sablonlari)
# Sonra production Keycloak'ta YENI secret'lar doldurulmalidir.

param(
    [string]$Server = "192.168.20.8",
    [string]$User = "odak"
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -User $User -Server $Server

$remote = ConvertTo-UnixShell @"
set -e
mkdir -p /home/odak/mng_common /home/odak/MonitraNG/ApplicationResources/mng_apps
if [ -f /home/odak/mng_common/.env.odak.prod.example ] && [ ! -f /home/odak/mng_common/.env ]; then
  cp /home/odak/mng_common/.env.odak.prod.example /home/odak/mng_common/.env
  echo 'Created mng_common/.env from prod example'
elif [ -f /home/odak/mng_common/.env ]; then
  echo 'mng_common/.env already exists — skipped'
else
  echo 'WARN: mng_common/.env.odak.prod.example missing — run sync-mng-common-prod.ps1 first'
fi
APPS=/home/odak/MonitraNG/ApplicationResources/mng_apps
if [ -f "`$APPS/.env.odak.prod.example" ] && [ ! -f "`$APPS/.env" ]; then
  cp "`$APPS/.env.odak.prod.example" "`$APPS/.env"
  echo 'Created mng_apps/.env from prod example'
elif [ -f "`$APPS/.env" ]; then
  echo 'mng_apps/.env already exists — skipped'
else
  echo 'WARN: mng_apps/.env.odak.prod.example missing — run sync-odak-prod.ps1 -Full first'
fi
"@

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 60
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.ExitStatus -ne 0) { throw "Bootstrap failed" }
Remove-SSHSession -SessionId $session.SessionId | Out-Null
Write-Host "Bootstrap bitti. KEYCLOAK_CLIENT_SECRET ve MNGKEEPER_LICENSE_MASTER_KEY sunucuda .env icinde doldurun."
