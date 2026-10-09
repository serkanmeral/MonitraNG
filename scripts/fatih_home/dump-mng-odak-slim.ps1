<#
.SYNOPSIS
  Odak sunucusundan slim mng_odak mongodump alır (online / Fatih Home exclude listesi).

.DESCRIPTION
  monitrang online aktarımındaki gibi @users/@groups ve ağır koleksiyonları hariç tutar.
  Çıktı: docs/fatih_home/artifacts/odak-mongo-mng_odak-<stamp>/

.EXAMPLE
  .\scripts\fatih_home\dump-mng-odak-slim.ps1 -WhatIf

.EXAMPLE
  .\scripts\fatih_home\dump-mng-odak-slim.ps1

.EXAMPLE
  .\scripts\fatih_home\dump-mng-odak-slim.ps1 -Server 192.168.20.8
#>
param(
    [string]$Server = "192.168.20.20",
    [string]$Database = "mng_odak",
    [string]$MongoContainer = "mongo",
    [string]$OutputRoot = "",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
. (Join-Path $repoRoot "scripts/odak/OdakSshCommon.ps1")

$excludeCollections = @(
    "@users",
    "@groups",
    "sec_events",
    "@workflow_instances",
    "@workflow_node_executions",
    "mon_metrics",
    "@job_executions"
)

if (-not $OutputRoot) {
    $OutputRoot = Join-Path $repoRoot "docs/fatih_home/artifacts"
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$localOut = Join-Path $OutputRoot "odak-mongo-mng_odak-$stamp"
$remoteArchive = "/tmp/mng_odak_slim_$stamp.archive.gz"
$remoteManifest = "/tmp/mng_odak_slim_$stamp.manifest.json"
$localArchive = Join-Path $localOut "mng_odak_slim.archive.gz"
$localManifest = Join-Path $localOut "manifest.json"

Write-Host "=== Fatih Home — slim mng_odak dump ===" -ForegroundColor Cyan
Write-Host "Kaynak : $Server / $Database"
Write-Host "Exclude: $($excludeCollections -join ', ')"
Write-Host "Cikti  : $localOut"
if (Test-OdakProductionServer -Server $Server) {
    Write-Host "UYARI: Production sunucu secildi." -ForegroundColor Yellow
}

if ($WhatIf) {
    Write-Host "WhatIf: SSH/dump yapilmadi." -ForegroundColor Yellow
    exit 0
}

Import-Module Posh-SSH -Force
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
if (-not $session) { throw "SSH oturumu acilamadi: $Server" }

try {
    $mongo = Get-OdakMongoCredentials -SshSession $session
    $escapedPass = $mongo.Password.Replace("'", "'\''")

    $excludeArgs = ($excludeCollections | ForEach-Object {
        $c = $_.Replace("'", "'\''")
        "--excludeCollection='$c'"
    }) -join " "

    $dumpCmd = ConvertTo-UnixShell @"
set -e
docker exec $MongoContainer mongodump \
  -u $($mongo.Username) -p '$escapedPass' --authenticationDatabase admin \
  --db $Database \
  $excludeArgs \
  --gzip --archive=$remoteArchive
ls -lh $remoteArchive
"@

    Write-Host "mongodump basliyor..." -ForegroundColor Green
    $dumpResult = Invoke-SSHCommand -SessionId $session.SessionId -Command $dumpCmd -TimeOut 7200
    if ($dumpResult.ExitStatus -ne 0) {
        throw "mongodump basarisiz: $($dumpResult.Error -join "`n")`n$($dumpResult.Output -join "`n")"
    }
    $dumpResult.Output | ForEach-Object { Write-Host "  $_" }

    $sizeCmd = "stat -c%s $remoteArchive 2>/dev/null || wc -c < $remoteArchive"
    $sz = Invoke-SSHCommand -SessionId $session.SessionId -Command $sizeCmd -TimeOut 30
    $bytes = [int64](($sz.Output -join "").Trim())
    if ($bytes -lt 1000) {
        throw "Dump cok kucuk gorunuyor: $bytes byte"
    }

    $exportedAt = (Get-Date).ToUniversalTime().ToString("o")
    $excludeJson = ($excludeCollections | ForEach-Object { "`"$_`"" }) -join ", "
    $manifestBody = @"
{
  "exportedAt": "$exportedAt",
  "sourceHost": "$Server",
  "database": "$Database",
  "format": "mongodump --gzip --archive",
  "excludedCollections": [ $excludeJson ],
  "reason": "Fatih Home / online model: preserve domain users-groups; skip heavy/runtime collections",
  "archiveBytes": $bytes,
  "notes": "MinIO not included. DI templates = separate seed."
}
"@
    $manifestB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($manifestBody))
    $writeManifest = "echo '$manifestB64' | base64 -d > $remoteManifest"
    $null = Invoke-SSHCommand -SessionId $session.SessionId -Command $writeManifest -TimeOut 30

    New-Item -ItemType Directory -Path $localOut -Force | Out-Null
    Write-Host "Archive indiriliyor ($bytes byte)..." -ForegroundColor Green
    Get-SCPItem -ComputerName $Server -Credential $cred -Path $remoteArchive -PathType File -Destination $localOut -AcceptKey -Force
    $downloaded = Get-ChildItem $localOut -Filter "*.archive.gz" | Select-Object -First 1
    if (-not $downloaded) { throw "SCP sonrasi archive bulunamadi: $localOut" }
    if ($downloaded.FullName -ne $localArchive) {
        Move-Item -Force $downloaded.FullName $localArchive
    }

    Get-SCPItem -ComputerName $Server -Credential $cred -Path $remoteManifest -PathType File -Destination $localOut -AcceptKey -Force
    $manFile = Get-ChildItem $localOut -Filter "*.manifest.json" | Select-Object -First 1
    if ($manFile -and $manFile.FullName -ne $localManifest) {
        Move-Item -Force $manFile.FullName $localManifest
    }
    if (-not (Test-Path $localManifest)) {
        Set-Content -Path $localManifest -Value $manifestBody -Encoding utf8
    }

    $cleanup = "rm -f $remoteArchive $remoteManifest"
    $null = Invoke-SSHCommand -SessionId $session.SessionId -Command $cleanup -TimeOut 30

    Write-Host "Tamam." -ForegroundColor Green
    Write-Host "  Archive : $localArchive"
    Write-Host "  Manifest: $localManifest"
    Write-Host "  Boyut   : $([math]::Round($bytes / 1MB, 2)) MB"
}
finally {
    if ($session) { Remove-SSHSession -SessionId $session.SessionId | Out-Null }
}
