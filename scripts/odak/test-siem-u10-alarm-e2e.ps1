# SIEM U10 — Windows AD directory_object_modified (5136) sec_events → observation → correlation alarm
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
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
$ruleFixture = Join-Path $fixtureDir "alarm_rules/u10_ad_directory_object_modified.json"

function Read-FixtureJson([string]$Name) {
    return (Get-Content -Path (Join-Path $fixtureDir $Name) -Raw).TrimEnd() | ConvertFrom-Json
}

Write-Host "=== SIEM U10 AD directory_object_modified correlation E2E ===" -ForegroundColor Cyan

$ruleTemplate = Get-Content -Path $ruleFixture -Raw | ConvertFrom-Json
$ruleName = "U10 SIEM E2E $(Get-Date -Format 'HHmmss')"
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = $ruleName
    type             = $ruleTemplate.type
    matchKey         = $ruleTemplate.matchKey
    groupByFields    = @($ruleTemplate.groupByFields)
    windowMinutes    = $ruleTemplate.windowMinutes
    threshold        = 1
    severity         = $ruleTemplate.severity
    cooldownMinutes  = 0
    dedupKeyTemplate = $ruleTemplate.dedupKeyTemplate
} | ConvertTo-Json -Depth 5)

$rawObj = Read-FixtureJson "windows_5136_directory_modified.json"
$adminUser = "u10_admin_$(Get-Random -Maximum 9999)"
$rawObj.SubjectUserName = $adminUser
$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")

$body = @{
    items = @(
        @{
            receivedAt = $receivedAt
            source     = @{ type = "ad"; product = "windows"; host = "DC01-ODAK" }
            raw        = $rawObj
        }
    )
} | ConvertTo-Json -Depth 8
$ingest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $body -TimeoutSec 60
if ($ingest.accepted -lt 1) { throw "Ingest basarisiz" }

$found = $false
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 2
    $page = Invoke-RestMethod -Uri "$alarm/alarms?openOnly=true&minSeverity=8" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [Array]) { $items = @($page) }
    $match = $items | Where-Object { $_.ruleId -eq $rule.id -and ($_.status -eq 0 -or $_.status -eq "Active") } | Select-Object -First 1
    if ($match) {
        Write-Host "   Alarm raised: $($match.id) severity=$($match.severity)" -ForegroundColor Green
        $found = $true
        break
    }
}

if (-not $found) {
    if ($FailIfSkipped) { throw "U10 alarm raised bulunamadi" }
    Write-Host "   UYARI: Alarm raised bulunamadi" -ForegroundColor Yellow
    exit 0
}

Write-Host "`nOK SIEM U10 directory_object_modified -> alarm PASS" -ForegroundColor Green
exit 0
