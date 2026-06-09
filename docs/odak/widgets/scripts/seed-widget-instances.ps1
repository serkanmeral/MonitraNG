# Alarm / SIEM / Operation Core starter @widgets (+ opsiyonel @dashboards)
#
# Kullanim (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\widgets\scripts\seed-widget-instances.ps1
#   .\docs\odak\widgets\scripts\seed-widget-instances.ps1 -Module alarm
#   .\docs\odak\widgets\scripts\seed-widget-instances.ps1 -SkipDashboards
#   .\docs\odak\widgets\scripts\seed-widget-instances.ps1 -WhatIf
#
# Onkosullar:
#   - @widget_categories modul seed (reset-widget-catalog.ps1 veya setup-widget-templates-datasets.ps1)
#   - @widget_templates V1 seed (setup-widget-templates-datasets.ps1)
#   - OC widget'lari icin: operationcore-demo-seed.json (seed-operation-core-demo.ps1)

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [string]$SeedFile = "",
    [string]$OcContextFile = "",
    [ValidateSet('all', 'alarm', 'siem', 'operation-core')]
    [string]$Module = 'all',
    [switch]$SkipDashboards,
    [switch]$Recreate,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
if ([string]::IsNullOrEmpty($SeedFile)) {
    $SeedFile = Join-Path $repoRoot "docs/odak/widgets/datasets/widget_instances_seed_v1.json"
}
if ([string]::IsNullOrEmpty($OcContextFile)) {
    $OcContextFile = Join-Path $repoRoot "docs/odak/operationcore/scripts/operationcore-demo-seed.json"
}

$helpers = Join-Path $PSScriptRoot "widget-instance-helpers.ps1"
if (-not (Test-Path $helpers)) {
    Write-Host "widget-instance-helpers.ps1 bulunamadi: $helpers" -ForegroundColor Red
    exit 1
}
. $helpers

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
    $null = & $loadTokenScript -AutoRefresh:$false 2>&1
    if (Test-Path $tokenFile) {
        $token = (Get-Content $tokenFile -Raw).Trim()
    }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token bulunamadi. Once get-operationcore-token.ps1 calistirin." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}
if (-not [string]::IsNullOrEmpty($Domain)) {
    $headers["X-Domain-Name"] = $Domain
}
$dataPath = "/data/api/v1/data"

$seed = Get-Content $SeedFile -Raw -Encoding UTF8 | ConvertFrom-Json

function Build-OcContext {
    param([string]$Path, $ContextKeys)
    $ctx = @{}
    if (-not (Test-Path $Path)) {
        Write-Host "  Uyari: OC context dosyasi yok: $Path" -ForegroundColor Yellow
        Write-Host "  OC widget'lari icin once seed-operation-core-demo.ps1 calistirin." -ForegroundColor Yellow
        return $ctx
    }
    $oc = Get-Content $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($ContextKeys.ocWorkspaceId) {
        $key = [string]$ContextKeys.ocWorkspaceId
        $ctx['ocWorkspaceId'] = $oc.$key
    }
    if ($ContextKeys.ocStateOpenId) {
        $parts = [string]$ContextKeys.ocStateOpenId -split '\.'
        if ($parts.Count -eq 2) {
            $ctx['ocStateOpenId'] = $oc.($parts[0]).($parts[1])
        }
    }
    if ($ContextKeys.ocStateInProgressId) {
        $parts = [string]$ContextKeys.ocStateInProgressId -split '\.'
        if ($parts.Count -eq 2) {
            $ctx['ocStateInProgressId'] = $oc.($parts[0]).($parts[1])
        }
    }
    return $ctx
}

function Get-DashboardBySlug {
    param([string]$Slug)
    $filter = [uri]::EscapeDataString("slug:eq:$Slug")
    $uri = "$BaseUrl$dataPath/@dashboards?filter=$filter&limit=1"
    $list = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET -TimeoutSec 30
    if ($list -is [array] -and $list.Count -gt 0) { return $list[0] }
    if ($list.items -and $list.items.Count -gt 0) { return $list.items[0] }
    return $null
}

Write-Host ''
Write-Host "Widget instance seed ($BaseUrl, domain=$Domain, module=$Module)" -ForegroundColor Cyan
Write-Host ''

Write-Host 'Kategoriler yukleniyor...' -ForegroundColor Yellow
$categoryMap = Get-WidgetCategoryMap -BaseUrl $BaseUrl -Headers $headers -DataPath $dataPath
if ($categoryMap.Count -lt 4) {
    Write-Host "  Uyari: Az modul kategorisi ($($categoryMap.Count)). reset-widget-catalog.ps1 calistirin." -ForegroundColor Yellow
}

$placeholderCtx = Build-OcContext -Path $OcContextFile -ContextKeys $seed.contextKeys

$widgetIds = @{}
$created = 0
$skipped = 0

foreach ($slot in $seed.widgets) {
    $mod = [string]$slot.module
    if ($Module -ne 'all' -and $mod -ne $Module) { continue }

    $wName = [string]$slot.name
    $tid = [string]$slot.templateId
    $order = if ($null -ne $slot.order) { [int]$slot.order } else { 0 }
    $titleOverride = if ($slot.titleOverride) { [string]$slot.titleOverride } else { '' }

    $params = @{}
    if ($slot.parameters) {
        try {
            $params = Resolve-SeedParameterPlaceholders -ParametersNode $slot.parameters -Context $placeholderCtx
        }
        catch {
            if ($mod -eq 'operation-core') {
                Write-Host "  ATLA $wName — $($_.Exception.Message)" -ForegroundColor DarkYellow
                $skipped++
                continue
            }
            throw
        }
    }

    Write-Host "Widget: $wName <- $tid" -ForegroundColor Yellow
    if ($slot.description) {
        Write-Host "  $($slot.description)" -ForegroundColor DarkGray
    }

    $before = Get-WidgetByName -BaseUrl $BaseUrl -Headers $headers -Name $wName -DataPath $dataPath
    $id = Ensure-WidgetInstance -BaseUrl $BaseUrl -Headers $headers -WidgetName $wName -TemplateId $tid `
        -CategoryMap $categoryMap -Parameters $params -TitleOverride $titleOverride -Order $order `
        -WhatIf:$WhatIf -Recreate:$Recreate -DataPath $dataPath
    $widgetIds[$wName] = $id
    if ($before -and -not $Recreate) { $skipped++ } else { $created++ }
}

Write-Host ''
Write-Host "Widget ozet: $($widgetIds.Count) islenen, ~$created yeni/guncellenen, ~$skipped mevcut" -ForegroundColor Cyan

if (-not $SkipDashboards -and $seed.dashboards) {
    Write-Host ''
    Write-Host 'Dashboard layout seed...' -ForegroundColor Yellow
    foreach ($dash in $seed.dashboards) {
        $dashModule = [string]$dash.module
        if (-not [string]::IsNullOrEmpty($dashModule) -and $Module -ne 'all' -and $dashModule -ne $Module) {
            continue
        }

        $slug = [string]$dash.slug
        $rows = @()
        foreach ($layoutRow in $dash.layoutRows) {
            $cols = @()
            foreach ($col in $layoutRow.cols) {
                $wName = [string]$col.widgetName
                if (-not $widgetIds.ContainsKey($wName)) {
                    if ($Module -ne 'all') { continue }
                    $id = Ensure-WidgetInstance -BaseUrl $BaseUrl -Headers $headers -WidgetName $wName `
                        -TemplateId ([string]($seed.widgets | Where-Object { $_.name -eq $wName } | Select-Object -First 1).templateId) `
                        -CategoryMap $categoryMap -WhatIf:$WhatIf -DataPath $dataPath
                    if ($id) { $widgetIds[$wName] = $id }
                }
                if (-not $widgetIds.ContainsKey($wName)) {
                    Write-Host "  ATLA dashboard $slug — widget yok: $wName" -ForegroundColor DarkYellow
                    continue
                }
                $cols += @{
                    span     = [int]$col.span
                    widgetId = [string]$widgetIds[$wName]
                }
            }
            if ($cols.Count -gt 0) {
                $rows += @{ cols = $cols }
            }
        }

        if ($rows.Count -eq 0) { continue }

        $dashBody = @{
            name        = [string]$dash.name
            title       = [string]$dash.title
            description = if ($dash.description) { [string]$dash.description } else { $null }
            slug        = $slug
            isDefault   = $false
            isActive    = $true
            order       = if ($null -ne $dash.order) { [int]$dash.order } else { 0 }
            permissions = $null
            layout      = @{
                type = 'rows'
                rows = $rows
            }
        }
        $json = ($dashBody | ConvertTo-Json -Depth 30 -Compress)

        $existing = Get-DashboardBySlug -Slug $slug
        if ($WhatIf) {
            Write-Host "  WhatIf dashboard slug=$slug rows=$($rows.Count)" -ForegroundColor DarkYellow
            continue
        }
        if ($existing) {
            $id = Get-RowId $existing
            Invoke-RestMethod -Uri "$BaseUrl$dataPath/@dashboards/$id" -Headers $headers -Method PUT -Body $json -TimeoutSec 60 | Out-Null
            Write-Host "  OK dashboard guncellendi slug=$slug id=$id" -ForegroundColor Green
        }
        else {
            $createdDash = Invoke-RestMethod -Uri "$BaseUrl$dataPath/@dashboards" -Headers $headers -Method POST -Body $json -TimeoutSec 60
            $id = Get-RowId $createdDash
            Write-Host "  OK dashboard olusturuldu slug=$slug id=$id" -ForegroundColor Green
        }
        Write-Host "  UI: /dashboards/$slug" -ForegroundColor Gray
    }
}

Write-Host ''
Write-Host 'Tamamlandi.' -ForegroundColor Cyan
Write-Host '  Liste: /apps/widgets — Modul + Tur filtreleri' -ForegroundColor Gray
Write-Host '  Dashboard: /dashboards/seed-alarm-overview | seed-siem-overview | seed-oc-workspace' -ForegroundColor Gray
Write-Host ''
