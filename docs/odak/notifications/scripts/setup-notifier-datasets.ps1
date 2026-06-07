# MngNotifier — DG category + @mail_layouts + @mail_templates + seed kayitlari
#
# Kullanim (repo kokunden — OC ile ayni token akisi):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\notifications\scripts\setup-notifier-datasets.ps1
#
# Alternatif: -Token veya $env:NOTIFIER_TOKEN

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [string]$Token = "",
    [string]$LoadTokenScript = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path

if ([string]::IsNullOrEmpty($Token)) {
    $Token = $env:NOTIFIER_TOKEN
}
if ([string]::IsNullOrEmpty($Token)) {
    if ([string]::IsNullOrEmpty($LoadTokenScript)) {
        $LoadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    }
    if (Test-Path $LoadTokenScript) {
        $Token = & $LoadTokenScript
    }
}
$categoryFile = Join-Path $repoRoot "docs/odak/notifications/datasets/notifier_dataset_category.json"
$datasetsFile = Join-Path $repoRoot "docs/odak/notifications/datasets/notifier_datasets.json"
$layoutsSeedFile = Join-Path $repoRoot "docs/odak/notifications/datasets/notifier_mail_layouts_seed.json"
$templatesSeedFile = Join-Path $repoRoot "docs/odak/notifications/datasets/notifier_mail_templates_seed.json"

$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "Token bulunamadi. Once calistirin:" -ForegroundColor Red
    Write-Host "  .\docs\odak\operationcore\scripts\get-operationcore-token.ps1" -ForegroundColor Yellow
    Write-Host "veya -Token / `$env:NOTIFIER_TOKEN kullanin." -ForegroundColor Yellow
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
Write-Host "MngNotifier - DG category + datasets + seed ($BaseUrl)" -ForegroundColor Cyan
Write-Host ''

# 1) Category
Write-Host '1) Dataset category: NotifierDatasets' -ForegroundColor Yellow
$cat = Get-Content $categoryFile -Raw -Encoding UTF8 | ConvertFrom-Json
$catBody = @{
    categoryName        = $cat.categoryName
    categoryDescription = $cat.categoryDescription
    isSystemCategory    = $cat.isSystemCategory
} | ConvertTo-Json -Compress

$catUri = "$BaseUrl$categoriesPath"
$r = Invoke-DgPost -Uri $catUri -BodyJson $catBody -Label "category"
if ($r.Ok) {
    Write-Host "  NotifierDatasets OK$(if ($r.Skipped) { ' (zaten var)' })" -ForegroundColor Green
}
else {
    Write-Host "  HATA category HTTP $($r.Code)" -ForegroundColor Red
    if ($r.Body) { Write-Host "  $($r.Body)" -ForegroundColor Gray }
    exit 1
}

$categoryId = $null
$listUri = "$BaseUrl$categoriesPath`?pageSize=100&search=Notifier"
try {
    $irmGet = @{ Uri = $listUri; Method = "GET"; Headers = $headers }
    if ($listUri.StartsWith("https://")) { $irmGet.SkipCertificateCheck = $true }
    $list = Invoke-RestMethod @irmGet
    $items = $list.items
    if (-not $items) { $items = $list.data }
    if ($items) {
        $found = $items | Where-Object { $_.categoryName -eq "NotifierDatasets" } | Select-Object -First 1
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

# 2) Datasets
$schemas = Get-Content $datasetsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$order = @("@mail_layouts", "@mail_templates")
$byName = @{}
foreach ($s in $schemas) { $byName[$s.name] = $s }

$i = 0
foreach ($name in $order) {
    if (-not $byName.ContainsKey($name)) {
        Write-Host "  Eksik tanim: $name" -ForegroundColor Red
        continue
    }
    $i++
    Write-Host ('{0}) Dataset {1}' -f $i, $name) -ForegroundColor Yellow
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

# 3) Seed records
function Seed-DatasetRecords {
    param([string]$SeedFile, [string]$StepLabel)
    Write-Host $StepLabel -ForegroundColor Yellow
    $seed = Get-Content $SeedFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $dataset = $seed.dataset
    $uri = "$BaseUrl$dataPath/$dataset"
    $count = 0
    foreach ($rec in $seed.records) {
        $count++
        $body = $rec | ConvertTo-Json -Depth 20 -Compress
        $sr = Invoke-DgPost -Uri $uri -BodyJson $body -Label $dataset
        $key = if ($rec.templateKey) { $rec.templateKey } elseif ($rec.layoutKey) { $rec.layoutKey } else { "record-$count" }
        if ($sr.Ok) {
            Write-Host "  $key OK$(if ($sr.Skipped) { ' (zaten var)' })" -ForegroundColor Green
        }
        else {
            Write-Host "  HATA $key HTTP $($sr.Code)" -ForegroundColor Red
            if ($sr.Body) { Write-Host "  $($sr.Body)" -ForegroundColor Gray }
        }
    }
}

Seed-DatasetRecords -SeedFile $layoutsSeedFile -StepLabel '3) Seed @mail_layouts'
Seed-DatasetRecords -SeedFile $templatesSeedFile -StepLabel '4) Seed @mail_templates'

Write-Host ''
Write-Host "Tamamlandi. Category ID: $categoryId" -ForegroundColor Cyan
Write-Host "Sonraki: MngNotifier send-template testi (Bearer token + work-item-transitioned)" -ForegroundColor Cyan
