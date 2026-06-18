# Odak Siparis — odak_musteri_kalite_isterleri + odak_kalite_isteri_sablonlari + kalem qualityRequirementIds
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\siparis\scripts\setup-odak-musteri-kalite-isterleri-dataset.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$SeedTemplates = $true,
    [switch]$SkipKalemleri = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$datasetDir = Join-Path $repoRoot "docs/odak/siparis/datasets"
$categoryFile = Join-Path $repoRoot "docs/odak/is_surecleri/datasets/odak_business_dataset_category.json"
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
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

function Convert-FieldOptions {
    param($Options)
    if (-not $Options) { return $null }
    $lookup = $Options.lookup
    if (-not $lookup) { return $null }
    if ($lookup.staticItems) {
        $items = @($lookup.staticItems | ForEach-Object {
            @{ value = [string]$_.value; label = [string]$_.label }
        })
        return @{ lookup = @{ source = [string]$lookup.source; staticItems = $items } }
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

function Seed-QualityTemplates {
    $dataset = "odak_kalite_isteri_sablonlari"
    $listUri = '{0}{1}/{2}?limit=1' -f $BaseUrl, $dataPath, [Uri]::EscapeDataString($dataset)
    $existing = @(Get-Items (Invoke-Dg -Method GET -Uri $listUri))
    if ($existing.Count -gt 0) {
        Write-Host "  Sablon seed atlandi (kayit var)" -ForegroundColor Gray
        return
    }
    $templates = @(
        @{ kod = "KI-COC"; ad = "COC / Uygunluk Belgesi"; aciklama = "Certificate of Conformance"; faiUygulanacak = $false; sektor = "genel"; sira = 10; aktif = $true },
        @{ kod = "KI-FAI"; ad = "First Article Inspection"; aciklama = "Ilk parca muayenesi"; faiUygulanacak = $true; sektor = "havacilik"; sira = 20; aktif = $true },
        @{ kod = "KI-MTR"; ad = "Malzeme Test Raporu (MTR)"; aciklama = "Material test report"; faiUygulanacak = $false; sektor = "genel"; sira = 30; aktif = $true },
        @{ kod = "KI-AS9102"; ad = "AS9102 FAI Formu"; aciklama = "Havacilik FAI dokumantasyonu"; faiUygulanacak = $true; sektor = "havacilik"; sira = 40; aktif = $true },
        @{ kod = "KI-ROHS"; ad = "RoHS Uygunluk"; aciklama = "RoHS declaration"; faiUygulanacak = $false; sektor = "diger"; sira = 50; aktif = $true }
    )
    foreach ($t in $templates) {
        $createUri = '{0}{1}/{2}' -f $BaseUrl, $dataPath, [Uri]::EscapeDataString($dataset)
        Invoke-Dg -Method POST -Uri $createUri -Body $t | Out-Null
    }
    Write-Host "  OK: $($templates.Count) sablon seed" -ForegroundColor Green
}

Write-Host "`n=== setup-odak-musteri-kalite-isterleri-dataset ===" -ForegroundColor Cyan
$categoryId = Ensure-DatasetCategory -CategoryName "BusinessDatasets"
Ensure-DatasetSchema -CategoryId $categoryId -DatasetFilePath (Join-Path $datasetDir "odak_musteri_kalite_isterleri_dataset.json")
Ensure-DatasetSchema -CategoryId $categoryId -DatasetFilePath (Join-Path $datasetDir "odak_kalite_isteri_sablonlari_dataset.json")
if (-not $SkipKalemleri) {
    Ensure-DatasetSchema -CategoryId $categoryId -DatasetFilePath (Join-Path $datasetDir "odak_siparis_kalemleri_dataset.json")
} else {
    Write-Host "  Kalemler semasi atlandi (-SkipKalemleri)" -ForegroundColor Gray
}
if ($SeedTemplates) { Seed-QualityTemplates }
Write-Host "`nTamamlandi. Hub: Musteriler -> expand -> Kalite Isterleri." -ForegroundColor Cyan
Write-Host "Tek basina kalem alani icin: setup-odak-siparis-kalemleri-dataset.ps1" -ForegroundColor Gray
