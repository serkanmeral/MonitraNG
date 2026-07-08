# Zimmet — dataset semalari + Automated Forms kurulumu (F0)
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\zimmet\scripts\setup-zimmet-datasets-and-forms.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$SkipSchema = $false,
    [switch]$SkipForms = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/ZimmetDgCommon.ps1")

$datasetDir = Join-Path $repoRoot "docs/odak/zimmet/datasets"
$formDir = Join-Path $repoRoot "docs/odak/zimmet/automated-forms"

$datasetFiles = @(
    "zimmet_urun_gruplari_dataset.json",
    "zimmet_urunler_dataset.json",
    "zimmet_depolar_dataset.json",
    "zimmet_depo_lokasyonlari_dataset.json",
    "zimmet_demirbaslar_dataset.json"
)

$formFiles = @(
    "zimmet_urun_gruplari_automated_form.json",
    "zimmet_urunler_automated_form.json",
    "zimmet_depolar_automated_form.json",
    "zimmet_depo_lokasyonlari_automated_form.json",
    "zimmet_demirbaslar_automated_form.json"
)

$ctx = Initialize-ZimmetDgSession -BaseUrl $BaseUrl -UseGateway:$UseGateway -RepoRoot $repoRoot

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Zimmet — datasets + Automated Forms" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if (-not $SkipSchema) {
    Write-Host "[1] Dataset kategori + semalar..." -ForegroundColor Yellow
    $catId = Ensure-ZimmetDatasetCategory -Ctx $ctx
    foreach ($file in $datasetFiles) {
        $path = Join-Path $datasetDir $file
        if (-not (Test-Path $path)) { throw "Dataset dosyasi yok: $path" }
        Sync-ZimmetDatasetSchema -Ctx $ctx -CategoryId $catId -DatasetFile $path
    }
}
else {
    Write-Host "[1] SKIP schema (-SkipSchema)" -ForegroundColor DarkYellow
}

if (-not $SkipForms) {
    Write-Host "`n[2] Automated Forms..." -ForegroundColor Yellow
    foreach ($file in $formFiles) {
        $path = Join-Path $formDir $file
        if (-not (Test-Path $path)) { throw "Form dosyasi yok: $path" }
        Ensure-ZimmetAutomatedForm -Ctx $ctx -FormFile $path
    }
}
else {
    Write-Host "[2] SKIP forms (-SkipForms)" -ForegroundColor DarkYellow
}

Write-Host "`nTamamlandi." -ForegroundColor Green
Write-Host "Runtime: /apps/automated-forms/view/zimmet-demirbaslar-form" -ForegroundColor Cyan
