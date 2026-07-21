# MonitraNG -> monitrang.com kaynak senkronu (git push/pull gerektirmez)
# OpenSSH scp + sunucuda tar extract. .env taşınmaz / ezilmez.
#
# Kullanım (repo kökünden):
#   .\scripts\mngonline\sync-mngonline-source.ps1 -Paths Mng.Ui
#   .\scripts\mngonline\sync-mngonline-source.ps1 -Paths MngKeeper,MngGateway,ApplicationResources/mng_apps
#   .\scripts\mngonline\sync-mngonline-source.ps1 -Full

param(
    [string]$Server = "monitrang-server",
    [string]$RemoteMonitraRoot = "/root/MonitraNG",
    [string[]]$Paths = @(),
    [switch]$Full
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "MngOnlineSshCommon.ps1")

$RepoRoot = Get-MngOnlineRepoRoot -ScriptRoot $PSScriptRoot
Set-Location $RepoRoot

if ($Full -or $Paths.Count -eq 0) {
    if (-not $Full -and $Paths.Count -eq 0) {
        Write-Host "No -Paths given; using -Full default set. Pass -Paths for selective sync." -ForegroundColor Yellow
    }
    $Paths = Get-MngOnlineDefaultSyncPaths
}

$resolved = @()
foreach ($p in $Paths) {
    $item = $p.Trim().TrimEnd('/', '\')
    if (-not $item) { continue }
    if (-not (Test-Path $item)) {
        throw "Path not found: $item"
    }
    $resolved += ($item -replace '\\', '/')
}
if ($resolved.Count -eq 0) {
    throw "No valid paths to sync."
}

Write-Host "=== mngonline sync ===" -ForegroundColor Cyan
Write-Host "Server: $Server"
Write-Host "Remote: $RemoteMonitraRoot"
Write-Host "Paths:  $($resolved -join ', ')"
Write-Host ""

Test-MngOnlineSsh -Server $Server

$TarName = "monitrang-mngonline-sync.tar"
$TarPath = Join-Path $env:TEMP $TarName
if (Test-Path $TarPath) { Remove-Item $TarPath -Force }

$tarExcludes = @(
    "*/node_modules/*", "*/.nuxt/*", "*/.output/*", "*/.nitro/*",
    "*/bin/*", "*/obj/*", "*/.git/*", "*/.vs/*",
    "*/ApplicationResources/mng_apps/.env",
    "*/mng_apps/.env"
)
$tarArgs = @("-cf", $TarPath)
foreach ($ex in $tarExcludes) { $tarArgs += "--exclude=$ex" }
foreach ($p in $resolved) { $tarArgs += $p }

Write-Host "Creating archive..." -ForegroundColor Cyan
& tar @tarArgs
if ($LASTEXITCODE -ne 0) { throw "tar failed (exit $LASTEXITCODE)" }

$sizeMb = [math]::Round((Get-Item $TarPath).Length / 1MB, 2)
Write-Host "Paket: $TarPath ($sizeMb MB)"

$remoteTar = "/tmp/$TarName"
Write-Host "Uploading..." -ForegroundColor Cyan
Send-MngOnlineScp -Server $Server -LocalPath $TarPath -RemoteDestination $remoteTar

# Preserve .env; archive excludes .env but restore is belt-and-suspenders.
$remoteExtract = @"
set -e
mkdir -p '$RemoteMonitraRoot'
ENV_FILE='$RemoteMonitraRoot/ApplicationResources/mng_apps/.env'
if [ -f "`$ENV_FILE" ]; then
  cp -a "`$ENV_FILE" /tmp/mng_apps.env.bak
  echo 'Preserved .env'
fi
tar -xf '$remoteTar' -C '$RemoteMonitraRoot'
if [ -f /tmp/mng_apps.env.bak ]; then
  mkdir -p '$RemoteMonitraRoot/ApplicationResources/mng_apps'
  cp -a /tmp/mng_apps.env.bak "`$ENV_FILE"
  rm -f /tmp/mng_apps.env.bak
  echo 'Restored .env'
fi
rm -f '$remoteTar'
test -f '$RemoteMonitraRoot/ApplicationResources/mng_apps/docker-compose.production.yml'
echo 'Extracted OK'
ls -la '$RemoteMonitraRoot/ApplicationResources/mng_apps/docker-compose.production.yml'
"@

Write-Host "Extracting on server..." -ForegroundColor Cyan
$exit = Invoke-MngOnlineRemoteBash -Server $Server -ScriptBody $remoteExtract -RemoteName "mngonline-sync-extract.sh"
if ($exit -ne 0) { throw "Remote extract failed (exit $exit)" }

Remove-Item $TarPath -Force -ErrorAction SilentlyContinue
Write-Host "Sync tamam." -ForegroundColor Green
