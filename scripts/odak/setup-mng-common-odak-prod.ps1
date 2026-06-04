# Production mng_common compose up (Docker + dizinler hazir olmali)
param([string]$Server = "192.168.20.8", [string]$User = "odak")

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -User $User -Server $Server

$odakOverride = if (Test-OdakProductionServer -Server $Server) { "docker-compose.odak.prod.yml" } else { "docker-compose.odak.yml" }

$remote = ConvertTo-UnixShell @"
set -e
command -v docker >/dev/null || { echo 'HATA: Docker yok — IT sudo+docker veya setup-docker-odak-prod.ps1'; exit 1; }
cd /home/odak/mng_common
test -f docker-compose.yml || { echo 'HATA: mng_common eksik — sync-mng-common-prod.ps1'; exit 1; }
test -f $odakOverride || { echo 'HATA: $odakOverride eksik — sync-mng-common-prod.ps1'; exit 1; }
test -f .env || { echo 'HATA: .env eksik — bootstrap-odak-prod.ps1'; exit 1; }
echo '=== mng_common compose up (production, bagimsiz yigin) ==='
docker compose -f docker-compose.yml -f $odakOverride --env-file .env pull
docker compose -f docker-compose.yml -f $odakOverride --env-file .env up -d
echo '=== network ==='
docker network inspect mng_common_mng_network
echo '=== ps ==='
docker compose -f docker-compose.yml -f $odakOverride --env-file .env ps
echo '=== keycloak health ==='
curl -s -o /dev/null -w 'keycloak=%{http_code}\n' http://127.0.0.1:8080/keycloak/ || true
"@

Write-Host "mng_common baslatiliyor ($Server) — image pull uzun surebilir..." -ForegroundColor Cyan
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 3600
$r.Output | ForEach-Object { Write-Host $_ }
Remove-SSHSession -SessionId $session.SessionId | Out-Null
if ($r.ExitStatus -ne 0) { throw "mng_common up failed" }
