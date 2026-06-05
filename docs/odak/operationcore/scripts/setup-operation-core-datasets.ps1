# Operation Core (OC) - Dataset category + op_* schemas (Odak API Gateway)
# Ref: docs/odak/operationcore/README.md
#
# Usage (repo kokunden veya bu klasorden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\operationcore\scripts\setup-operation-core-datasets.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [string]$LoadTokenScript = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$categoryFile = Join-Path $repoRoot "docs/odak/operationcore/datasets/operationcore_dataset_category.json"
$datasetsFile = Join-Path $repoRoot "docs/odak/operationcore/datasets/operationcore_datasets_phase1_draft_2026-05-26.json"

$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }

if ([string]::IsNullOrEmpty($LoadTokenScript)) {
    $LoadTokenScript = Join-Path $PSScriptRoot "load-operationcore-token.ps1"
}
$loadTokenScript = $LoadTokenScript
if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-operationcore-token.ps1 bulunamadi: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi. get-operationcore-token.ps1 ayarlarini kontrol edin." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
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
            $output = & curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $Uri 2>&1 | Out-String
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
Write-Host "Operation Core - DG category + datasets ($BaseUrl)" -ForegroundColor Cyan
Write-Host ''

if (-not (Test-Path $categoryFile)) { throw "Missing: $categoryFile" }
if (-not (Test-Path $datasetsFile)) { throw "Missing: $datasetsFile" }

Write-Host '1) Dataset category: OperationCoreDatasets' -ForegroundColor Yellow
$cat = Get-Content $categoryFile -Raw -Encoding UTF8 | ConvertFrom-Json
$catBody = @{
    categoryName        = $cat.categoryName
    categoryDescription = $cat.categoryDescription
    isSystemCategory    = $cat.isSystemCategory
} | ConvertTo-Json -Compress

$catUri = "$BaseUrl$categoriesPath"
$r = Invoke-DgPost -Uri $catUri -BodyJson $catBody -Label "category"
if ($r.Ok) {
    Write-Host "  OperationCoreDatasets OK$(if ($r.Skipped) { ' (zaten var)' })" -ForegroundColor Green
}
else {
    Write-Host "  HATA category HTTP $($r.Code)" -ForegroundColor Red
    if ($r.Body) { Write-Host "  $($r.Body)" -ForegroundColor Gray }
    exit 1
}

$categoryId = $null
$listUri = '{0}{1}?pageSize=100&search=OperationCore' -f $BaseUrl, $categoriesPath
try {
    $irmGet = @{ Uri = $listUri; Method = "GET"; Headers = $headers }
    if ($listUri.StartsWith("https://")) { $irmGet.SkipCertificateCheck = $true }
    $list = Invoke-RestMethod @irmGet
    $items = $list.items
    if (-not $items) { $items = $list.data }
    if ($items) {
        $found = $items | Where-Object { $_.categoryName -eq "OperationCoreDatasets" } | Select-Object -First 1
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
$order = @(
    "op_states", "op_priorities", "op_work_item_types", "op_fields", "op_workspaces",
    "op_state_flows", "op_rules", "op_forms", "op_profiles", "op_boards", "op_labels",
    "op_sla_policies", "op_notification_policies", "op_dashboards", "op_saved_filters", "op_reports",
    "op_work_items", "op_comments", "op_activities", "op_links", "op_work_item_timelines", "op_notifications",
    "op_work_item_schedules", "op_tags"
)
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
Write-Host 'Revizyon: node docs/odak/operationcore/scripts/build-operationcore-datasets-draft.mjs' -ForegroundColor Gray
