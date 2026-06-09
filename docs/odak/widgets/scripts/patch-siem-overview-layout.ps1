# SIEM Özet Paneli layout — senaryo kartlarını tam genişlik satıra taşır
#
#   .\docs\odak\widgets\scripts\patch-siem-overview-layout.ps1
#
# Alternatif (tüm SIEM seed): seed-widget-instances.ps1 -Module siem

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Domain = "odak"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
$helpers = Join-Path $PSScriptRoot "widget-instance-helpers.ps1"
. $helpers

$token = $env:WIDGET_TOKEN
if ([string]::IsNullOrEmpty($token)) {
    $loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
    if (Test-Path $loadToken) { $token = & $loadToken }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token bulunamadi." -ForegroundColor Red
    exit 1
}

$headers = @{
    Authorization   = "Bearer $token"
    "X-Domain-Name" = $Domain
    "Content-Type"  = "application/json"
}

$dataPath = "/data/api/v1/data"
$slug = "seed-siem-overview"

function Get-DashboardBySlugLocal {
    param([string]$Slug)
    $filter = [uri]::EscapeDataString("slug:eq:$Slug")
    $uri = "$BaseUrl$dataPath/@dashboards?filter=$filter&limit=1"
    $list = Invoke-RestMethod -Uri $uri -Headers $headers -Method GET -TimeoutSec 30
    if ($list -is [array] -and $list.Count -gt 0) { return $list[0] }
    if ($list.items -and $list.items.Count -gt 0) { return $list.items[0] }
    return $null
}

$dash = Get-DashboardBySlugLocal -Slug $slug
if (-not $dash) {
    Write-Host "Dashboard bulunamadi: $slug" -ForegroundColor Red
    exit 1
}

function Get-WidgetIdByName {
    param([string]$Name)
    $row = Get-WidgetByName -BaseUrl $BaseUrl -Headers $headers -Name $Name -DataPath $dataPath
    if (-not $row) { return $null }
    return Get-RowId $row
}

$names = @(
    "seed-siem-events-total",
    "seed-siem-open-alarms",
    "seed-siem-login-failed",
    "seed-siem-scenario-cards",
    "seed-siem-events-hourly-trend",
    "seed-siem-recent-events-table"
)

$ids = @{}
foreach ($n in $names) {
    $id = Get-WidgetIdByName -Name $n
    if (-not $id) {
        Write-Host "Widget bulunamadi: $n — once seed-widget-instances.ps1 -Module siem calistirin." -ForegroundColor Red
        exit 1
    }
    $ids[$n] = $id
}

$rows = @(
    @{
        cols = @(
            @{ span = 4; widgetId = $ids["seed-siem-events-total"] },
            @{ span = 4; widgetId = $ids["seed-siem-open-alarms"] },
            @{ span = 4; widgetId = $ids["seed-siem-login-failed"] }
        )
    },
    @{
        cols = @(
            @{ span = 12; widgetId = $ids["seed-siem-scenario-cards"] }
        )
    },
    @{
        cols = @(
            @{ span = 5; widgetId = $ids["seed-siem-events-hourly-trend"] },
            @{ span = 7; widgetId = $ids["seed-siem-recent-events-table"] }
        )
    }
)

$body = @{}
foreach ($prop in $dash.PSObject.Properties) {
    if ($prop.Name -notmatch '^_' -and $prop.Name -ne 'dataId') {
        $body[$prop.Name] = $prop.Value
    }
}
$body.layout = @{ type = "rows"; rows = $rows }

$dashId = Get-RowId $dash
$json = ($body | ConvertTo-Json -Depth 30 -Compress)
Invoke-RestMethod -Uri "$BaseUrl$dataPath/@dashboards/$dashId" -Headers $headers -Method PUT -Body $json -TimeoutSec 60 | Out-Null

Write-Host "OK SIEM overview layout guncellendi — /dashboards/$slug" -ForegroundColor Green
Write-Host "  Satir 1: 3 KPI karti | Satir 2: senaryo seridi (12) | Satir 3: grafik + tablo" -ForegroundColor Gray
