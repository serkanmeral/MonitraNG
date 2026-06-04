# SIEM Odak — tum E2E scriptlerini sirayla calistir
param(
    [switch]$SkipFaz1,
    [switch]$SkipBenchmarks,
    [switch]$Quick
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Invoke-Step([string]$Name, [string]$Script, [string[]]$ExtraArgs = @()) {
    $path = Join-Path $root $Script
    if (-not (Test-Path $path)) { throw "Script eksik: $path" }
    Write-Host "`n========== $Name ==========" -ForegroundColor Cyan
    & $path @ExtraArgs
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
    Invoke-Step "Engine syslog S4.1" "test-engine-syslog-s4.1.ps1" @("-VerifyOdakMongo")
}

Invoke-Step "Purge alarm observation queue" "purge-alarm-observation-queue.ps1"
Invoke-Step "U1 alarm" "test-siem-u1-alarm-e2e.ps1"
Invoke-Step "U4 alarm" "test-siem-u4-alarm-e2e.ps1"
Invoke-Step "U2 sequence alarm" "test-siem-u2-alarm-e2e.ps1"

Invoke-Step "Purge workflow event queue" "purge-workflow-event-inbound-queue.ps1"
Invoke-Step "Purge workflow queue" "purge-workflow-execution-queue.ps1"

Invoke-Step "U1 workflow" "test-siem-u1-workflow-e2e.ps1"
Invoke-Step "U4 workflow" "test-siem-u4-workflow-e2e.ps1"
Invoke-Step "U1 approval block" "test-siem-u1-approval-block-e2e.ps1"

if (-not $SkipBenchmarks) {
    Invoke-Step "P0 baseline" "benchmark-siem-p0-baseline.ps1" @("-IncludeDetectionLag", "-DurationSec", "15", "-TargetEps", "20")
}

Write-Host "`n=== SIEM E2E Suite PASS ===" -ForegroundColor Green
exit 0
