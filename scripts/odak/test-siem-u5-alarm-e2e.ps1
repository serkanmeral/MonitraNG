# SIEM U5 — firewall allowed_flow spike (traffic to dstIp/dstPort) → correlation alarm
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
$ruleFixture = Join-Path $fixtureDir "alarm_rules/u5_firewall_traffic_spike.json"

function Read-Fixture([string]$Name) {
    $path = Join-Path $fixtureDir $Name
    if (-not (Test-Path $path)) { throw "Fixture eksik: $path" }
    return (Get-Content -Path $path -Raw).TrimEnd()
}

Write-Host "=== SIEM U5 firewall traffic spike correlation E2E ===" -ForegroundColor Cyan

Write-Host "`n1) U5 correlation rule (threshold=$Threshold, cooldown=0)..." -ForegroundColor Cyan
$ruleTemplate = Get-Content -Path $ruleFixture -Raw | ConvertFrom-Json
$ruleName = "U5 SIEM E2E $(Get-Date -Format 'HHmmss')"
$rule = Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
    name             = $ruleName
    type             = $ruleTemplate.type
    matchKey         = $ruleTemplate.matchKey
    groupByFields    = @($ruleTemplate.groupByFields)
    windowMinutes    = $ruleTemplate.windowMinutes
    threshold        = $Threshold
    severity         = $ruleTemplate.severity
    cooldownMinutes  = 0
    dedupKeyTemplate = $ruleTemplate.dedupKeyTemplate
} | ConvertTo-Json -Depth 5)
Write-Host "   ruleId=$($rule.id) matchKey=$($ruleTemplate.matchKey) groupBy=dstIp,dstPort" -ForegroundColor DarkGray

$firewallRaw = Read-Fixture "firewall_allow.syslog.txt"
$receivedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$dstIp = "10.0.1.$((Get-Random -Minimum 10 -Maximum 250))"
$dstPort = 8443
$firewallLine = $firewallRaw -replace 'DST=10\.0\.0\.10', "DST=$dstIp" -replace 'DPT=443', "DPT=$dstPort"

Write-Host "`n2) POST $Threshold x allowed_flow sec-events (dstIp=$dstIp dstPort=$dstPort)..." -ForegroundColor Yellow
for ($i = 0; $i -lt $Threshold; $i++) {
    $body = @{
        items = @(
            @{
                receivedAt = $receivedAt
                source     = @{ type = "firewall"; product = "generic-syslog"; host = "fw-u5-e2e" }
                raw        = $firewallLine
            }
        )
    } | ConvertTo-Json -Depth 8
    $ingest = Invoke-RestMethod -Uri $reactor -Method POST -Headers $hdr -Body $body -TimeoutSec 60
    if ($ingest.accepted -lt 1) { throw "Ingest basarisiz: $($ingest | ConvertTo-Json -Compress)" }
    Start-Sleep -Milliseconds 400
}
Write-Host "   Ingest tamam" -ForegroundColor Green

Write-Host "`n3) Alarm raised bekleniyor (severity>=7)..." -ForegroundColor Cyan
$found = $false
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 2
    $page = Invoke-RestMethod -Uri "$alarm/alarms?openOnly=true&minSeverity=7" -Headers $hdr
    $items = @($page.items)
    if ($items.Count -eq 0 -and $page -is [Array]) { $items = @($page) }
    $match = $items | Where-Object {
        $_.ruleId -eq $rule.id -and ($_.status -eq 0 -or $_.status -eq "Active")
    } | Select-Object -First 1
    if ($match) {
        $ctx = $match.context
        $ctxDst = if ($ctx.dstIp) { $ctx.dstIp } else { $ctx.DstIp }
        Write-Host "   Alarm raised: $($match.id) severity=$($match.severity) dstIp=$ctxDst windowCount=$($ctx.windowCount)" -ForegroundColor Green
        if ($ctxDst -ne $dstIp) {
            Write-Host "FAIL: context.dstIp beklenen $dstIp, gelen $ctxDst" -ForegroundColor Red
            exit 1
        }
        $found = $true
        break
    }
}

if (-not $found) {
    Write-Host "FAIL: U5 correlation alarm bulunamadi" -ForegroundColor Red
    if ($FailIfSkipped) { exit 1 }
    exit 1
}

Write-Host "`nOK SIEM U5 allowed_flow spike -> alarm PASS" -ForegroundColor Green
exit 0
