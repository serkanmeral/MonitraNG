# SIEM yerel CI kapisi — unit test + benchmark JSON dogrulama (Odak E2E yok)
param(
    [switch]$SkipBenchmarkVerify
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

Write-Host "=== SIEM local CI gate ===" -ForegroundColor Cyan

& (Join-Path $PSScriptRoot "test-siem-unit-gate.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipBenchmarkVerify) {
    Write-Host ""
    & (Join-Path $PSScriptRoot "verify-siem-benchmark-baselines.ps1")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "`n=== SIEM local CI gate PASS ===" -ForegroundColor Green
exit 0
