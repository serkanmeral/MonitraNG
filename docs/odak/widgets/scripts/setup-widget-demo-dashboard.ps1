# Generic dashboard surface demo — @widgets instance + @dashboards layout (slug=widgets-demo)
#
# Kullanim (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\widgets\scripts\setup-widget-demo-dashboard.ps1
#
# UI: http://192.168.20.20:3000/dashboards/widgets-demo
# Dogrulama: smoke-widget-demo-dashboard.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$seedFile = Join-Path $repoRoot "docs/odak/widgets/datasets/widget_demo_dashboard_seed.json"
$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$tokenFile = "$env:TEMP\operationcore_dg_token.txt"
$token = $null
if (Test-Path $tokenFile) {
    $token = (Get-Content $tokenFile -Raw).Trim()
}
if ([string]::IsNullOrEmpty($token) -and -not [string]::IsNullOrEmpty($env:WIDGET_TOKEN)) {
    $token = $env:WIDGET_TOKEN.Trim()
}
if ([string]::IsNullOrEmpty($token) -and (Test-Path $loadTokenScript)) {
    $token = & $loadTokenScript
    if ($token -match '\s') { $token = $token.Trim() }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token bulunamadi. Once get-operationcore-token.ps1 calistirin." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization   = "Bearer $token"
    "X-Domain-Name" = $Domain
    "Content-Type"  = "application/json"
}

$dataPath = "/data/api/v1/data"
$seed = Get-Content $seedFile -Raw -Encoding UTF8 | ConvertFrom-Json

function Get-RowId($row) {
    if ($null -eq $row) { return $null }
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    if ($row.id) { return [string]$row.id }
    if ($row.data) { return Get-RowId $row.data }
    if ($row.result) { return Get-RowId $row.result }
    return $null
}

function Get-LocalizedTitle($titleObj) {
    if ($null -eq $titleObj) { return "Widget" }
    if ($titleObj -is [string]) { return $titleObj }
    if ($titleObj.tr) { return [string]$titleObj.tr }
    if ($titleObj.en) { return [string]$titleObj.en }
    return "Widget"
}

function Get-LegacyTypeFromKind([string]$kind) {
    switch ($kind) {
        'stat' { return 'card' }
        'chart' { return 'chart' }
        'table' { return 'table' }
        'banner' { return 'banner' }
        'gauge' { return 'gauge' }
        'map' { return 'map' }
        default { return 'card' }
    }
}

function Get-CategoryId($categoryField) {
    if ($null -eq $categoryField) { return $null }
    if ($categoryField -is [string]) { return $categoryField }
    $id = Get-RowId $categoryField
    if ($id) { return $id }
    return $null
}

function Build-ManifestBinding($binding) {
    $fieldMap = @{}
    if ($binding.fieldMap) {
        foreach ($prop in $binding.fieldMap.PSObject.Properties) {
            $fieldMap[$prop.Name] = $prop.Value
        }
    }
    if ($binding.serviceRef -match ':static/') {
        return @{
            kind       = 'static'
            parameters = @{}
            fieldMap   = $fieldMap
        }
    }
    if ($binding.serviceRef) {
        return @{
            kind        = 'serviceRef'
            serviceRef  = [string]$binding.serviceRef
            parameters  = @{}
            fieldMap    = $fieldMap
        }
    }
    if ($binding.queryRef) {
        return @{
            kind       = 'queryRef'
            queryRef   = [string]$binding.queryRef
            parameters = @{}
            fieldMap   = $fieldMap
        }
    }
    return @{ kind = 'static'; parameters = @{}; fieldMap = $fieldMap }
}

function Build-LegacyDataSource($binding) {
    $manifestBinding = Build-ManifestBinding $binding
    if ($manifestBinding.kind -eq 'queryRef') {
        $queryRef = [string]$binding.queryRef
        if ($queryRef -match '^@([^/]+)/queries/(.+)$') {
            return @{
                type       = 'data'
                dataset    = $Matches[1]
                getMethod  = 'predefined'
                predefined = @{
                    queryName  = $Matches[2]
                    parameters = @{}
                }
            }
        }
        throw "Gecersiz queryRef: $queryRef"
    }
    if ($manifestBinding.kind -eq 'serviceRef') {
        $mapping = @{
            items = if ($binding.fieldMap.rows) { [string]$binding.fieldMap.rows } else { 'items' }
            total = if ($binding.fieldMap.total) { [string]$binding.fieldMap.total } else { 'total' }
            value = if ($binding.fieldMap.value) { [string]$binding.fieldMap.value } else { 'value' }
        }
        return @{
            type      = 'data'
            dataset   = '__manifest_service__'
            getMethod = 'default'
            default   = @{}
            mapping   = $mapping
        }
    }
    return @{
        type      = 'data'
        dataset   = '__manifest_static__'
        getMethod = 'default'
        default   = @{ limit = 0 }
    }
}

function Get-TemplateRow([string]$templateId) {
    $filter = [uri]::EscapeDataString("templateId:eq:$templateId")
    $uri = "$BaseUrl$dataPath/@widget_templates?filter=$filter&limit=1"
    $list = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET -TimeoutSec 30
    if ($list -is [array] -and $list.Count -gt 0) { return $list[0] }
    if ($list.items -and $list.items.Count -gt 0) { return $list.items[0] }
    return $null
}

function Get-WidgetByName([string]$name) {
    $filter = [uri]::EscapeDataString("name:eq:$name")
    $uri = "$BaseUrl$dataPath/@widgets?filter=$filter&limit=1"
    $list = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET -TimeoutSec 30
    if ($list -is [array] -and $list.Count -gt 0) { return $list[0] }
    if ($list.items -and $list.items.Count -gt 0) { return $list.items[0] }
    return $null
}

function Build-WidgetBody([string]$widgetName, $templateRow) {
    $manifest = $templateRow.manifest
    if (-not $manifest) { throw "Template manifest yok: $($templateRow.templateId)" }

    $presetId = $manifest.presentation.defaultPreset
    if (-not $presetId -and $manifest.presentation.preset) {
        $presetId = $manifest.presentation.preset
    }
    if (-not $presetId) { $presetId = 'stat-simple' }

    $kind = [string]$manifest.presentation.kind
    $legacyType = Get-LegacyTypeFromKind $kind
    $categoryId = Get-CategoryId $templateRow.category
    if (-not $categoryId) {
        $categoryId = Get-CategoryId $manifest.category
    }
    if (-not $categoryId) {
        throw "Kategori cozulemedi: $($templateRow.templateId)"
    }

    $definition = ($manifest | ConvertTo-Json -Depth 40 | ConvertFrom-Json)
    $definition | Add-Member -NotePropertyName name -NotePropertyValue $widgetName -Force
    $definition | Add-Member -NotePropertyName isActive -NotePropertyValue $true -Force
    $definition | Add-Member -NotePropertyName parameters -NotePropertyValue @{} -Force

    $config = @{}
    if ($manifest.presentation.config) {
        $cfgJson = $manifest.presentation.config | ConvertTo-Json -Depth 20
        $cfgObj = $cfgJson | ConvertFrom-Json
        foreach ($prop in $cfgObj.PSObject.Properties) {
            $config[$prop.Name] = $prop.Value
        }
    }
    $config['manifestBinding'] = Build-ManifestBinding $manifest.dataBinding
    $config['templateId'] = [string]$manifest.templateId
    $config['templateVersion'] = [string]$manifest.templateVersion
    $config['manifestVersion'] = [string]$manifest.manifestVersion
    $config['presentationPreset'] = $presetId
    $config['presentationKind'] = $kind
    if ($kind -eq 'stat') {
        $config['valueField'] = 'value'
    }
    $config['manifest'] = $definition

    return @{
        name        = $widgetName
        title       = Get-LocalizedTitle $manifest.title
        description = if ($templateRow.description) { [string]$templateRow.description } else { $null }
        category    = $categoryId
        type        = $legacyType
        dataSource  = Build-LegacyDataSource $manifest.dataBinding
        config      = $config
        isActive    = $true
        order       = 0
    }
}

function Ensure-Widget([string]$widgetName, [string]$templateId) {
    $existing = Get-WidgetByName $widgetName
    if ($existing) {
        $id = Get-RowId $existing
        Write-Host "  widget mevcut name=$widgetName id=$id" -ForegroundColor Gray
        return $id
    }

    $templateRow = Get-TemplateRow $templateId
    if (-not $templateRow) {
        throw "Sablon bulunamadi: $templateId (once setup-widget-templates-datasets.ps1)"
    }

    $body = Build-WidgetBody $widgetName $templateRow
    $json = ($body | ConvertTo-Json -Depth 40 -Compress)

    if ($WhatIf) {
        Write-Host "  WhatIf POST @widgets name=$widgetName template=$templateId" -ForegroundColor DarkYellow
        return "whatif-$widgetName"
    }

    $created = Invoke-RestMethod -Uri "$BaseUrl$dataPath/@widgets" -Headers $headers -Method POST -Body $json -TimeoutSec 60
    $id = Get-RowId $created
    if (-not $id) {
        $again = Get-WidgetByName $widgetName
        $id = Get-RowId $again
    }
    if (-not $id) {
        throw "Widget olusturuldu ama id alinamadi: $widgetName"
    }
    Write-Host "  OK widget olusturuldu name=$widgetName id=$id" -ForegroundColor Green
    return $id
}

Write-Host ''
Write-Host "Widget demo dashboard ($BaseUrl, domain=$Domain)" -ForegroundColor Cyan
Write-Host ''

$widgetIds = @{}
foreach ($slot in $seed.widgets) {
    $wName = [string]$slot.name
    $tid = [string]$slot.templateId
    Write-Host "Widget: $wName <- $tid" -ForegroundColor Yellow
    $widgetIds[$wName] = Ensure-Widget $wName $tid
}

$rows = @()
foreach ($layoutRow in $seed.layoutRows) {
    $cols = @()
    foreach ($col in $layoutRow.cols) {
        $wName = [string]$col.widgetName
        if (-not $widgetIds.ContainsKey($wName)) {
            throw "Layout widgetName bilinmiyor: $wName"
        }
        $cols += @{
            span     = [int]$col.span
            widgetId = [string]$widgetIds[$wName]
        }
    }
    $rows += @{ cols = $cols }
}

$dashBody = @{}
foreach ($prop in $seed.dashboard.PSObject.Properties) {
    $dashBody[$prop.Name] = $prop.Value
}
$dashBody['layout'] = @{
    type = 'rows'
    rows = $rows
}

$slug = [string]$seed.dashboard.slug
$dashJson = ($dashBody | ConvertTo-Json -Depth 30 -Compress)

$listUri = "$BaseUrl$dataPath/@dashboards?filter=slug:eq:$slug&limit=1"
$list = Invoke-RestMethod -Uri $listUri -Headers $headers -Method GET -TimeoutSec 30
$existing = $null
if ($list -is [array] -and $list.Count -gt 0) { $existing = $list[0] }
elseif ($list.items -and $list.items.Count -gt 0) { $existing = $list.items[0] }

if ($existing) {
    $id = Get-RowId $existing
    Write-Host ''
    Write-Host "Dashboard mevcut slug=$slug id=$id — PUT layout sync" -ForegroundColor Yellow
    if (-not $WhatIf) {
        Invoke-RestMethod -Uri "$BaseUrl$dataPath/@dashboards/$id" -Headers $headers -Method PUT -Body $dashJson -TimeoutSec 60 | Out-Null
        Write-Host "  OK $($rows.Count) satir layout guncellendi" -ForegroundColor Green
    }
}
else {
    Write-Host ''
    Write-Host "Dashboard yeni slug=$slug — POST" -ForegroundColor Yellow
    if (-not $WhatIf) {
        $created = Invoke-RestMethod -Uri "$BaseUrl$dataPath/@dashboards" -Headers $headers -Method POST -Body $dashJson -TimeoutSec 60
        $id = Get-RowId $created
        Write-Host "  OK olusturuldu id=$id" -ForegroundColor Green
    }
}

Write-Host ''
Write-Host "UI: http://192.168.20.20:3000/dashboards/$slug" -ForegroundColor Cyan
Write-Host "Smoke: smoke-widget-demo-dashboard.ps1" -ForegroundColor Cyan
