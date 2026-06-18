# Odak Siparis — hub ayarlari + bildirim politikasi dataset semalari
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\siparis\scripts\setup-odak-siparis-hub-settings.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$datasetDir = Join-Path $repoRoot "docs/odak/siparis/datasets"
$categoryFile = Join-Path $repoRoot "docs/odak/is_surecleri/datasets/odak_business_dataset_category.json"
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }

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

function Ensure-DatasetCategory {
    param([string]$CategoryName)
    $listUri = '{0}{1}?pageSize=200' -f $BaseUrl, $categoriesPath
    $found = @(Get-Items (Invoke-Dg -Method GET -Uri $listUri)) | Where-Object { $_.categoryName -eq $CategoryName } | Select-Object -First 1
    if ($found) {
        $id = $found.dataId; if (-not $id) { $id = $found.__dataId }
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
        Write-Host "  SYNC: $name ($($fields.Count) alan)" -ForegroundColor Green
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
        Write-Host "  OK: $name olusturuldu" -ForegroundColor Green
    }
}

Write-Host "`n=== setup-odak-siparis-hub-settings ===" -ForegroundColor Cyan
$categoryId = Ensure-DatasetCategory -CategoryName "BusinessDatasets"
Ensure-DatasetSchema -CategoryId $categoryId -DatasetFilePath (Join-Path $datasetDir "odak_siparis_hub_ayarlari_dataset.json")
Ensure-DatasetSchema -CategoryId $categoryId -DatasetFilePath (Join-Path $datasetDir "odak_siparis_notification_policies_dataset.json")
Write-Host "`nTamamlandi. UI: /apps/odak-siparis/packages/settings" -ForegroundColor Cyan
