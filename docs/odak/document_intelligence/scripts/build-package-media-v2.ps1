# V2 medya paketi binary uretimi (V1 sample/ dosyalarina dokunmaz)
#
# Kullanım:
#   .\build-package-media-v2.ps1

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$v2Dir = Join-Path $repoRoot "docs/odak/document_intelligence/sample/v2"
New-Item -ItemType Directory -Path $v2Dir -Force | Out-Null

$dashOut = Join-Path $v2Dir "ODK-PACKAGE-DASHBOARD-template-seed-v2.xlsx"
$briefOut = Join-Path $v2Dir "ODK-PACKAGE-BRIEF-template-seed-v2.pptx"

Write-Host "V2 dashboard XLSX -> $dashOut" -ForegroundColor Cyan
& (Join-Path $scriptDir "build-package-dashboard-seed-xlsx.ps1") -OutputPath $dashOut

Write-Host "V2 brief PPTX -> $briefOut" -ForegroundColor Cyan
& (Join-Path $scriptDir "build-package-brief-seed-pptx.ps1") -OutputPath $briefOut

Get-ChildItem $v2Dir | Format-Table Name, Length
