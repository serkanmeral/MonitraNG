# Odak Egitim — DG dataset kurulumu (birim, egitim, katilim)
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\egitim\scripts\setup-odak-egitim-datasets.ps1
#
# Production:
#   $env:MNG_OC_USE_PROD_TOKEN = "1"; .\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
#   .\docs\odak\egitim\scripts\setup-odak-egitim-datasets.ps1 -BaseUrl "http://192.168.20.8:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$datasetDir = Join-Path $repoRoot "docs/odak/egitim/datasets"
$categoryFile = Join-Path $repoRoot "docs/odak/is_surecleri/datasets/odak_business_dataset_category.json"

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }

$ocScripts = Join-Path $repoRoot "docs/odak/operationcore/scripts"
$loadTokenScript = Join-Path $ocScripts "load-operationcore-token.ps1"
if (-not (Test-Path $loadTokenScript)) { throw "Token script yok: $loadTokenScript" }
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi. Once get-operationcore-token.ps1 calistirin." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$irmParams = @{ Headers = $headers; ErrorAction = "Stop" }
if ($BaseUrl.StartsWith("https://") -and (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" })) {
    $irmParams.SkipCertificateCheck = $true
}

function Invoke-Dg {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [object]$Body = $null
    )
    $params = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($Uri.StartsWith("https://") -and $irmParams.ContainsKey("SkipCertificateCheck")) {
        $params.SkipCertificateCheck = $true
    }
    if ($null -ne $Body) {
        $params.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 25 -Compress }
        $params.ContentType = "application/json"
    }
    return Invoke-RestMethod @params
}

function Get-Items {
    param($Response)
    if (-not $Response) { return @() }
    if ($Response -is [Array]) { return $Response }
    foreach ($prop in @("data", "Data", "items", "Items", "results", "Results")) {
        if ($null -ne $Response.$prop) {
            $items = $Response.$prop
            if ($items -is [Array]) { return $items }
            return @($items)
        }
    }
    return @($Response)
}

function Ensure-DatasetCategory {
    param([string]$CategoryName)
    $listUri = '{0}{1}?pageSize=200' -f $BaseUrl, $categoriesPath
    $found = @(Get-Items (Invoke-Dg -Method GET -Uri $listUri)) | Where-Object { $_.categoryName -eq $CategoryName } | Select-Object -First 1
    if ($found) {
        $id = $found.dataId; if (-not $id) { $id = $found.__dataId }
        Write-Host "  Category mevcut: $CategoryName ($id)" -ForegroundColor Yellow
        return $id
    }
    $cat = Get-Content $categoryFile -Raw -Encoding UTF8 | ConvertFrom-Json
    try {
        Invoke-Dg -Method POST -Uri "$BaseUrl$categoriesPath" -Body @{
            categoryName        = $cat.categoryName
            categoryDescription = $cat.categoryDescription
            isSystemCategory    = $false
        } | Out-Null
    }
    catch {
        Write-Host "  Category POST (devam): $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
    $found2 = @(Get-Items (Invoke-Dg -Method GET -Uri $listUri)) | Where-Object { $_.categoryName -eq $CategoryName } | Select-Object -First 1
    if (-not $found2) { throw "Category olusturulamadi: $CategoryName" }
    $id = $found2.dataId; if (-not $id) { $id = $found2.__dataId }
    Write-Host "  OK: Category $CategoryName -> $id" -ForegroundColor Green
    return $id
}

function Ensure-DatasetFromJson {
    param([string]$JsonPath, [string]$CategoryId)
    $schema = Get-Content $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $getUri = '{0}{1}/{2}' -f $BaseUrl, $datasetsPath, [Uri]::EscapeDataString($schema.name)
    $exists = $false
    try {
        $null = Invoke-Dg -Method GET -Uri $getUri
        $exists = $true
    }
    catch { }

    $fields = @($schema.fields | ForEach-Object {
        $f = @{
            fieldType = $_.fieldType
            name      = $_.name
            title     = $_.title
            mandatory = $_.mandatory
            unique    = $_.unique
            isArray   = $_.isArray
        }
        if ($_.relationDataset) { $f.relationDataset = $_.relationDataset }
        if ($null -ne $_.defaultValue) { $f.defaultValue = $_.defaultValue }
        if ($_.options) { $f.options = $_.options }
        if ($_.validation) { $f.validation = $_.validation }
        $f
    })

    if ($exists) {
        $body = @{
            Description = $schema.description
            ForceSchema = $schema.forceSchema
            Logging     = $schema.logging
            PublishMode = $schema.publish_mode
            Fields      = $fields
            IndexList   = @($schema.indexList)
        }
        Invoke-Dg -Method PUT -Uri $getUri -Body $body | Out-Null
        Write-Host "  SYNC: $($schema.name)" -ForegroundColor Green
        return
    }

    $body = @{
        Name        = $schema.name
        Description = $schema.description
        Category    = $CategoryId
        ForceSchema = $schema.forceSchema
        Logging     = $schema.logging
        PublishMode = $schema.publish_mode
        Fields      = $fields
        IndexList   = @($schema.indexList)
    }
    Invoke-Dg -Method POST -Uri "$BaseUrl$datasetsPath" -Body $body | Out-Null
    Write-Host "  OK: $($schema.name) olusturuldu" -ForegroundColor Green
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Odak Egitim — dataset kurulumu" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$categoryId = Ensure-DatasetCategory -CategoryName "BusinessDatasets"

foreach ($file in @(
        "odak_birimler_dataset.json",
        "odak_egitimler_dataset.json",
        "odak_egitim_katilimlari_dataset.json"
    )) {
    $path = Join-Path $datasetDir $file
    if (-not (Test-Path $path)) { throw "Dataset dosyasi yok: $path" }
    Ensure-DatasetFromJson -JsonPath $path -CategoryId $categoryId
}

Write-Host "`nTamamlandi. Sonraki: patch-odak-egitim-side-menu.ps1" -ForegroundColor Cyan
