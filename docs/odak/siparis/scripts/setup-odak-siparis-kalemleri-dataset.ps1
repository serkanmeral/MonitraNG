# Odak Siparis — odak_siparis_kalemleri dataset + Automated Form kurulumu
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\siparis\scripts\setup-odak-siparis-kalemleri-dataset.ps1
#
# Runtime (hub entegrasyonu oncesi test):
#   /apps/automated-forms/view/odak-siparis-kalemleri-form
#
# Not: sideMenuConfig.enabled=false — kalemler hub detay sekmesinden acilacak.

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$SkipSchema = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$datasetDir = Join-Path $repoRoot "docs/odak/siparis/datasets"
$categoryFile = Join-Path $repoRoot "docs/odak/is_surecleri/datasets/odak_business_dataset_category.json"
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$datasetFile = Join-Path $datasetDir "odak_siparis_kalemleri_dataset.json"
$formFile = Join-Path $datasetDir "odak_siparis_kalemleri_automated_form.json"

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }

if (-not (Test-Path $ocTokenScript)) { throw "Token script yok: $ocTokenScript" }
if (-not (Test-Path $datasetFile)) { throw "Dataset dosyasi yok: $datasetFile" }
if (-not (Test-Path $formFile)) { throw "Form dosyasi yok: $formFile" }

$token = & $ocTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [object]$Body = $null
    )
    $params = @{
        Uri         = $Uri
        Method      = $Method
        Headers     = $headers
        ErrorAction = "Stop"
    }
    if ($Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
        $params.SkipCertificateCheck = $true
    }
    if ($null -ne $Body) {
        $params.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 30 -Compress }
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

function Get-DataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    return $id
}

function Convert-FieldOptions {
    param($Options)
    if (-not $Options) { return $null }
    $lookup = $Options.lookup
    if (-not $lookup) { return $null }
    if ($lookup.staticItems) {
        $items = @($lookup.staticItems | ForEach-Object {
            @{ value = [string]$_.value; label = [string]$_.label }
        })
        return @{
            lookup = @{
                source      = [string]$lookup.source
                staticItems = $items
            }
        }
    }
    if ($lookup.source) {
        return @{ lookup = @{ source = [string]$lookup.source } }
    }
    return $null
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
    if (-not (Test-Path $categoryFile)) { throw "Category file yok: $categoryFile" }
    $cat = Get-Content $categoryFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $body = @{
        categoryName        = $cat.categoryName
        categoryDescription = $cat.categoryDescription
        isSystemCategory    = $false
    }
    try { Invoke-Dg -Method POST -Uri "$BaseUrl$categoriesPath" -Body $body | Out-Null } catch { }
    $found2 = @(Get-Items (Invoke-Dg -Method GET -Uri $listUri)) | Where-Object { $_.categoryName -eq $CategoryName } | Select-Object -First 1
    if (-not $found2) { throw "Category olusturulamadi: $CategoryName" }
    $id = $found2.dataId; if (-not $id) { $id = $found2.__dataId }
    Write-Host "  OK: Category $CategoryName -> $id" -ForegroundColor Green
    return $id
}

function Ensure-DatasetSchema {
    param(
        [string]$CategoryId,
        [string]$DatasetFilePath
    )
    $schema = Get-Content $DatasetFilePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $name = $schema.name
    $getUri = '{0}{1}/{2}' -f $BaseUrl, $datasetsPath, [Uri]::EscapeDataString($name)
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
        if ($_.defaultValue -ne $null) { $f.defaultValue = $_.defaultValue }
        if ($_.validation) { $f.validation = $_.validation }
        $fieldOptions = Convert-FieldOptions -Options $_.options
        if ($fieldOptions) { $f.options = $fieldOptions }
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
        Write-Host "  SYNC: dataset $name ($($fields.Count) alan)" -ForegroundColor Green
    }
    else {
        $body = @{
            Name        = $name
            Description = $schema.description
            Category    = $CategoryId
            ForceSchema = $schema.forceSchema
            Logging     = $schema.logging
            PublishMode = $schema.publish_mode
            Fields      = $fields
            IndexList   = @($schema.indexList)
        }
        Invoke-Dg -Method POST -Uri "$BaseUrl$datasetsPath" -Body $body | Out-Null
        Write-Host "  OK: dataset $name olusturuldu" -ForegroundColor Green
    }
}

function Ensure-AutomatedForm {
    param([string]$FormFilePath)
    $formDef = Get-Content $FormFilePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $formCode = $formDef.formCode
    $filter = "formCode:eq:$formCode"
    $existing = @(Get-Items (Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/@automated_forms?limit=1&filter=$([Uri]::EscapeDataString($filter))"))
    $body = @{
        formName       = $formDef.formName
        formCode       = $formDef.formCode
        description    = $formDef.description
        datasetName    = $formDef.datasetName
        isActive       = $formDef.isActive
        sideMenuConfig = $formDef.sideMenuConfig
        listConfig     = $formDef.listConfig
        formConfig     = $formDef.formConfig
    }
    if ($existing.Count -gt 0) {
        $id = $existing[0].__dataId; if (-not $id) { $id = $existing[0].dataId }
        Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/@automated_forms/$id" -Body $body | Out-Null
        Write-Host "  SYNC: $formCode ($id)" -ForegroundColor Yellow
    }
    else {
        $created = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/@automated_forms" -Body $body
        $id = Get-DataId $created
        Write-Host "  OK: $formCode -> $id" -ForegroundColor Green
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Odak Siparis — odak_siparis_kalemleri" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if (-not $SkipSchema) {
    Write-Host "[1] Dataset semasi..." -ForegroundColor Yellow
    $categoryId = Ensure-DatasetCategory -CategoryName "BusinessDatasets"
    Ensure-DatasetSchema -CategoryId $categoryId -DatasetFilePath $datasetFile
}
else {
    Write-Host "[1] Dataset sema atlandi (-SkipSchema)" -ForegroundColor Gray
}

Write-Host "[2] @automated_forms..." -ForegroundColor Yellow
Ensure-AutomatedForm -FormFilePath $formFile

Write-Host ""
Write-Host "Tamamlandi." -ForegroundColor Cyan
Write-Host "AF runtime: /apps/automated-forms/view/odak-siparis-kalemleri-form" -ForegroundColor Gray
Write-Host "Sonraki: migrate-legacy-package-to-dg.ps1 (MO yok)" -ForegroundColor Gray
