# Widget template catalog — DG category + @widget_categories seed + @widget_templates + manifest seed
#
# Kullanim (repo kokunden — Operation Core ile ayni token akisi):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\widgets\scripts\setup-widget-templates-datasets.ps1
#
# Alternatif: -Token veya $env:WIDGET_TOKEN

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [string]$Token = "",
    [string]$LoadTokenScript = "",
    [switch]$SkipCategories,
    [switch]$SkipTemplatesSeed
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path

if ([string]::IsNullOrEmpty($Token)) {
    $Token = $env:WIDGET_TOKEN
}
if ([string]::IsNullOrEmpty($Token)) {
    if ([string]::IsNullOrEmpty($LoadTokenScript)) {
        $LoadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    }
    if (Test-Path $LoadTokenScript) {
        $Token = & $LoadTokenScript
    }
}

$categoryFile = Join-Path $repoRoot "docs/odak/widgets/datasets/widget_dataset_category.json"
$datasetCreateFile = Join-Path $repoRoot "docs/odak/widgets/datasets/widget-templates-dataset-create.json"
$categoriesSeedFile = Join-Path $repoRoot "docs/odak/widgets/datasets/widget_categories_seed_v1.json"
$templatesSeedFile = Join-Path $repoRoot "docs/odak/widgets/datasets/widget_templates_seed_v1.json"

$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "Token bulunamadi. Once calistirin:" -ForegroundColor Red
    Write-Host "  .\docs\odak\operationcore\scripts\get-operationcore-token.ps1" -ForegroundColor Yellow
    Write-Host "veya -Token / `$env:WIDGET_TOKEN kullanin." -ForegroundColor Yellow
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $Token"
    "Content-Type"  = "application/json"
}

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$useCurl = $null -ne (Get-Command curl.exe -ErrorAction SilentlyContinue)

function Invoke-DgRequest {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        [string]$BodyJson = "",
        [string]$Label = ""
    )
    if ($Method -eq "POST" -and $useCurl) {
        $bodyFile = [System.IO.Path]::GetTempFileName()
        try {
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllText($bodyFile, $BodyJson, $utf8NoBom)
            $output = & curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $Token" -H "Content-Type: application/json; charset=utf-8" --data-binary "@$bodyFile" $Uri 2>&1 | Out-String
            $lines = ($output.Trim() -split "`n")
            $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]', '').Trim() } else { "" }
            $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count - 2)] -join "`n").Trim() } else { "" }
            if ($httpCode -in @("200", "201")) { return @{ Ok = $true; Body = $responseBody } }
            if ($httpCode -eq "409" -or ($httpCode -eq "400" -and $responseBody -match "mevcut|already|zaten|duplicate|unique|exists|EXIST")) {
                return @{ Ok = $true; Skipped = $true; Body = $responseBody }
            }
            return @{ Ok = $false; Code = $httpCode; Body = $responseBody }
        }
        finally {
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
        }
    }
    try {
        $irm = @{ Uri = $Uri; Method = $Method; Headers = $headers }
        if ($Uri.StartsWith("https://")) { $irm.SkipCertificateCheck = $true }
        if ($Method -eq "POST" -and -not [string]::IsNullOrEmpty($BodyJson)) {
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            $irm.Body = $utf8NoBom.GetBytes($BodyJson)
            $irm.ContentType = "application/json; charset=utf-8"
        }
        $response = Invoke-RestMethod @irm
        return @{ Ok = $true; Data = $response }
    }
    catch {
        $statusCode = $null
        $errMsg = $_.Exception.Message
        try {
            $statusCode = [int]$_.Exception.Response.StatusCode
            if ($_.ErrorDetails.Message) {
                $errMsg = $_.ErrorDetails.Message
            }
            elseif ($_.Exception.Response) {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $errMsg = $reader.ReadToEnd()
                $reader.Close()
            }
        }
        catch { }
        if ($Method -eq "POST" -and ($statusCode -eq 409 -or ($statusCode -eq 400 -and $errMsg -match "mevcut|already|zaten|duplicate|unique|exists|EXIST"))) {
            return @{ Ok = $true; Skipped = $true; Body = $errMsg }
        }
        return @{ Ok = $false; Code = $statusCode; Body = $errMsg }
    }
}

function ConvertTo-DgDatasetBody {
    param($Schema, [string]$CategoryId)
    $publishMode = if ($Schema.publishMode) { $Schema.publishMode } else { $Schema.publish_mode }
    @{
        Name        = $Schema.name
        Description = $Schema.description
        Category    = $CategoryId
        ForceSchema = $Schema.forceSchema
        Logging     = $Schema.logging
        PublishMode = $publishMode
        Fields      = $Schema.fields
        Validations = $Schema.validations
        Queries     = $Schema.queries
        IndexList   = $Schema.indexList
    }
}

function Resolve-WidgetCategoryMap {
    $map = @{}
    $uri = "$BaseUrl$dataPath/@widget_categories?pageSize=200"
    try {
        $irm = @{ Uri = $uri; Method = "GET"; Headers = $headers }
        if ($uri.StartsWith("https://")) { $irm.SkipCertificateCheck = $true }
        $data = Invoke-RestMethod @irm
        $items = @()
        if ($data -is [array]) {
            $items = @($data)
        }
        elseif ($data.items) {
            $items = @($data.items)
        }
        elseif ($data.data) {
            $items = @($data.data)
        }
        foreach ($item in $items) {
            $name = $item.name
            if ([string]::IsNullOrEmpty($name)) { $name = $item.Name }
            $id = $item.__dataId
            if ([string]::IsNullOrEmpty($id)) { $id = $item.dataId }
            $desc = $item.description
            if ([string]::IsNullOrEmpty($desc)) { $desc = $item.Description }
            if (-not [string]::IsNullOrEmpty($name) -and -not [string]::IsNullOrEmpty($id)) {
                $map[$name] = $id
                if ($desc -match '^domain:(.+)$') {
                    $map[$Matches[1].ToLower()] = $id
                }
            }
        }
    }
    catch {
        Write-Host "  Uyari: @widget_categories listesi alinamadi: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    return $map
}

Write-Host ''
Write-Host "Widget templates - DG kurulum ($BaseUrl)" -ForegroundColor Cyan
Write-Host ''

# 1) Dataset category: WidgetDatasets
Write-Host '1) Dataset category: WidgetDatasets' -ForegroundColor Yellow
$cat = Get-Content $categoryFile -Raw -Encoding UTF8 | ConvertFrom-Json
$catBody = @{
    categoryName        = $cat.categoryName
    categoryDescription = $cat.categoryDescription
    isSystemCategory    = $cat.isSystemCategory
} | ConvertTo-Json -Compress

$catUri = "$BaseUrl$categoriesPath"
$r = Invoke-DgRequest -Uri $catUri -Method POST -BodyJson $catBody -Label "category"
if ($r.Ok) {
    Write-Host "  WidgetDatasets OK$(if ($r.Skipped) { ' (zaten var)' })" -ForegroundColor Green
}
else {
    Write-Host "  Uyari: category HTTP $($r.Code) - mevcut kategori aranacak" -ForegroundColor Yellow
    if ($r.Body) { Write-Host "  $($r.Body)" -ForegroundColor Gray }
}

$categoryId = $null
$listUri = "${BaseUrl}${categoriesPath}?pageSize=100&search=Widget"
$list = Invoke-DgRequest -Uri $listUri -Method GET
if ($list.Ok) {
    $items = $list.Data.items
    if (-not $items) { $items = $list.Data.data }
    if ($items) {
        $found = $items | Where-Object { $_.categoryName -eq "WidgetDatasets" } | Select-Object -First 1
        if ($found) { $categoryId = $found.dataId }
    }
}
if ([string]::IsNullOrEmpty($categoryId)) {
    $categoryId = "672cb92a-c3dd-4083-82eb-c103a82eba60"
    Write-Host "  Category ID (WidgetDatasets fallback): $categoryId" -ForegroundColor Yellow
}
else {
    Write-Host "  Category ID (DG): $categoryId" -ForegroundColor Green
}

# 2) @widget_templates dataset
Write-Host '2) Dataset @widget_templates' -ForegroundColor Yellow
$schema = Get-Content $datasetCreateFile -Raw -Encoding UTF8 | ConvertFrom-Json
$body = ConvertTo-DgDatasetBody -Schema $schema -CategoryId $categoryId | ConvertTo-Json -Depth 30 -Compress
$uri = "$BaseUrl$datasetsPath"
$dr = Invoke-DgRequest -Uri $uri -Method POST -BodyJson $body -Label "@widget_templates"
if ($dr.Ok) {
    Write-Host "  @widget_templates OK$(if ($dr.Skipped) { ' (zaten var)' })" -ForegroundColor Green
}
else {
    Write-Host "  HATA @widget_templates HTTP $($dr.Code)" -ForegroundColor Red
    if ($dr.Body) { Write-Host "  $($dr.Body)" -ForegroundColor Gray }
    exit 1
}

# 3) @widget_categories V1 seed
if (-not $SkipCategories) {
    Write-Host '3) Seed @widget_categories (V1 domain kategorileri)' -ForegroundColor Yellow
    $categories = Get-Content $categoriesSeedFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $catUri = "$BaseUrl$dataPath/@widget_categories"
    foreach ($rec in $categories) {
        $body = $rec | ConvertTo-Json -Depth 10 -Compress
        $sr = Invoke-DgRequest -Uri $catUri -Method POST -BodyJson $body -Label $rec.name
        if ($sr.Ok) {
            Write-Host "  $($rec.name) OK$(if ($sr.Skipped) { ' (zaten var)' })" -ForegroundColor Green
        }
        else {
            Write-Host "  HATA $($rec.name) HTTP $($sr.Code)" -ForegroundColor Red
            if ($sr.Body) { Write-Host "  $($sr.Body)" -ForegroundColor Gray }
        }
    }
}
else {
    Write-Host '3) @widget_categories seed atlandi (-SkipCategories)' -ForegroundColor DarkGray
}

# 4) @widget_templates manifest seed
if (-not $SkipTemplatesSeed) {
    Write-Host '4) Seed @widget_templates (widget_templates_seed_v1.json)' -ForegroundColor Yellow
    $categoryMap = Resolve-WidgetCategoryMap
    if ($categoryMap.Count -eq 0) {
        Write-Host "  Uyari: @widget_categories bos veya okunamadi - relation cozulemeyebilir." -ForegroundColor Yellow
    }

    $seed = Get-Content $templatesSeedFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $dataset = $seed.dataset
    $tplUri = "$BaseUrl$dataPath/$dataset"
    $activeCount = 0
    $inactiveCount = 0

    foreach ($rec in $seed.records) {
        $payload = @{}
        foreach ($prop in $rec.PSObject.Properties) {
            if ($prop.Name -ne "categoryName") {
                $payload[$prop.Name] = $prop.Value
            }
        }
        $catName = $rec.categoryName
        if (-not [string]::IsNullOrEmpty($catName)) {
            $domainMap = @{
                'alarm-kpi'            = 'alarm'
                'alarm-charts'         = 'alarm'
                'siem-kpi'             = 'siem'
                'siem-charts'          = 'siem'
                'oc-kpi'               = 'operation-core'
                'oc-work-queues'       = 'operation-core'
                'di-lists'             = 'document-intelligence'
                'di-quick-access'      = 'document-intelligence'
            }
            if ($domainMap.ContainsKey($catName)) {
                $catName = $domainMap[$catName]
            }
            if ($categoryMap.ContainsKey($catName)) {
                $payload.category = $categoryMap[$catName]
            }
            else {
                Write-Host "  Uyari: kategori bulunamadi '$catName' - $($rec.templateId) relation bos kalabilir." -ForegroundColor Yellow
            }
        }
        if ($rec.isActive) { $activeCount++ } else { $inactiveCount++ }

        $body = ($payload | ConvertTo-Json -Depth 30 -Compress)
        $sr = Invoke-DgRequest -Uri $tplUri -Method POST -BodyJson $body -Label $rec.templateId
        if ($sr.Ok) {
            $flag = if ($rec.isActive) { "P0" } else { "P1/P2" }
            Write-Host "  $($rec.templateId) [$flag] OK$(if ($sr.Skipped) { ' (zaten var)' })" -ForegroundColor Green
        }
        else {
            Write-Host "  HATA $($rec.templateId) HTTP $($sr.Code)" -ForegroundColor Red
            if ($sr.Body) { Write-Host "  $($sr.Body)" -ForegroundColor Gray }
        }
    }

    Write-Host "  Ozet: $activeCount aktif (P0), $inactiveCount pasif (P1/P2)" -ForegroundColor Cyan
}
else {
    Write-Host '4) @widget_templates seed atlandi (-SkipTemplatesSeed)' -ForegroundColor DarkGray
}

Write-Host ''
Write-Host "Tamamlandi. Dogrulama:" -ForegroundColor Cyan
Write-Host '  GET .../data/@widget_templates?filter=domain:eq:alarm&limit=20' -ForegroundColor Gray
Write-Host '  GET .../data/@widget_categories?limit=50' -ForegroundColor Gray
Write-Host "Sonraki: Faz 0 UI - manifest adapter + preset registry (Mng.Ui)" -ForegroundColor Cyan
