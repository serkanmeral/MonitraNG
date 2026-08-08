# Resolve (close) all open alarms for a domain. Optional Mongo wipe of @mon_alarms.
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$Server = "192.168.20.20",
    [int]$PageSize = 100,
    [switch]$Apply,
    [switch]$HardDeleteMongo
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$null = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$tokenFile = Join-Path $env:TEMP "serkan_token.txt"
if (-not (Test-Path $tokenFile)) { throw "Token dosyasi yok: $tokenFile" }
$token = (Get-Content -Path $tokenFile -Raw).Trim()
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$alarmsApi = "$Gateway/alarm/api/v1/alarms"

Write-Host "=== Purge / resolve open alarms ($Domain) ===" -ForegroundColor Cyan
if (-not $Apply) { Write-Host "   Dry-run (-Apply ile uygula)" -ForegroundColor Yellow }

$resolved = 0
$failed = 0
$skip = 0
$total = $null

do {
    $page = Invoke-RestMethod -Uri "$alarmsApi`?openOnly=true&skip=$skip&limit=$PageSize" -Headers $hdr
    if ($null -eq $total) { $total = [int]$page.total }
    $items = @($page.items)
    if ($items.Count -eq 0) { break }

    Write-Host "   Page skip=$skip count=$($items.Count) totalOpen~$total" -ForegroundColor DarkGray
    foreach ($alarm in $items) {
        if ($Apply) {
            try {
                Invoke-RestMethod -Uri "$alarmsApi/$($alarm.id)/resolve" -Method POST -Headers $hdr | Out-Null
                Write-Host "   RESOLVED $($alarm.id) sev=$($alarm.severity) rule=$($alarm.ruleId)" -ForegroundColor Green
                $resolved++
            } catch {
                Write-Host "   FAIL $($alarm.id): $($_.Exception.Message)" -ForegroundColor Red
                $failed++
                $skip++
            }
        } else {
            Write-Host "   WOULD RESOLVE $($alarm.id) sev=$($alarm.severity) rule=$($alarm.ruleId)" -ForegroundColor DarkGray
            $resolved++
        }
    }

    if (-not $Apply) {
        $skip += $items.Count
    }
    # When applying, resolved alarms drop out of openOnly list — keep skip=0
} while ($items.Count -gt 0 -and ($Apply -or $skip -lt $total))

Write-Host "Summary: resolvedOrWould=$resolved failed=$failed openTotalWas=$total" -ForegroundColor Cyan

if ($HardDeleteMongo) {
    Write-Host "`n=== Hard delete Mongo @mon_alarms (+ runtime windows) ===" -ForegroundColor Cyan
    if (-not $Apply) {
        Write-Host "   WOULD wipe mng_$Domain.@mon_alarms (and correlation/activity/sequence/due)" -ForegroundColor Yellow
    } else {
        Import-Module Posh-SSH -Force -ErrorAction Stop
        . (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
        Initialize-OdakSshEnvironment -Server $Server
        $cred = Get-OdakSshCredential -Server $Server
        $session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
        try {
            $dbName = "mng_$Domain"
            $collections = @(
                "@mon_alarms",
                "@mon_alarm_correlation_windows",
                "@mon_alarm_observation_activity",
                "@mon_alarm_sequence_state",
                "@mon_alarm_scenario_due_state",
                "@mon_alarm_scenario_executions"
            )
            foreach ($col in $collections) {
                $cmd = "docker exec mongodb mongosh $dbName --quiet --eval `"db.getCollection('$col').deleteMany({})`""
                $r = Invoke-SSHCommand -SessionId $session.SessionId -Command $cmd
                Write-Host "   wiped $col : $($r.Output)" -ForegroundColor Green
            }
        } finally {
            if ($session) { Remove-SSHSession -SessionId $session.SessionId | Out-Null }
        }
    }
}

if ($Apply) {
    Write-Host "OK alarm cleanup done." -ForegroundColor Green
} else {
    Write-Host "OK dry-run (-Apply ile uygula; tam silmek icin -HardDeleteMongo)" -ForegroundColor Green
}
exit 0
