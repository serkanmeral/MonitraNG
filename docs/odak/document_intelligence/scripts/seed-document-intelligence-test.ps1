# Document Intelligence — test/demo bootstrap (Odak 192.168.20.20)
#
# F1-0: Teslimat Omurgasi installer + Odak ornek icerik.
# Yeni ortam (musteri) icin core-only:
#   .\docs\odak\project_management\scripts\install-teslimat-omurgasi.ps1
#
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
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$installer = Join-Path $repoRoot "docs/odak/project_management/scripts/install-teslimat-omurgasi.ps1"

$skipIds = [System.Collections.Generic.List[string]]::new()
if ($SkipCategories) { $skipIds.Add("template-categories") | Out-Null }
if ($SkipResources) {
    $skipIds.Add("resource-roots") | Out-Null
    $skipIds.Add("side-menu") | Out-Null
    $skipIds.Add("tutorials") | Out-Null
}
if ($SkipCoc) { $skipIds.Add("coc-standard") | Out-Null }

$seedExtra = @{
    tutorials = @{ Server = $Server }
}
if ($CocReplace) { $seedExtra["coc-standard"] = @{ Replace = $true } }

Write-Host "Delegating to Teslimat Omurgasi installer" -ForegroundColor Cyan

$installParams = @{
    BaseUrl            = $BaseUrl
    Token              = $Token
    IncludeOdakContent = (-not $SkipCoc -or -not $SkipResources)
    WhatIf             = $WhatIf
    SkipSeedIds        = @($skipIds)
    SeedExtraParams    = $seedExtra
}
if ($SkipDatasets) {
    $installParams.SkipSeeds = $false
    Write-Host "SkipDatasets: schema sync yine calisir (eksik dataset/alan kapanir). Eski davranis artik yok." -ForegroundColor Yellow
}

& $installer @installParams
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
