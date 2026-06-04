# SIEM Faz 2 U1 — sec_events → monitra.observations → correlation alarm
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [int]$Threshold = 3,
    [switch]$FailIfSkipped
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$token = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$alarm = "$Gateway/alarm/api/v1"
$reactor = "$Gateway/reactor/api/v1/ingest/sec-events"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$fixtureDir = Join-Path $repoRoot "tests/fixtures/siem"

function Read-Fixture([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content -Path $path -Raw).TrimEnd()
}

Write-Host "=== SIEM U1 brute-force correlation E2E ===" -ForegroundColor Cyan

Write-Host "`n1) U1 correlation rule (threshold=$Threshold, cooldown=0)..." -ForegroundColor Cyan
$ruleName = "U1 SIEM E2E $(Get-Date -Format 'HHmmss')"
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = $ruleName
    type             = "correlation"
    matchKey         = "login_failed"
    groupByFields    = @("userId", "srcIp")
    windowMinutes    = 5
    threshold        = $Threshold
    severity         = 7
    cooldownMinutes  = 0
    dedupKeyTemplate = "{ruleId}:{groupKey}"
} | ConvertTo-Json -Depth 5)
Write-Host "   ruleId=$($rule.id)" -ForegroundColor DarkGray

$windowsRaw = Read-Fixture "windows_4625_failed_logon.json"
$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")

Write-Host "`n2) POST $Threshold x login_failed sec-events..." -ForegroundColor Yellow
for ($i = 0; $i -lt $Threshold; $i++) {
    $windowsObj = $windowsRaw | ConvertFrom-Json
    $windowsObj.TimeCreated = $receivedAt
    $body = @{
        items = @(
            @{
                receivedAt = $receivedAt
                source     = @{ type = "ad"; product = "windows"; host = "dc01" }
                raw        = $windowsObj
            }
        )
    } | ConvertTo-Json -Depth 8
    $ingest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $body -TimeoutSec 60
    if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
    Start-Sleep -Milliseconds 300
}
Write-Host "   Ingest tamam" -ForegroundColor Green

Write-Host "`n3) Alarm raised bekleniyor..." -ForegroundColor Cyan
$found = $false
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 2
    $page = Invoke-RestMethod -Uri "$alarm/alarms?openOnly=true&minSeverity=7" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [array]) { $items = @($page) }
    $match = $items | Where-Object {
        $_.ruleId -eq $rule.id -and ($_.status -eq 0 -or $_.status -eq "Active")
    } | Select-Object -First 1
    if ($match) {
        Write-Host "   Alarm raised: $($match.id) severity=$($match.severity)" -ForegroundColor Green
        $found = $true
        break
    }
}

if (-not $found) {
    Write-Host "FAIL: U1 correlation alarm bulunamadi (ObservationPublish acik mi? mngalarm-worker ConsumeObservations=true?)" -ForegroundColor Red
    if ($FailIfSkipped) { exit 1 }
    exit 1
}

Write-Host "`nOK SIEM U1 sec_events -> observation -> alarm PASS" -ForegroundColor Green
exit 0
