# Reporting catalog meta datasets: @reporting_categories + @reporting_reports
#
# Usage (repo root):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   $env:DI_TOKEN = (Get-Content $env:TEMP\operationcore_dg_token.txt -Raw).Trim()
#   .\docs\odak\reporting_services\scripts\setup-reporting-catalog-datasets.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$schemaFile = Join-Path $scriptDir "..\datasets\reporting_catalog_datasets.json"

if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "Token yok. DI_TOKEN veya get-operationcore-token.ps1 kullanin." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization  = "Bearer $Token"
    "Content-Type" = "application/json"
}

$datasetsPath = "$BaseUrl/data/api/v1/datasets"

# Resolve SystemDatasets category from @side_menu (or @automated_forms)
$categoryId = $null
foreach ($probe in @("@side_menu", "@automated_forms")) {
    try {
        $ds = Invoke-RestMethod -Uri "$datasetsPath/$probe" -Headers $headers -Method GET
        if ($ds.category) { $categoryId = [string]$ds.category }
        elseif ($ds.Category) { $categoryId = [string]$ds.Category }
        if ($categoryId) {
            Write-Host "Category from ${probe}: $categoryId" -ForegroundColor Gray
            break
        }
    } catch { }
}

if (-not $categoryId) {
    Write-Host "SystemDatasets category bulunamadi (@side_menu)." -ForegroundColor Red
    exit 1
}

$schemaDoc = Get-Content $schemaFile -Raw -Encoding UTF8 | ConvertFrom-Json

function Upsert-Dataset {
    param($Schema)

    $name = [string]$Schema.Name
    $body = @{
        Name        = $name
        Description = [string]$Schema.Description
        Category    = $categoryId
        ForceSchema = $true
        Logging     = [string]$Schema.Logging
        PublishMode = [string]$Schema.PublishMode
        Fields      = @($Schema.Fields)
        IndexList   = @($Schema.IndexList)
    }
    $json = $body | ConvertTo-Json -Depth 20 -Compress

    $exists = $false
    try {
        $null = Invoke-RestMethod -Uri "$datasetsPath/$name" -Headers $headers -Method GET
        $exists = $true
    } catch { }

    if ($exists) {
        Write-Host "PUT $name ..." -ForegroundColor Yellow
        Invoke-RestMethod -Uri "$datasetsPath/$name" -Headers $headers -Method PUT -Body $json | Out-Null
        Write-Host "  OK updated" -ForegroundColor Green
    } else {
        Write-Host "POST $name ..." -ForegroundColor Yellow
        Invoke-RestMethod -Uri "$datasetsPath" -Headers $headers -Method POST -Body $json | Out-Null
        Write-Host "  OK created" -ForegroundColor Green
    }
}

foreach ($ds in @($schemaDoc.datasets)) {
    Upsert-Dataset -Schema $ds
}

Write-Host "`nTamam. Datasetler: @reporting_categories, @reporting_reports" -ForegroundColor Cyan
