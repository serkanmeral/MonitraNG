# Faz 5.3: SIEM dashboard-summary smoke gate (SLO: ilk istek < 3s, cache < 1s)
param(
    [string]$BaseUrl = $env:MNG_REACTOR_BASE_URL,
    [string]$Domain = 'odak',
    [int]$RangeHours = 24,
    [double]$ColdMaxSeconds = 3.0,
    [double]$WarmMaxSeconds = 1.0
)

$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir '../../../..')).Path

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = 'http://192.168.20.8:5040/reactor/api/v1'
}

$tokenScript = Join-Path $repoRoot 'docs/odak/operationcore/scripts/load-operationcore-token.ps1'
if (-not (Test-Path $tokenScript)) {
    throw "Token script bulunamadi: $tokenScript"
}

$env:MNG_OC_USE_PROD_TOKEN = '1'
$token = & $tokenScript -AutoRefresh
$hdr = @{
    Authorization = "Bearer $token"
    'X-Domain-Name' = $Domain
}

$url = "$BaseUrl/sec-events/dashboard-summary?rangeHours=$RangeHours"
Write-Host "=== dashboard-summary smoke ($Domain, ${RangeHours}h) ===" -ForegroundColor Cyan
Write-Host "URL: $url" -ForegroundColor DarkGray

function Measure-DashboardRequest {
    param([string]$Label)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-WebRequest -Uri $url -Headers $hdr -UseBasicParsing -TimeoutSec 60
    $sw.Stop()
    $body = $resp.Content | ConvertFrom-Json
    [PSCustomObject]@{
        Label = $Label
        StatusCode = $resp.StatusCode
        Seconds = $sw.Elapsed.TotalSeconds
        EventsTotal = $body.eventsTotal
    }
}

$r1 = Measure-DashboardRequest -Label 'cold'
Start-Sleep -Milliseconds 200
$r2 = Measure-DashboardRequest -Label 'warm-cache'

Write-Host "  $($r1.Label): $($r1.StatusCode) in $($r1.Seconds.ToString('F2'))s (events=$($r1.EventsTotal))" -ForegroundColor $(if ($r1.Seconds -le $ColdMaxSeconds) { 'Green' } else { 'Red' })
Write-Host "  $($r2.Label): $($r2.StatusCode) in $($r2.Seconds.ToString('F2'))s" -ForegroundColor $(if ($r2.Seconds -le $WarmMaxSeconds) { 'Green' } else { 'Yellow' })

$failed = $false
if ($r1.StatusCode -ne 200) { $failed = $true; Write-Host 'FAIL: cold request not 200' -ForegroundColor Red }
if ($r1.Seconds -gt $ColdMaxSeconds) { $failed = $true; Write-Host "FAIL: cold > ${ColdMaxSeconds}s" -ForegroundColor Red }
if ($r2.Seconds -gt $WarmMaxSeconds) { $failed = $true; Write-Host "FAIL: warm > ${WarmMaxSeconds}s" -ForegroundColor Red }

if ($failed) { exit 1 }
Write-Host 'PASS dashboard-summary smoke' -ForegroundColor Green
exit 0
