# SIEM Odak — tum E2E scriptlerini sirayla calistir
param(
    [switch]$SkipFaz1,
    [switch]$SkipBenchmarks,
    [switch]$Quick
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Invoke-Step([string]$Name, [string]$Script, [hashtable]$Params = @{}) {
    $path = Join-Path $root $Script
    if (-not (Test-Path $path)) { throw "Script eksik: $path" }
    Write-Host "`n========== $Name ==========" -ForegroundColor Cyan
    & $path @Params
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAIL: $Name ($Script)" -ForegroundColor Red
        exit 1
    }
    Write-Host "OK: $Name" -ForegroundColor Green
}

Write-Host "=== SIEM E2E Suite (Odak) ===" -ForegroundColor Cyan
if ($Quick) {
    $SkipFaz1 = $true
    $SkipBenchmarks = $true
}

if (-not $SkipFaz1) {
    Invoke-Step "Faz1 sec_events" "test-siem-faz1-e2e.ps1"
    Write-Host "`n========== Engine syslog S4.1 ==========" -ForegroundColor Cyan
    & (Join-Path $root "test-engine-syslog-s4.1.ps1") -VerifyOdakMongo
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAIL: Engine syslog S4.1" -ForegroundColor Red
        exit 1
    }
    Write-Host "OK: Engine syslog S4.1" -ForegroundColor Green
    Write-Host "`n========== Engine WEC batch S5 ==========" -ForegroundColor Cyan
    & (Join-Path $root "test-engine-wec-ingest-e2e.ps1") -VerifyOdakMongo
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAIL: Engine WEC batch S5" -ForegroundColor Red
        exit 1
    }
    Write-Host "OK: Engine WEC batch S5" -ForegroundColor Green
}

Invoke-Step "B1 firewall vendor ingest" "test-siem-firewall-vendor-ingest.ps1" @{ Vendor = "all" }
Invoke-Step "B1 windows extended ingest" "test-siem-windows-extended-ingest.ps1"
Invoke-Step "B1 bastion ingest" "test-siem-bastion-ingest.ps1"
Invoke-Step "NxLog WEC template format" "test-nxlog-wec-template-e2e.ps1"

Invoke-Step "Purge alarm observation queue" "purge-alarm-observation-queue.ps1"
Invoke-Step "U1 alarm" "test-siem-u1-alarm-e2e.ps1"
Invoke-Step "U1 linux auth alarm" "test-siem-linux-auth-u1-alarm-e2e.ps1"
Invoke-Step "U4 alarm" "test-siem-u4-alarm-e2e.ps1"
Invoke-Step "U6 rule change alarm" "test-siem-u6-alarm-e2e.ps1"
Invoke-Step "U8 AD group member alarm" "test-siem-u8-alarm-e2e.ps1"
Invoke-Step "U9 AD account created alarm" "test-siem-u9-alarm-e2e.ps1"
Invoke-Step "U10 AD directory modified alarm" "test-siem-u10-alarm-e2e.ps1"
Invoke-Step "U5 traffic spike alarm" "test-siem-u5-alarm-e2e.ps1"
Invoke-Step "U3 privileged outside window" "test-siem-u3-alarm-e2e.ps1"
Write-Host "`n========== U7 new flow baseline ==========" -ForegroundColor Cyan
& (Join-Path $root "test-siem-u7-alarm-e2e.ps1") -ResetBaseline
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL: U7 new flow baseline (test-siem-u7-alarm-e2e.ps1)" -ForegroundColor Red
    exit 1
}
Write-Host "OK: U7 new flow baseline" -ForegroundColor Green
Invoke-Step "U2 sequence alarm" "test-siem-u2-alarm-e2e.ps1"

Invoke-Step "Purge workflow/alarm MQ queues" "purge-workflow-queues.ps1" @{ Apply = $true }

Invoke-Step "U1 workflow" "test-siem-u1-workflow-e2e.ps1"
Invoke-Step "U1 linux auth workflow" "test-siem-linux-auth-u1-workflow-e2e.ps1"
Invoke-Step "U4 workflow" "test-siem-u4-workflow-e2e.ps1"
Invoke-Step "U1 approval block" "test-siem-u1-approval-block-e2e.ps1"
Invoke-Step "U1 linux auth approval block" "test-siem-linux-auth-u1-approval-block-e2e.ps1"

if (-not $SkipBenchmarks) {
    Write-Host "`n========== P0 baseline ==========" -ForegroundColor Cyan
    $bench = Join-Path $root "benchmark-siem-p0-baseline.ps1"
    & $bench -IncludeDetectionLag -DurationSec 15 -TargetEps 10
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAIL: P0 baseline ($bench)" -ForegroundColor Red
        exit 1
    }
    Write-Host "OK: P0 baseline" -ForegroundColor Green
}

Write-Host "`n=== SIEM E2E Suite PASS ===" -ForegroundColor Green
exit 0
