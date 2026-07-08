# Test deploy: G5 sevkiyat listesi XLSX (build + seed + publish + catalog seed)
#
# Kullanim:
#   .\docs\odak\document_intelligence\scripts\deploy-shipment-list-design-test.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN,
    [switch]$SkipCatalogSeed = $false,
    [switch]$Replace = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host "G5 Shipment List test deploy -> $BaseUrl" -ForegroundColor Cyan

& (Join-Path $scriptDir "build-shipment-list-seed-xlsx.ps1")

if (-not $SkipCatalogSeed) {
    & (Join-Path $scriptDir "seed-dm-data-sources.ps1") -BaseUrl $BaseUrl -Token $Token
    & (Join-Path $scriptDir "seed-dm-document-producers.ps1") -BaseUrl $BaseUrl -Token $Token
}

$seedArgs = @{ BaseUrl = $BaseUrl; Token = $Token }
& (Join-Path $scriptDir "seed-designer-template-shipment-list-standard.ps1") @seedArgs
& (Join-Path $scriptDir "patch-shipment-list-standard-test.ps1") -BaseUrl $BaseUrl -Token $Token

Write-Host "Deploy tamam. Smoke: .\scripts\tests\MngDocument\smoke-shipment-list-xlsx-test.ps1" -ForegroundColor Green
