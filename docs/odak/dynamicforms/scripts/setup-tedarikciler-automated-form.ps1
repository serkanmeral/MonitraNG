# tedarikciler dataset (zenginlestirme) + seed + Automated Form
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\dynamicforms\scripts\setup-tedarikciler-automated-form.ps1
#
# Form runtime: http://192.168.20.20:3000/apps/automated-forms/view/tedarikciler-form

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$SkipSeed = $false,
    [switch]$SkipForm = $false,
    [switch]$SkipSchema = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$categoryFile = Join-Path $repoRoot "docs/odak/operationcore/datasets/tedarikciler_dataset_category.json"
$datasetFile = Join-Path $repoRoot "docs/odak/operationcore/datasets/tedarikciler_dataset.json"
$seedFile = Join-Path $repoRoot "docs/odak/dynamicforms/datasets/tedarikciler_seed.json"
$formFile = Join-Path $repoRoot "docs/odak/dynamicforms/datasets/tedarikciler_automated_form.json"
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }

if (-not (Test-Path $ocTokenScript)) { throw "Token script yok: $ocTokenScript" }
$token = & $ocTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

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
    $params = @{
        Uri         = $Uri
        Method      = $Method
        Headers     = $headers
        ErrorAction = "Stop"
    }
    if ($Uri.StartsWith("https://") -and $irmParams.ContainsKey("SkipCertificateCheck")) {
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

function Ensure-DatasetCategory {
    param([string]$CategoryName)
    $listUri = '{0}{1}?pageSize=200' -f $BaseUrl, $categoriesPath
    $items = @(Get-Items (Invoke-Dg -Method GET -Uri $listUri))
    $found = $items | Where-Object { $_.categoryName -eq $CategoryName } | Select-Object -First 1
    if ($found) {
        $id = $found.dataId; if (-not $id) { $id = $found.__dataId }
        Write-Host "  Category mevcut: $CategoryName ($id)" -ForegroundColor Yellow
        return $id
    }
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
        return @{
            lookup = @{
                source = [string]$lookup.source
            }
        }
    }
    return $null
}

function Ensure-DatasetSchema {
    param([string]$CategoryId)
    $schema = Get-Content $datasetFile -Raw -Encoding UTF8 | ConvertFrom-Json
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
        Write-Host "  SYNC: dataset $name semasi guncellendi ($($fields.Count) alan)" -ForegroundColor Green
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

function Upsert-Supplier {
    param([hashtable]$Record)
    $kod = $Record.kod
    $filter = "kod:eq:$kod"
    $existing = @(Get-Items (Invoke-Dg -Method GET -Uri "$BaseUrl$dataPath/tedarikciler?limit=1&filter=$([Uri]::EscapeDataString($filter))"))
    $body = @{}
    foreach ($key in $Record.Keys) {
        if ($key -eq "anaTedarikciKod") { continue }
        $body[$key] = $Record[$key]
    }
    if ($existing.Count -gt 0) {
        $id = $existing[0].__dataId; if (-not $id) { $id = $existing[0].dataId }
        Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/tedarikciler/$id" -Body $body | Out-Null
        Write-Host "  SYNC: $kod ($id)" -ForegroundColor Yellow
        return $id
    }
    $created = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/tedarikciler" -Body $body
    $id = Get-DataId $created
    Write-Host "  OK: $kod -> $id" -ForegroundColor Green
    return $id
}

function Ensure-AutomatedForm {
    $formDef = Get-Content $formFile -Raw -Encoding UTF8 | ConvertFrom-Json
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
        Write-Host "  SYNC: automated form $formCode ($id)" -ForegroundColor Yellow
    }
    else {
        $created = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/@automated_forms" -Body $body
        $id = Get-DataId $created
        Write-Host "  OK: automated form $formCode -> $id" -ForegroundColor Green
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "tedarikciler - dataset + Automated Form" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "[1] Dataset kategori + sema..." -ForegroundColor Yellow
$categoryId = Ensure-DatasetCategory -CategoryName "BusinessDatasets"
if (-not $SkipSchema) {
    Ensure-DatasetSchema -CategoryId $categoryId
}
else {
    Write-Host "  Schema atlandi (-SkipSchema)" -ForegroundColor Gray
}

if (-not $SkipSeed) {
    Write-Host "[2] tedarikciler seed..." -ForegroundColor Yellow
    $seed = Get-Content $seedFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $idByKod = @{}
    foreach ($row in $seed) {
        $ht = @{}
        $row.PSObject.Properties | ForEach-Object { $ht[$_.Name] = $_.Value }
        $id = Upsert-Supplier -Record $ht
        $idByKod[$row.kod] = $id
    }
    foreach ($row in $seed) {
        if (-not $row.anaTedarikciKod) { continue }
        $parentKod = $row.anaTedarikciKod
        if (-not $idByKod.ContainsKey($row.kod) -or -not $idByKod.ContainsKey($parentKod)) { continue }
        $childId = $idByKod[$row.kod]
        $parentId = $idByKod[$parentKod]
        Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/tedarikciler/$childId" -Body @{
            anaTedarikciId = $parentId
        } | Out-Null
        Write-Host "  LINK: $($row.kod) -> ana tedarikci $parentKod" -ForegroundColor Green
    }
}
else {
    Write-Host "[2] Seed atlandi (-SkipSeed)" -ForegroundColor Gray
}

if (-not $SkipForm) {
    Write-Host "[3] @automated_forms tedarikciler-form..." -ForegroundColor Yellow
    Ensure-AutomatedForm
}
else {
    Write-Host "[3] Form atlandi (-SkipForm)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Tamamlandi." -ForegroundColor Cyan
Write-Host ('Form listesi:  {0} -> UI /apps/automated-forms' -f $BaseUrl) -ForegroundColor Gray
Write-Host 'Form runtime:  /apps/automated-forms/view/tedarikciler-form' -ForegroundColor Gray
Write-Host 'Builder:       /apps/automated-forms/edit/tedarikciler-form' -ForegroundColor Gray
