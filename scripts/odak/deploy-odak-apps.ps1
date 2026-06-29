# Odak sunucuda mng_apps build + up (ön koşul: sync-odak-source.ps1, mng_common ayakta)
#
# OFFLINE (prod/test internetsiz): deploy-odak-prod-offline.ps1 / deploy-odak-test-offline.ps1
#   Gelistirme makinesinde docker build -> tar -> sunucuda docker load -> bu script -NoBuild
#
# Kullanım:
#   .\scripts\odak\deploy-odak-apps.ps1
#   .\scripts\odak\deploy-odak-apps.ps1 -Services mngkeeper,mngui
#   .\scripts\odak\deploy-odak-apps.ps1 -Services mngoperations -NoCache   # kritik backend fix
#   .\scripts\odak\deploy-odak-apps.ps1 -NoBuild
#   $env:ODAK_SSH_PASSWORD = '...'

param(
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$RemoteAppsDir = "/home/odak/MonitraNG/ApplicationResources/mng_apps",
    [string]$Services = "",
    [switch]$NoBuild,
    [switch]$FullBuild,
    # Kritik backend fix sonrası: build cache'i atla (normal build bazen değişen kaynağı
    # cache'ten alıp eski binary'yi paketliyor — build ~36sn = sahte).
    [switch]$NoCache
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -User $User -Server $Server

$odakComposeFile = Get-OdakComposeOdakFile -Server $Server
$compose = "docker compose -f docker-compose.production.yml -f $odakComposeFile --env-file .env"
if (Test-OdakProductionServer -Server $Server) {
    Write-Host "Production deploy -> $Server ($odakComposeFile)" -ForegroundColor Magenta
}

$noCacheFlag = if ($NoCache) { " --no-cache" } else { "" }

if (-not $NoBuild) {
    if ($Services) {
        $svc = $Services -replace ',', ' '
        $buildCmd = "cd '$RemoteAppsDir' && $compose build$noCacheFlag $svc"
    } else {
        $buildCmd = "cd '$RemoteAppsDir' && $compose build$noCacheFlag"
    }
} else {
    $buildCmd = "echo 'Skip build'"
}

if ($Services) {
    $svc = $Services -replace ',', ' '
    $upCmd = "cd '$RemoteAppsDir' && $compose up -d $svc"
} else {
    $upCmd = "cd '$RemoteAppsDir' && $compose up -d"
}

$remote = @"
set -e
test -f '$RemoteAppsDir/docker-compose.production.yml' || { echo 'Missing apps dir. Run sync-odak-source.ps1 first.'; exit 1; }
test -f '$RemoteAppsDir/.env' || { echo 'Missing .env. Copy .env.odak.example to .env on server.'; exit 1; }
docker network inspect mng_common_mng_network >/dev/null || { echo 'Start mng_common first.'; exit 1; }
$buildCmd
$upCmd
$compose ps
curl -s -o /dev/null -w 'gateway=%{http_code} ' http://127.0.0.1:5040/health || true
curl -s -o /dev/null -w 'ui=%{http_code} ' http://127.0.0.1:3000/ || true
curl -s -o /dev/null -w 'oc_live=%{http_code}\n' http://127.0.0.1:3000/api/operations/v1/health/live || true
"@

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
Write-Host "Deploy başlıyor (build uzun sürebilir)..."
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 3600
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.Error) { $r.Error | ForEach-Object { Write-Host $_ } }
if ($r.ExitStatus -ne 0) { throw "Deploy failed (exit $($r.ExitStatus))" }
Remove-SSHSession -SessionId $session.SessionId | Out-Null
Write-Host "Deploy bitti."
