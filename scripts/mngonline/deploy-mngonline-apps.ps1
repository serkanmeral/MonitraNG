# monitrang.com: sunucuda mng_apps build + up
# Ön koşul: sync-mngonline-source.ps1 (veya -FromGit) ve mng_common network ayakta.
#
# Kullanım (repo kökünden):
#   .\scripts\mngonline\deploy-mngonline-apps.ps1 -Services mngui
#   .\scripts\mngonline\deploy-mngonline-apps.ps1 -Services mngkeeper,mnggateway -NoCache
#   .\scripts\mngonline\deploy-mngonline-apps.ps1 -FromGit -Services mngnotifier -Backup
#   .\scripts\mngonline\deploy-mngonline-apps.ps1 -Services mngui -DryRun

param(
    [string]$Server = "monitrang-server",
    [string]$RemoteMonitraRoot = "/root/MonitraNG",
    [string]$RemoteAppsDir = "",
    [string]$Services = "",
    [switch]$NoBuild,
    [switch]$NoCache,
    [switch]$FromGit,
    [switch]$Backup,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "MngOnlineSshCommon.ps1")

if (-not $RemoteAppsDir) {
    $RemoteAppsDir = "$RemoteMonitraRoot/ApplicationResources/mng_apps"
}

# Host nginx on monitrang.com needs published ports (see docker-compose.mngonline.yml).
$compose = "docker compose -f docker-compose.production.yml -f docker-compose.mngonline.yml --env-file .env"
$noCacheFlag = if ($NoCache) { " --no-cache" } else { "" }

$serviceList = @()
if ($Services) {
    $serviceList = Sort-MngOnlineServices -Services @($Services -split ',')
}

Write-Host "=== mngonline deploy ===" -ForegroundColor Cyan
Write-Host "Server:   $Server"
Write-Host "Apps dir: $RemoteAppsDir"
Write-Host "Mode:     $(if ($FromGit) { 'FromGit' } else { 'SyncedTree' })"
Write-Host "Services: $(if ($serviceList.Count) { $serviceList -join ', ' } else { '(ALL — dikkat)' })"
Write-Host "Build:    $(if ($NoBuild) { 'skip' } else { if ($NoCache) { 'no-cache' } else { 'yes' } })"
Write-Host "Backup:   $Backup"
Write-Host ""

if (-not $serviceList.Count) {
    Write-Host "WARNING: No -Services given. Full stack build/up will run." -ForegroundColor Yellow
    $confirm = Read-Host "Type YES to continue full deploy"
    if ($confirm -ne "YES") {
        throw "Aborted. Pass -Services explicitly (e.g. -Services mngui)."
    }
}

Test-MngOnlineSsh -Server $Server

$svcArgs = if ($serviceList.Count) { ($serviceList -join ' ') } else { "" }

$gitBlock = ""
if ($FromGit) {
    $gitBlock = @"
echo '=== git sync (origin/main) ==='
cd '$RemoteMonitraRoot'
git fetch origin
git reset --hard origin/main
git status -sb
"@
}

$backupBlock = ""
if ($Backup) {
    $backupBlock = @"
echo '=== pre-deploy backup ==='
if [ -x '$RemoteMonitraRoot/scripts/backup-pre-deploy.sh' ]; then
  bash '$RemoteMonitraRoot/scripts/backup-pre-deploy.sh' || echo 'backup script returned non-zero (continuing)'
else
  echo 'backup-pre-deploy.sh not found or not executable — skip'
fi
"@
}

if (-not $NoBuild) {
    if ($svcArgs) {
        $buildCmd = "cd '$RemoteAppsDir' && $compose build$noCacheFlag $svcArgs"
    }
    else {
        $buildCmd = "cd '$RemoteAppsDir' && $compose build$noCacheFlag"
    }
}
else {
    $buildCmd = "echo 'Skip build'"
}

if ($svcArgs) {
    $upCmd = "cd '$RemoteAppsDir' && $compose up -d --no-deps --force-recreate $svcArgs"
}
else {
    $upCmd = "cd '$RemoteAppsDir' && $compose up -d --force-recreate"
}

$healthBlock = @"
echo '=== health snapshot ==='
cd '$RemoteAppsDir'
$compose ps
for c in mngkeeper mngdatagateway mnggateway mngui mngnotifier mnghub; do
  st=`$(docker inspect -f '{{.State.Status}}' `$c 2>/dev/null || echo missing)
  echo "`$c=`$st"
done
curl -sI -o /dev/null -w 'app.monitrang.com=%{http_code}\n' https://app.monitrang.com/ --max-time 15 || true
"@

$remote = @"
set -e
test -f '$RemoteAppsDir/docker-compose.production.yml' || { echo 'Missing compose. Run sync-mngonline-source.ps1 first.'; exit 1; }
test -f '$RemoteAppsDir/docker-compose.mngonline.yml' || { echo 'Missing docker-compose.mngonline.yml. Run sync with ApplicationResources/mng_apps.'; exit 1; }
test -f '$RemoteAppsDir/.env' || { echo 'Missing .env on server.'; exit 1; }
docker network inspect mng_common_mng_network >/dev/null || { echo 'mng_common_mng_network missing.'; exit 1; }
$gitBlock
$backupBlock
echo '=== build ==='
$buildCmd
echo '=== up ==='
$upCmd
$healthBlock
echo 'Deploy remote steps done.'
"@

if ($DryRun) {
    Write-Host "[DryRun] Remote script:" -ForegroundColor Yellow
    Write-Host $remote
    exit 0
}

Write-Host "Deploy başlıyor (build uzun sürebilir)..." -ForegroundColor Cyan
$exit = Invoke-MngOnlineRemoteBash -Server $Server -ScriptBody $remote -RemoteName "mngonline-deploy.sh"
if ($exit -ne 0) {
    throw "Deploy failed (exit $exit)"
}
Write-Host "Deploy bitti." -ForegroundColor Green
