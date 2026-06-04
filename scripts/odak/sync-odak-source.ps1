# MonitraNG -> Odak sunucu kaynak senkronu (git push/pull gerektirmez)
# Upload: Send-OdakRemoteFile (SCP; basarisiz olursa SFTP fallback — bkz. OdakSshCommon.ps1)
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
    [switch]$IncludeMngCommon,
    [switch]$MngCommonOnly
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $RepoRoot

$DefaultFullPaths = @(
    "ApplicationResources/mng_apps",
    "tests/fixtures/siem",
    "MngGateway", "MngKeeper", "MngDataGateway", "MngReactor", "MngEngine", "MngHub",
    "MngScheduler", "MngWorkflow", "MngAlarm", "MngOperations", "MngDocument", "MngAdmin", "MngNotifier",
    "Mng.Ui", "MngDomainUI"
)

if ($MngCommonOnly) {
    $IncludeMngCommon = $true
} elseif ($Full -or $Paths.Count -eq 0) {
    $Paths = $DefaultFullPaths
}

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -User $User -Server $Server
if (Test-OdakProductionServer -Server $Server) {
    Write-Host "Production sync -> $Server" -ForegroundColor Magenta
}

if (-not $MngCommonOnly) {
    $TarPath = Join-Path $env:TEMP "monitrang-odak-sync.tar"
    if (Test-Path $TarPath) { Remove-Item $TarPath -Force }

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
    Send-OdakRemoteFile -ComputerName $Server -Credential $cred -LocalPath $TarPath -RemoteDestination "/home/odak/" -AcceptKey

    $remoteExtract = ConvertTo-UnixShell @"
set -e
mkdir -p '$RemoteMonitraRoot'
rmdir '$RemoteMonitraRoot'/MngReactor 2>/dev/null || true
rmdir '$RemoteMonitraRoot'/MngHub 2>/dev/null || true
rmdir '$RemoteMonitraRoot'/MngLLM 2>/dev/null || true
rm -rf '$RemoteMonitraRoot'/MngEngine/MngEngine.Service 2>/dev/null || true
tar -xf /home/odak/monitrang-odak-sync.tar -C '$RemoteMonitraRoot'
echo Extracted to $RemoteMonitraRoot
ls -la '$RemoteMonitraRoot/ApplicationResources/mng_apps/docker-compose.production.yml' 2>/dev/null || true
"@

    $session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
    $r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remoteExtract -TimeOut 120
    $r.Output | ForEach-Object { Write-Host $_ }
    if ($r.ExitStatus -ne 0) { throw "Remote extract failed: $($r.Error)" }
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}

if ($IncludeMngCommon) {
    $CommonTar = Join-Path $env:TEMP "mng_common_odak_sync.tar"
    Push-Location (Join-Path $RepoRoot "ApplicationResources/mng_common")
    $commonComposeOdak = if (Test-OdakProductionServer -Server $Server) { "docker-compose.odak.prod.yml" } else { "docker-compose.odak.yml" }
    & tar -cf $CommonTar --exclude=data/.npm --exclude=data/*.db docker-compose.yml $commonComposeOdak .env.odak.prod.example .env.odak.example env.example mongo-init mongo-express mosquitto nginx scalar-config 2>$null
    Pop-Location
    Send-OdakRemoteFile -ComputerName $Server -Credential $cred -LocalPath $CommonTar -RemoteDestination "/home/odak/" -AcceptKey
    $session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
    $commonExtract = ConvertTo-UnixShell "mkdir -p '$RemoteMngCommon' && tar -xf /home/odak/mng_common_odak_sync.tar -C '$RemoteMngCommon' && ls -la '$RemoteMngCommon/docker-compose.odak.prod.yml' 2>/dev/null || ls -la '$RemoteMngCommon/docker-compose.odak.yml' 2>/dev/null || true"
    $cr = Invoke-SSHCommand -SessionId $session.SessionId -Command $commonExtract -TimeOut 120
    $cr.Output | ForEach-Object { Write-Host $_ }
    if ($cr.ExitStatus -ne 0) { throw "mng_common extract failed: $($cr.Error)" }
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
    Write-Host "mng_common synced to $RemoteMngCommon"
}

Write-Host "Sync tamam."
