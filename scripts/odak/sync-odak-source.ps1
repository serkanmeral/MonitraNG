# MonitraNG -> Odak sunucu kaynak senkronu (git push/pull gerektirmez)
# Kullanım (repo kökünden):
#   .\scripts\odak\sync-odak-source.ps1
#   .\scripts\odak\sync-odak-source.ps1 -Paths MngKeeper,Mng.Ui,ApplicationResources/mng_apps
#   .\scripts\odak\sync-odak-source.ps1 -IncludeMngCommon
#   $env:ODAK_SSH_PASSWORD = '...'   # parolasız otomasyon (Read-Host atlanır)

param(
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$RemoteMonitraRoot = "/home/odak/MonitraNG",
    [string]$RemoteMngCommon = "/home/odak/mng_common",
    [string[]]$Paths = @(),
    [switch]$Full,
    [switch]$IncludeMngCommon
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $RepoRoot

$DefaultFullPaths = @(
    "ApplicationResources/mng_apps",
    "MngGateway", "MngKeeper", "MngDataGateway", "MngReactor", "MngHub",
    "MngScheduler", "MngWorkflow", "MngAlarm", "MngOperations", "MngDocument", "MngAdmin", "MngNotifier",
    "Mng.Ui", "MngDomainUI"
)

if ($Full -or $Paths.Count -eq 0) {
    $Paths = $DefaultFullPaths
}

$TarPath = Join-Path $env:TEMP "monitrang-odak-sync.tar"
if (Test-Path $TarPath) { Remove-Item $TarPath -Force }

# Windows tar: paths relative to repo root
# Build artifaktları / bağımlılıklar sunucuda yeniden üretilir (.dockerignore zaten dışlar) —
# tar'a almak transferi gereksiz büyütür (ör. Mng.Ui/node_modules ~350MB).
$tarExcludes = @(
    "*/node_modules/*", "*/.nuxt/*", "*/.output/*", "*/.nitro/*",
    "*/bin/*", "*/obj/*", "*/.git/*", "*/.vs/*"
)
$tarArgs = @("-cf", $TarPath)
foreach ($ex in $tarExcludes) { $tarArgs += "--exclude=$ex" }
foreach ($p in $Paths) {
    if (-not (Test-Path $p)) { throw "Path not found: $p" }
    $tarArgs += $p
}
& tar @tarArgs
if ($LASTEXITCODE -ne 0) { throw "tar failed" }

Write-Host "Paket: $TarPath ($((Get-Item $TarPath).Length / 1MB) MB)"

$cred = Get-OdakSshCredential -User $User -Server $Server

Set-SCPItem -ComputerName $Server -Credential $cred -Path $TarPath -Destination "/home/odak/" -AcceptKey

$remoteExtract = @"
set -e
mkdir -p '$RemoteMonitraRoot'
# Bos gitlink/stub klasorleri tar'in uzerine yazmasini engelleyebilir (MngReactor C6).
find '$RemoteMonitraRoot' -mindepth 1 -maxdepth 2 -type d -empty -delete 2>/dev/null || true
tar -xf /home/odak/monitrang-odak-sync.tar -C '$RemoteMonitraRoot'
echo "Extracted to $RemoteMonitraRoot"
ls -la '$RemoteMonitraRoot/ApplicationResources/mng_apps/docker-compose.production.yml' 2>/dev/null || true
"@

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remoteExtract -TimeOut 120
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.ExitStatus -ne 0) { throw "Remote extract failed: $($r.Error)" }
Remove-SSHSession -SessionId $session.SessionId | Out-Null

if ($IncludeMngCommon) {
    $CommonTar = Join-Path $env:TEMP "mng_common_odak_sync.tar"
    Push-Location (Join-Path $RepoRoot "ApplicationResources/mng_common")
    & tar -cf $CommonTar --exclude=data/.npm --exclude=data/*.db docker-compose.yml docker-compose.odak.yml env.example mongo-init mongo-express mosquitto nginx scalar-config 2>$null
    Pop-Location
    Set-SCPItem -ComputerName $Server -Credential $cred -Path $CommonTar -Destination "/home/odak/" -AcceptKey
    $session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
    Invoke-SSHCommand -SessionId $session.SessionId -Command "mkdir -p '$RemoteMngCommon' && tar -xf /home/odak/mng_common_odak_sync.tar -C '$RemoteMngCommon'" | Out-Null
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
    Write-Host "mng_common synced to $RemoteMngCommon"
}

Write-Host "Sync tamam."
