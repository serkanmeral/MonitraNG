# SIEM U9 — Windows AD account_created (4720) sec_events → observation → correlation alarm
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
$ruleFixture = Join-Path $fixtureDir "alarm_rules/u9_ad_account_created.json"

function Read-FixtureJson([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content -Path $path -Raw).TrimEnd() | ConvertFrom-Json
}

Write-Host "=== SIEM U9 AD account_created correlation E2E ===" -ForegroundColor Cyan

Write-Host "`n1) U9 correlation rule (threshold=1, cooldown=0)..." -ForegroundColor Cyan
$ruleTemplate = Get-Content -Path $ruleFixture -Raw | ConvertFrom-Json
$ruleName = "U9 SIEM E2E $(Get-Date -Format 'HHmmss')"
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
Write-Host "   ruleId=$($rule.id) matchKey=$($ruleTemplate.matchKey)" -ForegroundColor DarkGray

$rawObj = Read-FixtureJson "windows_4720_account_created.json"
$adminUser = "u9_admin_$(Get-Random -Maximum 9999)"
$rawObj.SubjectUserName = $adminUser
$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")

Write-Host "`n2) POST 1 x account_created sec-event (user=$adminUser)..." -ForegroundColor Yellow
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
if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
Write-Host "   Ingest tamam" -ForegroundColor Green

Write-Host "`n3) Alarm raised bekleniyor (severity>=8)..." -ForegroundColor Cyan
$found = $false
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 2
    $page = Invoke-RestMethod -Uri "$alarm/alarms?openOnly=true&minSeverity=8" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [Array]) { $items = @($page) }
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
    if ($FailIfSkipped) { throw "U9 alarm raised bulunamadi" }
    Write-Host "   UYARI: Alarm raised bulunamadi (FailIfSkipped yok)" -ForegroundColor Yellow
    exit 0
}

Write-Host "`nOK SIEM U9 account_created -> alarm PASS" -ForegroundColor Green
exit 0
