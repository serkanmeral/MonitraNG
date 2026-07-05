# Document Intelligence (DM) - Dataset category + dm_* schemas (Odak API Gateway)
# Ref: docs/odak/document_intelligence/MonitraNG_Document_Intelligence_Planning.md
#
# Kullanim (repo kokunden veya bu klasorden):
#   $env:DI_TOKEN = "<bearer-token>"
#   .\docs\odak\document_intelligence\scripts\setup-document-intelligence-datasets.ps1
#
# veya:
#   .\setup-document-intelligence-datasets.ps1 -Token "<bearer-token>" -BaseUrl "http://192.168.20.20:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [string]$Token = $env:DI_TOKEN
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$categoryFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/documentintelligence_dataset_category.json"
$datasetsFile = Join-Path $repoRoot "docs/odak/document_intelligence/datasets/documentintelligence_datasets_phase1.json"

$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }

if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "Token bulunamadi. -Token parametresi verin veya `$env:DI_TOKEN ayarlayin." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $Token"
    "Content-Type"  = "application/json"
}

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$useCurl = $BaseUrl.StartsWith("https://") -and (Get-Command curl.exe -ErrorAction SilentlyContinue)

function Invoke-DgPost {
    param([string]$Uri, [string]$BodyJson, [string]$Label)
    if ($useCurl) {
        $bodyFile = [System.IO.Path]::GetTempFileName()
        try {
            $BodyJson | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = & curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $Token" -H "Content-Type: application/json" -d "@$bodyFile" $Uri 2>&1 | Out-String
            $lines = ($output.Trim() -split "`n")
            $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]', '').Trim() } else { "" }
            $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count - 2)] -join "`n").Trim() } else { "" }
            if ($httpCode -in @("200", "201")) { return @{ Ok = $true; Body = $responseBody } }
            if ($httpCode -eq "409" -or ($httpCode -eq "400" -and $responseBody -match "mevcut|already|zaten|duplicate|unique")) {
                return @{ Ok = $true; Skipped = $true; Body = $responseBody }
            }
            return @{ Ok = $false; Code = $httpCode; Body = $responseBody }
        }
        finally {
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
        }
    }
    try {
        $irm = @{ Uri = $Uri; Method = "POST"; Headers = $headers; Body = $BodyJson }
        if ($Uri.StartsWith("https://")) { $irm.SkipCertificateCheck = $true }
        $null = Invoke-RestMethod @irm
        return @{ Ok = $true }
    }
    catch {
        $statusCode = $null
        try { $statusCode = [int]$_.Exception.Response.StatusCode } catch { }
        $errMsg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        if ($statusCode -eq 409 -or ($statusCode -eq 400 -and $errMsg -match "mevcut|already|zaten|duplicate|unique")) {
            return @{ Ok = $true; Skipped = $true; Body = $errMsg }
        }
        return @{ Ok = $false; Code = $statusCode; Body = $errMsg }
    }
}

function ConvertTo-DgDatasetBody {
    param($Schema, [string]$CategoryId)
    @{
        Name        = $Schema.name
        Description = $Schema.description
        Category    = $CategoryId
        ForceSchema = $Schema.forceSchema
        Logging     = $Schema.logging
        PublishMode = $Schema.publish_mode
        Fields      = $Schema.fields
        Validations = $Schema.validations
        Queries     = $Schema.queries
        IndexList   = $Schema.indexList
    }
}

Write-Host ''
Write-Host "Document Intelligence - DG category + datasets ($BaseUrl)" -ForegroundColor Cyan
Write-Host ''

if (-not (Test-Path $categoryFile)) { throw "Missing: $categoryFile" }
if (-not (Test-Path $datasetsFile)) { throw "Missing: $datasetsFile" }

Write-Host '1) Dataset category: DocumentIntelligenceDatasets' -ForegroundColor Yellow
$cat = Get-Content $categoryFile -Raw -Encoding UTF8 | ConvertFrom-Json
$catBody = @{
    categoryName        = $cat.categoryName
    categoryDescription = $cat.categoryDescription
    isSystemCategory    = $cat.isSystemCategory
} | ConvertTo-Json -Compress

$catUri = "$BaseUrl$categoriesPath"
$r = Invoke-DgPost -Uri $catUri -BodyJson $catBody -Label "category"
if ($r.Ok) {
    Write-Host "  DocumentIntelligenceDatasets OK$(if ($r.Skipped) { ' (zaten var)' })" -ForegroundColor Green
}
else {
    Write-Host "  HATA category HTTP $($r.Code)" -ForegroundColor Red
    if ($r.Body) { Write-Host "  $($r.Body)" -ForegroundColor Gray }
    exit 1
}

$categoryId = $null
$listUri = '{0}{1}?pageSize=100&search=DocumentIntelligence' -f $BaseUrl, $categoriesPath
try {
    $irmGet = @{ Uri = $listUri; Method = "GET"; Headers = $headers }
    if ($listUri.StartsWith("https://")) { $irmGet.SkipCertificateCheck = $true }
    $list = Invoke-RestMethod @irmGet
    $items = $list.items
    if (-not $items) { $items = $list.data }
    if ($items) {
        $found = $items | Where-Object { $_.categoryName -eq "DocumentIntelligenceDatasets" } | Select-Object -First 1
        if ($found) { $categoryId = $found.dataId }
    }
}
catch {
    Write-Host "  Uyari: kategori listesi alinamadi, JSON category ID kullanilacak." -ForegroundColor Yellow
}
if ([string]::IsNullOrEmpty($categoryId)) {
    $categoryId = $cat.'__dataId'
    Write-Host "  Category ID (JSON sabit): $categoryId" -ForegroundColor Yellow
}
else {
    Write-Host "  Category ID (DG): $categoryId" -ForegroundColor Green
}

$schemas = Get-Content $datasetsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$order = @("dm_resources", "dm_resource_versions", "dm_resource_permissions", "dm_resource_links", "dm_template_categories", "dm_document_templates", "dm_generation_counters", "dm_letterheads")
$byName = @{}
foreach ($s in $schemas) { $byName[$s.name] = $s }

$i = 0
foreach ($name in $order) {
    if (-not $byName.ContainsKey($name)) {
        Write-Host "  Eksik tanim: $name" -ForegroundColor Red
        continue
    }
    $i++
    Write-Host ('{0}) {1}' -f $i, $name) -ForegroundColor Yellow
    $body = ConvertTo-DgDatasetBody -Schema $byName[$name] -CategoryId $categoryId | ConvertTo-Json -Depth 30 -Compress
    $uri = "$BaseUrl$datasetsPath"
    $dr = Invoke-DgPost -Uri $uri -BodyJson $body -Label $name
    if ($dr.Ok) {
        Write-Host "  $name OK$(if ($dr.Skipped) { ' (zaten var)' })" -ForegroundColor Green
    }
    else {
        Write-Host "  HATA $name HTTP $($dr.Code)" -ForegroundColor Red
        if ($dr.Body) { Write-Host "  $($dr.Body)" -ForegroundColor Gray }
    }
}

Write-Host ''
Write-Host "Tamamlandi. Category ID: $categoryId" -ForegroundColor Cyan
