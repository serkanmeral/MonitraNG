# V1 medya paketi şablonlarını sample/v1/ arşivinden geri yükler (factory rebuild sonrası).
#
# Kullanım:
#   .\docs\odak\document_intelligence\scripts\restore-package-media-v1-samples.ps1

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$sampleDir = Join-Path $repoRoot "docs/odak/document_intelligence/sample"
$v1Dir = Join-Path $sampleDir "v1"

$files = @(
    "ODK-PACKAGE-DASHBOARD-template-seed.xlsx",
    "ODK-PACKAGE-BRIEF-template-seed.pptx"
)

foreach ($name in $files) {
    $src = Join-Path $v1Dir $name
    $dst = Join-Path $sampleDir $name
    if (-not (Test-Path $src)) { throw "V1 arsiv dosyasi yok: $src" }
    Copy-Item $src $dst -Force
    Write-Host "OK restore $name" -ForegroundColor Green
}

Write-Host "V1 sample dosyalari geri yuklendi. V2 icin build-package-media-v2.ps1 kullanin." -ForegroundColor Cyan
