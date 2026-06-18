# Odak Siparis — tum DG dataset + AF kurulumu (MO bagimsiz)
#
# Siralama: odak_musteriler -> odak_musteri_kisileri -> odak_is_paketleri -> odak_siparis_kalemleri -> odak_ncr + odak_capa -> odak_sevkiyat*
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\siparis\scripts\setup-odak-siparis-datasets.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$SkipMaster = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path

$commonArgs = @{
    BaseUrl     = $BaseUrl
    UseGateway  = $UseGateway
}
if ($UseGateway) { $commonArgs.UseGateway = $true }

Write-Host "`n=== setup-odak-siparis-datasets ===" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl`n" -ForegroundColor Cyan

if (-not $SkipMaster) {
    Write-Host "[1/6] Master datasets (odak_musteriler + legacyFirmId)..." -ForegroundColor Yellow
    & (Join-Path $repoRoot "docs/odak/is_surecleri/scripts/setup-odak-master-datasets.ps1") @commonArgs
}
else {
    Write-Host "[1/6] Master atlandi (-SkipMaster)" -ForegroundColor Gray
}

Write-Host "[2/6] odak_musteri_kisileri + customerContactId..." -ForegroundColor Yellow
& (Join-Path $scriptDir "setup-odak-musteri-kisileri-dataset.ps1") @commonArgs

Write-Host "[3/6] odak_is_paketleri..." -ForegroundColor Yellow
& (Join-Path $scriptDir "setup-odak-is-paketleri-dataset.ps1") @commonArgs

Write-Host "[4/6] odak_siparis_kalemleri (parentPackageId)..." -ForegroundColor Yellow
& (Join-Path $scriptDir "setup-odak-siparis-kalemleri-dataset.ps1") @commonArgs

Write-Host "[5/6] odak_ncr + odak_capa (Kalite, AF yok)..." -ForegroundColor Yellow
& (Join-Path $scriptDir "setup-odak-siparis-ncr-capa-datasets.ps1") @commonArgs

Write-Host "[6/7] odak_sevkiyatlar + odak_sevkiyat_kalemleri..." -ForegroundColor Yellow
& (Join-Path $scriptDir "setup-odak-siparis-sevkiyat-datasets.ps1") @commonArgs

Write-Host "[7/7] odak_siparis_hub_ayarlari + odak_siparis_notification_policies..." -ForegroundColor Yellow
& (Join-Path $scriptDir "setup-odak-siparis-hub-settings.ps1") @commonArgs

Write-Host "`nTamamlandi. Sonraki adimlar:" -ForegroundColor Cyan
Write-Host "  migrate-legacy-firms-to-dg.ps1" -ForegroundColor Gray
Write-Host "  migrate-legacy-all-packages-to-dg.ps1" -ForegroundColor Gray
Write-Host "  verify-legacy-dg-migration.ps1" -ForegroundColor Gray
Write-Host "  migrate-legacy-ncs-to-dg.ps1" -ForegroundColor Gray
