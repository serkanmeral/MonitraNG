# Document Intelligence — test ortami (192.168.20.20) tam bootstrap
#
# Dataset'ler + tasarimci kategorileri + CoC sablonu + ornek dokuman agaci + yan menu.
#
# Kullanim (repo kokunden):
#   .\docs\odak\document_intelligence\scripts\seed-document-intelligence-test.ps1
#   .\docs\odak\document_intelligence\scripts\seed-document-intelligence-test.ps1 -WhatIf
#   .\docs\odak\document_intelligence\scripts\seed-document-intelligence-test.ps1 -SkipResources
#   .\docs\odak\document_intelligence\scripts\seed-document-intelligence-test.ps1 -CocReplace

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Server = "192.168.20.20",
    [string]$Token = $env:DI_TOKEN,
    [switch]$WhatIf = $false,
    [switch]$SkipDatasets = $false,
    [switch]$SkipCategories = $false,
    [switch]$SkipCoc = $false,
    [switch]$SkipResources = $false,
    [switch]$CocReplace = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

$token = $Token
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token yok. Once get-operationcore-token.ps1 calistirin veya -Token / `$env:DI_TOKEN kullanin." -ForegroundColor Red
    exit 1
}
$env:DI_TOKEN = $token.Trim()

function Invoke-Step {
    param(
        [string]$Title,
        [string]$ScriptName,
        [hashtable]$ExtraParams = @{}
    )
    $path = Join-Path $scriptDir $ScriptName
    if (-not (Test-Path $path)) { throw "Script yok: $path" }

    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
    $params = @{
        BaseUrl = $BaseUrl
        Token   = $env:DI_TOKEN
    }
    foreach ($k in $ExtraParams.Keys) { $params[$k] = $ExtraParams[$k] }
    if ($WhatIf -and $ScriptName -notmatch "setup-document-intelligence-datasets") {
        $params.WhatIf = $true
    }

    & $path @params
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "Adim basarisiz: $ScriptName (exit $LASTEXITCODE)"
    }
}

Write-Host ""
Write-Host "Document Intelligence TEST bootstrap" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Gray
if ($WhatIf) { Write-Host "(WhatIf — yazma adimlari atlanir veya dry-run)" -ForegroundColor Yellow }
Write-Host ""

if (-not $SkipDatasets) {
    Invoke-Step -Title "1/7 Dataset category + dm_* schemas" -ScriptName "setup-document-intelligence-datasets.ps1"
    Invoke-Step -Title "2/7 dm_document_templates schema patch" -ScriptName "patch-document-intelligence-templates-dataset.ps1"
    Invoke-Step -Title "3/9 dm_document_context_types seed (G3)" -ScriptName "seed-dm-document-context-types.ps1"
    Invoke-Step -Title "4/9 dm_data_sources seed (G4)" -ScriptName "seed-dm-data-sources.ps1"
    Invoke-Step -Title "5/9 dm_document_producers seed (G4)" -ScriptName "seed-dm-document-producers.ps1"
}
else {
    Write-Host "SKIP datasets (SkipDatasets)" -ForegroundColor Yellow
}

if (-not $SkipCategories) {
    Invoke-Step -Title "6/9 Template category tree" -ScriptName "seed-designer-template-categories.ps1"
}
else {
    Write-Host "SKIP categories (SkipCategories)" -ForegroundColor Yellow
}

if (-not $SkipCoc) {
    $cocParams = @{}
    if ($CocReplace) { $cocParams.Replace = $true }
    if ($WhatIf) { $cocParams.WhatIf = $true }
    Invoke-Step -Title "7/9 COC-STANDARD template" -ScriptName "seed-designer-template-coc-standard.ps1" -ExtraParams $cocParams
}
else {
    Write-Host "SKIP CoC template (SkipCoc)" -ForegroundColor Yellow
}

if (-not $SkipResources) {
    Invoke-Step -Title "8/10 Ust klasorler (Sayfalar / Dokumanlar)" -ScriptName "seed-resource-root-folders.ps1" -ExtraParams @{ Server = $Server }
    Invoke-Step -Title "9/10 MonitraNG tutorials (dm_resources)" -ScriptName "seed-monitrang-tutorials.ps1" -ExtraParams @{ Server = $Server }
    Invoke-Step -Title "10/10 Side menu entry" -ScriptName "patch-document-side-menu.ps1"
}
else {
    Write-Host "SKIP resources + side menu (SkipResources)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Test bootstrap tamamlandi." -ForegroundColor Green
Write-Host "Dogrulama:" -ForegroundColor Cyan
Write-Host "  Belge yoneticisi: /apps/document-intelligence" -ForegroundColor Gray
Write-Host "  Belge tasarimcisi: /apps/document-intelligence/designer" -ForegroundColor Gray
