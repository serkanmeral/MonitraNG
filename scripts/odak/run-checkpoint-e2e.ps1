# Checkpoint C7 — Odak E2E regresyon paketi
param(
    [string]$Gateway = "http://192.168.20.20:5040"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$odak = Join-Path $root "odak"

$scripts = @(
    "test-operator-smoke.ps1",
    "test-alarm-lifecycle-e2e.ps1",
    "test-alarm-faz2-e2e.ps1",
    "test-alarm-approval-e2e.ps1",
    "test-alarm-rules-crud-e2e.ps1",
    "test-observation-native-e2e.ps1",
    "test-reactor-observation-e2e.ps1",
    "test-p4-engine-command-e2e.ps1",
    "test-parallel-fork-e2e.ps1",
    "test-parallel-join-e2e.ps1"
)

$failed = @()
foreach ($s in $scripts) {
    $path = Join-Path $odak $s
    Write-Host "`n========== $s ==========" -ForegroundColor Magenta
    if ($s -eq "test-reactor-observation-e2e.ps1") {
        & $path -Gateway $Gateway -FailIfSkipped
    } else {
        & $path -Gateway $Gateway
    }
    if ($LASTEXITCODE -ne 0) {
        $failed += $s
    }
}

if ($failed.Count -gt 0) {
    Write-Host "`nFAIL: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "`nOK checkpoint E2E suite ($($scripts.Count) scripts)" -ForegroundColor Green
exit 0
