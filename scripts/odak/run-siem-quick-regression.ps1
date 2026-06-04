# Odak lab — SIEM Quick regression wrapper (E2E suite -Quick + on kosul kontrolu)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [switch]$SkipUnitGate
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$repoRoot = Split-Path $root -Parent

Write-Host "=== SIEM Quick regression (Odak) ===" -ForegroundColor Cyan

if (-not $SkipUnitGate) {
    Write-Host "`n--- Unit gate ---" -ForegroundColor Cyan
    & (Join-Path $repoRoot "scripts/ci/test-siem-unit-gate.ps1")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "`n--- Gateway preflight ($Gateway) ---" -ForegroundColor Cyan
try {
    $null = Invoke-WebRequest -Uri "$Gateway/health" -UseBasicParsing -TimeoutSec 10
    Write-Host "   Gateway OK" -ForegroundColor Green
} catch {
    Write-Host "FAIL: Gateway erisilemiyor: $Gateway" -ForegroundColor Red
    exit 1
}

Write-Host "`n--- E2E suite -Quick ---" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "test-siem-e2e-suite.ps1") -Quick
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== SIEM Quick regression PASS ===" -ForegroundColor Green
exit 0
