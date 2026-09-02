# Document Intelligence (DM) - Dataset category + dm_* schemas (Odak API Gateway)
# F1-0: create-or-merge via Teslimat Omurgasi installer library (POST skip is not enough).
#
#   .\docs\odak\document_intelligence\scripts\setup-document-intelligence-datasets.ps1
#   .\setup-document-intelligence-datasets.ps1 -Token "<bearer-token>" -BaseUrl "http://192.168.20.20:5040"
#
# Full install (schemas + core seed + verify):
#   .\docs\odak\project_management\scripts\install-teslimat-omurgasi.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$installLib = Join-Path $repoRoot "docs/odak/project_management/scripts/lib/TeslimatInstallCommon.ps1"
$manifestPath = Join-Path $repoRoot "docs/odak/project_management/install/manifest.json"
. $installLib

if (-not $UseGateway) {
    Write-Host "UseGateway:$false is no longer a separate path; gateway /data/api/v1 is required." -ForegroundColor Yellow
}

$token = Get-TeslimatToken -Token $Token -BaseUrl $BaseUrl -RepoRoot $repoRoot
if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "Token bulunamadi. -Token parametresi verin veya `$env:DI_TOKEN ayarlayin." -ForegroundColor Red
    exit 1
}
$headers = New-TeslimatDgHeaders -Token $token
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

Write-Host ''
Write-Host "Document Intelligence - DG category + datasets ($BaseUrl)" -ForegroundColor Cyan
Write-Host ''

$catFile = Join-Path $repoRoot $manifest.category.file
$categoryId = Ensure-TeslimatDatasetCategory -BaseUrl $BaseUrl -Headers $headers -CategoryFile $catFile -WhatIf:$WhatIf
$byName = Import-TeslimatSchemaMap -RepoRoot $repoRoot -RelativeFiles @($manifest.schemaFiles)

foreach ($name in @($manifest.datasetOrder)) {
    if (-not $byName.ContainsKey($name)) {
        Write-Host "  Eksik tanim: $name" -ForegroundColor Red
        exit 1
    }
    Write-Host $name -ForegroundColor Yellow
    $null = Ensure-TeslimatDataset -BaseUrl $BaseUrl -Headers $headers -Schema $byName[$name] -CategoryId $categoryId -WhatIf:$WhatIf
}

Write-Host ''
Write-Host "Tamamlandi. Category ID: $categoryId" -ForegroundColor Cyan
Write-Host "Dogrulama + core seed: install-teslimat-omurgasi.ps1" -ForegroundColor Gray
