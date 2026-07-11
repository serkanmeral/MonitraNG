# Zimmet — tüm Reporting katalog raporlarını seed et (idempotent).
#
# Kullanım (repo kökü):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\zimmet\scripts\seed-zimmet-reporting-all.ps1
#
# Browse: /apps/reporting/browse → Zimmet

param([string]$BaseUrl = "http://192.168.20.20:5040")

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

$wrappers = @(
    "seed-zimmet-reporting-depolar.ps1",
    "seed-zimmet-reporting-urunler.ps1",
    "seed-zimmet-reporting-urun-gruplari.ps1",
    "seed-zimmet-reporting-demirbaslar.ps1",
    "seed-zimmet-reporting-garanti.ps1",
    "seed-zimmet-reporting-personel.ps1"
)

Write-Host "=== Zimmet reporting seed (all) ===" -ForegroundColor Cyan
foreach ($w in $wrappers) {
    $path = Join-Path $scriptDir $w
    if (-not (Test-Path $path)) { throw "Wrapper yok: $path" }
    Write-Host "`n--- $w ---" -ForegroundColor Yellow
    & $path -BaseUrl $BaseUrl
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
        throw "Seed basarisiz: $w (exit $LASTEXITCODE)"
    }
}

Write-Host "`n--- DI document templates ---" -ForegroundColor Yellow
& (Join-Path $scriptDir "seed-zimmet-reporting-document-templates.ps1") -BaseUrl $BaseUrl

Write-Host "`nTamam. /apps/reporting/browse → Zimmet (6 rapor + DI belgeler)" -ForegroundColor Green
