# Alarm Faz 2 E2E — correlation window + scheduled validation scan
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$alarm = "$Gateway/alarm/api/v1"

function Send-Event([string]$userId) {
    Invoke-RestMethod -Uri "$alarm/dev/observations/ingest" -Method POST -Headers $hdr -Body (@{
        domainName = $Domain
        key = "auth_failure"
        kind = "event"
        dimensions = @{ userId = $userId; srcIp = "10.0.0.1" }
    } | ConvertTo-Json)
}

Write-Host "Creating correlation rule (threshold=3, window=60m)..." -ForegroundColor Cyan
$corrRule = Invoke-RestMethod -Uri "$alarm/rules?domainName=$Domain" -Method POST -Headers $hdr -Body (@{
    name = "Auth failure correlation E2E"
    type = "correlation"
    matchKey = "auth_failure"
    threshold = 3
    severity = 6
    cooldownMinutes = 0
    windowMinutes = 60
    groupByFields = @("userId")
} | ConvertTo-Json)

Write-Host "Sending 3 auth_failure events for user=e2e-user..." -ForegroundColor Yellow
$r1 = Send-Event "e2e-user"
$r2 = Send-Event "e2e-user"
$r3 = Send-Event "e2e-user"
Write-Host "  batch raised=$($r3.alarmsRaised) updated=$($r3.alarmsUpdated)"

if ($r3.alarmsRaised -lt 1 -and $r3.alarmsUpdated -lt 1) {
    Write-Host "FAIL: expected alarm raised on 3rd event" -ForegroundColor Red
    exit 1
}
Write-Host "OK correlation alarm raised/updated" -ForegroundColor Green

Write-Host "`nCreating scheduled rule (staleness=1 min)..." -ForegroundColor Cyan
$schedRule = Invoke-RestMethod -Uri "$alarm/rules?domainName=$Domain" -Method POST -Headers $hdr -Body (@{
    name = "Heartbeat staleness E2E"
    type = "scheduled"
    matchKey = "agent_heartbeat"
    threshold = 0
    severity = 4
    stalenessMinutes = 1
} | ConvertTo-Json)

Write-Host "Sending heartbeat..." -ForegroundColor Yellow
Invoke-RestMethod -Uri "$alarm/dev/observations/ingest" -Method POST -Headers $hdr -Body (@{
    domainName = $Domain; key = "agent_heartbeat"; kind = "metric"; value = 1
} | ConvertTo-Json) | Out-Null

Write-Host "Waiting 70s for staleness..." -ForegroundColor DarkGray
Start-Sleep -Seconds 70

Write-Host "Running validation scan..." -ForegroundColor Yellow
$scan = Invoke-RestMethod -Uri "$alarm/validation/run" -Method POST -Headers $hdr
Write-Host "  scheduledRaised=$($scan.scheduledRaised) correlationResolved=$($scan.correlationResolved)"

if ($scan.scheduledRaised -lt 1) {
    Write-Host "FAIL: expected scheduled staleness alarm" -ForegroundColor Red
    exit 1
}
Write-Host "OK scheduled staleness alarm raised" -ForegroundColor Green

Write-Host "`nAll Alarm Faz 2 E2E checks passed." -ForegroundColor Green
exit 0
