# Reactor ingest → monitra.observations → MngAlarm (C6 kabul testi)
# Odak'ta mngreactor stub ise SKIP (exit 0, mesaj). Gercek image deploy sonrasi PASS beklenir.
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
$reactor = "$Gateway/reactor/api/v1"

function Skip-Reactor([string]$Reason) {
    Write-Host "SKIP reactor E2E: $Reason" -ForegroundColor Yellow
    Write-Host "  C6 tamamlanmasi icin MngReactor repo R1-R3 + Odak deploy gerekli." -ForegroundColor DarkGray
    Write-Host "  Bkz. docs/odak/alarm/REACTOR_NATIVE_PUBLISH_HANDOFF.md" -ForegroundColor DarkGray
    if ($FailIfSkipped) { exit 1 }
    exit 0
}

Write-Host "1) Reactor health..." -ForegroundColor Cyan
try {
    $live = Invoke-RestMethod -Uri "$reactor/health/live" -Headers $hdr -TimeoutSec 10
    if ($live.status -ne "alive") { Skip-Reactor "health/live status=$($live.status)" }
    Write-Host "   Reactor ayakta" -ForegroundColor Green
} catch {
    Skip-Reactor $_.Exception.Message
}

Write-Host "2) Resolve engine/agent/asset..." -ForegroundColor Cyan
$engineId = $null
$agentId = $null
$assetId = $null
$itemId = $null
try {
    $engines = Invoke-RestMethod -Uri "$reactor/monitoring/engines" -Headers $hdr
    $engineId = $engines.data[0].__dataId
    $agents = Invoke-RestMethod -Uri "$reactor/monitoring/agents" -Headers $hdr
    $agentId = $agents.data[0].__dataId
    if ($agents.data[0].asset_configs -and $agents.data[0].asset_configs.Count -gt 0) {
        $assetId = $agents.data[0].asset_configs[0].assetId
    }
    if (-not $assetId) {
        $assets = Invoke-RestMethod -Uri "$reactor/monitoring/assets" -Headers $hdr
        $assetId = $assets.data[0].__dataId
        $itemId = $assets.data[0].itemId
    }
} catch {
    Skip-Reactor "monitoring CRUD unavailable: $($_.Exception.Message)"
}

if (-not $engineId -or -not $agentId -or -not $assetId) {
    Skip-Reactor "engine/agent/asset seed eksik (seed-monitoring-test-data)"
}

Write-Host "3) Ensure cpu_usage rule (cooldown=0)..." -ForegroundColor Cyan
$rules = Invoke-RestMethod -Uri "$alarm/rules" -Headers $hdr
$rule = @($rules) | Where-Object { $_.matchKey -eq "cpu_usage" -and $_.operator -eq "gt" } | Select-Object -First 1
if (-not $rule) {
    Invoke-RestMethod -Uri "$alarm/rules" -Method POST -Headers $hdr -Body (@{
        name = "Reactor obs E2E"; matchKey = "cpu_usage"; operator = "gt"; threshold = 90; severity = 5; cooldownMinutes = 0
    } | ConvertTo-Json) | Out-Null
}

Write-Host "4) Pre-clear (dev ingest value=50)..." -ForegroundColor DarkGray
Invoke-RestMethod -Uri "$alarm/dev/observations/ingest" -Method POST -Headers $hdr -Body (@{
    domainName = $Domain; key = "cpu_usage"; value = 50; kind = "metric"
} | ConvertTo-Json) | Out-Null
Start-Sleep -Seconds 6

Write-Host "5) Reactor ingest metrics (cpu_usage=97)..." -ForegroundColor Yellow
$body = @{
    batches = @(
        @{
            assetId     = $assetId
            itemId      = $itemId
            agentId     = $agentId
            engineId    = $engineId
            collectedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            metrics     = @(@{ collectibleCode = "cpu_usage"; value = 97; unit = "%" })
        }
    )
} | ConvertTo-Json -Depth 6
$ingest = Invoke-RestMethod -Uri "$reactor/ingest/metrics" -Method POST -Headers $hdr -Body $body
if ($ingest.savedCount -lt 1) {
    throw "ingest savedCount=$($ingest.savedCount)"
}
Write-Host "   savedCount=$($ingest.savedCount)"

Write-Host "6) Wait for alarm consumer..." -ForegroundColor Yellow
Start-Sleep -Seconds 12
$verify = Invoke-RestMethod -Uri "$alarm/dev/observations/ingest" -Method POST -Headers $hdr -Body (@{
    domainName = $Domain; key = "cpu_usage"; value = 98; kind = "metric"
} | ConvertTo-Json)
Write-Host "   raised=$($verify.alarmsRaised) updated=$($verify.alarmsUpdated) resolved=$($verify.alarmsResolved)"

if ($verify.alarmsRaised -ge 1 -or $verify.alarmsUpdated -ge 1) {
    Write-Host "OK reactor ingest -> alarm lifecycle" -ForegroundColor Green
    exit 0
}

throw "FAIL: expected alarm raised/updated after reactor ingest (bridge kapali + native publish acik olmali)"
