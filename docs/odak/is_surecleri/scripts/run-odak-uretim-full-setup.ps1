# Odak Uretim — tam kurulum (master dataset + OC workspace + demo)
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\is_surecleri\scripts\run-odak-uretim-full-setup.ps1
#   .\docs\odak\is_surecleri\scripts\run-odak-uretim-full-setup.ps1 -SkipDemo
#   .\docs\odak\is_surecleri\scripts\run-odak-uretim-full-setup.ps1 -SmokeTest

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$MoBaseUrl = "http://192.168.20.20:5040/operations",
    [switch]$SkipDemo = $false,
    [switch]$SmokeTest = $false,
    [switch]$ReloadMetadataCache = $true
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Odak Uretim — TAM KURULUM" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$tokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/get-operationcore-token.ps1"
$loadScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$token = & $loadScript
if ([string]::IsNullOrEmpty($token)) {
    if (Test-Path $tokenScript) {
        Write-Host "Token yok — get-operationcore-token calistiriliyor..." -ForegroundColor Yellow
        & $tokenScript
        $token = & $loadScript
    }
}
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

& (Join-Path $scriptDir "setup-odak-master-datasets.ps1") -BaseUrl $BaseUrl
& (Join-Path $scriptDir "seed-odak-master-data.ps1") -BaseUrl $BaseUrl

$seedParams = @{
    BaseUrl              = $BaseUrl
    MoBaseUrl            = $MoBaseUrl
    ReloadMetadataCache  = $ReloadMetadataCache
}
if ($SmokeTest) { $seedParams.SmokeTest = $true }
if (-not $SkipDemo) { $seedParams.SeedDemo = $true }

& (Join-Path $scriptDir "seed-operation-core-odak-uretim.ps1") @seedParams

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "KURULUM TAMAMLANDI" -ForegroundColor Green
Write-Host "UI: http://192.168.20.20:3000/apps/operation-core/workspace" -ForegroundColor Green
Write-Host "Workspace: Odak Uretim" -ForegroundColor Green
Write-Host "Ozet: docs/odak/is_surecleri/seed/odak-uretim-seed.json" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green
