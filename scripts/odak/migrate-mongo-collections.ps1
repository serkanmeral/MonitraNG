<#
.SYNOPSIS
  Test MongoDB'den production MongoDB'ye secili collection'lari tasir (mongodump/mongorestore).

.EXAMPLE
  .\migrate-mongo-collections.ps1 -CollectionList "@users,@groups,@datasets,@side_menu" -DropExisting
#>
param(
    [string]$SourceServer = "192.168.20.20",
    [string]$DestServer = "192.168.20.8",
    [string]$Database = "mng_odak",
    [string[]]$Collections = @(),
    [string]$CollectionList = "",
    [switch]$DropExisting,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$mongoUser = "admin"
$mongoPass = "admin123"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"

if ($CollectionList) {
    $Collections = $CollectionList.Split(",") | ForEach-Object { $_.Trim().Trim('"').Trim("'") } | Where-Object { $_ }
}
if (-not $Collections -or $Collections.Count -eq 0) {
    throw "En az bir collection gerekli: -CollectionList 'a,b'"
}
if ($Collections.Count -eq 1 -and $Collections[0] -match ",") {
    $Collections = $Collections[0].Split(",") | ForEach-Object { $_.Trim().Trim('"').Trim("'") } | Where-Object { $_ }
}

function Invoke-RemoteMongoDumpCollection {
    param($Session, [string]$Col, [string]$RemoteArchive)
    $escaped = $Col.Replace("'", "'\\''")
    $cmd = ConvertTo-UnixShell "docker exec mongo mongodump -u $mongoUser -p $mongoPass --authenticationDatabase admin -d $Database -c '$escaped' --archive > '$RemoteArchive'"
    $r = Invoke-SSHCommand -SessionId $Session.SessionId -Command $cmd -TimeOut 3600
    if ($r.ExitStatus -ne 0) {
        throw "mongodump basarisiz ($Col): $($r.Error -join "`n")`n$($r.Output -join "`n")"
    }
    $sizeCmd = "wc -c < '$RemoteArchive'"
    $sz = Invoke-SSHCommand -SessionId $Session.SessionId -Command $sizeCmd -TimeOut 30
    $bytes = [int](($sz.Output -join "").Trim())
    if ($bytes -lt 50) {
        throw "Dump bos gorunuyor ($Col): $bytes byte"
    }
    return $bytes
}

function Invoke-RemoteMongoRestoreCollection {
    param($Session, [string]$RemoteArchive, [switch]$Drop)
    $dropFlag = if ($Drop) { "--drop" } else { "" }
    $cmd = ConvertTo-UnixShell "cat '$RemoteArchive' | docker exec -i mongo mongorestore -u $mongoUser -p $mongoPass --authenticationDatabase admin --nsInclude=$Database.* $dropFlag --archive"
    $r = Invoke-SSHCommand -SessionId $Session.SessionId -Command $cmd -TimeOut 3600
    if ($r.ExitStatus -ne 0) {
        throw "mongorestore basarisiz: $($r.Error -join "`n")`n$($r.Output -join "`n")"
    }
    $r.Output | Where-Object { $_ -match "done|restored|finished" } | ForEach-Object { Write-Host "  $_" }
}

Write-Host "=== Mongo collection migration ===" -ForegroundColor Cyan
Write-Host "Kaynak : $SourceServer / $Database"
Write-Host "Hedef  : $DestServer / $Database"
Write-Host "Collection'lar ($($Collections.Count)):"
$Collections | ForEach-Object { Write-Host "  - $_" }
if ($DropExisting) { Write-Host "Mod: hedef collection'lar DROP ile uzerine yazilacak" -ForegroundColor Yellow }
if ($WhatIf) {
    Write-Host "WhatIf: islem yapilmadi." -ForegroundColor Yellow
    exit 0
}

Initialize-OdakSshEnvironment -Server $SourceServer
$srcCred = Get-OdakSshCredential -Server $SourceServer
Initialize-OdakSshEnvironment -Server $DestServer
$dstCred = Get-OdakSshCredential -Server $DestServer

$src = New-SSHSession -ComputerName $SourceServer -Credential $srcCred -AcceptKey
$dst = New-SSHSession -ComputerName $DestServer -Credential $dstCred -AcceptKey

try {
    $i = 0
    foreach ($col in $Collections) {
        $i++
        $safeName = ($col -replace '[^a-zA-Z0-9._-]', '_')
        $remoteDump = "/tmp/mng_odak_${stamp}_${safeName}.archive"
        $localDump = Join-Path $env:TEMP "mng_odak_${stamp}_${safeName}.archive"

        Write-Host "[$i/$($Collections.Count)] $col" -ForegroundColor Cyan
        $bytes = Invoke-RemoteMongoDumpCollection -Session $src -Col $col -RemoteArchive $remoteDump
        Write-Host "  dump: $([math]::Round($bytes / 1KB, 1)) KB"

        Get-SCPItem -ComputerName $SourceServer -Credential $srcCred -Path $remoteDump -PathType File -Destination (Split-Path $localDump -Parent) -AcceptKey -ErrorAction Stop
        $downloaded = Join-Path (Split-Path $localDump -Parent) (Split-Path $remoteDump -Leaf)
        if ($downloaded -ne $localDump) { Move-Item -Force $downloaded $localDump }

        Send-OdakRemoteFile -ComputerName $DestServer -Credential $dstCred -LocalPath $localDump -RemoteDestination $remoteDump -AcceptKey
        Invoke-RemoteMongoRestoreCollection -Session $dst -RemoteArchive $remoteDump -Drop:$DropExisting

        Invoke-SSHCommand -SessionId $src.SessionId -Command "rm -f '$remoteDump'" -TimeOut 30 | Out-Null
        Invoke-SSHCommand -SessionId $dst.SessionId -Command "rm -f '$remoteDump'" -TimeOut 30 | Out-Null
        if (Test-Path $localDump) { Remove-Item $localDump -Force }
        Write-Host "  OK" -ForegroundColor Green
    }
} finally {
    Remove-SSHSession $src.SessionId | Out-Null
    Remove-SSHSession $dst.SessionId | Out-Null
}

Write-Host "=== Tamamlandi ===" -ForegroundColor Green
